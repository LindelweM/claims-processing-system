using Claims.Integration.ClientRegistry;
using Claims.Integration.Payments;
using Claims.Integration.PolicyManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
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

        // Validation and verification calls are POSTs only because they carry a body; they
        // change nothing, so retrying them is safe.
        services.AddDownstream<IClientRegistryClient, ClientRegistryClient>(section, "ClientRegistry", retryPosts: true);
        services.AddDownstream<IPolicyManagerClient, PolicyManagerClient>(section, "PolicyManager", retryPosts: true);

        // A payment instruction that times out may still have been taken, so it is never
        // retried automatically: a second attempt could pay the claim twice.
        services.AddDownstream<IPaymentClient, PaymentClient>(section, "Payments", retryPosts: false);

        services.AddOptions<PaymentCallbackOptions>()
            .Bind(configuration.GetSection(PaymentCallbackOptions.ConfigurationSection))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.CallbackSigningSecret),
                $"{PaymentCallbackOptions.ConfigurationSection}:{nameof(PaymentCallbackOptions.CallbackSigningSecret)} must be configured.")
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Registers one typed client, bound to its own options and wrapped in the standard
    /// retry and circuit-breaker policy.
    /// </summary>
    /// <param name="services">Container to register into.</param>
    /// <param name="section">The <see cref="ConfigurationSection"/> configuration.</param>
    /// <param name="name">Subsection holding this system's options.</param>
    /// <param name="retryPosts">
    /// Whether failed POSTs are retried. Only pass true where repeating the call cannot repeat
    /// its effect.
    /// </param>
    private static void AddDownstream<TClient, TImplementation>(
        this IServiceCollection services,
        IConfiguration section,
        string name,
        bool retryPosts)
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
        .AddStandardResilienceHandler(options =>
        {
            if (!retryPosts)
            {
                options.Retry.DisableForUnsafeHttpMethods();
            }
        });
    }
}
