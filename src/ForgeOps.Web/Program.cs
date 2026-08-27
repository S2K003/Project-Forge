using ForgeOps.Web;
using ForgeOps.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var options = builder.Configuration.GetSection("ForgeOps").Get<ForgeOpsWebOptions>() ?? new ForgeOpsWebOptions();
builder.Services.AddSingleton(options);

// One HttpClient pointed at the API. In Demo Mode nothing calls it.
var apiBase = string.IsNullOrWhiteSpace(options.ApiBaseUrl)
    ? builder.HostEnvironment.BaseAddress
    : options.ApiBaseUrl;
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBase) });

builder.Services.AddScoped<ForgeOpsApiClient>();
builder.Services.AddScoped<AppModeService>();
builder.Services.AddScoped<AiBridgeMonitor>();
builder.Services.AddScoped<JourneyPlayer>();

await builder.Build().RunAsync();
