using System.Diagnostics;
using AngleFlow.Api.Contracts;
using AngleFlow.Api.Observability;
using AngleFlow.Domain.Applications;
using AngleFlow.Infrastructure.Services;
using DomainApplicationId = AngleFlow.Domain.Applications.ApplicationId;

namespace AngleFlow.Api.Endpoints;

public static class ApplicationEndpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/applications").WithTags("Applications");

        group.MapPost("/", CreateAsync)
            .Produces<ApplicationResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/", ListAsync).Produces<PagedApplicationsResponse>();
        group.MapGet("/{id:guid}", GetAsync)
            .Produces<ApplicationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapGet("/{id:guid}/timeline", TimelineAsync)
            .Produces<IReadOnlyList<TimelineEventResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/{id:guid}/withdraw", WithdrawAsync)
            .Produces<ApplicationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        HttpRequest request,
        CreateApplicationRequest body,
        IApplicationService service,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var idempotencyKey = request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Idempotency-Key is required",
                    extensions: new Dictionary<string, object?> { ["code"] = "IDEMPOTENCY_KEY_REQUIRED" });
            }

            var result = await service.CreateAsync(body.ToCommand(), idempotencyKey, cancellationToken);
            var response = ApplicationResponse.From(result.Application);
            AngleFlowTelemetry.ApplicationsCreated.Add(1, new KeyValuePair<string, object?>("replayed", result.Replayed));
            return Results.Accepted($"/api/applications/{response.Id:D}", response);
        }
        finally
        {
            AngleFlowTelemetry.RequestDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private static async Task<IResult> GetAsync(Guid id, IApplicationService service, CancellationToken cancellationToken)
    {
        var application = await service.GetAsync(new(id), cancellationToken);
        return application is null
            ? NotFound(id)
            : Results.Ok(ApplicationResponse.From(application));
    }

    private static async Task<IResult> TimelineAsync(Guid id, IApplicationService service, CancellationToken cancellationToken)
    {
        var applicationId = new DomainApplicationId(id);
        if (await service.GetAsync(applicationId, cancellationToken) is null)
        {
            return NotFound(id);
        }

        var timeline = await service.GetTimelineAsync(applicationId, cancellationToken);
        return Results.Ok(timeline.Select(TimelineEventResponse.From).ToArray());
    }

    private static async Task<IResult> ListAsync(
        int page,
        int pageSize,
        ApplicationStatus? status,
        IApplicationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(page, pageSize, status, cancellationToken);
        return Results.Ok(new PagedApplicationsResponse(
            result.Items.Select(ApplicationResponse.From).ToArray(),
            result.Page,
            result.PageSize,
            result.Total));
    }

    private static async Task<IResult> WithdrawAsync(
        Guid id,
        IApplicationService service,
        CancellationToken cancellationToken)
    {
        var application = await service.WithdrawAsync(new(id), cancellationToken);
        return Results.Ok(ApplicationResponse.From(application));
    }

    private static IResult NotFound(Guid id) => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Application not found",
        detail: $"Application {id:D} was not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "APPLICATION_NOT_FOUND" });
}
