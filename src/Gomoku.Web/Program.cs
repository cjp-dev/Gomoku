using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Gomoku.App.Services;
using Gomoku.App.ViewModels;
using Gomoku.Web;
using Gomoku.Web.Services;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton(new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton(services => (IJSInProcessRuntime)services.GetRequiredService<IJSRuntime>());
builder.Services.AddSingleton<BrowserDialogService>();
builder.Services.AddSingleton<IDialogService>(services => services.GetRequiredService<BrowserDialogService>());
builder.Services.AddSingleton<IGameFileService, BrowserGameFileService>();
builder.Services.AddSingleton<ISoundService, BrowserSoundService>();
builder.Services.AddSingleton<BrainDocs>();
builder.Services.AddSingleton<ISettingsStore, LocalStorageSettingsStore>();
builder.Services.AddSingleton<IEngineHost, WebEngineHost>();

builder.Services.AddSingleton(services => new MainViewModel(
    services.GetRequiredService<IDialogService>(),
    services.GetRequiredService<IGameFileService>(),
    services.GetRequiredService<ISoundService>(),
    engineHost: services.GetRequiredService<IEngineHost>(),
    settingsStore: services.GetRequiredService<ISettingsStore>()));

await builder.Build().RunAsync();

