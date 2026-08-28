using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeOps.Forge;

public static class DependencyInjection
{
    public static IServiceCollection AddForgeOpsForge(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CodeRunnerOptions>()
            .Bind(configuration.GetSection(CodeRunnerOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<RoslynCompiler>();
        services.AddSingleton<GeneratedCodeAuditor>();
        services.AddSingleton<SandboxRunner>();
        services.AddScoped<ForgePipeline>();

        return services;
    }
}
