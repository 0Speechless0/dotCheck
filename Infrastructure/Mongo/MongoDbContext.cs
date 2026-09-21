using MongoDB.Driver;
using dotCheck.Domain.Entities;

namespace dotCheck.Infrastructure.Mongo;

public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration configuration)
    {
        var connectionString = configuration["MongoDb:ConnectionString"]
            ?? throw new InvalidOperationException("MongoDb:ConnectionString 未設定。");
        var databaseName = configuration["MongoDb:DatabaseName"]
            ?? throw new InvalidOperationException("MongoDb:DatabaseName 未設定。");

        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<CheckItem> Items => _database.GetCollection<CheckItem>("items");
    public IMongoCollection<UncheckedReason> UncheckedReasons => _database.GetCollection<UncheckedReason>("uncheckedReasons");
    public IMongoCollection<PaymentBill> PaymentBills => _database.GetCollection<PaymentBill>("paymentBills");
    public IMongoCollection<Receipt> Receipts => _database.GetCollection<Receipt>("receipts");
    public IMongoCollection<PayoutBill> PayoutBills => _database.GetCollection<PayoutBill>("payoutBills");
    public IMongoCollection<UserLoginLog> UserLoginLogs => _database.GetCollection<UserLoginLog>("userLoginLogs");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Users.Indexes.CreateOneAsync(
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(x => x.UserName),
                new CreateIndexOptions { Unique = true, Name = "ux_userName" }),
            cancellationToken: cancellationToken);

        await Items.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<CheckItem>(
                Builders<CheckItem>.IndexKeys.Ascending(x => x.Status).Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_status_createdAt" }),
            new CreateIndexModel<CheckItem>(
                Builders<CheckItem>.IndexKeys.Ascending(x => x.OwnerUserId).Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_owner_createdAt" })
        ], cancellationToken);

        await UncheckedReasons.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<UncheckedReason>(
                Builders<UncheckedReason>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.UpdatedAt),
                new CreateIndexOptions { Name = "ix_user_updatedAt" }),
            new CreateIndexModel<UncheckedReason>(
                Builders<UncheckedReason>.IndexKeys.Ascending(x => x.ItemId).Ascending(x => x.UserId),
                new CreateIndexOptions { Unique = true, Name = "ux_item_user" })
        ], cancellationToken);

        await PaymentBills.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<PaymentBill>(
                Builders<PaymentBill>.IndexKeys.Ascending("payers.userId").Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_payers_user_createdAt" }),
            new CreateIndexModel<PaymentBill>(
                Builders<PaymentBill>.IndexKeys.Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_createdAt" })
        ], cancellationToken);

        await Receipts.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Receipt>(
                Builders<Receipt>.IndexKeys.Ascending(x => x.PaymentBillId).Ascending(x => x.UserId),
                new CreateIndexOptions { Unique = true, Name = "ux_bill_user" }),
            new CreateIndexModel<Receipt>(
                Builders<Receipt>.IndexKeys.Ascending(x => x.Signature),
                new CreateIndexOptions { Unique = true, Name = "ux_signature" }),
            new CreateIndexModel<Receipt>(
                Builders<Receipt>.IndexKeys.Ascending(x => x.UserId).Descending(x => x.IssuedAt),
                new CreateIndexOptions { Name = "ix_user_issuedAt" })
        ], cancellationToken);

        await PayoutBills.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<PayoutBill>(
                Builders<PayoutBill>.IndexKeys.Ascending(x => x.UserId).Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_user_createdAt" }),
            new CreateIndexModel<PayoutBill>(
                Builders<PayoutBill>.IndexKeys.Ascending(x => x.PaymentBillId).Ascending(x => x.UserId),
                new CreateIndexOptions { Unique = true, Name = "ux_paymentBill_user" }),
            new CreateIndexModel<PayoutBill>(
                Builders<PayoutBill>.IndexKeys.Ascending(x => x.Signature),
                new CreateIndexOptions { Unique = true, Name = "ux_signature" })
        ], cancellationToken);

        await UserLoginLogs.Indexes.CreateOneAsync(
            new CreateIndexModel<UserLoginLog>(
                Builders<UserLoginLog>.IndexKeys.Ascending(x => x.UserId).Descending(x => x.LoginAt),
                new CreateIndexOptions { Name = "ix_user_loginAt" }),
            cancellationToken: cancellationToken);
    }
}
