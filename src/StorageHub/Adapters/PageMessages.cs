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

    public static void Apply(Harmony harmony, Action<string> log)
    {
        try
        {
            harmony.Patch(AccessTools.Method(typeof(BaseWebView), "HandleMessageEmitted"), prefix: new HarmonyMethod(typeof(PageMessages), nameof(TakeOurs)));
            log("listening for panel messages");
        }
        catch (Exception ex)
        {
            log("could not listen for panel messages, its buttons will do nothing: " + ex.Message);
        }
    }

    public static bool TryTake(out PageCommand command)
    {
        if (Queue.TryDequeue(out var queued))
        {
            command = queued;

            return true;
        }

        command = new PageCommand.Unknown("");

        return false;
    }

    private static bool TakeOurs(string serializedMessage)
    {
        if (!PageCommand.IsOurs(serializedMessage)) return true;

        Queue.Enqueue(PageCommand.Parse(serializedMessage));

        return false;
    }
}
