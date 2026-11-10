using System.Net.Http.Headers;

namespace FinanceManager.Api.Http;

internal enum VersionHeaderError { None, Missing, Invalid }

internal static class AccountVersionHeaders
{
    public static string Format(byte[] version) => $"\"{Convert.ToBase64String(version)}\"";

    public static VersionHeaderError Read(IHeaderDictionary headers, out byte[] version)
    {
        version = [];
        if (!headers.TryGetValue("If-Match", out var values))
        {
            return VersionHeaderError.Missing;
        }
        if (values.Count != 1 || !EntityTagHeaderValue.TryParse(values[0], out var tag) ||
            tag.IsWeak || tag.Tag.Length < 2)
        {
            return VersionHeaderError.Invalid;
        }
        var encoded = tag.Tag.AsSpan(1, tag.Tag.Length - 2);
        Span<byte> bytes = stackalloc byte[8];
        if (!Convert.TryFromBase64Chars(encoded, bytes, out var length) || length != bytes.Length ||
            !encoded.SequenceEqual(Convert.ToBase64String(bytes)))
        {
            return VersionHeaderError.Invalid;
        }
        version = bytes.ToArray();
        return VersionHeaderError.None;
    }
}
