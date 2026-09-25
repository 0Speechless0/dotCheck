using System.Globalization;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.AspNetCore.Components.Forms;
using dotCheck.Application.DTOs;
using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;
using dotCheck.Domain.Enums;
using dotCheck.Infrastructure.Security;

namespace dotCheck.Application.Services;

public sealed class PayoutBillService(
    IPayoutBillRepository payoutBillRepository,
    IUserRepository userRepository,
    IItemRepository itemRepository,
    IPaymentBillRepository paymentBillRepository,
    UserAsymmetricKeyService keyService)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private const long MaxImportBytes = 5 * 1024 * 1024;

    public async Task<IReadOnlyList<PayoutBill>> BuildForPaymentBillAsync(
        PaymentBill paymentBill,
        IReadOnlyList<CheckItem> confirmedItems,
        IReadOnlyList<User> activeUsers,
        CancellationToken cancellationToken = default)
    {
        var result = new List<PayoutBill>();
        var totalAmount = paymentBill.TotalAmount;

        foreach (var user in activeUsers)
        {
            var userItems = confirmedItems
                .Where(x => x.OwnerUserId == user.Id)
                .OrderBy(x => x.Id)
                .ToList();

            var itemTotalAmount = userItems.Sum(x => x.Amount);
            var payableAmount = totalAmount - itemTotalAmount;


        }

        return result;
    }

    public Task InsertManyAsync(
        IEnumerable<PayoutBill> payoutBills,
        CancellationToken cancellationToken = default) =>
        payoutBillRepository.InsertManyAsync(payoutBills, cancellationToken);

    public async Task<IReadOnlyList<PayoutBillDto>> GetUserPayoutBillsAsync(
        ObjectId userId,
        CancellationToken cancellationToken = default)
    {
        var bills = await payoutBillRepository.FindByUserAsync(userId, cancellationToken);
        return bills.Select(x => new PayoutBillDto(x)).ToList();
    }

    public async Task<string> ExportTextAsync(
        ObjectId userId,
        IEnumerable<ObjectId> payoutBillIds,
        CancellationToken cancellationToken = default)
    {
        var ids = payoutBillIds.Distinct().ToArray();
        if (ids.Length == 0)
            return "[]";

        var bills = await payoutBillRepository.FindByUserAsync(userId, cancellationToken);
        var selected = bills.Where(x => ids.Contains(x.Id)).ToList();
        return JsonSerializer.Serialize(selected.Select(ToEnvelope), JsonOptions);
    }

    public async Task<int> ImportAsync(
        ObjectId currentUserId,
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        if (file.Size <= 0 || file.Size > MaxImportBytes)
            throw new InvalidOperationException("撥款單匯入檔案不得超過 5MB。");

        string text;
        await using (var stream = file.OpenReadStream(MaxImportBytes, cancellationToken))
        using (var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true))
        {
            text = await reader.ReadToEndAsync(cancellationToken);
        }

        List<PayoutBillEnvelope> envelopes;
        try
        {
            envelopes = JsonSerializer.Deserialize<List<PayoutBillEnvelope>>(text, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("撥款單文字檔格式不正確。", ex);
        }

        if (envelopes.Count == 0)
            throw new InvalidOperationException("撥款單文字檔沒有可匯入的資料。");

        var user = await userRepository.FindByIdAsync(currentUserId, cancellationToken)
            ?? throw new InvalidOperationException("找不到目前登入使用者。");

        var imported = 0;
        var billIdSet = new HashSet<ObjectId>();
        var signatureSet = new HashSet<string>(StringComparer.Ordinal);

        foreach (var envelope in envelopes)
        {
            ValidateEnvelope(envelope);

            if (!string.Equals(envelope.KeyId, user.AsymmetricKeyId, StringComparison.Ordinal)
                || !string.Equals(envelope.PublicKey, user.PublicKeyPem, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("撥款單金鑰不是目前使用者註冊的金鑰。");
            }

            var calculatedHash = UserAsymmetricKeyService.ComputeSha256(envelope.Payload);
            if (!string.Equals(calculatedHash, envelope.PayloadHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("撥款單內容雜湊不一致，內容可能已被竄改。");

            if (!keyService.VerifyHash(user, calculatedHash, envelope.Signature))
                throw new InvalidOperationException("撥款單數位簽章驗證失敗，內容可能已被竄改。");

            if (!signatureSet.Add(envelope.Signature))
                throw new InvalidOperationException("匯入檔內存在重複的數位簽章。");

            var payload = DeserializePayload(envelope.Payload);

            if (!ObjectId.TryParse(payload.PayoutBillId, out var payoutBillId)
                || !ObjectId.TryParse(payload.PaymentBillId, out var paymentBillId)
                || !ObjectId.TryParse(payload.UserId, out var userId)
                || userId != currentUserId)
            {
                throw new InvalidOperationException("撥款單識別資料不正確。");
            }

            if (!billIdSet.Add(payoutBillId))
                throw new InvalidOperationException("匯入檔內存在重複的撥款單 ID。");

            if (await payoutBillRepository.FindByIdAsync(payoutBillId, cancellationToken) is not null)
                throw new InvalidOperationException($"撥款單 {payoutBillId} 已存在，無法重複匯入。");

            if (await payoutBillRepository.ExistsBySignatureAsync(envelope.Signature, cancellationToken))
                throw new InvalidOperationException("此撥款單的數位簽章已經匯入過。");

            var paymentBill = await paymentBillRepository.FindByIdAsync(paymentBillId, cancellationToken)
                ?? throw new InvalidOperationException("找不到撥款單對應的繳費單。");

            var payloadItemIds = ParseItemIds(payload.ItemIds);
            if (payloadItemIds.Count == 0)
                throw new InvalidOperationException("撥款單沒有項目 ID。");

            if (!payloadItemIds.All(paymentBill.ItemIds.Contains))
                throw new InvalidOperationException("撥款單項目不屬於對應繳費單。");

            var items = new List<CheckItem>(payloadItemIds.Count);
            foreach (var itemId in payloadItemIds)
            {
                var item = await itemRepository.FindByIdAsync(itemId, cancellationToken)
                    ?? throw new InvalidOperationException($"找不到撥款項目 {itemId}。");

                if (item.OwnerUserId != currentUserId || item.Status != ItemStatus.Settled)
                    throw new InvalidOperationException("撥款單包含不屬於目前使用者或尚未結清的項目。");

                items.Add(item);
            }

            var itemTotalAmount = items.Sum(x => x.Amount);
            var startDate = items.Min(x => x.FormDate);
            var endDate = items.Max(x => x.FormDate);

            if (itemTotalAmount != payload.ItemTotalAmount
                || startDate != ParseUtcDate(payload.StartDate)
                || endDate != ParseUtcDate(payload.EndDate))
            {
                throw new InvalidOperationException("撥款單項目內容與目前系統資料不一致。");
            }

            var expectedPayoutAmount =
                payload.ItemTotalAmount * (payload.TotalUserCount - 1);

            if (expectedPayoutAmount != payload.PayoutAmount)
                throw new InvalidOperationException("撥款單可撥金額計算不正確。");

            if (await payoutBillRepository.ExistsByPaymentBillAndUserAsync(
                paymentBillId,
                currentUserId,
                cancellationToken))
            {
                throw new InvalidOperationException("該繳費單已有此使用者的撥款單。");
            }

            var payoutBill = new PayoutBill
            {
                Id = payoutBillId,
                PaymentBillId = paymentBillId,
                UserId = currentUserId,
                ItemIds = payloadItemIds,
                StartDate = ParseUtcDate(payload.StartDate),
                EndDate = ParseUtcDate(payload.EndDate),
                ItemTotalAmount = payload.ItemTotalAmount,
                TotalUserCount = payload.TotalUserCount,
                PayoutAmount = payload.PayoutAmount,
                CreatedAt = ParseUtcDate(payload.CreatedAt),
                KeyId = user.AsymmetricKeyId,
                PublicKey = user.PublicKeyPem,
                SignatureAlgorithm = envelope.SignatureAlgorithm,
                VerificationMethod = envelope.VerificationMethod,
                Payload = envelope.Payload,
                PayloadHash = envelope.PayloadHash,
                Signature = envelope.Signature
            };

            try
            {
                await payoutBillRepository.InsertAsync(payoutBill, cancellationToken);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
            {
                throw new InvalidOperationException("撥款單已存在或與既有資料重複。", ex);
            }

            imported++;
        }

        return imported;
    }

    private static string SerializePayload(PayoutBillPayload payload) =>
        JsonSerializer.Serialize(payload, JsonOptions);

    private static PayoutBillEnvelope ToEnvelope(PayoutBill bill) => new()
    {
        FormatVersion = 1,
        KeyId = bill.KeyId,
        PublicKey = bill.PublicKey,
        SignatureAlgorithm = bill.SignatureAlgorithm,
        VerificationMethod = bill.VerificationMethod,
        Payload = bill.Payload,
        PayloadHash = bill.PayloadHash,
        Signature = bill.Signature
    };

    private static List<ObjectId> ParseItemIds(IEnumerable<string> itemIds)
    {
        var result = new List<ObjectId>();
        foreach (var itemId in itemIds)
        {
            if (!ObjectId.TryParse(itemId, out var objectId))
                throw new InvalidOperationException("撥款單包含無效的項目 ID。");

            result.Add(objectId);
        }

        return result.Distinct().ToList();
    }

    private static DateTime ParseUtcDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture).UtcDateTime;

    private static PayoutBillPayload DeserializePayload(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<PayoutBillPayload>(value, JsonOptions)
                ?? throw new JsonException();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("撥款單 Payload 格式不正確。", ex);
        }
    }

    private static void ValidateEnvelope(PayoutBillEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.KeyId)
            || string.IsNullOrWhiteSpace(envelope.PublicKey)
            || string.IsNullOrWhiteSpace(envelope.Payload)
            || string.IsNullOrWhiteSpace(envelope.PayloadHash)
            || string.IsNullOrWhiteSpace(envelope.Signature))
        {
            throw new InvalidOperationException("撥款單缺少必要的簽章資料。");
        }
    }

    public async Task<List<PayoutBill>> CreatePayoutBillAsync(
        PaymentBill paymentBill,
        List<CheckItem> confirmedItems,
        List<PaymentPayer> payers,
        decimal totalAmount, CancellationToken cancellationToken)
    {
        List<PayoutBill> result = new();
        int settledCount = 0;
        int allUserCount = paymentBill.Payers.Count() + payers.Count();
        var userItemGroups = confirmedItems
            .GroupBy(x => x.OwnerUserId)
            .ToDictionary(g => g.Key, g => g.ToList());
        foreach (var payer in payers)
        {


            if (payer.PayableAmount > 0)
                continue;

            if (await payoutBillRepository.ExistsByPaymentBillAndUserAsync(
                paymentBill.Id,
                payer.UserId,
                cancellationToken))
            {
                continue;
            }



            var payoutId = ObjectId.GenerateNewId();
            var startDate = userItemGroups[payer.UserId].Min(x => x.FormDate);
            var endDate = userItemGroups[payer.UserId].Max(x => x.FormDate);
            var createdAt = DateTime.UtcNow;

            result.Add(new PayoutBill
            {
                Id = payoutId,
                PaymentBillId = paymentBill.Id,
                UserId = payer.UserId,
                ItemIds = userItemGroups[payer.UserId].Select(x => x.Id).ToList(),
                StartDate = startDate,
                EndDate = endDate,
                ItemTotalAmount = totalAmount,
                TotalUserCount = allUserCount,
                PayoutAmount = -payer.PayableAmount,
                CreatedAt = createdAt,
                SignatureAlgorithm = UserAsymmetricKeyService.SignatureAlgorithm,
                VerificationMethod = "使用該使用者註冊之公鑰驗證 Payload SHA-256 的 RSA-PSS-SHA256 數位簽章。",
                SignatureStatus = PayoutBillSignStatus.Pending,

            });

        }
        await payoutBillRepository.InsertManyAsync(result);
        return result;

    }
    public async Task SignPayoutBillAsync(
    ObjectId payoutBillId,
    ObjectId userId)
    {
        User singntureUser = await userRepository.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("找不到簽署使用者。");
        PayoutBill payoutBill =
            await payoutBillRepository.FindByIdAsync(payoutBillId)
            ?? throw new InvalidOperationException(
                "找不到撥款單。");

        // 只允許撥款單本人簽名
        if (!string.Equals(
                payoutBill.UserId.ToString(),
                userId.ToString(),
                StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "無權限簽署此撥款單。");
        }

        // 防止重複簽名
        if (!string.IsNullOrWhiteSpace(payoutBill.Signature))
        {
            throw new InvalidOperationException(
                "此撥款單已完成已收款簽名。");
        }

        // 沒有可撥金額就不應建立簽名
        if (payoutBill.PayoutAmount <= 0)
        {
            throw new InvalidOperationException(
                "撥款金額必須大於 0。");
        }

        // 固定欄位順序產生簽名內容
        var payload = new PayoutBillPayload
        {
            PayoutBillId = payoutBill.Id.ToString(),
            UserId = payoutBill.UserId.ToString(),
            
            ItemIds = payoutBill.ItemIds.OrderBy(x => x),
            StartDate =  payoutBill.StartDate,
            EndDate  = payoutBill.EndDate,
            payoutBill.ItemTotalAmount,
            payoutBill.PayoutAmount
        };

        var payloadJson = JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

        // 全部欄位值先做 SHA-256
        var payloadHash =
        UserAsymmetricKeyService.ComputeSha256(payloadJson);


        // 使用 RSA-PSS-SHA256 簽名 Hash
        var signature = keyService.SignHash(
            singntureUser,
            payloadHash);

        
        payoutBill.PayloadHash = payloadHash;

        payoutBill.Signature = signature;

        payoutBill.SignatureAlgorithm =
            "RSA-PSS-SHA256";

        payoutBill.SignedAt =
            DateTime.UtcNow;

        payoutBill.SignatureStatus =
            PayoutBillSignStatus.ReceivedSigned;
        

        payoutBill.Payload = payloadJson;
        payoutBill.KeyId = singntureUser.AsymmetricKeyId;
        payoutBill.PublicKey = singntureUser.PublicKeyPem;

        await payoutBillRepository.UpdateAsync(
            payoutBill);
    }

    private sealed class PayoutBillEnvelope
    {
        public int FormatVersion { get; set; }
        public string KeyId { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty;
        public string SignatureAlgorithm { get; set; } = string.Empty;
        public string VerificationMethod { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string PayloadHash { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }

    private sealed class PayoutBillPayload
    {
        public string PayoutBillId { get; set; } = string.Empty;
        public string PaymentBillId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string[] ItemIds { get; set; } = [];
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public decimal ItemTotalAmount { get; set; }
        public int TotalUserCount { get; set; }
        public decimal PayoutAmount { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }
}
