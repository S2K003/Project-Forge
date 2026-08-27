using System.Net.Http.Headers;
using ForgeOps.AI.Ollama;
using ForgeOps.AI.Prompts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ForgeOps.AI;

public static class DependencyInjection
{
    /// <summary>
    /// Wires the AI Gateway, provider abstraction and AI Bridge probe (ProjectForge.md §9).
    /// The concrete provider is chosen from <c>Ai:Provider</c> — feature code only sees
    /// <see cref="AiGateway"/> and <see cref="IAiProvider"/>.
    /// </summary>
    public static IServiceCollection AddForgeOpsAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Ai:BaseUrl is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Model), "Ai:Model is required.")
            .ValidateOnStart();

        services.AddSingleton<PromptManager>();
        services.AddSingleton<AiTelemetry>();

        services.AddSingleton<CircuitBreaker>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            return new CircuitBreaker(
                options.CircuitFailureThreshold,
                TimeSpan.FromSeconds(options.CircuitResetSeconds));
        });

        services.AddHttpClient(OllamaBridgeProvider.HttpClientName, ConfigureBridgeClient);
        services.AddHttpClient(OllamaBridgeProbe.HttpClientName, (sp, client) =>
        {
            ConfigureBridgeClient(sp, client);
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.ProbeTimeoutSeconds) + 1);
        });

        services.AddSingleton<IAiBridgeProbe, OllamaBridgeProbe>();

        var provider = configuration.GetSection(AiOptions.SectionName)["Provider"] ?? "OllamaBridge";
        if (string.Equals(provider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAiProvider, MockAiProvider>();
        }
        else
        {
            services.AddSingleton<IAiProvider, OllamaBridgeProvider>();
        }

        services.AddScoped<AiGateway>();
        return services;
    }

    private static void ConfigureBridgeClient(IServiceProvider sp, HttpClient client)
    {
        var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));

        if (!string.IsNullOrWhiteSpace(options.BridgeToken))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.BridgeToken);
        }
    }
}
