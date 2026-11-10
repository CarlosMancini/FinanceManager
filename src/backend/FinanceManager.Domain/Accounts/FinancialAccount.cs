using System.Text;

namespace FinanceManager.Domain.Accounts;

public enum AccountType { Checking, Savings, Wallet }
public enum AccountStatus { Active, Archived }

public sealed class FinancialAccount
{
    private FinancialAccount() { }

    public FinancialAccount(Guid ownerId, string name, AccountType type)
    {
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        Edit(name, type);
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Name { get; private set; } = "";
    public string NormalizedName { get; private set; } = "";
    public AccountType Type { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public AccountStatus Status { get; private set; } = AccountStatus.Active;
    public bool AcceptsNewTransactions => Status == AccountStatus.Active;

    public bool Edit(string name, AccountType type)
    {
        ArgumentNullException.ThrowIfNull(name);
        var trimmed = name.Trim();
        if (trimmed.Length is < 1 or > 120)
        {
            throw new ArgumentException("O nome deve ter de 1 a 120 caracteres.", nameof(name));
        }
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentException("Tipo de conta inválido.", nameof(type));
        }

        var normalized = trimmed.Normalize(NormalizationForm.FormC).ToUpperInvariant().Normalize(NormalizationForm.FormC);
        if (Name == trimmed && Type == type)
        {
            return false;
        }
        Name = trimmed;
        NormalizedName = normalized;
        Type = type;
        return true;
    }

    public bool ChangeStatus(AccountStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentException("Estado de conta inválido.", nameof(status));
        }
        if (Status == status)
        {
            return false;
        }
        Status = status;
        return true;
    }
}
