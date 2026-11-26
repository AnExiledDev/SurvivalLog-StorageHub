using Jint;
using Xunit;

namespace StorageHub.Tests;

/// <summary>The station screens are looked for only while a save is loaded; on the main menu there are none.</summary>
public class StationPollTests
{
    private readonly Engine _engine = new();

    public StationPollTests()
    {
        _engine.Execute(Resource("StorageHub.Tests.PageFake.js"));
        _engine.Execute(Resource("ExiledToolkit.api.js"));
        _engine.Execute(Resource("StorageHub.page.js"));
        // The stove's page, the same as in StationTabBarTests.
        _engine.Execute(
            "gameWindow.MutationObserver = function () { this.observe = function () {}; this.disconnect = function () {}; };" +
            "var host = gameEl(gameDoc.body, 'div', 'cooking-system');" +
            "var panel = gameEl(host, 'div', 'panel');" +
            "var bar = gameEl(panel, 'div', 'bag-tabs'); bar.id = 'bagTabs';" +
            "var pinned = gameEl(bar, 'div', 'bag-tab-pinned-slot'); pinned.id = 'bagTabPinned';" +
            "gameEl(pinned, 'div', 'bag-tab active').textContent = 'Inventory';" +
            "gameEl(bar, 'div', 'bag-tabs-scroll');" +
            "document.frames.push({ src: 'file:///D:/Game/WebUI/Cooking/Cooking.html', contentDocument: gameDoc, contentWindow: gameWindow });");
    }

    [Fact]
    public void Before_a_save_is_loaded_no_station_gets_a_home_tab()
    {
        _engine.Execute("poll(); window.__storagehub.station({ kind: 'cooking', storages: [] });");

        Assert.False(Is("gameDoc.querySelector('#bagTabs .slmh-tab')"));
    }

    [Fact]
    public void Once_a_save_is_loaded_the_station_gets_its_home_tab()
    {
        _engine.Execute("window.__storagehub.live(true, ''); poll(); window.__storagehub.station({ kind: 'cooking', storages: [] });");

        Assert.True(Is("gameDoc.querySelector('#bagTabs .slmh-tab')"));
    }

    private bool Is(string expression) => _engine.Evaluate($"!!({expression})").AsBoolean();

    private static string Resource(string name)
    {
        using var stream = typeof(StationPollTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
