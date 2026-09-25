using dotCheck.Application.Interfaces;
using dotCheck.Domain.Entities;
using dotCheck.Domain.Enums;
using dotCheck.Infrastructure.Mongo;

namespace dotCheck.Application.Services;

public class FinanceService(
    ItemService itemService,
    IUserRepository userRepository,
    PaymentService paymentService,
    PayoutBillService payoutBillService
)
{


    public async Task<(PaymentBill Bill, List<PayoutBill> PayoutBills, int SettledCount)> SettleMoney(
         IReadOnlyList<CheckItem> confirmedItems,
        CancellationToken cancellationToken = default)
    {
        if (confirmedItems == null)
            throw new ArgumentNullException(nameof(confirmedItems));

        if (confirmedItems.Count == 0)
            throw new InvalidOperationException("目前沒有可生成繳費單的已認結項目。");


        var allUserIds = (await userRepository.FindAllActiveAsync()).Select(r => r.Id).ToList();
        if (allUserIds.Count == 0)
            throw new InvalidOperationException("目前沒有可用的系統使用者。");

        var ownerAmounts = confirmedItems
            .GroupBy(x => x.OwnerUserId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        decimal totalAmount = confirmedItems.Sum(x => x.Amount);

        var userGroups = allUserIds
            .Select(userId => new PaymentPayer
            {
                UserId = userId,
                PayableAmount =
                (
                    totalAmount - ownerAmounts.GetValueOrDefault(userId)
                    - ownerAmounts.GetValueOrDefault(userId) * (decimal)(allUserIds.Count - 1)
                ) / (decimal)allUserIds.Count
            })
            .Where(x => x.PayableAmount != 0)
            .GroupBy(x => x.PayableAmount > 0 ? "Pay" : "Receive")
            .ToDictionary(g => g.Key, g => g.ToList());
        if(!userGroups.ContainsKey("Pay") || !userGroups.ContainsKey("Receive"))
        {
            throw new Exception("目前不需要生成撥款單和繳費單");
        }
        PaymentBill bill = await paymentService.CreatePaymentBillAsync(confirmedItems.ToList(), userGroups["Pay"], totalAmount, cancellationToken);
        List<PayoutBill> payoutBills = await payoutBillService.CreatePayoutBillAsync(bill, confirmedItems.ToList(), userGroups["Receive"], totalAmount, cancellationToken);
        return (bill, payoutBills, confirmedItems.Count);
    }
}