using FinanceManager.Application.Authentication;
using FinanceManager.Domain.Authentication;

namespace FinanceManager.UnitTests.Authentication;

public sealed class OwnerProfilePasswordTests
{
    [Theory]
    [InlineData("Aa1!aaaaaa😀", false)]
    [InlineData("Aa1!aaaaaaa😀", true)]
    [InlineData("aa1!aaaaaaaa", false)]
    [InlineData("AA1!AAAAAAAA", false)]
    [InlineData("Aa١!aaaaaaaa", false)]
    [InlineData("Aa1 aaaaaaaa", false)]
    [InlineData(" Aa1!aaaaaaa", true)]
    public void PasswordPolicyUsesUnicodeCodePointsAndRequiredCategories(string password, bool valid) =>
        Assert.Equal(valid, RegistrationPasswordPolicy.Evaluate(password).IsValid);

    [Fact]
    public void PasswordCommandStringRepresentationDoesNotRevealSecrets()
    {
        var password = new OwnerPasswordChange("atual-secreta", "nova-secreta", "confirmacao-secreta");
        Assert.DoesNotContain("secreta", password.ToString(), StringComparison.Ordinal);
    }
}
