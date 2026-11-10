using System.Data;
using System.Globalization;
using FinanceManager.Application.Transactions;
using FinanceManager.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FinanceManager.Infrastructure.Persistence;

internal sealed class SqlTransactionHistoryRepository(FinanceManagerDbContext database) : ITransactionHistoryRepository
{
    public Task<bool> AccountExistsAsync(Guid ownerId, Guid accountId, CancellationToken cancellationToken) =>
        database.FinancialAccounts.AnyAsync(row => row.OwnerId == ownerId && row.Id == accountId, cancellationToken);

    public async Task<TransactionHistoryPage> QueryAsync(TransactionHistoryQuery request, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var filtered = database.FinancialTransactions.AsNoTracking().Where(row => row.OwnerId == request.OwnerId &&
            row.TransactionDate >= request.From && row.TransactionDate <= request.To);
        if (request.AccountId is Guid account) filtered = filtered.Where(row => row.AccountId == account);
        if (request.Kind is TransactionKind kind) filtered = filtered.Where(row => row.Kind == kind);

        var count = await filtered.LongCountAsync(cancellationToken);
        var items = await filtered.OrderByDescending(row => row.TransactionDate).ThenByDescending(row => row.Id)
            .Skip(request.Offset).Take(request.PageSize)
            .Select(row => new StoredTransaction(row,
                EF.Property<byte[]>(row, FinancialTransactionConfiguration.VersionProperty)))
            .ToListAsync(cancellationToken);
        var totals = await ReadTotalsAsync(request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(items, request.Page, request.PageSize, count, request.From, request.To, totals);
    }

    private async Task<TransactionTotals> ReadTotalsAsync(TransactionHistoryQuery request, CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = database.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = """
            SELECT
              COALESCE(SUM(CASE WHEN [Kind] = N'Income' AND [Status] = N'Active' THEN CONVERT(decimal(38,2), [Amount]) ELSE CONVERT(decimal(38,2), 0) END), 0),
              COALESCE(SUM(CASE WHEN [Kind] = N'Expense' AND [Status] = N'Active' THEN CONVERT(decimal(38,2), [Amount]) ELSE CONVERT(decimal(38,2), 0) END), 0),
              COALESCE(SUM(CASE WHEN [Status] <> N'Active' THEN CONVERT(decimal(38,2), 0) WHEN [Kind] = N'Income' THEN CONVERT(decimal(38,2), [Amount]) ELSE -CONVERT(decimal(38,2), [Amount]) END), 0)
            FROM [FinancialTransactions]
            WHERE [OwnerId] = @owner AND [TransactionDate] >= @from AND [TransactionDate] <= @to
              AND (@account IS NULL OR [AccountId] = @account)
              AND (@kind IS NULL OR [Kind] = @kind)
            """;
        Add("@owner", request.OwnerId);
        Add("@from", request.From.ToDateTime(TimeOnly.MinValue));
        Add("@to", request.To.ToDateTime(TimeOnly.MinValue));
        Add("@account", request.AccountId ?? (object)DBNull.Value);
        Add("@kind", request.Kind?.ToString() ?? (object)DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        string Money(int ordinal) => reader.GetFieldValue<System.Data.SqlTypes.SqlDecimal>(ordinal)
            .ToString().Contains('.') ? reader.GetFieldValue<System.Data.SqlTypes.SqlDecimal>(ordinal).ToString() :
            reader.GetFieldValue<System.Data.SqlTypes.SqlDecimal>(ordinal).ToString() + ".00";
        return new(Money(0), Money(1), Money(2));

        void Add(string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
    }
}
