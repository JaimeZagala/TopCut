using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TopCut.Api;
using TopCut.Infrastructure;
using TopCut.Infrastructure.Identity;
using TopCut.Infrastructure.Persistence;
using TopCut.Web.Account;
using TopCut.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapTopCutApi();
app.MapAccountEndpoints();

await ApplyMigrationsAndSeedAsync(app);

app.Run();

/// <summary>
/// Brings a fresh database up to date and puts the catalog, the roles and the bootstrap Superadmin
/// in place. Safe to run on every start: all three steps are idempotent.
/// <para>
/// Migrating on startup suits a single-instance free App Service. If TopCut ever scales out, this
/// moves to a deploy step so two instances cannot migrate at once.
/// </para>
/// </summary>
static async Task ApplyMigrationsAndSeedAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    var db = services.GetRequiredService<TopCutDbContext>();
    await db.Database.MigrateAsync();

    await CatalogSeeder.SeedAsync(db);

    await IdentitySeeder.SeedRolesAsync(services.GetRequiredService<RoleManager<IdentityRole>>());
    await IdentitySeeder.SeedSuperadminAsync(
        services.GetRequiredService<UserManager<TopCutUser>>(),
        app.Configuration["Superadmin:Email"],
        app.Configuration["Superadmin:Password"]);
}

/// <summary>Exposed so the integration tests can boot the real application.</summary>
public partial class Program;
