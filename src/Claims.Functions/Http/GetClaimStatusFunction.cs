using System.Net;
using System.Text.Json;
using Claims.Contracts.Status;
using Claims.Domain;
using Claims.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Claims.Functions.Http;

/// <summary>
/// Reports where a claim has got to, for the channel that lodged it.
/// </summary>
public sealed class GetClaimStatusFunction
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly IClaimRepository _claims;
    private readonly ILogger<GetClaimStatusFunction> _logger;

    public GetClaimStatusFunction(IClaimRepository claims, ILogger<GetClaimStatusFunction> logger)
    {
        _claims = claims;
        _logger = logger;
    }
    
    /// <summary>Returns the current state of a claim and its audit trail.</summary>
    /// <param name="request">Incoming HTTP request.</param>
    /// <param name="claimId">The ID of the claim, taken from the route.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>The claim's status, or not found when the ID is unknown.</returns>
    [Function("GetClaimStatus")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "claims/{claimId:guid}")]
        HttpRequestData request,
        Guid claimId,
        CancellationToken cancellationToken)
    {
        var claim = await _claims.GetByIdAsync(claimId, cancellationToken);

        if (claim is null)
        {
            return await Json(request, HttpStatusCode.NotFound, new { error = $"No claim found with ID {claimId}."});
        }

        return await Json(request, HttpStatusCode.OK, ToResponse(claim));
    }

    /// <summary>Projects the claim aggregate onto the status contract.</summary>
    private static ClaimStatusResponse ToResponse(Claim claim) =>
        new()
        {
            ClaimId = claim.Id,
            ClaimReference = claim.ClaimReference,
            Type = claim.Type,
            Status = claim.Status,
            Priority = claim.Priority,
            SubmittedDate = claim.SubmittedDate,
            Deadline = claim.Deadline,
            SlaBreached = claim.SlaBreached,
            ApprovedAmount = claim.ApprovedAmount,
            Currency = claim.Currency,
            PaymentReference = claim.PaymentReference,
            PaymentStatus = claim.PaymentStatus,
            History = [.. claim.History
                .OrderBy(entry => entry.OccurredAt)
                .Select(entry => new ClaimStatusHistoryEntry
                {
                    Status = entry.Status,
                    OccuredAt = entry.OccurredAt,
                    Detail = entry.Detail
                })]
        };

    private static async Task<HttpResponseData> Json(HttpRequestData request, HttpStatusCode statusCode, object payload)
    {
        var response = request.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(payload, JsonSerializerOptions));
        return response;
    }
}
