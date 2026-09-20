using System.Globalization;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;
using TopCut.Web.Time;
using TopCut.Api;
using TopCut.Infrastructure;
using TopCut.Infrastructure.Persistence;
using TopCut.Web.Account;
using TopCut.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// La v1 es solo en espanol, pero los textos ya salen de .resx (ADR-0019) y las fechas y los
// numeros se formatean con la cultura, no a mano. Anadir un idioma sera anadir la cultura aqui.
builder.Services.AddLocalization();

// La zona horaria de la tienda, para que la hora que se teclea signifique lo mismo en el portatil
// de desarrollo y en un servidor que corre en UTC (ADR-0025).
builder.Services.AddSingleton<LocalTime>();

var connectionString = builder.Configuration.GetConnectionString("TopCut")
    ?? throw new InvalidOperationException("Falta la cadena de conexion 'TopCut'.");

builder.Services.AddTopCutInfrastructure(connectionString);

// TopCut.Api is a library hosted in this same process (ADR-0004): it brings authentication,
// authorization and the HTTP endpoints for external clients.
builder.Services.AddTopCutApi(builder.Configuration);

builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Friendly error pages are for the browser only. Re-executing the pipeline against a Razor page
// would turn an API 401 into a 400 with an HTML body, which is useless to a client expecting
// problem+json.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

var spanish = new CultureInfo("es-ES");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(spanish, spanish),
    SupportedCultures = [spanish],
    SupportedUICultures = [spanish]
});

// Enrutado explicito y despues de las paginas de error a proposito. Si no se pide aqui, ASP.NET lo
// coloca al principio de la tuberia, y entonces la reejecucion de /not-found cambia la ruta cuando
// el enrutado ya ha pasado: el navegador recibia un 404 con el cuerpo vacio.
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapTopCutApi();
app.MapAccountEndpoints();

using (var scope = app.Services.CreateScope())
{
    await DatabaseInitializer.InitializeAsync(
        scope.ServiceProvider,
        connectionString,
        app.Configuration["Superadmin:Email"],
        app.Configuration["Superadmin:Password"]);
}

app.Run();

/// <summary>Exposed so the integration tests can boot the real application.</summary>
public partial class Program;
