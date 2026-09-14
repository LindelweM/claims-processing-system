using Claims.Contracts.Enums;

namespace Claims.Domain;

/// <summary>
/// Assigns a processing priority to a claim from the type of cover being claimed.
/// </summary>
/// <remarks>
/// Priority is driven by claim type alone; the amount claimed does not raise it. A death
/// claim is critical because the family is left without income, funeral and dread disease
/// are high because the costs land immediately, and everything else is standard.
/// </remarks>
public static class PriorityResolver
{
    /// <summary>
    /// Resolves the priority a claim should be worked at.
    /// </summary>
    /// <param name="claimType">Category of cover being claimed.</param>
    /// <returns>The priority to assign to the claim.</returns>
    public static ClaimPriority Resolve(ClaimType claimType)
    {
        return claimType switch
        {
            ClaimType.Death => ClaimPriority.Critical,
            ClaimType.Funeral => ClaimPriority.High,
            ClaimType.DreadDisease => ClaimPriority.High,
            _ => ClaimPriority.Standard
        };
    }
}
