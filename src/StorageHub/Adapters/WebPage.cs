using System;
using System.IO;
using ExiledToolkit;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using StorageHub.Core;
using Vuplex.WebView;

namespace StorageHub.Adapters;

/// <summary>
/// The panel's half that lives in the game's web UI: page.js, run in the root document the game's own pages sit in,
/// the same way the ivmakk mods add their UI. The web view can reload at any time and lose the script, so it is
/// checked for every couple of seconds and put back when the page says it is missing.
/// </summary>
internal sealed class WebPage
{
    private const float CheckSeconds = 2f;
    private const string ScriptResource = "StorageHub.page.js";
    private const string NoScriptReply = "window.vuplex&&window.vuplex.postMessage('" + PageCommand.Prefix + "noscript')";

    private readonly Action<string> _log;
    private readonly Func<bool> _isPanelOpen;
    private readonly WordsOnce _words = new();
    private string _toggleKey = "";
    private string? _script;
    private float _nextCheckAt;
    private bool _hasReportedRunFailure;

    public WebPage(Action<string> log, Func<bool> isPanelOpen)
    {
        _log = log;
        _isPanelOpen = isPanelOpen;
    }

    /// <summary>The page's words name the panel key; a new key sends them again with the next call.</summary>
    public void UseToggleKey(string toggleKey) => _toggleKey = toggleKey;

    public void KeepInstalled(float now)
    {
        if (now < _nextCheckAt) return;

        _nextCheckAt = now + CheckSeconds;
        Run("window.__storagehub||" + NoScriptReply);
    }

    public void Install()
    {
        // A fresh script means a fresh page, which has no words until this sends them.
        _words.Forget();
        var words = Words();

        if (!Run(words + Script())) return;

        _words.Sent(words);
        _log("page script sent to the web UI");
    }

    public void Open(string viewJson) => Call($"open({viewJson})");

    public void Show(string viewJson) => Call($"setData({viewJson})");

    public void Close() => Call("close()");

    /// <summary>Every named item in the game, for the keep list's search; sent when the page asks, once per page load.</summary>
    public void Catalog(string catalogJson) => Call($"catalog({catalogJson})");

    /// <summary>The home storage list beside a station; "null" tells the page the list is turned off. False when it could not be sent.</summary>
    public bool Station(string stationJson) => Call($"station({stationJson})");

    /// <summary>Whether the HUD button can open the panel (a save is loaded), and the key it names in its tip.</summary>
    public bool Live(bool isLive, string keyName) => Run($"window.__storagehub&&window.__storagehub.live({(isLive ? "true" : "false")},{HubView.Str(keyName)})");

    /// <summary>A result line: in the panel's footer while it is open, as an Exiled's Toolkit toast otherwise.</summary>
    public void Status(string text)
    {
        var tone = StatusTone.Of(text);

        Call($"status({HubView.Str(text)},{HubView.Str(tone)})");

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
    public void OpenPop() => Run("window.__storagehub&&window.__storagehub.openPop&&window.__storagehub.openPop()");

    /// <summary>Closes the home storage window.</summary>
    public void ClosePop() => Run("window.__storagehub&&window.__storagehub.closePop&&window.__storagehub.closePop()");

    /// <summary>
    /// The words go in front only when the page does not have them yet (a new language or key): they are a few
    /// kilobytes, and once went out with every call.
    /// </summary>
    private bool Call(string expression)
    {
        var words = Words();

        if (!Run($"{_words.Pending(words)}window.__storagehub?window.__storagehub.{expression}:{NoScriptReply}")) return false;

        _words.Sent(words);

        return true;
    }

    private string Words() => PageWords.Script(GameLanguage.IsChinese(), _toggleKey);

    private bool Run(string javaScript)
    {
        var webView = GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;

        if (webView == null) return false;

        try
        {
            webView.ExecuteJavaScript(javaScript, null);

            return true;
        }
        catch (Exception ex)
        {
            if (!_hasReportedRunFailure) _log("could not run script in the web UI (reported once): " + ex.Message);

            _hasReportedRunFailure = true;

            return false;
        }
    }

    private string Script()
    {
        if (_script != null) return _script;

        using var stream = typeof(WebPage).Assembly.GetManifestResourceStream(ScriptResource) ?? throw new InvalidOperationException(ScriptResource + " is not embedded");
        using var reader = new StreamReader(stream);

        return _script = reader.ReadToEnd();
    }
}
