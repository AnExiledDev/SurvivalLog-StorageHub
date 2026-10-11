namespace StorageHub.Core;

/// <summary>
/// Decides what the panel's status line is told. An action's result is always sent, even when it repeats, because the
/// page unlocks its buttons when a status arrives. The once-a-second refresh speaks only when readiness changes, so a
/// "not available" line is not resent every second, does not outlive the problem, and never wipes an action's result.
/// </summary>
public sealed class StatusLine
{
    private string _shown = "";
    private bool _isNotReadyShown;

    /// <summary>The panel was just drawn with an empty status line.</summary>
    public void Cleared()
    {
        _shown = "";
        _isNotReadyShown = false;
    }

    /// <summary>An action finished; its result is always the text to send.</summary>
    public string Result(string status)
    {
        _shown = status;
        _isNotReadyShown = false;

        return status;
    }

    /// <summary>What to send after a refresh, given why the inventory could not be read (null when it could), or null to send nothing.</summary>
    public string? Refreshed(string? notReady)
    {
        if (notReady == null) return _isNotReadyShown ? Show("", isNotReady: false) : null;

        // An action can already have shown this exact text as its result; it still has to be cleared once the read works.
        if (notReady == _shown)
        {
            _isNotReadyShown = true;

            return null;
        }

        return Show(notReady, isNotReady: true);
    }

    private string Show(string text, bool isNotReady)
    {
        _shown = text;
        _isNotReadyShown = isNotReady;

        return text;
    }
}
