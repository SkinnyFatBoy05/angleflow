namespace AngleFlow.Domain.Applications;

public enum ApplicationStatus
{
    Submitted,
    ChecksRunning,
    ManualReview,
    Approved,
    Declined,
    Withdrawn,
    StartFailed
}

public enum CreditBand
{
    Poor,
    Fair,
    Good,
    Excellent
}

public enum RiskOutcome
{
    AutoApprove,
    ManualReview,
    Decline
}

public enum ReviewPriority
{
    None,
    Standard,
    High
}
