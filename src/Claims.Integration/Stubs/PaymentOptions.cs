namespace Claims.Integration.Stubs;

/// <summary>
/// Controls how <see cref="StubPaymentClient"/> settles the payments it accepts, so a local
/// run can exercise the completion callback without a payment provider.
/// </summary>
public sealed class PaymentOptions
{
    /// <summary>
    /// Whether an accepted instruction is followed by a simulated completion callback to the
    /// request's callback address. Turn this off to leave payments sitting as pending.
    /// </summary>
    public bool AutoCompletePayments { get; set; } = true;

    /// <summary>
    /// How long after acceptance the simulated completion callback is sent, standing in for
    /// the time a real provider takes to settle.
    /// </summary>
    public int AutoCompleteDelaySeconds { get; set; } = 5;
}
