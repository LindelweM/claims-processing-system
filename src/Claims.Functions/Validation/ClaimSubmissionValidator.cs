using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Claims.Contracts.Enums;
using Claims.Contracts.Intake;

namespace Claims.Functions.Validation;

/// <summary>
/// Checks a claim submission before anything is stored or started, so a malformed form is
/// answered with a 400 listing every problem rather than failing somewhere downstream.
/// </summary>
public static class ClaimSubmissionValidator
{
    /// <summary>Validates a submission.</summary>
    /// <param name="submission">Submission to check.</param>
    /// <param name="today">Today's date, which an incident date may not be after.</param>
    /// <returns>Problems keyed by the camel-case path of the field at fault; empty when valid.</returns>
    public static IReadOnlyDictionary<string, string[]> Validate(ClaimSubmissionRequest submission, DateOnly today)
    {
        var errors = new Dictionary<string, List<string>>();

        // DataAnnotations do not descend into nested objects, so each part is checked in turn.
        Annotate(submission, prefix: null, errors);
        AnnotateRequired(submission.Claimant, "claimant", errors);
        AnnotateRequired(submission.Policy, "policy", errors);
        AnnotateRequired(submission.Incident, "incident", errors);
        AnnotateRequired(submission.BankingDetails, "bankingDetails", errors);

        if (submission.ClaimType == ClaimType.Unknown || !Enum.IsDefined(submission.ClaimType))
        {
            Add(errors, "claimType", "Claim type must be one of the supported types.");
        }

        if (submission.Incident is { } incident && incident.IncidentDate > today)
        {
            Add(errors, "incident.incidentDate", "Incident date cannot be in the future.");
        }

        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }

    private static void AnnotateRequired(object? part, string name, Dictionary<string, List<string>> errors)
    {
        // A part sent as an explicit null gets past deserialisation, so it is caught here.
        if (part is null)
        {
            Add(errors, name, $"The {name} field is required.");
            return;
        }

        Annotate(part, name, errors);
    }

    private static void Annotate(object part, string? prefix, Dictionary<string, List<string>> errors)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(part, new ValidationContext(part), results, validateAllProperties: true);

        foreach (var result in results)
        {
            foreach (var member in result.MemberNames.DefaultIfEmpty(string.Empty))
            {
                var field = JsonNamingPolicy.CamelCase.ConvertName(member);
                var key = prefix is null ? field : $"{prefix}.{field}";
                Add(errors, key, result.ErrorMessage ?? "Invalid value.");
            }
        }
    }

    private static void Add(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            errors[key] = messages = [];
        }

        messages.Add(message);
    }
}
