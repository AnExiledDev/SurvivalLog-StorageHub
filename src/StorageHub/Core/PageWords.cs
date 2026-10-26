using System.Linq;

namespace StorageHub.Core;

/// <summary>
/// The panel's own words in the game's language, and the name of the key that opens it, as the statement that hands
/// them to page.js. The statement is built once per language and key, and sent only when it differs from the one the
/// page has (see <see cref="WordsOnce"/>).
/// </summary>
public static class PageWords
{
    private static readonly string English = Statement(isChinese: false);
    private static readonly string Chinese = Statement(isChinese: true);

    private static Built? _last;

    public static string Script(bool isChinese, string toggleKey)
    {
        var last = _last;

        if (last != null && last.IsChinese == isChinese && last.ToggleKey == toggleKey) return last.Script;

        var built = new Built(isChinese, toggleKey, (isChinese ? Chinese : English) + "window.__storagehubToggleKey=" + HubView.Str(toggleKey) + ";");
        _last = built;

        return built.Script;
    }

    private static string Statement(bool isChinese) =>
        "window.__storagehubWords={" + string.Join(",", Texts.Page.Select(word => HubView.Str(word.Key) + ":" + HubView.Str(word.Phrase.Pick(isChinese)))) + "};";

    /// <summary>Swapped whole, so a reader on another thread sees the old statement or the new one, never half of each.</summary>
    private sealed record Built(bool IsChinese, string ToggleKey, string Script);
}

/// <summary>
/// The words statement the page last got. The page keeps its words until it reloads, so they go out again only when
/// the language or the key changed, or after <see cref="Forget"/> (a reload).
/// </summary>
public sealed class WordsOnce
{
    private string? _sent;

    /// <summary>The statement to put in front of the next call: the words, or "" when the page already has them.</summary>
    public string Pending(string words) => words == _sent ? "" : words;

    /// <summary>Call once a script carrying the words has run.</summary>
    public void Sent(string words) => _sent = words;

    public void Forget() => _sent = null;
}

/// <summary>
/// The station list the page last got. While the page still holds a list, the same answer is not sent again: the page
/// would only compare it with what it shows and drop it.
/// </summary>
public sealed class StationAnswers
{
    private string? _sent;

    public bool IsNew(string answer, bool pageHasList) => !pageHasList || answer != _sent;

    public void Sent(string answer) => _sent = answer;

    public void Forget() => _sent = null;
}
