using EFCore.MigrationExtensions.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Outbox.WebApi.EFCore;

internal class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        Console.WriteLine($"Using DesignTimeDbContextFactory");

        var builder = new DbContextOptionsBuilder<AppDbContext>();

        builder.UseSqlObjects();
        builder.UseSnakeCaseNamingConvention();

        builder.UseNpgsql("Host=localhost;Port=5432;Database=outbox;Username=postgres;Password=postgres;", 
            options => options.MigrationsHistoryTable("_migrations", "outbox"));

        return new AppDbContext(builder.Options);
    }
}