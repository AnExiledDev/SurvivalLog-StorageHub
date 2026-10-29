using Jint;
using Xunit;

namespace StorageHub.Tests;

/// <summary>
/// The panel, its tips, its short messages and the inventory's Storage Hub window follow Exiled's Toolkit's UI size.
/// page.js as the plugin ships it runs in Jint on a fake Root.html (PageFake.js) whose Toolkit records what it is handed.
/// Zoom scales an element's own pixel offsets, so whatever the page places itself (a tip, the inventory window) must
/// work out its spot in screen pixels and write it in its own zoomed ones, or a bigger size moves it.
/// </summary>
public class UiSizeTests
{
    /// <summary>The mask fills the screen at any size, so only the plate on it can be fitted.</summary>
    [Fact]
    public void The_plate_is_handed_over_to_be_fitted()
    {
        var page = new Page();

        page.Run("window.__storagehub.status('Saved', 'ok')");

        Assert.Equal(1, page.Number("handed.length"));
        Assert.Equal("StorageHub", page.Text("handed[0].options.mod"));
        Assert.Equal(1, page.Number("handedWith('sh-plate').length"));
        Assert.True(page.Is("handedWith('sh-plate')[0].options === undefined || handedWith('sh-plate')[0].options.fit !== false"));
    }

    /// <summary>With the panel closed a result line is the plugin's Toolkit toast, so the page draws no box of its own.</summary>
    [Fact]
    public void A_result_with_the_panel_closed_draws_nothing_on_the_page()
    {
        var page = new Page();

        page.Run("window.__storagehub.status('Saved', 'ok')");

        Assert.False(page.Is("document.getElementById('sh-toast')"));
        Assert.Equal(0, page.Number("handedId('sh-toast').length"));
    }

    [Fact]
    public void Before_the_Toolkit_loads_the_panels_wait_for_it()
    {
        var page = new Page("delete window.ExiledUi");

        page.Run("window.__storagehub.status('Saved', 'ok')");

        Assert.Equal(1, page.Number("window.__exiledUiWait.length"));
        Assert.True(page.Is("window.__exiledUiWait[0][0].classList.contains('sh-plate')"));
    }

    /// <summary>
    /// The tip sits on the page's body, outside the zoomed plate, so it takes the plate's zoom to show at the same size,
    /// and is still centered under its button: 650 across, 8 px below it at 1, both times 1.5 at 1.5.
    /// </summary>
    [Fact]
    public void A_tip_in_a_zoomed_panel_takes_its_zoom_and_still_sits_under_its_button()
    {
        var page = new Page();

        page.Run("var tip = tipAt(1.5)");

        Assert.Equal("1.5", page.Text("tip.style.zoom"));
        // 650 less half of 200 x 1.5, then in the tip's own pixels: 500 / 1.5.
        Assert.Equal("333px", page.Text("tip.style.left"));
        // 400 below the button plus 8 x 1.5, over 1.5.
        Assert.Equal("275px", page.Text("tip.style.top"));
    }

    [Fact]
    public void A_tip_at_the_game_size_is_placed_as_before()
    {
        var page = new Page();

        page.Run("var tip = tipAt(1)");

        Assert.Equal("", page.Text("tip.style.zoom"));
        Assert.Equal("550px", page.Text("tip.style.left"));
        Assert.Equal("408px", page.Text("tip.style.top"));
    }

    /// <summary>
    /// The window keeps itself inside the screen wherever the panels are, so it is never shrunk, and it is placed again
    /// after each new size.
    /// </summary>
    [Fact]
    public void The_inventory_window_is_handed_over_never_shrunk_and_placed_again_after_each_size()
    {
        var page = new Page();

        page.Run("var pop = openPop()");

        Assert.Equal(1, page.Number("handedWith('slmh-pop').length"));
        Assert.True(page.Is("handedWith('slmh-pop')[0].el === pop"));
        Assert.True(page.Is("handedWith('slmh-pop')[0].options.fit === false"));
        Assert.True(page.Is("typeof handedWith('slmh-pop')[0].options.onZoom === 'function'"));
    }

    /// <summary>
    /// At 1.5 the window is 480 wide on screen, so it sits 24 left of the panels at 196, as tall as they are (700 from
    /// 200), and all of that is written in its own pixels, over 1.5. Its width stays 320, which the zoom makes 480.
    /// </summary>
    [Fact]
    public void A_zoomed_inventory_window_still_sits_beside_the_panels_and_as_tall_as_them()
    {
        var page = new Page();

        page.Run("var pop = openPop(); pop.style.zoom = '1.5'; handedWith('slmh-pop')[0].options.onZoom(1.5)");

        Assert.Equal("131px", page.Text("pop.style.left"));
        Assert.Equal("133px", page.Text("pop.style.top"));
        Assert.Equal("320px", page.Text("pop.style.width"));
        Assert.Equal("467px", page.Text("pop.style.height"));
    }

    [Fact]
    public void The_inventory_window_at_the_game_size_is_placed_as_before()
    {
        var page = new Page();

        page.Run("var pop = openPop()");

        Assert.Equal("356px", page.Text("pop.style.left"));
        Assert.Equal("200px", page.Text("pop.style.top"));
        Assert.Equal("320px", page.Text("pop.style.width"));
        Assert.Equal("700px", page.Text("pop.style.height"));
    }

    /// <summary>page.js on PageFake.js; <paramref name="before"/> runs between the two.</summary>
    private sealed class Page
    {
        private readonly Engine _engine = new();

        public Page(string? before = null)
        {
            _engine.Execute(Resource("StorageHub.Tests.PageFake.js"));
            if (before != null) _engine.Execute(before);
            _engine.Execute(Resource("StorageHub.page.js"));
        }

        public void Run(string statements) => _engine.Execute(statements);

        public string Text(string expression) => _engine.Evaluate($"String({expression})").AsString();

        public int Number(string expression) => (int)_engine.Evaluate($"Number({expression})").AsNumber();

        public bool Is(string expression) => _engine.Evaluate($"!!({expression})").AsBoolean();

        private static string Resource(string name)
        {
            using var stream = typeof(UiSizeTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }
    }
}
