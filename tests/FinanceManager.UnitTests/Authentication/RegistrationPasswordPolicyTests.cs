using FinanceManager.Domain.Authentication;

namespace FinanceManager.UnitTests.Authentication;

public sealed class RegistrationPasswordPolicyTests
{
    [Theory]
    [InlineData("Aa1!aaaaaaa", false)]
    [InlineData("Aa1!aaaaaaaa", true)]
    [InlineData("aa1!aaaaaaaa", false)]
    [InlineData("AA1!AAAAAAAA", false)]
    [InlineData("Aa١!aaaaaaaa", false)]
    [InlineData("Aa1 aaaaaaaa", false)]
    [InlineData(" Aa1!aaaaaaa", true)]
    public void EvaluatesEachRequirement(string password, bool valid) =>
        Assert.Equal(valid, RegistrationPasswordPolicy.Evaluate(password).IsValid);

    [Fact]
    public void CountsUnicodeCodePointsAndEnforcesBothLimits()
    {
        Assert.True(RegistrationPasswordPolicy.Evaluate("Aa1!" + new string('a', 124)).IsValid);
        Assert.False(RegistrationPasswordPolicy.Evaluate("Aa1!" + new string('a', 125)).IsValid);
        Assert.True(RegistrationPasswordPolicy.Evaluate("Aa1!aaaaaaa😀").IsValid);
    }
}
