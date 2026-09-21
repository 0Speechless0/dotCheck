using MongoDB.Bson;
using MongoDB.Driver;
using dotCheck.Application.DTOs;
using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;
using dotCheck.Domain.Enums;

namespace dotCheck.Application.Services;

public sealed class PaymentService(
    IItemRepository itemRepository,
    IUserRepository userRepository,
    IPaymentBillRepository paymentBillRepository,
    IReceiptRepository receiptRepository)
{
    public async Task<(PaymentBill Bill, int SettledCount)> CreatePaymentBillAsync(
        CancellationToken cancellationToken = default)
    {
        var confirmedItems = await itemRepository.FindAllByStatusAsync(ItemStatus.Confirmed, cancellationToken);
        if (confirmedItems.Count == 0)
            throw new InvalidOperationException("目前沒有可生成繳費單的已認結項目。");

        var itemIds = confirmedItems.Select(x => x.Id).ToArray();
        if (await paymentBillRepository.HasAnyItemAsync(itemIds, cancellationToken))
            throw new InvalidOperationException("部分已認結項目已有繳費單，為避免重複計費，本次操作已停止。");

        var allUsers = await userRepository.FindAllActiveAsync(cancellationToken);
        if (allUsers.Count == 0)
            throw new InvalidOperationException("目前沒有可用的系統使用者。");

        var ownerAmounts = confirmedItems
            .GroupBy(x => x.OwnerUserId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var totalAmount = confirmedItems.Sum(x => x.Amount);
        var payers = allUsers
            .Select(user => new PaymentPayer
            {
                UserId = user.Id,
                PayableAmount =
                (
                    totalAmount - ownerAmounts.GetValueOrDefault(user.Id)
                    - ownerAmounts.GetValueOrDefault(user.Id) * (allUsers.Count - 1)
                ) / allUsers.Count
            })
            .Where(x => x.PayableAmount > 0)
            .ToList();

        var bill = new PaymentBill
        {
            Id = ObjectId.GenerateNewId(),
            StartDate = confirmedItems.Min(x => x.FormDate),
            EndDate = confirmedItems.Max(x => x.FormDate),
            ItemIds = itemIds.ToList(),
            TotalAmount = totalAmount,
            Payers = payers,
            CreatedAt = DateTime.UtcNow
        };

        await paymentBillRepository.InsertAsync(bill, cancellationToken);

        var settledCount = 0;
        settledCount = await itemRepository.MarkSettledFromConfirmedAsync(cancellationToken);

        return (bill, settledCount);
    }

    public async Task<IReadOnlyList<PaymentBillDto>> GetUserPaymentBillsAsync(
        ObjectId userId,
        CancellationToken cancellationToken = default)
    {
        var bills = await paymentBillRepository.FindByUserAsync(userId, cancellationToken);
        var result = new List<PaymentBillDto>(bills.Count);

        foreach (var bill in bills)
        {
            var receipts = await receiptRepository.FindByPaymentBillAsync(bill.Id, cancellationToken);
            var paidUsers = receipts.Select(x => x.UserId).Distinct().Count();
            var paid = receipts.Any(x => x.UserId == userId);

            result.Add(new PaymentBillDto(bill, userId, paidUsers)
            {
                PaidReceiptExists = paid
            });
        }

        return result;
    }

    public async Task<PaymentBill?> GetByIdAsync(
        ObjectId paymentBillId,
        CancellationToken cancellationToken = default) =>
        await paymentBillRepository.FindByIdAsync(paymentBillId, cancellationToken);
}
