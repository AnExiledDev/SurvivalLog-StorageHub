using System;
using ExiledToolkit;
using ExiledToolkit.Game;
using ExiledToolkit.Web;
using StorageHub.Core;

namespace StorageHub.Adapters;

/// <summary>
/// The panel's half that lives in the game's web UI: page.js, run in the root document the game's own pages sit in,
/// the same way the ivmakk mods add their UI. Exiled's Toolkit keeps the script there (the web view can reload at any
/// time and lose it); this names the calls the panel takes.
/// </summary>
internal sealed class WebPage
{
    private const string ScriptResource = "StorageHub.page.js";
    private const string Global = "window.__storagehub";

    private readonly ExiledToolkit.Web.WebPage _script;
    private readonly Func<bool> _isPanelOpen;
    private string _toggleKey = "";

    public WebPage(Action<string> log, Func<bool> isPanelOpen)
    {
        _isPanelOpen = isPanelOpen;
        _script = new ExiledToolkit.Web.WebPage(typeof(WebPage).Assembly, ScriptResource, Global, PageCommand.Prefix + "noscript", log)
        {
            // The words are a few kilobytes, and once went out with every call; the Toolkit sends them only when the page lacks them.
            Words = isChinese => PageWords.Script(isChinese, _toggleKey),
        };
    }

    /// <summary>The page's words name the panel key; a new key sends them again with the next call.</summary>
    public void UseToggleKey(string toggleKey)
    {
        if (toggleKey == _toggleKey) return;

        _toggleKey = toggleKey;
        _script.ForgetWords();
    }

    public void KeepInstalled(double now) => _script.KeepInstalled(now);

    public void Install() => _script.Install();

    public void Open(string viewJson) => _script.Call($"open({viewJson})");

    public void Show(string viewJson) => _script.Call($"setData({viewJson})");

    public void Close() => _script.Call("close()");

    /// <summary>Every named item in the game, for the keep list's search; sent when the page asks, once per page load.</summary>
    public void Catalog(string catalogJson) => _script.Call($"catalog({catalogJson})");

    /// <summary>The home storage list beside a station; "null" tells the page the list is turned off. False when it could not be sent.</summary>
    public bool Station(string stationJson) => _script.Call($"station({stationJson})");

    /// <summary>
    /// Whether the HUD button can open the panel (a save is loaded), and the key it names in its tip. A call, so a page
    /// that has just been installed gets its words, which the button's tip is in, with the first one.
    /// </summary>
    public bool Live(bool isLive, string keyName) => _script.Call($"live({(isLive ? "true" : "false")},{JsText.Literal(keyName)})");

    /// <summary>A result line: in the panel's footer while it is open, as an Exiled's Toolkit toast otherwise.</summary>
    public void Status(string text)
    {
        var tone = StatusTone.Of(text);

        _script.Call($"status({JsText.Literal(text)},{JsText.Literal(tone)})");

        if (_isPanelOpen() || string.IsNullOrEmpty(text)) return;

        Toasts.Show(new ToastRequest(Texts.Source.Pick(GameLanguage.IsChinese()), text) { Kind = ToastKindOf(tone) });
    }

    private static ToastKind ToastKindOf(string tone) => tone switch
    {
        "ok" => ToastKind.Success,
        "warn" => ToastKind.Warning,
        "bad" => ToastKind.Error,
        _ => ToastKind.Info,
    };

    /// <summary>Opens the home storage window on the inventory screen, from its Mods menu row.</summary>
    public void OpenPop() => _script.Run(Global + "&&" + Global + ".openPop&&" + Global + ".openPop()");

    /// <summary>Closes the home storage window.</summary>
    public void ClosePop() => _script.Run(Global + "&&" + Global + ".closePop&&" + Global + ".closePop()");
}
