using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
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

// La v1 es solo en español, pero los textos ya salen de .resx (ADR-0019) y las fechas y los
// números se formatean con la cultura, no a mano. Añadir un idioma será añadir la cultura aquí.
builder.Services.AddLocalization();

// Por defecto ASP.NET Core escapa todo lo que no sea Basic Latin, así que "Clasificación" salía al
// HTML como "Clasificaci&#xF3;n": correcto pero ilegible en el fuente y más pesado. La página es
// UTF-8 y el idioma es español, así que se deja pasar el Latin-1, donde viven las tildes, la ene y
// los signos de apertura. Lo que importa (< > & " ') se sigue escapando igual.
builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement));

// La zona horaria de la tienda, para que la hora que se teclea signifique lo mismo en el portátil
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

// Enrutado explicito y después de las páginas de error a propósito. Si no se pide aquí, ASP.NET lo
// coloca al principio de la tuberia, y entonces la reejecución de /not-found cambia la ruta cuando
// el enrutado ya ha pasado: el navegador recibia un 404 con el cuerpo vacío.
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
