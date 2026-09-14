namespace Claims.Integration.Payments;

/// <summary>
/// Settings shared with the payment provider for authenticating its settlement callbacks.
/// </summary>
public sealed class PaymentCallbackOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string ConfigurationSection = "Payment";

    /// <summary>
    /// Secret the provider signs each callback with. Hold it in Key Vault, never in source.
    /// </summary>
    public string CallbackSigningSecret { get; set; } = string.Empty;

    /// <summary>
    /// How far a callback's timestamp may drift from the current time before it is refused,
    /// which bounds how long a captured callback could be replayed.
    /// </summary>
    public TimeSpan SignatureTolerance { get; set; } = TimeSpan.FromMinutes(5);
}
