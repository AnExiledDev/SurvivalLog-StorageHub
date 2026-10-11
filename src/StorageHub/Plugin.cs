using System.IO;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using StorageHub.Adapters;
using UnityEngine;

namespace StorageHub;

[BepInPlugin(Guid, "Storage Hub", Version)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.storagehub";
    private const string Version = "0.3.3";

    public override void Load()
    {
        var toggleKey = Config.Bind("Panel", "ToggleKey", KeyCode.F8, "Key that opens and closes the storage panel.").Value;
        var isVerbose = Config.Bind("Diagnostics", "Verbose", false, "Also log every planned move and every storage's tag rank, for working out why an item went where it did.").Value;
        var log = new HubLog(Log, Path.Combine(Paths.BepInExRootPath, "storage-hub.log"));
        var harmony = new Harmony(Guid);

        log.Write($"===== Storage Hub {Version} started, toggle key {toggleKey}, verbose {isVerbose}");
        InputGate.Apply(harmony, log.Write);
        PageMessages.Apply(harmony, log.Write);
        StoragePanel.Session = new HubSession(new GameStorages(log.Write), new WebPage(log.Write), new WebKeyboard(), toggleKey, isVerbose, log.Write);
        StoragePanel.Log = log.Write;
        AddComponent<StoragePanel>();
    }
}
