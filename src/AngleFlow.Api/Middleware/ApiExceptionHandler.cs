using AngleFlow.Domain.Applications;
using AngleFlow.Domain.Common;
using AngleFlow.Infrastructure.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AngleFlow.Api.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            ApplicationValidationException => (StatusCodes.Status400BadRequest, "Application validation failed", "APPLICATION_INVALID"),
            InvalidIdempotencyKeyException invalidKey => (StatusCodes.Status400BadRequest, "Invalid idempotency key", invalidKey.Code),
            IdempotencyConflictException conflict => (StatusCodes.Status409Conflict, "Idempotency conflict", conflict.Code),
            ApplicationNotFoundException notFound => (StatusCodes.Status404NotFound, "Application not found", notFound.Code),
            InvalidApplicationTransitionException invalidTransition => (StatusCodes.Status409Conflict, "Invalid application transition", invalidTransition.Code),
            WorkflowStartException => (StatusCodes.Status503ServiceUnavailable, "Workflow temporarily unavailable", "WORKFLOW_START_FAILED"),
            DomainException domain => (StatusCodes.Status400BadRequest, "Request rejected", domain.Code),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error", "INTERNAL_ERROR")
        };

        if (status >= 500)
        {
            logger.LogError(
                exception,
                "Request failed with {ExceptionType}; correlation {CorrelationId}",
                exception.GetType().Name,
                httpContext.TraceIdentifier);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= 500 ? "Retry later or provide the correlation ID to an operator." : exception.Message,
            Instance = httpContext.Request.Path,
            Type = $"https://github.com/skinnyfatboy/angleflow/errors/{code.ToLowerInvariant()}"
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = httpContext.TraceIdentifier;
        if (exception is ApplicationValidationException validation)
        {
            problem.Extensions["errors"] = validation.Errors;
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
