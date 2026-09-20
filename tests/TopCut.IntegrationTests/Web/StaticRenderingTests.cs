using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;

namespace TopCut.IntegrationTests.Web;

/// <summary>
/// The account pages are static SSR: there is no circuit, so the browser posts the form and the
/// server binds it by the <c>name</c> attribute of each input. MudBlazor's fields are not Blazor's
/// own <c>InputText</c>, so two things have to be handed to them explicitly — the <c>name</c>, and
/// <c>Text</c>, which is what makes MudBlazor emit a <c>value</c> attribute so that a form coming
/// back with validation errors still carries what the user typed.
/// <para>
/// If a MudBlazor upgrade stopped doing either, the pages would look perfectly fine and quietly
/// stop working, so both are pinned here. These render the components alone: no database, no HTTP.
/// </para>
/// </summary>
public class StaticRenderingTests
{
    [Fact]
    public async Task A_text_field_renders_the_name_the_form_binder_will_look_for()
    {
        var html = await RenderAsync<MudTextField<string>>(new Dictionary<string, object?>
        {
            ["name"] = "Input.Email",
            ["Value"] = "ana@topcut.test",
            ["Text"] = "ana@topcut.test"
        });

        Assert.Contains("name=\"Input.Email\"", html);
    }

    [Fact]
    public async Task A_text_field_keeps_what_was_typed_when_the_form_comes_back()
    {
        var withText = await RenderAsync<MudTextField<string>>(new Dictionary<string, object?>
        {
            ["name"] = "Input.Email",
            ["Value"] = "ana@topcut.test",
            ["Text"] = "ana@topcut.test"
        });

        var withoutText = await RenderAsync<MudTextField<string>>(new Dictionary<string, object?>
        {
            ["name"] = "Input.Email",
            ["Value"] = "ana@topcut.test"
        });

        // Value on its own is not enough in static rendering: MudBlazor only writes the value
        // attribute from Text. Without it, one failed validation empties the whole form.
        Assert.Contains("value=\"ana@topcut.test\"", withText);
        Assert.DoesNotContain("value=\"ana@topcut.test\"", withoutText);
    }

    [Fact]
    public async Task A_password_field_renders_as_a_password_input()
    {
        var html = await RenderAsync<MudTextField<string>>(new Dictionary<string, object?>
        {
            ["name"] = "Input.Password",
            ["InputType"] = InputType.Password
        });

        Assert.Contains("name=\"Input.Password\"", html);
        Assert.Contains("type=\"password\"", html);
    }

    [Fact]
    public async Task A_checkbox_renders_the_name_and_its_ticked_state()
    {
        var empty = await RenderAsync<MudCheckBox<bool>>(new Dictionary<string, object?>
        {
            ["name"] = "Input.ConsentAccepted",
            ["Value"] = false
        });

        var ticked = await RenderAsync<MudCheckBox<bool>>(new Dictionary<string, object?>
        {
            ["name"] = "Input.ConsentAccepted",
            ["Value"] = true
        });

        Assert.Contains("name=\"Input.ConsentAccepted\"", empty);
        Assert.Contains("type=\"checkbox\"", empty);

        // An unticked box sends nothing at all, which the server reads as consent refused. A ticked
        // one has to come back ticked if the rest of the form failed validation.
        Assert.DoesNotContain("checked", empty);
        Assert.Contains("checked", ticked);
    }

    private static async Task<string> RenderAsync<TComponent>(Dictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices();
        services.AddSingleton<IJSRuntime, UnreachableJsRuntime>();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(
                ParameterView.FromDictionary(parameters));

            return output.ToHtmlString();
        });
    }

    /// <summary>
    /// Static rendering has no browser to talk to. If a component tries anyway, that is worth
    /// finding out about rather than papering over with a silent no-op.
    /// </summary>
    private sealed class UnreachableJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException($"No hay JavaScript en render estatico: {identifier}");

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) =>
            throw new NotSupportedException($"No hay JavaScript en render estatico: {identifier}");
    }
}
