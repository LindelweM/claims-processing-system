using Claims.Integration.ClientRegistry;
using Claims.Integration.Payments;
using Claims.Integration.PolicyManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Claims.Integration;

/// <summary>
/// Registers the downstream systems the claims service calls.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Configuration section holding one subsection per downstream system.</summary>
    public const string ConfigurationSection = "Integration";

    /// <summary>
    /// Registers a typed client for the client registry, the policy manager and the payment
    /// provider, each addressed from its own subsection of <see cref="ConfigurationSection"/>.
    /// </summary>
    /// <param name="services">Container to register into.</param>
    /// <param name="configuration">Configuration to read endpoints from.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddClaimsIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationSection);

        services.AddDownstream<IClientRegistryClient, ClientRegistryClient>(section, "ClientRegistry");
        services.AddDownstream<IPolicyManagerClient, PolicyManagerClient>(section, "PolicyManager");
        services.AddDownstream<IPaymentClient, PaymentClient>(section, "Payments");

        return services;
    }

    /// <summary>
    /// Registers one typed client, bound to its own options and wrapped in the standard
    /// retry and circuit-breaker policy.
    /// </summary>
    private static void AddDownstream<TClient, TImplementation>(
        this IServiceCollection services,
        IConfiguration section,
        string name)
        where TClient : class
        where TImplementation : class, TClient
    {
        services.AddOptions<IntegrationOptions>(name)
            .Bind(section.GetSection(name))
            .Validate(
                options => options.BaseAddress is not null,
                $"Integration:{name}:BaseAddress must be configured.")
            .ValidateOnStart();

        services.AddHttpClient<TClient, TImplementation>(name, (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptionsMonitor<IntegrationOptions>>().Get(name);

            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        })
        .AddStandardResilienceHandler();
    }
}
