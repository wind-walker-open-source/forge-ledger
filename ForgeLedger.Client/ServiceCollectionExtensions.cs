using System;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeLedger.Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddForgeLedgerClient(
        this IServiceCollection services,
        Action<ForgeLedgerClientOptions> configure)
    {
        services.Configure(configure);

        services.AddHttpClient<IForgeLedgerService, ForgeLedgerClient>();

        return services;
    }
}