namespace Claims.Integration;

/// <summary>
/// Address and timeout for one downstream system the claims service calls.
/// </summary>
public sealed class IntegrationOptions
{
    /// <summary>Root address of the downstream service.</summary>
    public required Uri BaseAddress { get; set; }

    /// <summary>How long to wait for a response before abandoning the call.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
