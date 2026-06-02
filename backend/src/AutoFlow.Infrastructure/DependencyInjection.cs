using AutoFlow.Application.Abstractions;
using AutoFlow.Infrastructure.Claude;
using AutoFlow.Infrastructure.Persistence;
using AutoFlow.Infrastructure.Repositories;
using AutoFlow.Infrastructure.Storage;
using AutoFlow.Infrastructure.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AutoFlowDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("Default"))
               .UseSnakeCaseNamingConvention());

        services.AddScoped<IAutomationRepository, AutomationRepository>();
        services.AddSingleton<IIrValidator, IrValidator>();
        services.AddSingleton<IAssetStorage, MinioAssetStorage>();

        services.Configure<ClaudeOptions>(config.GetSection(ClaudeOptions.SectionName));
        services.Configure<MinioOptions>(config.GetSection(MinioOptions.SectionName));

        services.AddHttpClient<ICompilationService, ClaudeCompilationService>(c =>
            c.Timeout = TimeSpan.FromMinutes(2));

        return services;
    }
}
