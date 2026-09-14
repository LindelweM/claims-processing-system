using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Claims.Integration.Payments;

/// <summary>
/// Signs and verifies settlement callbacks, so only the payment provider can settle a claim.
/// </summary>
/// <remarks>
/// The signature is a hex HMAC-SHA256 over <c>{timestamp}.{body}</c>, keyed with the shared
/// secret. Including the timestamp means a captured callback cannot be replayed once it falls
/// outside the tolerance window.
/// </remarks>
public static class PaymentCallbackSignature
{
    /// <summary>Header carrying the Unix time, in seconds, at which the callback was signed.</summary>
    public const string TimestampHeader = "X-Payment-Timestamp";

    /// <summary>Header carrying the callback's signature.</summary>
    public const string SignatureHeader = "X-Payment-Signature";

    /// <summary>Computes the signature for a callback body.</summary>
    /// <param name="secret">Secret shared with the payment provider.</param>
    /// <param name="timestamp">Unix time, in seconds, the callback is signed at.</param>
    /// <param name="body">Raw callback body, exactly as sent.</param>
    /// <returns>The lowercase hex signature.</returns>
    public static string Compute(string secret, long timestamp, string body)
    {
        var payload = Encoding.UTF8.GetBytes($"{timestamp.ToString(CultureInfo.InvariantCulture)}.{body}");
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Checks a callback's signature and that it was signed recently.</summary>
    /// <param name="secret">Secret shared with the payment provider.</param>
    /// <param name="timestampHeader">Value of <see cref="TimestampHeader"/>, if sent.</param>
    /// <param name="signatureHeader">Value of <see cref="SignatureHeader"/>, if sent.</param>
    /// <param name="body">Raw callback body, exactly as received.</param>
    /// <param name="now">Current time.</param>
    /// <param name="tolerance">How far the timestamp may be from <paramref name="now"/>.</param>
    /// <returns>True only when the signature matches and the timestamp is within tolerance.</returns>
    public static bool IsValid(
        string secret,
        string? timestampHeader,
        string? signatureHeader,
        string body,
        DateTimeOffset now,
        TimeSpan tolerance)
    {
        if (string.IsNullOrEmpty(signatureHeader) ||
            !long.TryParse(timestampHeader, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp))
        {
            return false;
        }

        var signedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        if ((now - signedAt).Duration() > tolerance)
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(Compute(secret, timestamp, body));
        var actual = Encoding.ASCII.GetBytes(signatureHeader.ToLowerInvariant());

        // Constant-time comparison, so response timing cannot reveal how much of a guess matched.
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
