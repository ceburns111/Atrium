using Atrium.Design;
using Bunit;
using Microsoft.AspNetCore.Components.Web;

namespace Atrium.UnitTests;

/// <summary>
/// The shared <see cref="Menu"/> dropdown primitive (the topbar account menu today, reusable
/// elsewhere): the panel stays closed until the trigger is clicked, and either Esc or an outside
/// (backdrop) click dismisses it. Those are the interactions a keyboard/mouse user relies on to get
/// out of an open menu, so they're the ones worth pinning.
/// </summary>
public class MenuTests
{
    // Menu focuses its panel on open (ElementReference.FocusAsync -> a JS call); Loose JSInterop lets
    // that no-op in the headless renderer instead of throwing on an unplanned invocation.
    private static BunitContext NewContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return ctx;
    }

    private static IRenderedComponent<Menu> RenderMenu(BunitContext ctx) =>
        ctx.Render<Menu>(p =>
            p.Add(m => m.Label, "Account menu")
                .Add(m => m.Trigger, "A")
                .Add(m => m.ChildContent, "<a class=\"menu__item\">Sign out</a>")
        );

    [Fact]
    public void Panel_is_closed_until_the_trigger_is_clicked()
    {
        using var ctx = NewContext();
        var cut = RenderMenu(ctx);

        Assert.Empty(cut.FindAll(".menu__panel"));
        Assert.Equal("false", cut.Find(".menu__trigger").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Clicking_the_trigger_opens_the_panel()
    {
        using var ctx = NewContext();
        var cut = RenderMenu(ctx);

        cut.Find(".menu__trigger").Click();

        Assert.Contains("Sign out", cut.Find(".menu__panel").TextContent);
        Assert.Equal("true", cut.Find(".menu__trigger").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Escape_closes_an_open_panel()
    {
        using var ctx = NewContext();
        var cut = RenderMenu(ctx);
        cut.Find(".menu__trigger").Click();

        // Keydown is handled on the .menu wrapper; a real Esc bubbles here from the focused panel.
        cut.Find(".menu").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".menu__panel"));
    }

    [Fact]
    public void Clicking_the_backdrop_closes_an_open_panel()
    {
        using var ctx = NewContext();
        var cut = RenderMenu(ctx);
        cut.Find(".menu__trigger").Click();

        cut.Find(".menu__backdrop").Click();

        Assert.Empty(cut.FindAll(".menu__panel"));
    }
}
