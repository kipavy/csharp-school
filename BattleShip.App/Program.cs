using BattleShip.App;
using BattleShip.App.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var restBaseUrl = builder.Configuration["RestBaseUrl"] ?? "http://localhost:5086";
var grpcBaseUrl = builder.Configuration["GrpcBaseUrl"] ?? "https://localhost:7096";
var apiConfiguration = new ApiConfiguration(new Uri(restBaseUrl), new Uri(grpcBaseUrl));

builder.Services.AddSingleton(apiConfiguration);
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = sp.GetRequiredService<ApiConfiguration>().RestBaseAddress });
builder.Services.AddSingleton<GameStateService>();
builder.Services.AddScoped<GameApiClient>();
builder.Services.AddScoped<GameGrpcClient>();
builder.Services.AddScoped<GameHubClient>();

await builder.Build().RunAsync();
