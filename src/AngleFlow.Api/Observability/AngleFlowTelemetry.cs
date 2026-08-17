using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AngleFlow.Api.Observability;

public static class AngleFlowTelemetry
{
    public const string SourceName = "AngleFlow.Api";
    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(SourceName);
    public static readonly Counter<long> ApplicationsCreated = Meter.CreateCounter<long>("angleflow.applications.created");
    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>("angleflow.api.duration", "ms");
}
