using Jint;
using Xunit;

namespace StorageHub.Tests;

/// <summary>The ration press draws all its tabs in one scrolling row, where Home went last and scrolled out of sight. Its
/// bag's tab now goes in a box at the front of the bar with Home after it, the way the shredder draws its bar, so both
/// stay put while the storage tabs scroll.</summary>
public class PressTabBarTests
{
    private readonly Engine _engine = new();

    public PressTabBarTests()
    {
        _engine.Execute(Resource("StorageHub.Tests.PageFake.js"));
        _engine.Execute(Resource("ExiledToolkit.api.js"));
        _engine.Execute(Resource("StorageHub.page.js"));
        _engine.Execute("window.__storagehub.live(true, '')");
        // The ration press: its bag panel, the bar with an arrow at each end, and every tab drawn in the row between them.
        _engine.Execute(
            "gameWindow.MutationObserver = function () { this.observe = function () {}; this.disconnect = function () {}; };" +
            "gameWindow.State = { cartTabs: [{ ownerId: 1, name: 'Bag' }, { ownerId: 2, name: 'Fridge' }, { ownerId: 3, name: 'Cabinet' }], activeTab: 0, cartOwnerId: 1 };" +
            "var cart = gameEl(gameDoc.body, 'div', 'cart');" +
            "var bar = gameEl(cart, 'div', 'left-tabs');" +
            "gameEl(bar, 'button', 'tab-scroll-btn');" +
            "var row = gameEl(bar, 'div', 'left-tabs-scroll'); row.id = 'tabs';" +
            "gameEl(bar, 'button', 'tab-scroll-btn');" +
            // The page empties the row and draws every tab again whenever anything changes.
            "function draw() {" +
            "  row.textContent = '';" +
            "  gameWindow.State.cartTabs.forEach(function (t, i) { gameEl(row, 'button', 'left-tab' + (i === gameWindow.State.activeTab ? ' active' : '')).textContent = t.name; });" +
            "}" +
            "draw();" +
            "document.frames.push({ src: 'file:///D:/Game/WebUI/Compressor/Compressor.html', contentDocument: gameDoc, contentWindow: gameWindow });" +
            "poll();" +
            "window.__storagehub.station({ kind: 'compressor', storages: [] });");
    }

    [Fact]
    public void The_bags_tab_and_home_sit_at_the_front_of_the_bar_ahead_of_the_scrolling_tabs()
    {
        Assert.True(Is("bar.children[0].classList.contains('slmh-pinbox')"));
        Assert.Equal("Bag", Text("bar.children[0].children[0].textContent"));
        Assert.True(Is("bar.children[0].children[1].classList.contains('slmh-tab')"));
        Assert.Equal("Fridge,Cabinet", Text("row.children.map(function (t) { return t.textContent; }).join(',')"));
    }

    [Fact]
    public void After_the_page_draws_its_tabs_again_the_bag_has_one_tab_and_it_is_the_new_one()
    {
        _engine.Execute("draw(); var fresh = row.children[0]; poll();");

        Assert.Equal("Bag,Home", Text("bar.children[0].children.map(function (t) { return t.classList.contains('slmh-tab') ? 'Home' : t.textContent; }).join(',')"));
        Assert.True(Is("bar.children[0].children[0] === fresh"));
        Assert.Equal("Fridge,Cabinet", Text("row.children.map(function (t) { return t.textContent; }).join(',')"));
    }

    [Fact]
    public void Turned_off_the_bags_tab_goes_back_to_the_front_of_the_row_and_the_box_goes()
    {
        _engine.Execute("window.__storagehub.station(null)");

        Assert.False(Is("gameDoc.querySelector('.slmh-pinbox')"));
        Assert.Equal("Bag,Fridge,Cabinet", Text("row.children.map(function (t) { return t.textContent; }).join(',')"));
    }

    private bool Is(string expression) => _engine.Evaluate($"!!({expression})").AsBoolean();

    private string Text(string expression) => _engine.Evaluate($"String({expression})").AsString();

    private static string Resource(string name)
    {
        using var stream = typeof(PressTabBarTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
