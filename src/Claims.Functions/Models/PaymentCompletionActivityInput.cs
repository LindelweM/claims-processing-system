using Claims.Contracts.Integration;

public sealed class PaymentCompletionActivityInput
{
    public required Guid ClaimId { get; init; }
    public required PaymentCompletionNotification Notification { get; init; }

}