using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.Data;

/// <summary>
///     Creates the API database context for Entity Framework design-time operations such as migrations.
/// </summary>
public class ApiDbContextFactory : IDesignTimeDbContextFactory<ApiDbContext>
{
    public ApiDbContext CreateDbContext(string[] args) {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                               ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTIONSTRING")
                               ?? "Host=localhost;Port=5432;Database=meal_prep_dev;Username=root;Password=password";

        var optionsBuilder = new DbContextOptionsBuilder<ApiDbContext>();
        optionsBuilder.AddInterceptors(new TimestampInterceptor());
        optionsBuilder.UseNpgsql(connectionString);

        return new ApiDbContext(optionsBuilder.Options);
    }
}
