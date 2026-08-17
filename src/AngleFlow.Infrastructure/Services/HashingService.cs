using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AngleFlow.Domain.Applications;

namespace AngleFlow.Infrastructure.Services;

public static class HashingService
{
    public static string Hash(CreateApplicationCommand command)
    {
        var canonical = string.Join(
            '\u001f',
            command.ApplicantName.Trim(),
            command.Email.Trim().ToLowerInvariant(),
            command.AnnualIncome.ToString(CultureInfo.InvariantCulture),
            command.MonthlyExpenses.ToString(CultureInfo.InvariantCulture),
            command.AssetType.Trim().ToLowerInvariant(),
            command.AssetValue.ToString(CultureInfo.InvariantCulture),
            command.RequestedAmount.ToString(CultureInfo.InvariantCulture),
            command.TermMonths.ToString(CultureInfo.InvariantCulture),
            command.Scenario.Trim().ToLowerInvariant());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
