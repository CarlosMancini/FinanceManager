using System.Text.Json;
using FinanceManager.Application.Authentication;
using FinanceManager.Domain.Authentication;

namespace FinanceManager.Api.Controllers;

internal static class OwnerProfileRequests
{
    private static readonly HashSet<string> Fields = ["fullName", "nickname", "birthDate", "email", "phoneNumber"];

    public static bool TryRead(JsonElement root, out OwnerProfileCommand? command)
    {
        command = null;
        if (root.ValueKind != JsonValueKind.Object) return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        JsonElement changes = default, originals = default, password = default;
        foreach (var property in root.EnumerateObject())
        {
            if (!seen.Add(property.Name)) return false;
            switch (property.Name)
            {
                case "changes": changes = property.Value; break;
                case "originalValues": originals = property.Value; break;
                case "passwordChange": password = property.Value; break;
                default: return false;
            }
        }
        if (!TryFields(changes, out var desired) || !TryFields(originals, out var baseline) ||
            !desired.Keys.ToHashSet().SetEquals(baseline.Keys)) return false;
        OwnerPasswordChange? passwordChange = null;
        if (seen.Contains("passwordChange"))
        {
            if (password.ValueKind != JsonValueKind.Object) return false;
            var parts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in password.EnumerateObject())
            {
                if (field.Name is not ("currentPassword" or "newPassword" or "confirmPassword") ||
                    field.Value.ValueKind != JsonValueKind.String || !parts.TryAdd(field.Name, field.Value.GetString()!)) return false;
            }
            if (parts.Count != 3) return false;
            passwordChange = new(parts["currentPassword"], parts["newPassword"], parts["confirmPassword"]);
        }
        command = new(new OwnerProfileChanges(desired, baseline), passwordChange);
        return true;
    }

    private static bool TryFields(JsonElement value, out Dictionary<string, string?> result)
    {
        result = new(StringComparer.Ordinal);
        if (value.ValueKind != JsonValueKind.Object) return false;
        foreach (var field in value.EnumerateObject())
        {
            if (!Fields.Contains(field.Name) || (field.Value.ValueKind != JsonValueKind.String &&
                !(field.Name == "nickname" && field.Value.ValueKind == JsonValueKind.Null))) return false;
            var text = field.Value.ValueKind == JsonValueKind.Null ? null : field.Value.GetString();
            if ((field.Name != "nickname" && string.IsNullOrWhiteSpace(text)) || !result.TryAdd(field.Name, text)) return false;
        }
        return true;
    }
}
