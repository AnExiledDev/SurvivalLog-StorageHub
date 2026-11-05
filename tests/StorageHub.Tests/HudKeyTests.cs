using Jint;
using Xunit;

namespace StorageHub.Tests;

/// <summary>
/// The chest button in the HUD, placed by Exiled's Toolkit's page API (api.js from the Toolkit's source): it shows once
/// a save is loaded, its tip names the toggle key only while one is set, and the game can run either script first.
/// </summary>
public class HudKeyTests
{
    private const string HudPage =
        "var hudWindow = { innerWidth: 1920, innerHeight: 1080, location: { href: 'file:///D:/Game/WebUI/CoreUI1/CoreUI1.html' } }; var hudDoc = new FakeDocument(hudWindow); hudWindow.document = hudDoc;" +
        "var tools = hudDoc.createElement('div'); tools.className = 'vertical-tools'; hudDoc.body.appendChild(tools);" +
        "document.frames.push({ src: 'file:///D:/Game/WebUI/CoreUI1/CoreUI1.html', contentDocument: hudDoc, contentWindow: hudWindow, isConnected: true });";

    private readonly Engine _engine = new();

    /// <param name="isToolkitFirst">The game runs the Toolkit's script first, or with false Storage Hub's first and the Toolkit's at <see cref="LoadToolkit"/>.</param>
    private void Start(bool isToolkitFirst = true)
    {
        _engine.Execute(Resource("StorageHub.Tests.PageFake.js"));

        if (isToolkitFirst) LoadToolkit();

        _engine.Execute(Resource("StorageHub.page.js"));
        _engine.Execute(HudPage);
    }

    private void LoadToolkit() => _engine.Execute(Resource("ExiledToolkit.api.js"));

    [Fact]
    public void An_unbound_toggle_key_is_left_out_of_the_chest_buttons_tip()
    {
        Start();

        _engine.Execute("window.__storagehub.live(true, '')");

        Assert.True(Is("hudDoc.querySelector('.sh-hud-btn')"));
        Assert.False(Is("hudDoc.querySelector('.entry-tip-key')"));
    }

    [Fact]
    public void A_bound_toggle_key_shows_in_the_chest_buttons_tip()
    {
        Start();

        _engine.Execute("window.__storagehub.live(true, 'F8')");

        Assert.Equal("F8", _engine.Evaluate("String(hudDoc.querySelector('.entry-tip-key').textContent)").AsString());
    }

    /// <summary>Before a save is loaded the button would open a panel with nothing to show.</summary>
    [Fact]
    public void The_chest_button_shows_only_while_a_save_is_loaded()
    {
        Start();

        _engine.Execute("poll()");

        Assert.False(Is("hudDoc.querySelector('.sh-hud-btn')"));

        _engine.Execute("window.__storagehub.live(true, ''); window.__storagehub.live(false, '')");

        Assert.False(Is("hudDoc.querySelector('.sh-hud-btn')"));
    }

    [Fact]
    public void A_click_on_the_chest_button_asks_the_plugin_to_open_the_panel()
    {
        Start();

        _engine.Execute("window.__storagehub.live(true, ''); posted.length = 0; hudDoc.querySelector('.sh-hud-btn').fire('click', { stopPropagation: function () {} })");

        Assert.Equal("slmod|storagehub|toggle", _engine.Evaluate("String(posted.join(','))").AsString());
    }

    /// <summary>The plugin's words arrive with its next call; the tip takes them at the next check, without a new button.</summary>
    [Fact]
    public void The_tip_follows_the_language_the_plugin_sends()
    {
        Start();

        _engine.Execute("window.__storagehub.live(true, ''); var button = hudDoc.querySelector('.sh-hud-btn'); window.__storagehubWords = { hudTip: 'The tip in the game language' }; poll()");

        Assert.Equal("The tip in the game language", _engine.Evaluate("String(hudDoc.querySelector('.entry-tip-body').textContent)").AsString());
        Assert.True(Is("hudDoc.querySelector('.sh-hud-btn') === button"));
    }

    /// <summary>
    /// The game may run Storage Hub's script before the Toolkit's: what the plugin said meanwhile waits, and the button
    /// and the first messages arrive once the Toolkit loads.
    /// </summary>
    [Fact]
    public void With_storage_hubs_script_first_the_button_and_the_ready_message_wait_for_the_toolkit()
    {
        Start(isToolkitFirst: false);

        _engine.Execute("window.__storagehub.live(true, 'F8')");

        Assert.False(Is("hudDoc.querySelector('.sh-hud-btn')"));
        Assert.Equal(0, (int)_engine.Evaluate("posted.length").AsNumber());

        LoadToolkit();

        Assert.True(Is("hudDoc.querySelector('.sh-hud-btn')"));
        Assert.Equal("F8", _engine.Evaluate("String(hudDoc.querySelector('.entry-tip-key').textContent)").AsString());
        Assert.Equal("slmod|storagehub|ready", _engine.Evaluate("String(posted[0])").AsString());
    }

    private bool Is(string expression) => _engine.Evaluate($"!!({expression})").AsBoolean();

    private static string Resource(string name)
    {
        using var stream = typeof(HudKeyTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
