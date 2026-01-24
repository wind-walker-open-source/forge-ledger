using Amazon.DynamoDBv2;
using ForgeLedger.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ForgeLedger.Tests.Helpers;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public IAmazonDynamoDB MockDynamoDB { get; } = Substitute.For<IAmazonDynamoDB>();
    public IForgeLedgerStore MockStore { get; } = Substitute.For<IForgeLedgerStore>();
    public IHttpClientFactory MockHttpClientFactory { get; } = Substitute.For<IHttpClientFactory>();

    public string TestApiKey { get; set; } = "test-api-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test configuration for API key
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForgeLedger:ApiKey"] = TestApiKey
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove real services
            var dynamoDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IAmazonDynamoDB));
            if (dynamoDescriptor != null)
                services.Remove(dynamoDescriptor);

            var storeDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IForgeLedgerStore));
            if (storeDescriptor != null)
                services.Remove(storeDescriptor);

            // Add mocks
            services.AddSingleton(MockDynamoDB);
            services.AddSingleton(MockStore);
        });
    }
}
