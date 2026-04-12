using Api.Startup;
using Xunit;

namespace Api.Tests.Startup;

public class ConfigurationLoggingExtensionsTests
{
    [Theory]
    [InlineData("OpenAI:ApiKey", "super-secret-value", "su***ue")]
    [InlineData("ConnectionStrings:DefaultConnection", "Host=localhost;Password=secret123;Username=app", "Ho***pp")]
    [InlineData("Redis", "localhost:6379,password=secret123,ssl=false", "localhost:6379,password=secret123,ssl=false")]
    public void MaskConfigurationValue_MasksSensitiveValues(string key, string value, string expected)
    {
        var masked = ConfigurationLoggingExtensions.MaskConfigurationValue(key, value);

        Assert.Equal(expected, masked);
    }

    [Fact]
    public void MaskConfigurationValue_MasksSensitiveConnectionStringSegments()
    {
        var value = "Host=localhost;Password=secret123;Username=app;Database=meal_prep";

        var masked = ConfigurationLoggingExtensions.MaskConfigurationValue("Database", value);

        Assert.Equal("Host=localhost;Password=se***23;Username=app;Database=meal_prep", masked);
    }
}
