using ForgeLedger.Contracts.Request;
using ForgeLedger.Contracts.Response;
using ForgeLedger.Core;
 
namespace ForgeLedger.Api;

public static class Endpoints
{
    private sealed class LogCategory
    {
    }
    public static void Map(WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "ForgeLedger" }));

        // Create a job header (META)
        // Notes:
        // - This endpoint is NOT idempotent by default. Retrying may create multiple jobs.
        // - If you need idempotency, add a client-generated idempotency key (future enhancement).
        app.MapPost("/jobs", async (
                HttpContext http,
                CreateJobRequest req,
                IForgeLedgerStore store,
                ILogger<LogCategory> log,
                CancellationToken ct) =>
            {
                // -------------------------------
                // (1) Input validation (400)
                // -------------------------------
                var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

                if (req is null)
                {
                    errors["body"] = new[] { "Request body is required." };
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(req.JobType))
                    {
                        errors[nameof(req.JobType)] = new[] { "JobType is required." };
                    }

                    if (req.ExpectedCount <= 0)
                    {
                        errors[nameof(req.ExpectedCount)] = new[] { "ExpectedCount must be greater than 0." };
                    }

                    // Optional: validate ItemIds count if a caller uses ExpectedCount as a constraint.
                    // (No validation of QueueName/QueueKind here; those are optional.)
                }

                if (errors.Count > 0)
                {
                    // (2) Stable error format (RFC7807)
                    return Results.ValidationProblem(errors, statusCode: StatusCodes.Status400BadRequest);
                }

                var safeReq = req!;

                // -------------------------------
                // (5) Correlation + minimal logging
                // -------------------------------
                var traceId = http.TraceIdentifier;
                log.LogInformation(
                    "CreateJob requested. jobType={JobType} expectedCount={ExpectedCount} queueName={QueueName} queueKind={QueueKind} traceId={TraceId}",
                    safeReq.JobType,
                    safeReq.ExpectedCount,
                    safeReq.QueueName,
                    safeReq.QueueKind,
                    traceId);

                try
                {
                    var created = await store.CreateJobAsync(safeReq, ct);

                    log.LogInformation(
                        "CreateJob created. jobId={JobId} status={Status} traceId={TraceId}",
                        created.JobId,
                        created.Status,
                        traceId);

                    return Results.Created($"/jobs/{created.JobId}", created);
                }
                catch (ArgumentException ex)
                {
                    // (2) Stable error format (RFC7807)
                    return Results.Problem(
                        title: "Invalid request",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status400BadRequest,
                        extensions: new Dictionary<string, object?> { ["traceId"] = traceId });
                }
                catch (InvalidOperationException ex)
                {
                    // (2) Stable error format (RFC7807)
                    return Results.Problem(
                        title: "Invalid operation",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status409Conflict,
                        extensions: new Dictionary<string, object?> { ["traceId"] = traceId });
                }
            })
            .WithName("CreateJob")
            // (3) Explicit response contracts (Swagger/OpenAPI)
            .Produces<CreateJobResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            // (4) Idempotency semantics documented
            .WithSummary("Create a new job")
            .WithDescription(
                "Creates a new job (META row) in the ForgeLedger table. " +
                "This endpoint is not idempotent by default; retries may create multiple jobs. " +
                "Use client-side retry discipline until an idempotency key is added.")
            // (6) Route naming is API Gateway compatible (no colon segments here)
            ;

        // Register items for a job (creates ITEM rows). Safe to call multiple times.
        app.MapPost("/jobs/{jobId}/items:register", async (string jobId, RegisterItemsRequest req, IForgeLedgerStore store, CancellationToken ct) =>
        {
            var result = await store.RegisterItemsAsync(jobId, req, ct);
            return Results.Ok(result);
        })
        .WithName("RegisterItems");

        // Get items for a job with optional status filter
        app.MapGet("/jobs/{jobId}/items", async (
            string jobId,
            string? status,
            int? limit,
            string? nextToken,
            IForgeLedgerStore store,
            CancellationToken ct) =>
        {
            var result = await store.GetItemsAsync(jobId, status, limit, nextToken, ct);
            return Results.Ok(result);
        })
        .WithName("GetItems")
        .Produces<GetItemsResponse>(StatusCodes.Status200OK)
        .WithSummary("Get items for a job")
        .WithDescription(
            "Returns items for a job with optional filtering by status. " +
            "Valid status values: PENDING, PROCESSING, COMPLETED, FAILED. " +
            "Supports pagination via limit and nextToken parameters.");

        // Claim an item for processing
        app.MapPost("/jobs/{jobId}/items/{itemId}:claim", async (string jobId, string itemId, IForgeLedgerStore store, CancellationToken ct) =>
        {
            var result = await store.TryClaimItemAsync(jobId, itemId, ct);
            return Results.Ok(result);
        })
        .WithName("ClaimItem");

        // Mark an item completed
        app.MapPost("/jobs/{jobId}/items/{itemId}:complete", async (string jobId, string itemId, ItemCompleteRequest req, IForgeLedgerStore store, CancellationToken ct) =>
        {
            var result = await store.MarkItemCompletedAsync(jobId, itemId, req, ct);
            return Results.Ok(result);
        })
        .WithName("CompleteItem");

        // Mark an item failed
        app.MapPost("/jobs/{jobId}/items/{itemId}:fail", async (string jobId, string itemId, ItemFailRequest req, IForgeLedgerStore store, CancellationToken ct) =>
        {
            var result = await store.MarkItemFailedAsync(jobId, itemId, req, ct);
            return Results.Ok(result);
        })
        .WithName("FailItem");

        // Read job status
        app.MapGet("/jobs/{jobId}", async (string jobId, IForgeLedgerStore store, CancellationToken ct) =>
        {
            var job = await store.GetJobAsync(jobId, ct);
            return job is null ? Results.NotFound() : Results.Ok(job);
        })
        .WithName("GetJob");

        app.MapGet("/", () => Results.Ok(new { service = "ForgeLedger", version = "v1" }));
    }
}