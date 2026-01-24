using Amazon.DynamoDBv2;
using Amazon.SimpleSystemsManagement;
using ForgeLedger.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace ForgeLedger.Tests.Helpers;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public IAmazonDynamoDB MockDynamoDB { get; } = Substitute.For<IAmazonDynamoDB>();
    public IForgeLedgerStore MockStore { get; } = Substitute.For<IForgeLedgerStore>();
    public IAmazonSimpleSystemsManagement MockSsm { get; } = Substitute.For<IAmazonSimpleSystemsManagement>();

    public string TestApiKey { get; set; } = "test-api-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear existing configuration sources that might look for AWS credentials
            config.Sources.Clear();

            // Add test configuration
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForgeLedger:ApiKey"] = TestApiKey,
                ["AWS:Region"] = "us-east-1"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove all AWS service registrations
            services.RemoveAll<IAmazonDynamoDB>();
            services.RemoveAll<IAmazonSimpleSystemsManagement>();
            services.RemoveAll<IForgeLedgerStore>();

            // Add mocks
            services.AddSingleton(MockDynamoDB);
            services.AddSingleton(MockSsm);
            services.AddSingleton(MockStore);
        });
    }
}
