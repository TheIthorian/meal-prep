using Api.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

/// <summary>
///     Represents the Entity Framework database context for the API.
/// </summary>
public partial class ApiDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>, IDataProtectionKeyContext
{
    public ApiDbContext() { }

    public ApiDbContext(DbContextOptions<ApiDbContext> options)
        : base(options) { }

    public override DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceUser> WorkspaceUsers => Set<WorkspaceUser>();
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.AddInterceptors(new TimestampInterceptor())
                .UseNpgsql(Environment.GetEnvironmentVariable("POSTGRES_CONNECTIONSTRING"));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Ignore<Entity>();
        modelBuilder.Ignore<WorkspaceEntity>();

        // Assign Entity properties
        foreach (var entityType in modelBuilder.Model
                     .GetEntityTypes()
                     .Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)))
        {
            var builder = modelBuilder.Entity(entityType.ClrType);

            builder.HasKey(nameof(Entity.Id));
            builder.Property(nameof(Entity.CreatedAt))
                .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
            builder.Property(nameof(Entity.UpdatedAt))
                .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
        }

        // Assign WorkspaceEntity properties
        foreach (var entityType in modelBuilder.Model
                     .GetEntityTypes()
                     .Where(t => typeof(WorkspaceEntity).IsAssignableFrom(t.ClrType)))
        {
            var builder = modelBuilder.Entity(entityType.ClrType);

            builder.HasOne(nameof(WorkspaceEntity.Workspace));
        }

        modelBuilder.Entity<Workspace>().HasIndex(w => w.Name);

        modelBuilder.Entity<WorkspaceUser>().HasKey(uw => new { uw.UserId, uw.WorkspaceId });
        modelBuilder.Entity<WorkspaceUser>()
            .HasOne(uw => uw.User)
            .WithMany(u => u.Workspaces)
            .HasForeignKey(uw => uw.UserId);
        modelBuilder.Entity<WorkspaceUser>()
            .HasOne(uw => uw.Workspace)
            .WithMany(w => w.Members)
            .HasForeignKey(uw => uw.WorkspaceId);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
