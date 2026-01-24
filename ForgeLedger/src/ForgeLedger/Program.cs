using Amazon.DynamoDBv2;
using ForgeLedger.Api;
using ForgeLedger.Core;
using ForgeLedger.Stores;
using Microsoft.OpenApi;

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
});

// AWS services
builder.Services.AddAWSService<IAmazonDynamoDB>();

// ForgeLedger core
builder.Services.AddSingleton<IForgeLedgerStore>(sp =>
{
    var ddb = sp.GetRequiredService<IAmazonDynamoDB>();

    var tableName =
        Environment.GetEnvironmentVariable("FORGELEDGER_TABLE")
        ?? Environment.GetEnvironmentVariable("JOBS_TABLE")
        ?? "ForgeLedger";

    return new DynamoDbForgeLedgerStore(ddb, tableName);
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

Endpoints.Map(app);

app.Run();