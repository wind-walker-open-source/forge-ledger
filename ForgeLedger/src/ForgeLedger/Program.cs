using Amazon.DynamoDBv2;
using Amazon.SimpleSystemsManagement;
using ForgeLedger.Api;
using ForgeLedger.Auth;
using ForgeLedger.Core;
using ForgeLedger.Stores;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ForgeLedger",
        Version = "v1",
        Description = "ForgeLedger – a deterministic job and workflow ledger for queue-driven distributed systems.",
        Contact = new OpenApiContact
        {
            Name = "Wind Walker Open Source",
            Url = new Uri("https://windwalkeropensource.com")
        }
    });

    // API Key authentication
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-API-KEY",
        Description = "API key required for authentication"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

// AWS services
builder.Services.AddAWSService<IAmazonDynamoDB>();
builder.Services.AddAWSService<IAmazonSimpleSystemsManagement>();

// HTTP client for webhooks
builder.Services.AddHttpClient("Webhook", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// API key provider (reads from Parameter Store, falls back to appsettings)
builder.Services.AddSingleton<ApiKeyProvider>();

// ForgeLedger core
builder.Services.AddSingleton<IForgeLedgerStore>(sp =>
{
    var ddb = sp.GetRequiredService<IAmazonDynamoDB>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<DynamoDbForgeLedgerStore>>();

    var tableName =
        Environment.GetEnvironmentVariable("FORGELEDGER_TABLE")
        ?? Environment.GetEnvironmentVariable("JOBS_TABLE")
        ?? "ForgeLedger";

    return new DynamoDbForgeLedgerStore(ddb, tableName, httpClientFactory, logger);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.RoutePrefix = "swagger";
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ForgeLedger v1");
    c.DocumentTitle = "ForgeLedger – API Docs";
});

// Exception-to-HTTP mapping (409/400)
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (InvalidOperationException ex)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

app.UseHttpsRedirection();

// API key authentication (skips /health, /swagger, /)
app.UseApiKeyAuthentication();

Endpoints.Map(app);

app.Run();

// Make Program class accessible for integration testing
public partial class Program { }