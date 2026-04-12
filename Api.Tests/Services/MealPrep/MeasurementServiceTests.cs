using Api.Services.MealPrep;

namespace Api.Tests.Services.MealPrep;

public class MeasurementServiceTests
{
    private readonly MeasurementService _measurementService = new();

    [Fact]
    public void ScaleAmount_ShouldScaleUsingServingRatio()
    {
        var scaledAmount = _measurementService.ScaleAmount(200m, 4m, 6m);

        Assert.Equal(300m, scaledAmount);
    }

    [Fact]
    public void Normalize_ShouldReturnCanonicalMassUnit()
    {
        var normalized = _measurementService.Normalize("kg");

        Assert.Equal("mass", normalized.Kind);
        Assert.Equal("g", normalized.CanonicalUnit);
        Assert.Equal(1000m, normalized.FactorToCanonical);
        Assert.False(normalized.IsApproximate);
    }

    [Fact]
    public void ConvertForDisplay_ShouldPromoteLargeMassToKilograms()
    {
        var displayAmount = _measurementService.ConvertForDisplay(1500m, "g", false);

        Assert.Equal(1.5m, displayAmount.Amount);
        Assert.Equal("kg", displayAmount.Unit);
        Assert.False(displayAmount.IsApproximate);
    }
}
