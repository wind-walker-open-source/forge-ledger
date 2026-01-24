using System;
using Amazon.SimpleSystemsManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ForgeLedger.Client;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the ForgeLedger client with API key authentication.
    /// API key is loaded from options first (via appsettings), then falls back to AWS Parameter Store.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddForgeLedgerClient(
        this IServiceCollection services,
        Action<ForgeLedgerClientOptions> configure)
    {
        services.Configure(configure);

        // Register SSM client if not already registered (optional - will be null if AWS not configured)
        services.TryAddSingleton<IAmazonSimpleSystemsManagement>(_ =>
        {
            try
            {
                return new AmazonSimpleSystemsManagementClient();
            }
            catch
            {
                // AWS credentials not available - that's OK, we'll use appsettings
                return null!;
            }
        });

        // Register API key provider
        services.TryAddSingleton<ApiKeyProvider>();

        // Register the delegating handler
        services.AddTransient<ApiKeyDelegatingHandler>();

        // Register HttpClient with the API key handler
        services.AddHttpClient<IForgeLedgerService, ForgeLedgerClient>()
            .AddHttpMessageHandler<ApiKeyDelegatingHandler>();

        return services;
    }
}