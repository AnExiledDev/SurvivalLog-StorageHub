using Jint;
using Xunit;

namespace StorageHub.Tests;

/// <summary>Beside a storage or the basket, the bag panel's tab bar lives in the game's header (a <c>.header-slot</c>
/// child of the panel). Showing the Home list must leave that header drawn, so the player keeps the storage tabs and
/// can click back out of Home.</summary>
public class HomeHeaderTests
{
    private readonly Engine _engine = new();

    public HomeHeaderTests()
    {
        _engine.Execute(Resource("StorageHub.Tests.PageFake.js"));
        _engine.Execute(Resource("ExiledToolkit.api.js"));
        _engine.Execute(Resource("StorageHub.page.js"));
        _engine.Execute(
            "gameWindow.MutationObserver = function () { this.observe = function () {}; this.disconnect = function () {}; };" +
            "var host = gameEl(gameDoc.body, 'div', 'cooking-system');" +
            "var panel = gameEl(host, 'div', 'panel');" +
            "var bar = gameEl(panel, 'div', 'bag-tabs'); bar.id = 'bagTabs';" +
            "var pinned = gameEl(bar, 'div', 'bag-tab-pinned-slot'); pinned.id = 'bagTabPinned';" +
            "gameEl(pinned, 'div', 'bag-tab active').textContent = 'Inventory';" +
            "gameEl(bar, 'div', 'bag-tabs-scroll');" +
            "document.frames.push({ src: 'file:///D:/Game/WebUI/Cooking/Cooking.html', contentDocument: gameDoc, contentWindow: gameWindow });" +
            "poll();" +
            "window.__storagehub.station({ kind: 'cooking', storages: [] });");
    }

    [Fact]
    public void No_rule_hides_the_header_holding_the_tab_bar_while_home_is_shown()
    {
        // Every rule that hides a direct child of the panel while Home is on, tested against a child classed
        // header-slot: a ":not(...)" catch-all matches it unless it excludes header-slot by name.
        var hiders = _engine.Evaluate(
            "(function () {" +
            "  var css = gameDoc.getElementById('slmh-style').textContent;" +
            "  var out = [];" +
            "  css.split('}').forEach(function (rule) {" +
            "    var at = rule.indexOf('{'); if (at < 0) return;" +
            "    var sel = rule.slice(0, at).trim(), body = rule.slice(at + 1);" +
            "    if (sel.indexOf('.slmh-on>') !== 0 || !/visibility:\\s*hidden/.test(body)) return;" +
            "    var child = sel.slice('.slmh-on>'.length);" +
            "    if (child.indexOf('>') >= 0 || child.indexOf(' ') >= 0) return;" +
            "    var hits = child.indexOf(':not(') === 0 ? child.indexOf(':not(.header-slot)') < 0 : child === '.header-slot';" +
            "    if (hits) out.push(sel);" +
            "  });" +
            "  return out.join(' | ');" +
            "})()").AsString();

        Assert.Equal("", hiders);
    }

    [Fact]
    public void The_catch_all_still_hides_the_rest_of_the_panel()
    {
        Assert.True(_engine.Evaluate(
            "/\\.slmh-on>:not\\([^{]*\\{visibility:hidden\\}/.test(gameDoc.getElementById('slmh-style').textContent)").AsBoolean());
    }

    private static string Resource(string name)
    {
        using var stream = typeof(HomeHeaderTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
