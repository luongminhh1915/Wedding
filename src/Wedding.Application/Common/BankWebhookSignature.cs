using System.Security.Cryptography;
using System.Text;

namespace Wedding.Application.Common;

public static class BankWebhookSignature
{
    public static bool IsValid(string? secret, string? signatureHex, string body)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32 || string.IsNullOrWhiteSpace(signatureHex)) return false;
        byte[] signature;
        try { signature = Convert.FromHexString(signatureHex); } catch (FormatException) { return false; }
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
        return signature.Length == expected.Length && CryptographicOperations.FixedTimeEquals(signature, expected);
    }
}
