using System.IO;

namespace Api.Tests.Infrastructure;

internal static class TestEnvironment
{
    static TestEnvironment() {
        var envFile = FindEnvFile(".env.test");
        if (envFile == null)
            throw new InvalidOperationException(".env.test not found. It is required for tests.");

        DotNetEnv.Env.Load(envFile);

        if (string.Equals(
                Environment.GetEnvironmentVariable("Test__SilenceConsole"),
                "true",
                StringComparison.OrdinalIgnoreCase
            )) {
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
        }
    }

    public static string GetDatabaseConnectionString() {
        return GetRequired("ConnectionStrings__DefaultConnection");
    }

    public static string GetRedisConnectionString() {
        return GetRequired("ConnectionStrings__Redis");
    }

    public static string GetS3ServiceUrl() {
        return GetRequired("S3__ServiceUrl");
    }

    public static string GetS3AccessKey() {
        return GetRequired("S3__AccessKey");
    }

    public static string GetS3SecretKey() {
        return GetRequired("S3__SecretKey");
    }

    public static string GetS3Bucket() {
        return GetRequired("S3__BucketName");
    }

    public static string GetS3Region() {
        return GetRequired("S3__Region");
    }

    public static string GetOpenAiApiKey() {
        return GetRequired("OpenAI__ApiKey");
    }

    private static string GetRequired(string key) {
        var value = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(value)) return value;

        throw new InvalidOperationException($"Missing required test environment variable: {key}. Check .env.test.");
    }


    private static string? FindEnvFile(string fileName) {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null) {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        return null;
    }
}
