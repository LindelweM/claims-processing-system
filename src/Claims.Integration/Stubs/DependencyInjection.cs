using Claims.Integration.ClientRegistry;
using Claims.Integration.Payments;
using Claims.Integration.PolicyManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Claims.Integration.Stubs;

/// <summary>
/// Registers the in-memory stand-ins for the downstream systems.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Configuration section the stub payment behaviour binds from.</summary>
    public const string PaymentsSection = "Integration:Stubs:Payments";

    /// <summary>
    /// Replaces the client registry, policy manager and payment provider with stubs, so the
    /// service can be run end to end without reaching any external system.
    /// </summary>
    /// <param name="services">Container to register into.</param>
    /// <param name="configuration">Configuration to read stub behaviour from.</param>
    /// <returns>The container, for chaining.</returns>
    /// <remarks>
    /// Safe to call after <see cref="Claims.Integration.DependencyInjection.AddClaimsIntegration"/>:
    /// the real clients are removed first, so whichever endpoints are configured are not called.
    /// Never call this outside local runs and tests.
    /// </remarks>
    public static IServiceCollection AddClaimsIntegrationStubs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PaymentOptions>(configuration.GetSection(PaymentsSection));

        services.RemoveAll<IClientRegistryClient>();
        services.RemoveAll<IPolicyManagerClient>();
        services.RemoveAll<IPaymentClient>();

        services.AddSingleton<IClientRegistryClient, StubClientRegistryClient>();
        services.AddSingleton<IPolicyManagerClient, StubPolicyManagerClient>();
        services.AddSingleton<IPaymentClient, StubPaymentClient>();

        return services;
    }
}
