using AngleFlow.Domain.Applications;

namespace AngleFlow.Api.Contracts;

public sealed record CreateApplicationRequest(
    string ApplicantName,
    string Email,
    decimal AnnualIncome,
    decimal MonthlyExpenses,
    string AssetType,
    decimal AssetValue,
    decimal RequestedAmount,
    int TermMonths,
    string Scenario)
{
    public CreateApplicationCommand ToCommand() => new(
        ApplicantName,
        Email,
        AnnualIncome,
        MonthlyExpenses,
        AssetType,
        AssetValue,
        RequestedAmount,
        TermMonths,
        Scenario);
}
