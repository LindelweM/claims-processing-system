using Claims.Contracts.Enums;

namespace Claims.Domain.Sla;

/// <summary>
/// The turnaround times a claim is expected to be assessed within, and the deadlines they imply.
/// </summary>
/// <remarks>
/// Targets are elapsed calendar time, not working hours, so a claim submitted on a Friday
/// consumes its weekend. Move to a working-calendar implementation if the business measures
/// the SLA in business days.
/// </remarks>
public sealed class SlaPolicy
{
    /// <summary>Turnaround applied to a claim whose priority has no window of its own.</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(5);

    /// <summary>Turnaround target for each priority a claim can be worked at.</summary>
    private static readonly IReadOnlyDictionary<ClaimPriority, TimeSpan> Windows =
        new Dictionary<ClaimPriority, TimeSpan>
        {
            // Death claims leave a family without income, so they are assessed fastest.
            [ClaimPriority.Critical] = TimeSpan.FromHours(4),
            [ClaimPriority.High] = TimeSpan.FromHours(24),
            [ClaimPriority.Standard] = DefaultWindow
        };

    /// <summary>Returns the turnaround a claim of the given priority must be assessed within.</summary>
    /// <param name="priority">Priority the claim is worked at.</param>
    /// <returns>The turnaround target, or <see cref="DefaultWindow"/> for an unmapped priority.</returns>
    public TimeSpan WindowFor(ClaimPriority priority) =>
        Windows.TryGetValue(priority, out var window) ? window : DefaultWindow;

    /// <summary>Returns the date by which a claim submitted at the given time must be assessed.</summary>
    /// <param name="priority">Priority the claim is worked at.</param>
    /// <param name="submittedAt">When the claim was submitted.</param>
    /// <returns>The date by which the claim must be assessed.</returns>
    public DateTimeOffset DeadlineFrom(ClaimPriority priority, DateTimeOffset submittedAt) =>
        submittedAt.Add(WindowFor(priority));
}
