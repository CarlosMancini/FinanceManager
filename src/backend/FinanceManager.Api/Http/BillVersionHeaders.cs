using FinanceManager.Application.Bills;
using FinanceManager.Domain.Bills;
using Microsoft.Extensions.Primitives;

namespace FinanceManager.Api.Http;

internal static class BillVersionHeaders
{
    public static string Format(byte[] version, BillStatus status)
    {
        var payload = new byte[version.Length + 1];
        version.CopyTo(payload, 0);
        payload[^1] = (byte)status;
        return $"\"{Convert.ToBase64String(payload)}\"";
    }

    public static VersionHeaderError Read(IHeaderDictionary headers, out BillRevision revision)
    {
        revision = new([], default);
        if (!headers.TryGetValue("If-Match", out StringValues values)) return VersionHeaderError.Missing;
        if (values.Count != 1) return VersionHeaderError.Invalid;
        return Parse(values[0], out revision);
    }

    public static VersionHeaderError Parse(string? value, out BillRevision revision)
    {
        revision = new([], default);
        if (value is null || value.Length < 3 || value[0] != '"' || value[^1] != '"') return VersionHeaderError.Invalid;
        try
        {
            var payload = Convert.FromBase64String(value[1..^1]);
            if (payload.Length != 9 || !Enum.IsDefined((BillStatus)payload[^1])) return VersionHeaderError.Invalid;
            revision = new(payload[..^1], (BillStatus)payload[^1]);
            return VersionHeaderError.None;
        }
        catch (FormatException) { return VersionHeaderError.Invalid; }
    }
}
