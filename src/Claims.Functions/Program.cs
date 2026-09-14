using Claims.Domain.Sla;
using Claims.Integration;
using Claims.Integration.Stubs;
using Claims.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        services
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights();

        var connectionString = configuration.GetValue<string>(key: "ClaimsSqlConnectionString")
            ?? throw new InvalidOperationException("ClaimsSqlConnectionString is not configured.");

        services.AddClaimsPersistence(connectionString);

        services.AddClaimsIntegration(configuration);

        // Local runs and tests only: swaps the downstream clients for in-memory stand-ins.
        if (configuration.GetValue<bool>("Integration:UseStubs"))
        {
            services.AddClaimsIntegrationStubs(configuration);

            // Runs after the stubs bind their own section, so these keys take precedence.
            services.Configure<PaymentOptions>(options =>
            {
                var autoComplete = configuration.GetValue<bool?>(key: "Payment:AutoCompletePayments");
                if (autoComplete.HasValue)
                    options.AutoCompletePayments = autoComplete.Value;

                var delay = configuration.GetValue<int?>(key: "Payment:AutoCompleteDelaySeconds");
                if (delay.HasValue)
                    options.AutoCompleteDelaySeconds = delay.Value;
            });
        }

        services.AddSingleton<SlaPolicy>();
        services.AddSingleton(TimeProvider.System);
    }) // HostBuilder
    .Build();

host.Run();
