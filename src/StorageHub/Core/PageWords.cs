using System.Linq;
using ExiledToolkit.Web;

namespace StorageHub.Core;

/// <summary>
/// The panel's own words in the game's language, and the name of the key that opens it, as the statement that hands
/// them to page.js. The statement is built once per language and key, and Exiled's Toolkit sends it only when the page
/// lacks it.
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

        var built = new Built(isChinese, toggleKey, (isChinese ? Chinese : English) + "window.__storagehubToggleKey=" + JsText.Literal(toggleKey) + ";");
        _last = built;

        return built.Script;
    }

    private static string Statement(bool isChinese) =>
        "window.__storagehubWords={" + string.Join(",", Texts.Page.Select(word => JsText.Literal(word.Key) + ":" + JsText.Literal(word.Phrase.Pick(isChinese)))) + "};";

    /// <summary>Swapped whole, so a reader on another thread sees the old statement or the new one, never half of each.</summary>
    private sealed record Built(bool IsChinese, string ToggleKey, string Script);
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
