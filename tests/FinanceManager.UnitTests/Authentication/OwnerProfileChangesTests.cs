using FinanceManager.Domain.Authentication;

namespace FinanceManager.UnitTests.Authentication;

public sealed class OwnerProfileChangesTests
{
    private static readonly DateOnly Today = new(2026, 11, 22);
    private static RegistrationProfile Current() => RegistrationProfile.Create("Ana", "Nina", new(1990, 1, 1), "ana@example.test", "11999999999", Today);

    [Fact]
    public void RejectsStaleOriginalEvenWhenDesiredAlreadyMatches()
    {
        var edits = new OwnerProfileChanges(new Dictionary<string, string?> { ["fullName"] = "Ana" },
            new Dictionary<string, string?> { ["fullName"] = "Antiga" });
        var result = edits.Apply(Current(), Today);
        Assert.Contains("fullName", result.Conflicts);
        Assert.Null(result.Profile);
    }

    [Fact]
    public void MergesOnlyEditedFieldsAndAllowsRemovingNickname()
    {
        var edits = new OwnerProfileChanges(new Dictionary<string, string?> { ["nickname"] = null },
            new Dictionary<string, string?> { ["nickname"] = " Nina " });
        var result = edits.Apply(Current(), Today);
        Assert.Empty(result.Conflicts);
        Assert.Null(result.Profile!.Nickname);
        Assert.Equal("Ana", result.Profile.FullName);
        Assert.Equal("11999999999", result.Profile.PhoneNumber);
    }

    [Fact]
    public void RejectsDifferentKeysAndUnknownFields()
    {
        Assert.Throws<ArgumentException>(() => new OwnerProfileChanges(new Dictionary<string, string?> { ["nickname"] = null }, new Dictionary<string, string?>()));
        Assert.Throws<ArgumentException>(() => new OwnerProfileChanges(new Dictionary<string, string?> { ["ownerId"] = "x" }, new Dictionary<string, string?> { ["ownerId"] = "x" }));
    }

