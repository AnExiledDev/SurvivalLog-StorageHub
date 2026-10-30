using Jint;
using Xunit;

namespace StorageHub.Tests;

/// <summary>The chest button's tip names the toggle key only while one is set; an unbound key shows no key at all.</summary>
public class HudKeyTests
{
    private readonly Engine _engine = new();

    public HudKeyTests()
    {
        _engine.Execute(Resource("StorageHub.Tests.PageFake.js"));
        _engine.Execute(Resource("StorageHub.page.js"));
        _engine.Execute(
            "var hudWindow = { innerWidth: 1920, innerHeight: 1080 }; var hudDoc = new FakeDocument(hudWindow); hudWindow.document = hudDoc;" +
            "var tools = hudDoc.createElement('div'); tools.className = 'vertical-tools'; hudDoc.body.appendChild(tools);" +
            "document.frames.push({ src: 'file:///D:/Game/WebUI/CoreUI1/CoreUI1.html', contentDocument: hudDoc, contentWindow: hudWindow });");
    }

    [Fact]
    public void An_unbound_toggle_key_is_left_out_of_the_chest_buttons_tip()
    {
        _engine.Execute("window.__storagehub.live(true, '')");

        Assert.True(Is("hudDoc.querySelector('.sh-hud-btn')"));
        Assert.False(Is("hudDoc.querySelector('.entry-tip-key')"));
    }

    [Fact]
    public void A_bound_toggle_key_shows_in_the_chest_buttons_tip()
    {
        _engine.Execute("window.__storagehub.live(true, 'F8')");

        Assert.Equal("F8", _engine.Evaluate("String(hudDoc.querySelector('.entry-tip-key').textContent)").AsString());
    }

    private bool Is(string expression) => _engine.Evaluate($"!!({expression})").AsBoolean();

    private static string Resource(string name)
    {
        using var stream = typeof(HudKeyTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
