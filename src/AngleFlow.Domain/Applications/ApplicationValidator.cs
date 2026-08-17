using System.Net.Mail;

namespace AngleFlow.Domain.Applications;

public static class ApplicationValidator
{
    private static readonly HashSet<string> SupportedAssetTypes =
        new(StringComparer.OrdinalIgnoreCase) { "vehicle", "equipment" };

    private static readonly HashSet<string> SupportedScenarios =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "normal",
            "auto-approve",
            "manual-review",
            "decline",
            "transient-failure",
            "incident"
        };

    public static IReadOnlyList<ValidationError> Validate(CreateApplicationCommand command)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(command.ApplicantName))
        {
            errors.Add(new("APPLICANT_NAME_REQUIRED", "applicantName", "Applicant name is required."));
        }
        else if (command.ApplicantName.Trim().Length > 120)
        {
            errors.Add(new("APPLICANT_NAME_TOO_LONG", "applicantName", "Applicant name must be 120 characters or fewer."));
        }

        if (!IsValidEmail(command.Email))
        {
            errors.Add(new("EMAIL_INVALID", "email", "A valid email address is required."));
        }

        if (command.AnnualIncome <= 0)
        {
            errors.Add(new("ANNUAL_INCOME_INVALID", "annualIncome", "Annual income must be positive."));
        }

        if (command.MonthlyExpenses < 0)
        {
            errors.Add(new("MONTHLY_EXPENSES_INVALID", "monthlyExpenses", "Monthly expenses cannot be negative."));
        }

        if (!SupportedAssetTypes.Contains(command.AssetType))
        {
            errors.Add(new("ASSET_TYPE_UNSUPPORTED", "assetType", "Asset type must be vehicle or equipment."));
        }

        if (command.AssetValue <= 0)
        {
            errors.Add(new("ASSET_VALUE_INVALID", "assetValue", "Asset value must be positive."));
        }

        if (command.RequestedAmount <= 0)
        {
            errors.Add(new("REQUEST_AMOUNT_INVALID", "requestedAmount", "Requested amount must be positive."));
        }
        else if (command.RequestedAmount > command.AssetValue)
        {
            errors.Add(new("REQUEST_EXCEEDS_ASSET_VALUE", "requestedAmount", "Requested amount cannot exceed asset value."));
        }

        if (command.TermMonths is < 6 or > 84)
        {
            errors.Add(new("TERM_OUT_OF_RANGE", "termMonths", "Term must be between 6 and 84 months."));
        }

        if (!SupportedScenarios.Contains(command.Scenario))
        {
            errors.Add(new("SCENARIO_UNSUPPORTED", "scenario", "Scenario is not supported."));
        }

        return errors;
    }

    private static bool IsValidEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 254)
        {
            return false;
        }

        try
        {
            var address = new MailAddress(value);
            return string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase)
                && value.Contains('@', StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
