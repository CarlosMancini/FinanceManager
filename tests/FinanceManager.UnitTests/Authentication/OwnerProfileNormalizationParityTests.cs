using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FinanceManager.UnitTests.Authentication;

public sealed class OwnerProfileNormalizationParityTests
{
    [Fact]
    public void FrontendInvariantUppercaseMatchesCurrentDotnetRuntimeForEveryUnicodeScalar()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "FinanceManager.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var path = Path.Combine(root.FullName, "src", "frontend", "src", "app", "auth", "owner-profile-normalization.ts");
        var match = Regex.Match(File.ReadAllText(path), @"const mappings = '([^']*)';");
        Assert.True(match.Success);
        var map = match.Groups[1].Value.Split(',').Select(pair => pair.Split(':'))
            .ToDictionary(pair => int.Parse(pair[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                pair => int.Parse(pair[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        for (var value = 0; value <= 0x10ffff; value++)
        {
            if (!Rune.IsValid(value)) continue;
            var source = char.ConvertFromUtf32(value);
            var expected = source.ToUpperInvariant();
            var actual = char.ConvertFromUtf32(map.GetValueOrDefault(value, value));
            Assert.True(expected == actual, $"Normalização divergente para U+{value:X}.");
        }
    }
}
