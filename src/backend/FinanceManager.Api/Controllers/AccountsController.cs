using System.Security.Claims;
using FinanceManager.Api.Http;
using FinanceManager.Application.Accounts;
using FinanceManager.Domain.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts")]
public sealed class AccountsController(FinancialAccountService accounts) : ControllerBase
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var filter = ParseStatus(status);
        if (status is not null && filter is null)
        {
            return Failure(AccountError.InvalidInput);
        }
        var rows = await accounts.ListAsync(OwnerId, filter, cancellationToken);
        return Ok(rows.Select(row => AccountResponse.From(row.Account)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await accounts.GetAsync(OwnerId, id, cancellationToken);
        return row is null ? Failure(AccountError.NotFound) : Ok(WriteVersion(row));
    }

    [HttpPost]
    public async Task<IActionResult> Create(AccountRequest request, CancellationToken cancellationToken)
    {
        if (ParseType(request.Type) is not AccountType type)
        {
            return Failure(AccountError.InvalidInput);
        }
        var result = await accounts.CreateAsync(OwnerId, request.Name, type, cancellationToken);
        return result.Value is { } row
            ? CreatedAtAction(nameof(Get), new { id = row.Account.Id }, WriteVersion(row))
            : Failure(result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, AccountRequest request, CancellationToken cancellationToken)
    {
        var header = AccountVersionHeaders.Read(Request.Headers, out var version);
        if (header != VersionHeaderError.None)
        {
            return await VersionFailureAsync(id, header, cancellationToken);
        }
        if (ParseType(request.Type) is not AccountType type)
        {
            return Failure(AccountError.InvalidInput);
        }
        var result = await accounts.EditAsync(OwnerId, id, version, request.Name, type, cancellationToken);
        return result.Value is { } row ? Ok(WriteVersion(row)) : Failure(result.Error);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, AccountStatusRequest request, CancellationToken cancellationToken)
    {
        var header = AccountVersionHeaders.Read(Request.Headers, out var version);
        if (header != VersionHeaderError.None)
        {
            return await VersionFailureAsync(id, header, cancellationToken);
        }
        if (ParseStatus(request.Status) is not AccountStatus status)
        {
            return Failure(AccountError.InvalidInput);
        }
        var result = await accounts.ChangeStatusAsync(OwnerId, id, version, status, cancellationToken);
        return result.Value is { } row ? Ok(WriteVersion(row)) : Failure(result.Error);
    }

    private async Task<IActionResult> VersionFailureAsync(Guid id, VersionHeaderError error, CancellationToken cancellationToken)
    {
        if (await accounts.GetAsync(OwnerId, id, cancellationToken) is null)
        {
            return Failure(AccountError.NotFound);
        }
        return error == VersionHeaderError.Missing
            ? ApiProblems.Result(428, "ACCOUNT_VERSION_REQUIRED", "Recarregue a conta antes de salvar.")
            : ApiProblems.Result(400, "INVALID_ACCOUNT_VERSION", "A versão informada para a conta é inválida.");
    }

    private AccountResponse WriteVersion(StoredAccount row)
    {
        Response.Headers.ETag = AccountVersionHeaders.Format(row.Version);
        return AccountResponse.From(row.Account);
    }

    private static ObjectResult Failure(AccountError error) => error switch
    {
        AccountError.InvalidInput => ApiProblems.Result(400, "VALIDATION_FAILED", "Confira o nome, o tipo e o estado informados."),
        AccountError.NotFound => ApiProblems.Result(404, "ACCOUNT_NOT_FOUND", "Conta não encontrada."),
        AccountError.NameConflict => ApiProblems.Result(409, "ACCOUNT_NAME_CONFLICT", "Outra conta já usa esse nome."),
        AccountError.StaleVersion => ApiProblems.Result(412, "STALE_ACCOUNT_VERSION", "A conta mudou. Recarregue os dados antes de tentar novamente."),
        _ => throw new InvalidOperationException("Resultado de conta inválido.")
    };

    private static AccountType? ParseType(string value) => value switch
    {
        "checking" => AccountType.Checking,
        "savings" => AccountType.Savings,
        "wallet" => AccountType.Wallet,
        _ => null
    };

    private static AccountStatus? ParseStatus(string? value) => value switch
    {
        "active" => AccountStatus.Active,
        "archived" => AccountStatus.Archived,
        _ => null
    };
}

public sealed record AccountRequest(string Name, string Type);
public sealed record AccountStatusRequest(string Status);

internal sealed record AccountResponse(Guid Id, string Name, string Type, string Currency, string Status)
{
    public static AccountResponse From(FinancialAccount account) =>
        new(account.Id, account.Name, account.Type.ToString().ToLowerInvariant(), account.Currency,
            account.Status.ToString().ToLowerInvariant());
}
