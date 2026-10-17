using System.Linq;

namespace StorageHub.Core;

/// <summary>
/// The panel's own words in the game's language, and the name of the key that opens it, as the statement that hands
/// them to page.js before every call.
/// </summary>
public static class PageWords
{
    private static readonly string English = Statement(isChinese: false);
    private static readonly string Chinese = Statement(isChinese: true);

    public static string Script(bool isChinese, string toggleKey) =>
        (isChinese ? Chinese : English) + "window.__storagehubToggleKey=" + HubView.Str(toggleKey) + ";";

    private static string Statement(bool isChinese) =>
        "window.__storagehubWords={" + string.Join(",", Texts.Page.Select(word => HubView.Str(word.Key) + ":" + HubView.Str(word.Phrase.Pick(isChinese)))) + "};";
}
