using Jint;
using Xunit;

namespace StorageHub.Tests;

/// <summary>A station's tab bar takes the same flat, segmented look as the storage screen's while its Home tab is in it,
/// and puts the normal look back once the tab goes.</summary>
public class StationTabBarTests
{
    private readonly Engine _engine = new();

    public StationTabBarTests()
    {
        _engine.Execute(Resource("StorageHub.Tests.PageFake.js"));
        _engine.Execute(Resource("ExiledToolkit.api.js"));
        _engine.Execute(Resource("StorageHub.page.js"));
        _engine.Execute("window.__storagehub.live(true, '')");
        // The stove's page: its bag panel with the tab bar, the pinned group holding the bag's tab, and the scrolling group.
        _engine.Execute(
            "gameWindow.MutationObserver = function () { this.observe = function () {}; this.disconnect = function () {}; };" +
            "var host = gameEl(gameDoc.body, 'div', 'cooking-system');" +
            "var panel = gameEl(host, 'div', 'panel');" +
            "var bar = gameEl(panel, 'div', 'bag-tabs'); bar.id = 'bagTabs';" +
            "var pinned = gameEl(bar, 'div', 'bag-tab-pinned-slot'); pinned.id = 'bagTabPinned';" +
            "gameEl(pinned, 'div', 'bag-tab active').textContent = 'Inventory';" +
            "var row = gameEl(bar, 'div', 'bag-tabs-scroll');" +
            // The page shows its arrow buttons from what it finds when the row scrolls; here it only counts the checks.
            "var checks = 0; row.addEventListener('scroll', function () { checks++; });" +
            "document.frames.push({ src: 'file:///D:/Game/WebUI/Cooking/Cooking.html', contentDocument: gameDoc, contentWindow: gameWindow });" +
            "poll();" +
            "window.__storagehub.station({ kind: 'cooking', storages: [] });");
    }

    [Fact]
    public void The_bar_turns_segmented_with_the_home_tab_in_it()
    {
        Assert.True(Is("gameDoc.querySelector('#bagTabs .slmh-tab')"));
        Assert.True(Is("bar.classList.contains('slmh-seg')"));
        Assert.True(Is("gameDoc.getElementById('slmh-segstyle')"));
    }

    [Fact]
    public void The_bar_gets_the_games_look_back_when_the_list_is_turned_off()
    {
        _engine.Execute("window.__storagehub.station(null)");

        Assert.False(Is("gameDoc.querySelector('#bagTabs .slmh-tab')"));
        Assert.False(Is("bar.classList.contains('slmh-seg')"));
    }

    [Fact]
    public void The_page_checks_its_tab_arrows_again_once_the_home_tab_is_in()
    {
        // The home tab takes room from the scrolling row, which may now run past the panel's edge.
        Assert.True(_engine.Evaluate("checks").AsNumber() >= 1);
    }

    [Fact]
    public void The_page_checks_its_tab_arrows_again_once_the_home_tab_goes()
    {
        _engine.Execute("checks = 0; window.__storagehub.station(null)");

        Assert.True(_engine.Evaluate("checks").AsNumber() >= 1);
    }

    private bool Is(string expression) => _engine.Evaluate($"!!({expression})").AsBoolean();

    private static string Resource(string name)
    {
        using var stream = typeof(StationTabBarTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
