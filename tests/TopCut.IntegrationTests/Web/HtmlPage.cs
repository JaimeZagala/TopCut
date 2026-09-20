using System.Text.RegularExpressions;

namespace TopCut.IntegrationTests.Web;

/// <summary>
/// Just enough HTML reading to submit a form the way a browser would: pick up the hidden fields the
/// server put in <i>that</i> form — the antiforgery token and Blazor's form handler — and read back
/// what a field was rendered with.
/// <para>
/// Forms are looked up one at a time because a page often has more than one: a signed-in visitor
/// sees the sign-out form in the top bar on every page, token and all.
/// </para>
/// <para>Deliberately not a full HTML parser: the point of these tests is the round trip.</para>
/// </summary>
internal static partial class HtmlPage
{
    [GeneratedRegex(@"<input\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InputTag();

    [GeneratedRegex(@"([A-Za-z_][\w:.-]*)\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex Attribute();

    /// <summary>Every <c>&lt;input&gt;</c> in the given markup, as its attributes.</summary>
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Inputs(string html) =>
    [
        .. InputTag().Matches(html).Select(tag => (IReadOnlyDictionary<string, string>)Attribute()
            .Matches(tag.Value)
            .ToDictionary(
                attribute => attribute.Groups[1].Value.ToLowerInvariant(),
                attribute => attribute.Groups[2].Value))
    ];

    /// <summary>
    /// The fields a browser would send from the named Blazor form without the user touching
    /// anything: the antiforgery token and the name of the form being submitted.
    /// </summary>
    public static Dictionary<string, string> HiddenFieldsOf(string html, string formName) =>
        Inputs(FormNamed(html, formName))
            .Where(input => input.GetValueOrDefault("type") == "hidden" && input.ContainsKey("name"))
            .ToDictionary(input => input["name"], input => input.GetValueOrDefault("value", string.Empty));

    /// <summary>The first antiforgery token on the page, for forms that post nothing else.</summary>
    public static string AntiforgeryToken(string html) =>
        Inputs(html)
            .First(input => input.GetValueOrDefault("name") == "__RequestVerificationToken")["value"];

    /// <summary>What a checkbox sends when it is ticked: its own value, or "on" if it has none.</summary>
    public static string TickedValueOf(string html, string name) =>
        Inputs(html)
            .FirstOrDefault(input => input.GetValueOrDefault("name") == name)
            ?.GetValueOrDefault("value", "on")
        ?? "on";

    /// <summary>The <c>value</c> a field was rendered with, or <c>null</c> when it has none.</summary>
    public static string? ValueOf(string html, string name) =>
        Inputs(html)
            .FirstOrDefault(input => input.GetValueOrDefault("name") == name)
            ?.GetValueOrDefault("value");

    /// <summary>
    /// The markup of the form whose Blazor handler is <paramref name="formName"/>. Forms do not
    /// nest, so splitting on the closing tag is enough to tell them apart.
    /// </summary>
    private static string FormNamed(string html, string formName)
    {
        var handler = $"value=\"{formName}\"";

        var form = html
            .Split("<form", StringSplitOptions.RemoveEmptyEntries)
            .Select(fragment => fragment.Split("</form>")[0])
            .FirstOrDefault(fragment => fragment.Contains("_handler") && fragment.Contains(handler));

        return form ?? throw new InvalidOperationException($"La pagina no trae el formulario '{formName}'.");
    }
}
