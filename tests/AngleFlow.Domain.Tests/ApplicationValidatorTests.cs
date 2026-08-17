using AngleFlow.Domain.Applications;

namespace AngleFlow.Domain.Tests;

public sealed class ApplicationValidatorTests
{
    private static readonly CreateApplicationCommand Valid = new(
        "Taylor Morgan",
        "taylor@example.test",
        120_000m,
        3_500m,
        "vehicle",
        65_000m,
        45_000m,
        48,
        "normal");

    [Fact]
    public void Validate_accepts_a_well_formed_synthetic_application()
    {
        Assert.Empty(ApplicationValidator.Validate(Valid));
    }

    [Theory]
    [InlineData("", "APPLICANT_NAME_REQUIRED")]
    [InlineData("not-an-email", "EMAIL_INVALID")]
    public void Validate_rejects_invalid_applicant_identity_fields(string value, string code)
    {
        var command = code == "APPLICANT_NAME_REQUIRED"
            ? Valid with { ApplicantName = value }
            : Valid with { Email = value };

        Assert.Contains(ApplicationValidator.Validate(command), error => error.Code == code);
    }

    [Fact]
    public void Validate_rejects_requested_amount_above_asset_value()
    {
        var command = Valid with { RequestedAmount = 66_000m };

        Assert.Contains(
            ApplicationValidator.Validate(command),
            error => error.Code == "REQUEST_EXCEEDS_ASSET_VALUE");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(85)]
    public void Validate_rejects_terms_outside_six_to_eighty_four_months(int term)
    {
        var command = Valid with { TermMonths = term };

        Assert.Contains(
            ApplicationValidator.Validate(command),
            error => error.Code == "TERM_OUT_OF_RANGE");
    }

    [Fact]
    public void Validate_rejects_unsupported_asset_types()
    {
        var command = Valid with { AssetType = "holiday" };

        Assert.Contains(
            ApplicationValidator.Validate(command),
            error => error.Code == "ASSET_TYPE_UNSUPPORTED");
    }
}
