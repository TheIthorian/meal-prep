using Api.Services.MealPrep;

namespace Api.Tests.Services.MealPrep;

public class MeasurementServiceTests
{
    private readonly MeasurementService _measurementService = new();

    [Fact]
    public void ScaleAmount_ShouldScaleUsingServingRatio() {
        var scaledAmount = _measurementService.ScaleAmount(200m, 4m, 6m);

        Assert.Equal(300m, scaledAmount);
    }

    [Fact]
    public void Normalize_ShouldReturnCanonicalMassUnit() {
        var normalized = _measurementService.Normalize("kg");

        Assert.Equal("mass", normalized.Kind);
        Assert.Equal("g", normalized.CanonicalUnit);
        Assert.Equal(1000m, normalized.FactorToCanonical);
        Assert.False(normalized.IsApproximate);
    }

    [Fact]
    public void ConvertForDisplay_ShouldPromoteLargeMassToKilograms() {
        var displayAmount = _measurementService.ConvertForDisplay(1500m, "g", false);

        Assert.Equal(1.5m, displayAmount.Amount);
        Assert.Equal("kg", displayAmount.Unit);
        Assert.False(displayAmount.IsApproximate);
    }

    [Fact]
    public void ParseDecimal_ShouldCombineWholeNumberAndUnicodeFraction() {
        Assert.Equal(1.25m, _measurementService.ParseDecimal("1 ¼"));
        Assert.Equal(2.5m, _measurementService.ParseDecimal("2 ½"));
        Assert.Equal(1.75m, _measurementService.ParseDecimal("1 ¾"));
    }

    [Fact]
    public void ParseDecimal_ShouldNotSumTwoUnrelatedNumbers() {
        Assert.Null(_measurementService.ParseDecimal("1 2"));
    }
}