    [Fact]
    public void RejectsExtraOriginalsAndNullRequiredValues()
    {
        Assert.Throws<ArgumentException>(() => new OwnerProfileChanges(new Dictionary<string, string?>(), new Dictionary<string, string?> { ["fullName"] = "Ana" }));
        foreach (var field in new[] { "fullName", "birthDate", "email", "phoneNumber" })
        {
            var invalid = new OwnerProfileChanges(new Dictionary<string, string?> { [field] = null },
                new Dictionary<string, string?> { [field] = field switch { "fullName" => "Ana", "birthDate" => "1990-01-01", "email" => "ana@example.test", _ => "11999999999" } });
            Assert.Throws<ArgumentException>(() => invalid.Apply(Current(), Today));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyNicknameRemovesIt(string value)
    {
        var edits = new OwnerProfileChanges(new Dictionary<string, string?> { ["nickname"] = value },
            new Dictionary<string, string?> { ["nickname"] = "Nina" });
        Assert.Null(edits.Apply(Current(), Today).Profile!.Nickname);
    }

    [Fact]
    public void RevertedIntermediateChangeRequiresNoFieldHistory()
    {
        var intermediate = new OwnerProfileChanges(new Dictionary<string, string?> { ["fullName"] = "Intermediário" },
            new Dictionary<string, string?> { ["fullName"] = "Ana" }).Apply(Current(), Today).Profile!;
        var reverted = new OwnerProfileChanges(new Dictionary<string, string?> { ["fullName"] = "Ana" },
            new Dictionary<string, string?> { ["fullName"] = "Intermediário" }).Apply(intermediate, Today).Profile!;
        var edits = new OwnerProfileChanges(new Dictionary<string, string?> { ["fullName"] = "Nome novo" },
            new Dictionary<string, string?> { ["fullName"] = "Ana" });
        var result = edits.Apply(reverted, Today);
        Assert.Empty(result.Conflicts);
        Assert.Equal("Nome novo", result.Profile!.FullName);
    }

    [Fact]
    public void ComparesNormalizedEmailAndPhone()
    {
        var edits = new OwnerProfileChanges(new Dictionary<string, string?> { ["email"] = "novo@example.test", ["phoneNumber"] = "11988888888" },
            new Dictionary<string, string?> { ["email"] = " ANA@EXAMPLE.TEST ", ["phoneNumber"] = "(11) 99999-9999" });
        var result = edits.Apply(Current(), Today);
        Assert.Empty(result.Conflicts);
        Assert.Equal("novo@example.test", result.Profile!.Email);
        Assert.Equal("11988888888", result.Profile.PhoneNumber);
    }

    [Fact]
    public void ComparesCanonicalOriginalsForTrimmedNamesAndMaskedPhone()
    {
        var current = RegistrationProfile.Create("  José da Silva  ", "  Zé  ", new(1990, 1, 1),
            "PESSOA@EXAMPLE.TEST", "(11) 99999-9999", Today);
        var edits = new OwnerProfileChanges(
            new Dictionary<string, string?> { ["fullName"] = "Nome novo", ["nickname"] = "Outro", ["email"] = "novo@example.test", ["phoneNumber"] = "11988887777" },
            new Dictionary<string, string?> { ["fullName"] = "José da Silva", ["nickname"] = "Zé", ["email"] = "pessoa@example.test", ["phoneNumber"] = "11999999999" });

        var result = edits.Apply(current, Today);

        Assert.Empty(result.Conflicts);
        Assert.Equal("Nome novo", result.Profile!.FullName);
    }

    [Fact]
    public void DetectsUnicodeCaseEquivalentEmailOriginal()
    {
        var current = RegistrationProfile.Create("Ana", null, new(1990, 1, 1), "pessoa@exemplo.tést", "11999999999", Today);
        var edits = new OwnerProfileChanges(
            new Dictionary<string, string?> { ["email"] = "novo@example.test" },
            new Dictionary<string, string?> { ["email"] = "PESSOA@EXEMPLO.TÉST" });

        var result = edits.Apply(current, Today);

        Assert.Empty(result.Conflicts);
    }

    [Theory]
    [InlineData("josé@example.test", "JOSÉ@EXAMPLE.TEST")]
    [InlineData("maçã@example.test", "MAÇÃ@EXAMPLE.TEST")]
    [InlineData("üÿ@example.test", "ÜŸ@EXAMPLE.TEST")]
    public void AccentedEmailCaseChangesPreserveStoredSpelling(string stored, string desired)
    {
        var current = RegistrationProfile.Create("Ana", null, new(1990, 1, 1), stored, "11999999999", Today);
        var edits = new OwnerProfileChanges(new Dictionary<string, string?> { ["email"] = desired },
            new Dictionary<string, string?> { ["email"] = stored });
        Assert.Equal(stored, edits.Apply(current, Today).Profile!.Email);
    }

    [Fact]
    public void DoesNotTreatSharpSAsTwoLettersInEmail()
    {
        var current = RegistrationProfile.Create("Ana", null, new(1990, 1, 1), "straße@example.test", "11999999999", Today);
        var edits = new OwnerProfileChanges(
            new Dictionary<string, string?> { ["email"] = "strasse@example.test" },
            new Dictionary<string, string?> { ["email"] = "straße@example.test" });

        var result = edits.Apply(current, Today);

        Assert.Empty(result.Conflicts);
        Assert.Equal("strasse@example.test", result.Profile!.Email);
    }

    [Fact]
    public void EmptyChangesPreserveProfile()
    {
        var result = new OwnerProfileChanges(new Dictionary<string, string?>(), new Dictionary<string, string?>()).Apply(Current(), Today);
        Assert.Empty(result.Conflicts);
        Assert.Equal("Nina", result.Profile!.Nickname);
    }

    [Theory]
    [InlineData("2026-11-23")]
    [InlineData("2026-02-31")]
    public void RejectsInvalidBirthDate(string value)
    {
        var edits = new OwnerProfileChanges(new Dictionary<string, string?> { ["birthDate"] = value },
            new Dictionary<string, string?> { ["birthDate"] = "1990-01-01" });
        Assert.Throws<ArgumentException>(() => edits.Apply(Current(), Today));
    }

    [Theory]
    [InlineData("1199999999")]
    [InlineData("119999999999")]
    public void RejectsInvalidPhone(string value)
    {
        var edits = new OwnerProfileChanges(new Dictionary<string, string?> { ["phoneNumber"] = value },
            new Dictionary<string, string?> { ["phoneNumber"] = "11999999999" });
        Assert.Throws<ArgumentException>(() => edits.Apply(Current(), Today));
    }

    [Fact]
    public void RejectsMissingRequiredField()
    {
        var edits = new OwnerProfileChanges(new Dictionary<string, string?> { ["fullName"] = " " },
            new Dictionary<string, string?> { ["fullName"] = "Ana" });
        Assert.Throws<ArgumentException>(() => edits.Apply(Current(), Today));
    }

    [Fact]
    public void AllowsDisjointConcurrentChange()
    {
        var changedElsewhere = RegistrationProfile.Create("Ana Atual", "Nina", new(1990, 1, 1), "ana@example.test", "11999999999", Today);
        var edits = new OwnerProfileChanges(new Dictionary<string, string?> { ["phoneNumber"] = "11988888888" },
            new Dictionary<string, string?> { ["phoneNumber"] = "11999999999" });
        var result = edits.Apply(changedElsewhere, Today);
        Assert.Empty(result.Conflicts);
        Assert.Equal("Ana Atual", result.Profile!.FullName);
        Assert.Equal("11988888888", result.Profile.PhoneNumber);
    }

    [Fact]
    public void EquivalentEmailDoesNotChangeStoredSpelling()
    {
        var edits = new OwnerProfileChanges(new Dictionary<string, string?> { ["email"] = " ANA@EXAMPLE.TEST " },
            new Dictionary<string, string?> { ["email"] = "ana@example.test" });
        Assert.Equal("ana@example.test", edits.Apply(Current(), Today).Profile!.Email);
    }

    [Fact]
    public void AcceptsTodayAndPreservesAccentedName()
    {
        var edits = new OwnerProfileChanges(new Dictionary<string, string?> { ["birthDate"] = "2026-11-22", ["fullName"] = "  Ana D'Ávila  " },
            new Dictionary<string, string?> { ["birthDate"] = "1990-01-01", ["fullName"] = "Ana" });
        var profile = edits.Apply(Current(), Today).Profile!;
        Assert.Equal(Today, profile.BirthDate);
        Assert.Equal("Ana D'Ávila", profile.FullName);
    }

    [Theory]
    [InlineData("teste@gmail")]
    [InlineData("teste@dominio..com")]
    [InlineData("teste@dominio.com,br")]
    public void RejectsInvalidEmail(string value)
    {
        var edits = new OwnerProfileChanges(new Dictionary<string, string?> { ["email"] = value },
            new Dictionary<string, string?> { ["email"] = "ana@example.test" });
        Assert.Throws<ArgumentException>(() => edits.Apply(Current(), Today));
    }

    [Theory]
    [InlineData("ana@dominio.com")]
    [InlineData("ana@dominio.com.br")]
    [InlineData("pessoa@exemplo.tést")]
    public void AcceptsValidEmail(string value)
    {
        var edits = new OwnerProfileChanges(new Dictionary<string, string?> { ["email"] = value },
            new Dictionary<string, string?> { ["email"] = "ana@example.test" });
        Assert.Equal(value, edits.Apply(Current(), Today).Profile!.Email);
    }
}
