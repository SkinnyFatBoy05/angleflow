using AngleFlow.Domain.Applications;
using AngleFlow.Infrastructure.Services;

namespace AngleFlow.Api.Contracts;

public sealed record ApplicationResponse(
    Guid Id,
    ApplicationStatus Status,
    string AssetType,
    decimal AssetValue,
    decimal RequestedAmount,
    int TermMonths,
    string Scenario,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ApplicationResponse From(ApplicationView view) => new(
        view.Id.Value,
        view.Status,
        view.AssetType,
        view.AssetValue,
        view.RequestedAmount,
        view.TermMonths,
        view.Scenario,
        view.CreatedAt,
        view.UpdatedAt);
}

public sealed record TimelineEventResponse(
    long Sequence,
    string EventType,
    ApplicationStatus Status,
    string ReasonCode,
    DateTimeOffset OccurredAt)
{
    public static TimelineEventResponse From(TimelineEventView view) => new(
        view.Sequence,
        view.EventType,
        view.Status,
        view.ReasonCode,
        view.OccurredAt);
}

public sealed record PagedApplicationsResponse(
    IReadOnlyList<ApplicationResponse> Items,
    int Page,
    int PageSize,
    int Total);
