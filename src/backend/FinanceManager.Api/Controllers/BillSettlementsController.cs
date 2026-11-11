using System.Globalization;
using System.Security.Claims;
using FinanceManager.Api.Http;
using FinanceManager.Application.Bills;
using FinanceManager.Domain.Bills;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/bills/{billId:guid}")]
public sealed class BillSettlementsController(BillSettlementService settlements) : ControllerBase
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("eligible-expenses")]
    public async Task<IActionResult> Eligible(Guid billId, [FromQuery] string? from, [FromQuery] string? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        if (!TryDate(from, out var parsedFrom) || !TryDate(to, out var parsedTo)) return Failure(SettlementError.InvalidInput);
        var result = await settlements.EligibleAsync(OwnerId, billId, parsedFrom, parsedTo, page, pageSize, cancellationToken);
        if (result.Value is not { } value) return Failure(result.Error);
        return Ok(new { items = value.Items.Select(row => new { transactionId = row.TransactionId,
            transactionVersion = AccountVersionHeaders.Format(row.Version), row.AccountId, row.AccountArchived,
            row.Amount, row.Description, transactionDate = row.TransactionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }),
            value.Page, value.PageSize, value.TotalCount });
    }

    [HttpPost("settlements")]
    public async Task<IActionResult> Settle(Guid billId, SettlementRequest request, CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var keys) || keys.Count != 1 ||
            !Guid.TryParseExact(keys[0], "D", out var key) || key == Guid.Empty)
            return ApiProblems.Result(400, "INVALID_IDEMPOTENCY_KEY", "Informe uma chave de tentativa válida.");
        if (BillVersionHeaders.Parse(request.BillVersion, out var billVersion) != VersionHeaderError.None)
            return ApiProblems.Result(400, "INVALID_BILL_VERSION", "A versão informada para a obrigação é inválida.");
        SettlementDraft draft;
        if (request.Mode == "link" && request.TransactionId is Guid transactionId &&
            AccountVersionHeaders.Parse(request.TransactionVersion, out var transactionVersion) == VersionHeaderError.None &&
            request.AccountId is null && request.PaymentDate is null)
            draft = new(SettlementMode.Link, billVersion, transactionId, transactionVersion, null, null);
        else if (request.Mode == "create" && request.AccountId is Guid accountId &&
            TryDate(request.PaymentDate, out var paymentDate) && paymentDate is not null && request.TransactionId is null && request.TransactionVersion is null)
            draft = new(SettlementMode.Create, billVersion, null, null, accountId, paymentDate);
        else return Failure(SettlementError.InvalidInput);
        var result = await settlements.SettleAsync(OwnerId, billId, key, draft, cancellationToken);
        if (result.Value is not { } receipt) return Failure(result.Error);
        var response = Receipt(receipt);
        return result.Replayed ? Ok(response) : StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("settlements/{settlementId:guid}/status")]
    public async Task<IActionResult> Reverse(Guid billId, Guid settlementId, SettlementStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Status != "reversed") return Failure(SettlementError.InvalidInput);
        var error = await settlements.ReverseAsync(OwnerId, billId, settlementId, cancellationToken);
        return error == SettlementError.None ? NoContent() : Failure(error);
    }

    private static object Receipt(SettlementReceipt receipt) => new { id = receipt.Id, receipt.BillId,
        receipt.TransactionId, origin = receipt.Origin == BillSettlementOrigin.LinkedExpense ? "linkedExpense" : "createdExpense",
        receipt.CreatedAtUtc };

    internal static ObjectResult Failure(SettlementError error) => error switch
    {
        SettlementError.InvalidInput => ApiProblems.Result(400, "VALIDATION_FAILED", "Confira os dados da baixa."),
        SettlementError.BillNotFound => ApiProblems.Result(404, "BILL_NOT_FOUND", "Obrigação não encontrada."),
        SettlementError.SettlementNotFound => ApiProblems.Result(404, "SETTLEMENT_NOT_FOUND", "Baixa não encontrada."),
        SettlementError.AccountNotFound => ApiProblems.Result(404, "ACCOUNT_NOT_FOUND", "Conta não encontrada."),
        SettlementError.TransactionNotFound => ApiProblems.Result(404, "TRANSACTION_NOT_FOUND", "Movimentação não encontrada."),
        SettlementError.BillNotOpen => ApiProblems.Result(409, "BILL_NOT_OPEN", "A obrigação não está em aberto."),
        SettlementError.ExpenseNotEligible => ApiProblems.Result(409, "EXPENSE_NOT_ELIGIBLE", "A despesa não atende aos critérios da baixa."),
        SettlementError.AccountArchived => ApiProblems.Result(409, "ACCOUNT_ARCHIVED", "Escolha uma conta ativa para criar a despesa."),
        SettlementError.IdempotencyConflict => ApiProblems.Result(409, "IDEMPOTENCY_KEY_CONFLICT", "A chave de tentativa já foi usada com outros dados."),
        SettlementError.StaleBillVersion => ApiProblems.Result(409, "STALE_BILL_VERSION", "A obrigação mudou. Recarregue os dados."),
        SettlementError.StaleTransactionVersion => ApiProblems.Result(409, "STALE_TRANSACTION_VERSION", "A movimentação mudou. Selecione-a novamente."),
        _ => throw new InvalidOperationException("Resultado de baixa inválido.")
    };

    private static bool TryDate(string? value, out DateOnly? date)
    {
        date = null;
        if (value is null) return true;
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false;
        date = parsed;
        return true;
    }
}

public sealed record SettlementRequest(string Mode, string BillVersion, Guid? TransactionId,
    string? TransactionVersion, Guid? AccountId, string? PaymentDate);
public sealed record SettlementStatusRequest(string Status);
