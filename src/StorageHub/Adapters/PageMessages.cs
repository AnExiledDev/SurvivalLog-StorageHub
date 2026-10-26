using System;
using System.Collections.Concurrent;
using HarmonyLib;
using StorageHub.Core;
using Vuplex.WebView.Internal;

namespace StorageHub.Adapters;

/// <summary>
/// Catches the panel's messages before the game's web layer sees them (it would log them as unknown) and queues them
/// for the next frame. Static because a Harmony prefix has nowhere else to look.
/// </summary>
internal static class PageMessages
{
    private static readonly ConcurrentQueue<PageCommand> Queue = new();
    private static Action<string> _log = _ => { };
    private static bool _hasReportedFailure;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        try
        {
            harmony.Patch(AccessTools.Method(typeof(BaseWebView), "HandleMessageEmitted"), prefix: new HarmonyMethod(typeof(PageMessages), nameof(TakeOurs)));
            log("listening for panel messages");
        }
        catch (Exception ex)
        {
            log("could not hook panel messages, its buttons will do nothing: " + ex.Message);
        }
    }

    private static readonly PageCommand Nothing = new PageCommand.Unknown("");

    /// <summary>Whether a message is waiting, so a frame with none skips the take entirely.</summary>
    public static bool HasQueued => !Queue.IsEmpty;

    public static bool TryTake(out PageCommand command)
    {
        if (Queue.TryDequeue(out var queued))
        {
            command = queued;

            return true;
        }

        command = Nothing;

        return false;
    }

    /// <summary>Runs the hook once on a message that is not ours, so it is compiled before the game's first web message.</summary>
    public static void WarmUp() => TakeOurs("warm-up");

    /// <summary>A throw here would break every web message in the game, so any failure lets the game handle the message.</summary>
    private static bool TakeOurs(string serializedMessage)
    {
        try
        {
            if (!PageCommand.IsOurs(serializedMessage)) return true;

            Queue.Enqueue(PageCommand.Parse(serializedMessage));

            return false;
        }
        catch (Exception ex)
        {
            ReportFailureOnce(ex);

            return true;
        }
    }

    private static void ReportFailureOnce(Exception ex)
    {
        if (_hasReportedFailure) return;

        _hasReportedFailure = true;

        _log("reading a panel message failed, the game handles it instead: " + ex.Message);
    }
}
