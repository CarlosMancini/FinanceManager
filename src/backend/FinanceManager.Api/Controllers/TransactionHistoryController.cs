using System.Globalization;
using System.Security.Claims;
using FinanceManager.Application.Transactions;
using FinanceManager.Domain.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionHistoryController(TransactionHistoryService history) : ControllerBase
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? accountId, [FromQuery] string? kind,
        [FromQuery] string? from, [FromQuery] string? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var parsedKind = kind switch { null => null, "income" => TransactionKind.Income, "expense" => TransactionKind.Expense, _ => (TransactionKind?)null };
        if (kind is not null && parsedKind is null || !TryDate(from, out var parsedFrom) || !TryDate(to, out var parsedTo))
            return TransactionsController.Failure(TransactionError.InvalidInput);
        var result = await history.QueryAsync(OwnerId,
            new(accountId, parsedKind, parsedFrom, parsedTo, page, pageSize), cancellationToken);
        if (result.Value is not { } value) return TransactionsController.Failure(result.Error);
        return Ok(new
        {
            items = value.Items.Select(row => TransactionResponse.From(row.Transaction)),
            value.Page,
            value.PageSize,
            value.TotalCount,
            from = value.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            to = value.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            value.Totals
        });
    }

    private static bool TryDate(string? input, out DateOnly? value)
    {
        value = null;
        if (input is null) return true;
        if (!DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return false;
        value = date;
        return true;
    }
}
