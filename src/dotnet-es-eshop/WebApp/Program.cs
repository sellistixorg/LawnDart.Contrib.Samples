using LawnDart.Samples.EShop.Infrastructure;
using LawnDart.Samples.EShop.WebApp.Components;
using LawnDart.Samples.EShop.WebApp.Placeholders;
using LawnDart.Samples.EShop.WebApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEShopStorefront(builder.Configuration);
builder.Services.AddShopperAuthentication();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddEShopWebAppPlaceholders();
builder.AddOrderStatusNotifications();

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapCatalogProductImages();

app.Run();

/// <summary>
/// Entry point marker for the storefront test host.
/// </summary>
public partial class Program;
