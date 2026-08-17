namespace AngleFlow.Worker.Workers;

public static class WorkerDefinitions
{
    public static readonly IReadOnlyList<string> ResourceFiles =
    [
        "asset-finance-application.bpmn",
        "application-risk.dmn",
        "manual-review.form"
    ];

    public static readonly IReadOnlyList<string> JobTypes =
    [
        "validate-application",
        "identity-check",
        "credit-check",
        "affordability-check",
        "fraud-check",
        "asset-check",
        "persist-decision",
        "create-offer",
        "send-notification",
        "escalate-review",
        "complete-application"
    ];
}
