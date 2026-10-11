using System;
using System.IO;
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
    private string? _script;
    private float _nextCheckAt;
    private bool _hasReportedRunFailure;

    public WebPage(Action<string> log)
    {
        _log = log;
    }

    public void KeepInstalled(float now)
    {
        if (now < _nextCheckAt) return;

        _nextCheckAt = now + CheckSeconds;
        Run("window.__storagehub||" + NoScriptReply);
    }

    public void Install()
    {
        if (Run(Script())) _log("page script sent to the web UI");
    }

    public void Open(string viewJson) => Call($"open({viewJson})");

    public void Show(string viewJson) => Call($"setData({viewJson})");

    public void Close() => Call("close()");

    /// <summary>A result line: in the panel's footer while it is open, as a toast otherwise.</summary>
    public void Status(string text) => Call($"status({HubView.Str(text)})");

    private void Call(string expression) => Run($"window.__storagehub?window.__storagehub.{expression}:{NoScriptReply}");

    private bool Run(string javaScript)
    {
        var webView = BaseSingleton<ReduxUISystem>.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;

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
