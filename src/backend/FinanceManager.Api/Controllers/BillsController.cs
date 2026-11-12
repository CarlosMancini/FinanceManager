using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using FinanceManager.Api.Http;
using FinanceManager.Application.Bills;
using FinanceManager.Domain.Bills;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/bills")]
public sealed partial class BillsController(BillService bills, TimeProvider clock) : ControllerBase
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? from, [FromQuery] string? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        if (!TryStatus(status, out var parsedStatus) || !TryDate(from, out var parsedFrom) || !TryDate(to, out var parsedTo))
            return Failure(BillError.InvalidInput);
        var result = await bills.QueryAsync(OwnerId, new(parsedStatus, parsedFrom, parsedTo, page, pageSize), cancellationToken);
        if (result.Value is not { } value) return Failure(result.Error);
        var today = BillService.Today(clock.GetUtcNow());
        return Ok(new
        {
            items = value.Items.Select(row => ToResponse(row, today)), value.Page, value.PageSize,
            value.TotalCount, totals = new { openAmount = value.OpenAmount }
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await bills.GetAsync(OwnerId, id, cancellationToken);
        return row is null ? Failure(BillError.NotFound) : Ok(Write(row));
    }

    [HttpPost]
    public async Task<IActionResult> Create(BillRequest request, CancellationToken cancellationToken)
    {
        if (!TryDraft(request, out var draft)) return Failure(BillError.InvalidInput);
        var result = await bills.CreateAsync(OwnerId, draft, cancellationToken);
        return result.Value is { } row ? CreatedAtAction(nameof(Get), new { id = row.Bill.Id }, Write(row)) : Failure(result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, BillRequest request, CancellationToken cancellationToken)
    {
        if (BillVersionHeaders.Read(Request.Headers, out var revision) is { } header && header != VersionHeaderError.None)
            return await VersionFailureAsync(id, header, cancellationToken);
        if (!TryDraft(request, out var draft)) return Failure(BillError.InvalidInput);
        var result = await bills.EditAsync(OwnerId, id, revision, draft, cancellationToken);
        return result.Value is { } row ? Ok(Write(row)) : Failure(result.Error);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> Cancel(Guid id, BillStatusRequest request, CancellationToken cancellationToken)
    {
        if (BillVersionHeaders.Read(Request.Headers, out var revision) is { } header && header != VersionHeaderError.None)
            return await VersionFailureAsync(id, header, cancellationToken);
        if (request.Status != "canceled") return Failure(BillError.InvalidInput);
        var result = await bills.CancelAsync(OwnerId, id, revision, cancellationToken);
        return result.Value is { } row ? Ok(Write(row)) : Failure(result.Error);
    }

    private object Write(StoredBill row)
    {
        var today = BillService.Today(clock.GetUtcNow());
        var status = row.Bill.GetStatus(today, row.HasActiveSettlement);
        Response.Headers.ETag = BillVersionHeaders.Format(row.Version, status);
        return ToResponse(row, today);
    }

    private static object ToResponse(StoredBill row, DateOnly today) => new
    {
        row.Bill.Id, row.Bill.Description,
        amount = row.Bill.Amount.ToString("0.00", CultureInfo.InvariantCulture),
        dueDate = row.Bill.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        row.Bill.Currency,
        status = Status(row.Bill.GetStatus(today, row.HasActiveSettlement)),
        activeSettlement = row.ActiveSettlement is null ? null : new
        {
            id = row.ActiveSettlement.Id,
            row.ActiveSettlement.TransactionId,
            origin = row.ActiveSettlement.Origin == BillSettlementOrigin.LinkedExpense ? "linkedExpense" : "createdExpense",
            row.ActiveSettlement.CreatedAtUtc
        }
    };

    private async Task<IActionResult> VersionFailureAsync(Guid id, VersionHeaderError error, CancellationToken token) =>
        await bills.GetAsync(OwnerId, id, token) is null ? Failure(BillError.NotFound) : error == VersionHeaderError.Missing
            ? ApiProblems.Result(428, "BILL_VERSION_REQUIRED", "Recarregue a obrigação antes de salvar.")
            : ApiProblems.Result(400, "INVALID_BILL_VERSION", "A versão informada para a obrigação é inválida.");

    internal static ObjectResult Failure(BillError error) => error switch
    {
        BillError.InvalidInput => ApiProblems.Result(400, "VALIDATION_FAILED", "Confira os dados da obrigação."),
        BillError.NotFound => ApiProblems.Result(404, "BILL_NOT_FOUND", "Obrigação não encontrada."),
        BillError.NotOpen => ApiProblems.Result(409, "BILL_NOT_OPEN", "A obrigação não está em aberto."),
        BillError.StaleVersion => ApiProblems.Result(412, "STALE_BILL_VERSION", "A obrigação mudou. Recarregue os dados."),
        _ => throw new InvalidOperationException("Resultado de obrigação inválido.")
    };

    private static bool TryDraft(BillRequest request, out BillDraft draft)
    {
        draft = default!;
        if (request is null || string.IsNullOrWhiteSpace(request.Description) || string.IsNullOrWhiteSpace(request.Amount) ||
            string.IsNullOrWhiteSpace(request.DueDate) || !AmountPattern().IsMatch(request.Amount) ||
            !decimal.TryParse(request.Amount, NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var amount) || !DateOnly.TryParseExact(request.DueDate, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var dueDate)) return false;
        if (amount <= 0m) return false;
        draft = new(request.Description, amount, dueDate);
        return true;
    }

    private static bool TryStatus(string? value, out BillFilterStatus status)
    {
        status = value switch { null or "open" => BillFilterStatus.Open, "upcoming" => BillFilterStatus.Upcoming,
            "dueToday" => BillFilterStatus.DueToday, "overdue" => BillFilterStatus.Overdue, "paid" => BillFilterStatus.Paid,
            "canceled" => BillFilterStatus.Canceled, "all" => BillFilterStatus.All, _ => (BillFilterStatus)(-1) };
        return Enum.IsDefined(status);
    }

    private static bool TryDate(string? value, out DateOnly? date)
    {
        date = null;
        if (value is null) return true;
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false;
        date = parsed;
        return true;
    }

    private static string Status(BillStatus status) => status switch
    {
        BillStatus.Upcoming => "upcoming", BillStatus.DueToday => "dueToday", BillStatus.Overdue => "overdue",
        BillStatus.Paid => "paid", BillStatus.Canceled => "canceled", _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    [GeneratedRegex("^(?:0|[1-9][0-9]*)(?:\\.[0-9]{1,2})?$")]
    private static partial Regex AmountPattern();
}

public sealed record BillRequest(string? Description, string? Amount, string? DueDate);
public sealed record BillStatusRequest(string Status);
