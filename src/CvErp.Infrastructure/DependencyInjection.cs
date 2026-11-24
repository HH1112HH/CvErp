using CvErp.Infrastructure.Data;
using CvErp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CvErp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing connection string 'Default'.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<DbSeeder>();
        services.AddScoped<NumberSequenceService>();
        services.AddScoped<AuditService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<ProductionService>();
        services.AddScoped<WorkflowService>();
        services.AddScoped<ReportingService>();

        return services;
    }
}
