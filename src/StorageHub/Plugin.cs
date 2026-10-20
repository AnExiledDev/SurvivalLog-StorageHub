using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using StorageHub.Adapters;
using StorageHub.Core;
using UnityEngine;

namespace StorageHub;

[BepInPlugin(Guid, "Storage Hub", Version)]
[BepInDependency(OptionalLive.ToolkitGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(StackingGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.storagehub";
    private const string Version = "0.9.4";

    /// <summary>Stacking moves one unit of a stack when the Home tab sends an item to the bag; without it the whole stack moves.</summary>
    private const string StackingGuid = "com.dev.survivallog.stacking";

    public override void Load()
    {
        if (!OptionalLive.IsToolkitInstalled) Log.LogInfo(OptionalLive.MissingNote);

        var toggleKey = Config.Bind("Panel", "ToggleKey", KeyCode.F8,
            "The key that opens and closes the Storage Hub panel. Default: F8.\n" +
            "打开和关闭 Storage Hub 面板的按键。默认 F8。");
        var isVerbose = Config.Bind("Diagnostics", "Verbose", false,
            new ConfigDescription("Write extra detail to BepInEx/storage-hub.log: every planned move, what the keep list left in your bag, and how well each storage's tags matched an item. Turn it on to find out why an item went where it did. Default: off.\n" +
            "在 BepInEx/storage-hub.log 日志里写入更多细节：每一步计划的移动、「留在背包」清单留在背包里的物品，以及每个储物家具的标签和物品有多匹配。想弄清某件物品为什么被放到那里时再打开。默认关闭。", null, "ModManager.Advanced"));
        var isKeepOn = Config.Bind("Keep", "Enabled", true,
            "When you put your bag away, leave the amounts on the panel's \"Keep in bag\" list in your bag. This counts for the whole bag and for one item at a time. Off: putting away moves everything, and the list stays saved for when you turn this back on. Default: on.\n" +
            "收纳背包时，按面板上「留在背包」清单里的数量把这些物品留在背包里。整个背包收纳和单件物品收纳都算。关闭后，收纳会把所有物品都放进储物家具，清单仍会保存，重新开启后照常生效。默认开启。");
        var isHomeListOn = Config.Bind("Stations", "HomeStorageList", true,
            "Add a searchable list of your home storages: a \"Home\" tab on the bag panel at the stove, rat cage, brewing barrel, dye vat, shredder, ration press and drone trade, and a \"Home storage\" window in the Mods menu on the inventory screen. At a station, clicking an item the station takes puts it into the station (at the drone trade, into your side of the trade), and anything else goes to your bag. Default: on.\n" +
            "加入一个可搜索的家中储物家具列表：灶台、鼠笼、酿酒桶、染料桶、粉碎机、口粮压制机和无人机交易的背包面板上多一个「家中」标签，背包界面的模组菜单里多一个「家中储物」窗口。在这些地方，点击这里能用的物品会直接放进去（无人机交易时放进你这一方的交易栏），其他物品放进背包。默认开启。");
        var isStorageTabsOn = Config.Bind("Stations", "StorageTabs", true,
            "At the stove, rat cage, brewing barrel, dye vat, shredder, ration press and drone trade, add a bag tab for every home storage holding something the station takes, next to the game's own storage tabs. Default: on.\n" +
            "在灶台、鼠笼、酿酒桶、染料桶、粉碎机、口粮压制机和无人机交易的背包标签旁，为每个放着这里能用的物品的家中储物家具加一个标签，和游戏自带的储物标签并列。默认开启。");
        var log = new HubLog(Log, Path.Combine(Paths.BepInExRootPath, "storage-hub.log"));
        var harmony = new Harmony(Guid);

        var page = new WebPage(log.Write);
        var keep = new KeepListKeeper(new KeepListFile(Path.Combine(Paths.ConfigPath, Guid + ".keep.json")), () => DateTime.Now, log.Write);
        var session = new HubSession(new GameStorages(log.Write), page, new WebKeyboard(), log.Write, keep);

        log.Write($"===== Storage Hub {Version} started");
        StationRules.UseLog(log.Write);
        InputGate.Apply(harmony, log.Write);
        PageMessages.Apply(harmony, log.Write);

        OptionalLive.Watch(() =>
        {
            session.Use(toggleKey.Value, isVerbose.Value, isKeepOn.Value, isHomeListOn.Value);
            log.Write($"toggle key {toggleKey.Value}, verbose {isVerbose.Value}, keep list {isKeepOn.Value}, home storage list {isHomeListOn.Value}");
        }, toggleKey, isVerbose, isKeepOn, isHomeListOn);

        OptionalLive.Watch(isStorageTabsOn, isOn =>
        {
            log.Write($"storage tabs {isOn}");
            StationHooks.Use(harmony, isOn, log.Write);
        });

        StoragePanel.Session = session;
        StoragePanel.Log = log.Write;
        AddComponent<StoragePanel>();
        StartOrganizer(harmony, log);
        StartDeliveryPutAway(harmony, page, log);
    }

    private void StartDeliveryPutAway(Harmony harmony, WebPage page, HubLog log)
    {
        var autoPutAway = Config.Bind("Delivery", "AutoPutAway", true,
            "When a drone comes home, put what it brought into the storages tagged for it, the same way \"Put whole bag away by tag\" does. Anything no storage takes stays where the game put it. It also counts each delivery as retrieving the supplies, so events that ask for a drone trade and a retrieve can finish; with this off, those events stay open when the drone hub unloads the drone for you. Default: on.\n" +
            "无人机回家时，把它带回的物资按标签放进对应的储物家具，和面板上「按标签收纳整个背包」按钮的做法一样。没有储物家具接收的物品留在游戏原本放的地方。同时会把每次送货算作「取回物资」，让要求无人机交易并取回物资的事件可以完成；关闭后，如果无人机中控台替你卸货，这类事件会一直无法完成。默认开启。");

        var skipRobotPutOff = Config.Bind("Delivery", "SkipRacksWithRobotPutOff", false,
            "When on, drone deliveries leave out any material rack whose robot put switch is off, the way the game's robots do. When off, deliveries fill every rack that has a slot for the item. Default: off.\n" +
            "开启后，无人机送货不会放进关闭了机器人放入开关的材料架，和游戏里机器人的做法一样。关闭时，送货会放进任何有对应格子的材料架。默认关闭。");

        var deliveries = new DeliveryPutAway(DeliveryHooks.TakeNext, new GameStorages(log.Write), new DroneRetrieves(log.Write), page, log.Write);

        OptionalLive.Watch(() =>
        {
            log.Write($"auto put-away of drone deliveries {autoPutAway.Value}, skip racks with robot put off {skipRobotPutOff.Value}");
            deliveries.Use(skipRobotPutOff.Value);
            DeliveryHooks.Use(harmony, autoPutAway.Value, log.Write);
        }, autoPutAway, skipRobotPutOff);

        DeliveryTicker.Deliveries = deliveries;
        DeliveryTicker.Log = log.Write;
        AddComponent<DeliveryTicker>();
    }

    private void StartOrganizer(Harmony harmony, HubLog log)
    {
        var organizeOnOpen = Config.Bind("Organize", "OrganizeOnOpen", true,
            "Tidy a storage each time you open it. Your own bag is never tidied when you open a storage. Default: on.\n" +
            "每次打开储物家具时整理里面的物品。打开储物家具时不会整理你自己的背包。默认开启。");
        var compactSmallItems = Config.Bind("Organize", "CompactSmallItems", true,
            "Pack big items toward the bottom-right and small items usually toward the top-left. Several layouts are tried, and the one kept leaves the fewest empty cells that only a small item could fill and the most room for big items. Items of the same kind stay together when that costs no room. The game's Sort button packs this way too. Off: storages are tidied the way the game's Sort does it, and the Sort button works as normal. Default: on.\n" +
            "大件物品靠右下角摆，小件物品一般靠左上角摆。会试几种摆法，选出只能放小件的零散空格最少、留给大件的空间最多的那一种。同类物品在不多占空间时会放在一起。游戏的「一键整理」按钮也会这样摆。关闭后，储物家具按游戏「一键整理」的方式整理，「一键整理」按钮保持原样。默认开启。");
        var smallItemMaxArea = Config.Bind("Organize", "SmallItemMaxArea", 2,
            new ConfigDescription("Items that take up this many cells or fewer count as small when CompactSmallItems packs a storage. 0 means no item counts as small. Default: 2.\n" +
            "占用格子数不超过这个数的物品，在 CompactSmallItems 摆放时算作小件。设为 0 表示没有物品算作小件。默认 2。", null, "ModManager.ShowWhen=CompactSmallItems"));
        var mergeStacks = Config.Bind("Organize", "MergeStacks", true,
            "When a storage is tidied, first top up part-full stacks of the same item from each other. Food only merges when the stacks spoil within an hour of each other. Stacks on moldy cells are left alone. Default: on.\n" +
            "整理储物家具时，先把同一种物品没堆满的几堆互相补满。食物只有在剩余保质期相差不超过 1 小时时才会合并。压在霉格上的物品不会合并。默认开启。");

        var bags = new BagOrganizer(OrganizeHooks.TakeDue, log.Write);

        OptionalLive.Watch(() =>
        {
            var smallMaxArea = Math.Max(0, smallItemMaxArea.Value);

            log.Write($"organize on open {organizeOnOpen.Value}, compact small items {compactSmallItems.Value}, small means up to {smallMaxArea} cells, merge stacks {mergeStacks.Value}");
            bags.Use(new PackOptions(smallMaxArea, compactSmallItems.Value), mergeStacks.Value);
            OrganizeHooks.Use(harmony, organizeOnOpen.Value, compactSmallItems.Value, log.Write);
        }, organizeOnOpen, compactSmallItems, smallItemMaxArea, mergeStacks);

        OrganizeTicker.Bags = bags;
        OrganizeTicker.Log = log.Write;
        AddComponent<OrganizeTicker>();
    }
}
