using Jint;
using Xunit;

namespace StorageHub.Tests;

/// <summary>A station page zooms itself to fit the window, and the Home tab and the flat look can then make its tab bar
/// taller. On the drone trade with a big bag and Pack Rat's scroll bars off, the bottom edge went past the window and
/// the page scrolled. The page fits itself again on a resize event, so one is sent whenever the bar's height changes.</summary>
public class PageRefitTests
{
    private readonly Engine _engine = new();

    public PageRefitTests()
    {
        _engine.Execute(Resource("StorageHub.Tests.PageFake.js"));
        _engine.Execute(Resource("ExiledToolkit.api.js"));
        _engine.Execute(Resource("StorageHub.page.js"));
        // The stove's page, with a tab bar that stands 31 px in the game's look and 38 px in the flat one.
        _engine.Execute(
            "gameWindow.MutationObserver = function () { this.observe = function () {}; this.disconnect = function () {}; };" +
            "var host = gameEl(gameDoc.body, 'div', 'cooking-system');" +
            "var panel = gameEl(host, 'div', 'panel');" +
            "var bar = gameEl(panel, 'div', 'bag-tabs'); bar.id = 'bagTabs';" +
            "var flatHeight = 38;" +
            "Object.defineProperty(bar, 'offsetHeight', { get: function () { return bar.classList.contains('slmh-seg') ? flatHeight : 31; } });" +
            "var pinned = gameEl(bar, 'div', 'bag-tab-pinned-slot'); pinned.id = 'bagTabPinned';" +
            "gameEl(pinned, 'div', 'bag-tab active').textContent = 'Inventory';" +
            "gameEl(bar, 'div', 'bag-tabs-scroll');" +
            "document.frames.push({ src: 'file:///D:/Game/WebUI/Cooking/Cooking.html', contentDocument: gameDoc, contentWindow: gameWindow });" +
            "poll();" +
            "function resizes() { return gameWindow.sent.filter(function (type) { return type === 'resize'; }).length; }");
    }

    [Fact]
    public void The_page_fits_itself_again_once_the_home_tab_and_flat_look_are_in()
    {
        Station();

        Assert.True(Is("bar.classList.contains('slmh-seg')"));
        Assert.Equal(1, Resizes());
    }

    [Fact]
    public void A_pass_that_leaves_the_bar_the_same_height_does_not_ask_again()
    {
        Station();
        Station();
        _engine.Execute("poll()");

        Assert.Equal(1, Resizes());
    }

    [Fact]
    public void A_bar_that_grows_later_asks_again()
    {
        Station();
        _engine.Execute("flatHeight = 62");
        Station();

        Assert.Equal(2, Resizes());
    }

    [Fact]
    public void The_page_fits_itself_again_once_the_home_tab_goes()
    {
        Station();
        _engine.Execute("window.__storagehub.station(null)");

        Assert.False(Is("bar.classList.contains('slmh-seg')"));
        Assert.Equal(2, Resizes());
    }

    private void Station() => _engine.Execute("window.__storagehub.station({ kind: 'cooking', storages: [] })");

    private int Resizes() => (int)_engine.Evaluate("resizes()").AsNumber();

    private bool Is(string expression) => _engine.Evaluate($"!!({expression})").AsBoolean();

    private static string Resource(string name)
    {
        using var stream = typeof(PageRefitTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
