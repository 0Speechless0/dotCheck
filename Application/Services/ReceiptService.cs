using System.Globalization;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using dotCheck.Application.DTOs;
using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;
using dotCheck.Infrastructure.Security;
using Microsoft.AspNetCore.Components.Forms;

namespace dotCheck.Application.Services;

public sealed class ReceiptService(
    IPaymentBillRepository paymentBillRepository,
    IReceiptRepository receiptRepository,
    IUserRepository userRepository,
    ReceiptCryptoService cryptoService)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private const long MaxImportBytes = 5 * 1024 * 1024;

    public async Task<IReadOnlyList<ReceiptDto>> GetUserReceiptsAsync(
        ObjectId userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var receipts = await receiptRepository.FindByUserAndDateRangeAsync(userId, startDate, endDate, cancellationToken);
        return receipts.Select(x => new ReceiptDto(x)).ToList();
    }

    public async Task<ReceiptDto> IssueReceiptAsync(
        ObjectId paymentBillId,
        ObjectId userId,
        CancellationToken cancellationToken = default)
    {
        var bill = await paymentBillRepository.FindByIdAsync(paymentBillId, cancellationToken)
            ?? throw new InvalidOperationException("找不到繳費單。");

        var user = await userRepository.FindByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("找不到使用者。");

        var payer = bill.Payers.FirstOrDefault(x => x.UserId == userId)
            ?? throw new InvalidOperationException("該使用者不在此繳費單的應繳納清單中。");

        if (await receiptRepository.FindByPaymentBillAndUserAsync(paymentBillId, userId, cancellationToken) is not null)
            throw new InvalidOperationException("該使用者已經開立過此繳費單的收據。");

        var receiptId = ObjectId.GenerateNewId();
        var issuedAt = DateTime.UtcNow;
        var payload = new ReceiptPayload
        {
            ReceiptId = receiptId.ToString(),
            PaymentBillId = bill.Id.ToString(),
            StartDate = bill.StartDate.ToString("O", CultureInfo.InvariantCulture),
            EndDate = bill.EndDate.ToString("O", CultureInfo.InvariantCulture),
            UserId = user.Id.ToString(),
            UserName = user.UserName,
            Amount = payer.PayableAmount,
            IssuedAt = issuedAt.ToString("O", CultureInfo.InvariantCulture)
        };

        var payloadText = JsonSerializer.Serialize(payload, JsonOptions);
        var signature = cryptoService.Sign(payloadText);

        if (await receiptRepository.ExistsBySignatureAsync(signature, cancellationToken))
            throw new InvalidOperationException("簽章內容已存在，為避免重複收據，操作已停止。");

        var receipt = new Receipt
        {
            Id = receiptId,
            PaymentBillId = bill.Id,
            UserId = user.Id,
            UserName = user.UserName,
            Amount = payer.PayableAmount,
            StartDate = bill.StartDate,
            EndDate = bill.EndDate,
            IssuedAt = issuedAt,
            KeyId = cryptoService.KeyId,
            PublicKey = cryptoService.PublicKey,
            SignatureAlgorithm = ReceiptCryptoService.SignatureAlgorithm,
            VerificationMethod = "使用 dotCheck 系統信任的公鑰驗證 Payload 的 RSA-PSS-SHA256 數位簽章。",
            Payload = payloadText,
            Signature = signature
        };

        try
        {
            await receiptRepository.InsertAsync(receipt, cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
        {
            throw new InvalidOperationException("該繳費單與使用者的收據已存在，無法重複開立。", ex);
        }

        return new ReceiptDto(receipt);
    }

    public async Task<string> ExportTextAsync(
        ObjectId userId,
        IEnumerable<ObjectId> receiptIds,
        CancellationToken cancellationToken = default)
    {
        var receipts = await receiptRepository.FindByIdsAndUserAsync(receiptIds, userId, cancellationToken);
        var envelopes = receipts.Select(ToEnvelope).ToList();
        return JsonSerializer.Serialize(envelopes, JsonOptions);
    }

    public async Task<int> ImportAsync(
        ObjectId currentUserId,
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        if (file.Size <= 0 || file.Size > MaxImportBytes)
            throw new InvalidOperationException("收據匯入檔案不得超過 5MB。");

        string text;
        await using (var stream = file.OpenReadStream(MaxImportBytes, cancellationToken))
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            text = await reader.ReadToEndAsync(cancellationToken);
        }

        List<ReceiptEnvelope> envelopes;
        try
        {
            envelopes = JsonSerializer.Deserialize<List<ReceiptEnvelope>>(text, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("收據文字檔格式不正確。", ex);
        }

        if (envelopes.Count == 0)
            throw new InvalidOperationException("收據文字檔沒有可匯入的收據。");

        var imported = 0;
        var signatureSet = new HashSet<string>(StringComparer.Ordinal);
        var receiptIdSet = new HashSet<ObjectId>();

        foreach (var envelope in envelopes)
        {
            if (string.IsNullOrWhiteSpace(envelope.Payload)
                || string.IsNullOrWhiteSpace(envelope.Signature)
                || string.IsNullOrWhiteSpace(envelope.KeyId))
            {
                throw new InvalidOperationException("收據缺少必要的簽章資料。");
            }

            if (!cryptoService.Verify(envelope.Payload, envelope.Signature, envelope.KeyId))
                throw new InvalidOperationException("收據數位簽章驗證失敗，內容可能已被竄改或金鑰不受信任。");

            if (!signatureSet.Add(envelope.Signature))
                throw new InvalidOperationException("匯入檔內存在重複的數位簽章。");

            ReceiptPayload payload;
            try
            {
                payload = JsonSerializer.Deserialize<ReceiptPayload>(envelope.Payload, JsonOptions)
                    ?? throw new JsonException();
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("收據 Payload 格式不正確。", ex);
            }

            if (!ObjectId.TryParse(payload.ReceiptId, out var receiptId)
                || !ObjectId.TryParse(payload.PaymentBillId, out var billId)
                || !ObjectId.TryParse(payload.UserId, out var userId))
            {
                throw new InvalidOperationException("收據識別資料不正確。");
            }

            if (userId != currentUserId)
                throw new InvalidOperationException("只能匯入目前登入使用者的收據。");

            if (!receiptIdSet.Add(receiptId))
                throw new InvalidOperationException("匯入檔內存在重複的收據 ID。");

            if (await receiptRepository.FindByIdAsync(receiptId, cancellationToken) is not null)
                throw new InvalidOperationException($"收據 {receiptId} 已存在，無法重複匯入。");

            if (await receiptRepository.ExistsBySignatureAsync(envelope.Signature, cancellationToken))
                throw new InvalidOperationException("此收據的數位簽章已經匯入過。");

            var bill = await paymentBillRepository.FindByIdAsync(billId, cancellationToken)
                ?? throw new InvalidOperationException("找不到收據對應的繳費單。");

            var payer = bill.Payers.FirstOrDefault(x => x.UserId == userId)
                ?? throw new InvalidOperationException("使用者不在對應繳費單的應繳納清單中。");

            if (payer.PayableAmount != payload.Amount)
                throw new InvalidOperationException("收據金額與繳費單應繳金額不一致。");

            if (await receiptRepository.FindByPaymentBillAndUserAsync(billId, userId, cancellationToken) is not null)
                throw new InvalidOperationException("該繳費單已有此使用者的收據，無法重複匯入。");

            var issuedAt = DateTimeOffset.Parse(payload.IssuedAt, CultureInfo.InvariantCulture).UtcDateTime;
            var startDate = DateTimeOffset.Parse(payload.StartDate, CultureInfo.InvariantCulture).UtcDateTime;
            var endDate = DateTimeOffset.Parse(payload.EndDate, CultureInfo.InvariantCulture).UtcDateTime;

            var receipt = new Receipt
            {
                Id = receiptId,
                PaymentBillId = billId,
                UserId = userId,
                UserName = payload.UserName,
                Amount = payload.Amount,
                StartDate = startDate,
                EndDate = endDate,
                IssuedAt = issuedAt,
                KeyId = envelope.KeyId,
                PublicKey = cryptoService.PublicKey,
                SignatureAlgorithm = envelope.SignatureAlgorithm,
                VerificationMethod = envelope.VerificationMethod,
                Payload = envelope.Payload,
                Signature = envelope.Signature
            };

            try
            {
                await receiptRepository.InsertAsync(receipt, cancellationToken);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
            {
                throw new InvalidOperationException("收據已存在或與既有收據重複。", ex);
            }

            imported++;
        }

        return imported;
    }

    private static ReceiptEnvelope ToEnvelope(Receipt receipt) => new()
    {
        FormatVersion = 1,
        KeyId = receipt.KeyId,
        PublicKey = receipt.PublicKey,
        SignatureAlgorithm = receipt.SignatureAlgorithm,
        VerificationMethod = receipt.VerificationMethod,
        Payload = receipt.Payload,
        Signature = receipt.Signature
    };

    private sealed class ReceiptEnvelope
    {
        public int FormatVersion { get; set; }
        public string KeyId { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty;
        public string SignatureAlgorithm { get; set; } = string.Empty;
        public string VerificationMethod { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }

    private sealed class ReceiptPayload
    {
        public string ReceiptId { get; set; } = string.Empty;
        public string PaymentBillId { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string IssuedAt { get; set; } = string.Empty;
    }
}
