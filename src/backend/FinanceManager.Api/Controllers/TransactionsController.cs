using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using FinanceManager.Api.Http;
using FinanceManager.Application.Transactions;
using FinanceManager.Domain.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed partial class TransactionsController(TransactionCommandService transactions) : ControllerBase
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await transactions.GetAsync(OwnerId, id, cancellationToken);
        return row is null ? Failure(TransactionError.NotFound) : Ok(WriteVersion(row));
    }

    [HttpPost]
    public async Task<IActionResult> Create(TransactionRequest request, CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var keys) || keys.Count != 1 ||
            !Guid.TryParseExact(keys[0], "D", out var requestId) || requestId == Guid.Empty)
            return ApiProblems.Result(400, "INVALID_IDEMPOTENCY_KEY", "Informe uma chave de tentativa válida.");
        if (!TryDraft(request, out var draft)) return Failure(TransactionError.InvalidInput);
        var result = await transactions.CreateAsync(OwnerId, requestId, draft, cancellationToken);
        if (result.Value is not { } row) return Failure(result.Error);
        Response.Headers.Location = Url.ActionLink(nameof(Get), values: new { id = row.Transaction.Id });
        var response = WriteVersion(row);
        return result.Replayed ? Ok(response) : StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, TransactionRequest request, CancellationToken cancellationToken)
    {
        var versionFailure = await ReadVersionAsync(id, cancellationToken);
        if (versionFailure.Result is not null) return versionFailure.Result;
        if (!TryDraft(request, out var draft)) return Failure(TransactionError.InvalidInput);
        var result = await transactions.EditAsync(OwnerId, id, versionFailure.Version, draft, cancellationToken);
        return result.Value is { } row ? Ok(WriteVersion(row)) : Failure(result.Error);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> Cancel(Guid id, TransactionStatusRequest request, CancellationToken cancellationToken)
    {
        var versionFailure = await ReadVersionAsync(id, cancellationToken);
        if (versionFailure.Result is not null) return versionFailure.Result;
        if (request.Status != "canceled") return Failure(TransactionError.InvalidInput);
        var result = await transactions.CancelAsync(OwnerId, id, versionFailure.Version, cancellationToken);
        return result.Value is { } row ? Ok(WriteVersion(row)) : Failure(result.Error);
    }

    private async Task<(byte[] Version, IActionResult? Result)> ReadVersionAsync(Guid id, CancellationToken token)
    {
        var header = AccountVersionHeaders.Read(Request.Headers, out var version);
        if (header == VersionHeaderError.None) return (version, null);
        if (await transactions.GetAsync(OwnerId, id, token) is null) return ([], Failure(TransactionError.NotFound));
        return ([], header == VersionHeaderError.Missing
            ? ApiProblems.Result(428, "TRANSACTION_VERSION_REQUIRED", "Recarregue a movimentação antes de salvar.")
            : ApiProblems.Result(400, "INVALID_TRANSACTION_VERSION", "A versão informada para a movimentação é inválida."));
    }

    private TransactionResponse WriteVersion(StoredTransaction row)
    {
        Response.Headers.ETag = AccountVersionHeaders.Format(row.Version);
        return TransactionResponse.From(row.Transaction);
    }

    internal static ObjectResult Failure(TransactionError error) => error switch
    {
        TransactionError.InvalidInput => ApiProblems.Result(400, "VALIDATION_FAILED", "Confira os dados da movimentação."),
        TransactionError.AccountNotFound => ApiProblems.Result(404, "ACCOUNT_NOT_FOUND", "Conta não encontrada."),
        TransactionError.NotFound => ApiProblems.Result(404, "TRANSACTION_NOT_FOUND", "Movimentação não encontrada."),
        TransactionError.AccountArchived => ApiProblems.Result(409, "ACCOUNT_ARCHIVED", "A conta está arquivada."),
        TransactionError.IdempotencyConflict => ApiProblems.Result(409, "IDEMPOTENCY_KEY_CONFLICT", "A chave de tentativa já foi usada com outros dados."),
        TransactionError.Canceled => ApiProblems.Result(409, "TRANSACTION_CANCELED", "A movimentação está cancelada e não pode ser alterada."),
        TransactionError.StaleVersion => ApiProblems.Result(412, "STALE_TRANSACTION_VERSION", "A movimentação mudou. Recarregue os dados antes de tentar novamente."),
        TransactionError.Linked => ApiProblems.Result(409, "TRANSACTION_LINKED", "A movimentação possui uma baixa ativa. Reverta a baixa antes de alterá-la."),
        _ => throw new InvalidOperationException("Resultado de movimentação inválido.")
    };

    internal static bool TryDraft(TransactionRequest request, out TransactionDraft draft)
    {
        draft = default!;
        if (request is null || request.AccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.Kind) ||
            string.IsNullOrWhiteSpace(request.Amount) || string.IsNullOrWhiteSpace(request.TransactionDate) ||
            string.IsNullOrWhiteSpace(request.Description)) return false;
        var kind = request.Kind switch { "income" => TransactionKind.Income, "expense" => TransactionKind.Expense, _ => (TransactionKind?)null };
        if (kind is null || !AmountPattern().IsMatch(request.Amount) ||
            !decimal.TryParse(request.Amount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) ||
            amount <= 0m ||
            !DateOnly.TryParseExact(request.TransactionDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date)) return false;
        draft = new(request.AccountId, kind.Value, amount, date, request.Description);
        return true;
    }

    [GeneratedRegex("^(?:0|[1-9][0-9]*)(?:\\.[0-9]{1,2})?$")]
    private static partial Regex AmountPattern();
}

public sealed record TransactionRequest(Guid AccountId, string? Kind, string? Amount, string? TransactionDate, string? Description);
public sealed record TransactionStatusRequest(string Status);

internal sealed record TransactionResponse(Guid Id, Guid AccountId, string Kind, string Amount, string TransactionDate,
    string Description, string Currency, string Source, string Status)
{
    public static TransactionResponse From(FinancialTransaction value) => new(value.Id, value.AccountId,
        value.Kind.ToString().ToLowerInvariant(), value.Amount.ToString("0.00", CultureInfo.InvariantCulture),
        value.TransactionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), value.Description, value.Currency,
        value.Source == TransactionSource.BillSettlement ? "billSettlement" : "manual",
        value.Status.ToString().ToLowerInvariant());
}
