using System.Linq;
using System.Text.RegularExpressions;

namespace StorageHub.Core;

/// <summary>
/// The colour of a status line: "bad" for a failure, "warn" when some of a move was left behind, "ok" when something
/// moved or was saved, "" for a plain line. Worked out from the mod's own phrases in either language, so every caller
/// keeps sending just the text.
/// </summary>
public static class StatusTone
{
    private static readonly Regex[] Bad = Patterns(Texts.Failed, Texts.KeepNotRead, Texts.KeepNotSaved, Texts.NotAvailable, Texts.NoBag, Texts.DeliveryUnread);
    private static readonly Regex[] Warn = Patterns(Texts.Nowhere, Texts.Stayed, Texts.StayedIn, Texts.DeliveryNoTaker);
    private static readonly Regex[] Ok = Patterns(Texts.Moved.One, Texts.Moved.Many, Texts.DeliveryMoved, Texts.Keeping, Texts.KeepingNone, Texts.OffKeepList);

    public static string Of(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (Bad.Any(pattern => pattern.IsMatch(text))) return "bad";
        if (Warn.Any(pattern => pattern.IsMatch(text))) return "warn";

        return Ok.Any(pattern => pattern.IsMatch(text)) ? "ok" : "";
    }

    // Each phrase in both languages, with its {0} holes matching anything.
    private static Regex[] Patterns(params Phrase[] phrases) =>
        phrases.SelectMany(phrase => new[] { phrase.En, phrase.Zh }).Select(Pattern).ToArray();

    private static Regex Pattern(string format) =>
        new(string.Join(".*?", Regex.Split(format, @"\{\d\}").Select(Regex.Escape)), RegexOptions.Singleline);
}
