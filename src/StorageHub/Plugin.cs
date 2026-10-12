using System;
using System.IO;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using StorageHub.Adapters;
using StorageHub.Core;
using UnityEngine;

namespace StorageHub;

[BepInPlugin(Guid, "Storage Hub", Version)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.storagehub";
    private const string Version = "0.4.1";

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
        StartOrganizer(harmony, log);
    }

    private void StartOrganizer(Harmony harmony, HubLog log)
    {
        var organizeOnOpen = Config.Bind("Organize", "OrganizeOnOpen", true, "Tidy a storage's items each time you open it. Your own bag is never tidied on open.").Value;
        var compactSmallItems = Config.Bind("Organize", "CompactSmallItems", true, "Pack small items into a square in the top-left corner and big items from the bottom-right, so the free space stays in one piece for big items. Also applies to the game's Sort button. Off: storages are tidied the way the game's Sort does it, and Sort is left alone.").Value;
        var smallItemMaxArea = Math.Max(0, Config.Bind("Organize", "SmallItemMaxArea", 2, "Items covering this many cells or fewer count as small.").Value);

        log.Write($"organize on open {organizeOnOpen}, compact small items {compactSmallItems}, small means up to {smallItemMaxArea} cells");

        if (!organizeOnOpen && !compactSmallItems) return;

        OrganizeHooks.Apply(harmony, organizeOnOpen, compactSmallItems, log.Write);
        OrganizeTicker.Bags = new BagOrganizer(OrganizeHooks.TakeDue, new PackOptions(smallItemMaxArea, compactSmallItems), log.Write);
        OrganizeTicker.Log = log.Write;
        AddComponent<OrganizeTicker>();
    }
}
