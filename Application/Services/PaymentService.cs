using MongoDB.Bson;
using MongoDB.Driver;
using dotCheck.Application.DTOs;
using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;
using dotCheck.Domain.Enums;

namespace dotCheck.Application.Services;

public sealed class PaymentService(
    IItemRepository itemRepository,
    IPaymentBillRepository paymentBillRepository,
    IReceiptRepository receiptRepository)
{
    public async Task<PaymentBill> CreatePaymentBillAsync(
        List<CheckItem> confirmedItems,
        List<PaymentPayer> payers,
        decimal totalAmount,
        CancellationToken cancellationToken = default)
    {
        var itemIds = confirmedItems.Select(x => x.Id).ToList();
        if (await paymentBillRepository.HasAnyItemAsync(itemIds, cancellationToken))
            throw new InvalidOperationException("部分已認結項目已有繳費單，為避免重複計費，本次操作已停止。");


        var bill = new PaymentBill
        {
            Id = ObjectId.GenerateNewId(),
            StartDate = confirmedItems.Min(x => x.FormDate),
            EndDate = confirmedItems.Max(x => x.FormDate),
            ItemIds = itemIds,
            TotalAmount = totalAmount,
            Payers = payers,
            CreatedAt = DateTime.UtcNow
        };

        await paymentBillRepository.InsertAsync(bill, cancellationToken);

        var settledCount = 0;
        settledCount = await itemRepository.MarkSettledFromConfirmedAsync(cancellationToken);

        return bill;
    }

    public async Task<IReadOnlyList<PaymentBillDto>> GetUserPaymentBillsAsync(
        ObjectId userId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PaymentBill> bills;
        if (startDate == null && endDate == null)
            bills = await paymentBillRepository.FindByUserAsync(userId, cancellationToken);
        else if (startDate == null || endDate == null)
            throw new ArgumentException("Start date and end date must be provided together.");
        else
        {
            bills = await paymentBillRepository.FindByUserWithDateRangeAsync(userId, startDate.Value, endDate.Value, cancellationToken);
        }

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
