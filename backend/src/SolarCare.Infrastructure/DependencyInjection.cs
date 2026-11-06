using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SolarCare.Application.Authentication;
using SolarCare.Application.Operations;
using SolarCare.Infrastructure.Authentication;
using SolarCare.Infrastructure.Operations;

namespace SolarCare.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSolarCareInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SolarCare")
            ?? throw new InvalidOperationException(
                "Configuration 'ConnectionStrings:SolarCare' is required.");

        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddScoped<IAuthRepository, DapperAuthRepository>();
        services.AddScoped<DapperOperationsRepository>();
        services.AddScoped<IOperationsRepository>(
            serviceProvider => serviceProvider.GetRequiredService<DapperOperationsRepository>());
        services.AddScoped<ICustomerRepository>(
            serviceProvider => serviceProvider.GetRequiredService<DapperOperationsRepository>());
        services.AddScoped<IInstallationRepository>(
            serviceProvider => serviceProvider.GetRequiredService<DapperOperationsRepository>());
        services.AddScoped<IEquipmentRepository>(
            serviceProvider => serviceProvider.GetRequiredService<DapperOperationsRepository>());
        services.AddScoped<IAssetRepository>(
            serviceProvider => serviceProvider.GetRequiredService<DapperOperationsRepository>());
        services.AddScoped<ITicketRepository>(
            serviceProvider => serviceProvider.GetRequiredService<DapperOperationsRepository>());
        services.AddScoped<IServiceOrderRepository>(
            serviceProvider => serviceProvider.GetRequiredService<DapperOperationsRepository>());
        services.AddScoped<IMaintenanceRepository>(
            serviceProvider => serviceProvider.GetRequiredService<DapperOperationsRepository>());
        services.AddScoped<IDashboardRepository>(
            serviceProvider => serviceProvider.GetRequiredService<DapperOperationsRepository>());
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITaxIdentifierProtector>(
            new AesTaxIdentifierProtector(configuration));
        return services;
    }
}
