using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StorageHub.Core;

/// <summary>One piece of on-screen text in the two languages the game ships. {0}, {1} are filled in by Say.</summary>
public readonly record struct Phrase(string En, string Zh)
{
    public string Pick(bool isChinese) => isChinese ? Zh : En;

    public string Say(bool isChinese, params object[] args) => string.Format(CultureInfo.InvariantCulture, Pick(isChinese), args);
}

/// <summary>A phrase about a count: English says "1 stack" and "2 stacks", Chinese has one form for both. {0} is the count.</summary>
public readonly record struct Counted(Phrase One, Phrase Many)
{
    public string Say(bool isChinese, int count) => (count == 1 ? One : Many).Say(isChinese, count);
}

/// <summary>The words the mod puts on screen: results on the status line, place names, time left, and the panel's own labels.</summary>
public static class Texts
{
    private const string Log = "BepInEx/storage-hub.log";

    // Results on the status line.
    public static readonly Phrase Failed = new($"That failed, see {Log}.", $"操作失败，详情见 {Log}。");
    public static readonly Phrase KeepNotRead = new($"Could not read the keep list, see {Log}.", $"无法读取「留在背包」清单，详情见 {Log}。");
    public static readonly Phrase KeepNotSaved = new($"Could not save the keep list, see {Log}.", $"无法保存「留在背包」清单，详情见 {Log}。");
    public static readonly Phrase NothingPutAway = new("Nothing was put away.", "没有收纳任何物品。");
    public static readonly Phrase NotAvailable = new("Not available right now: {0}", "暂时无法使用：{0}");
    public static readonly Phrase NoItemData = new("no item data yet (load a save first)", "还没有物品数据（请先载入存档）");
    public static readonly Phrase NoWorld = new("the game world is not loaded", "游戏世界还没有载入");
    public static readonly Phrase NoBag = new("No bag found.", "找不到背包。");
    public static readonly Phrase OffKeepList = new("{0} is off the keep list.", "{0}已移出「留在背包」清单。");
    public static readonly Phrase KeepingNone = new("Keeping no {0} in your bag for now.", "暂时不在背包里留{0}。");
    public static readonly Phrase Keeping = new("Keeping {0} {1} in your bag.", "背包里会留 {0} 个{1}。");
    public static readonly Phrase NothingOnFloor = new("Nothing on the floor at home.", "家里地面上没有物品。");
    public static readonly Phrase NothingToMove = new("Nothing to move.", "没有需要移动的物品。");
    public static readonly Counted Moved = new(new("Moved 1 stack.", "移动了 1 堆。"), new("Moved {0} stacks.", "移动了 {0} 堆。"));
    public static readonly Phrase Nowhere = new("{0} had nowhere to go: {1}.", "{0} 堆无处可放：{1}。");
    public static readonly Phrase Took = new("Took {0} into your bag.", "已收进背包：{0}。");
    public static readonly Phrase NoneStored = new("None in storage: {0}.", "储物家具里没有：{0}。");
    public static readonly Phrase KeepFilled = new("Your bag already holds everything on the keep list.", "背包里已经有「留在背包」清单上的全部物品。");
    public static readonly Phrase KeptInBag = new("Kept in your bag: {0}.", "留在背包里：{0}。");

    /// <summary>{0} is the start of a list cut short, {1} how many entries were left out.</summary>
    public static readonly Phrase ListMore = new("{0} and {1} more", "{0}，另外还有 {1} 项");

    /// <summary>{0} is a number of units, {1} the item.</summary>
    public static readonly Phrase Units = new("{0} {1}", "{1} ×{0}");

    public static readonly Phrase UnitsTo = new("{0} {1} to {2}", "{1} ×{0} 放进{2}");
    public static readonly Phrase UnitsIn = new("{0} {1} in {2}", "{1} ×{0} 在{2}");

    // Drone deliveries.
    public static readonly Phrase DeliveryNothing = new("Drone delivery: nothing new arrived.", "无人机送货：没有新到的物资。");
    public static readonly Phrase DeliveryNoTaker = new("Drone delivery: no storage takes any of it.", "无人机送货：没有储物家具接收这些物资。");
    public static readonly Phrase DeliveryMoved = new("Drone delivery: {0}.", "无人机送货：{0}。");
    public static readonly Phrase DeliveryUnread = new("Drone delivery: storages could not be read, everything stayed where it landed.", "无人机送货：无法读取储物家具，所有物资都留在原地。");
    public static readonly Phrase StayedIn = new("Stayed in {0}: {1}.", "留在{0}：{1}。");
    public static readonly Phrase Stayed = new("Stayed: {0}.", "留下：{0}。");

    // Places and items without a name of their own.
    public static readonly Phrase Floor = new("Floor", "地面");
    public static readonly Phrase Bag = new("Bag", "背包");
    public static readonly Phrase WorkbenchDrawer = new("Workbench drawer", "工作台抽屉");
    public static readonly Phrase DroneHub = new("Drone hub", "无人机中控台");
    public static readonly Phrase Drone = new("Drone", "无人机");
    public static readonly Phrase UnnamedItem = new("Item {0}", "物品 {0}");
    public static readonly Phrase UnnamedStorage = new("Storage {0}", "储物家具 {0}");

    // Time left before food spoils.
    public static readonly Phrase Spoiled = new("spoiled", "已腐败");
    public static readonly Phrase UnderAnHour = new("under 1h", "不到 1 小时");
    public static readonly Phrase HoursLeft = new("{0}h", "{0} 小时");
    public static readonly Phrase DaysLeft = new("{0}d {1}h", "{0} 天 {1} 小时");

    /// <summary>The panel's labels, by the key page.js asks for them with.</summary>
    public static readonly IReadOnlyList<(string Key, Phrase Phrase)> Page = new (string, Phrase)[]
    {
        ("search", new("Search items...", "搜索物品…")),
        ("searchKeep", new("Search every item to add it to the keep list...", "搜索任意物品，加入「留在背包」清单…")),
        ("rotView", new("Rotting soon", "即将腐败")),
        ("rotTip", new("Food in your bag, your storages and on the floor at home, soonest to spoil first", "背包、储物家具和家里地面上的食物，最快腐败的排在前面")),
        ("keepView", new("Keep in bag", "留在背包")),
        ("keepTip", new("Items putting the bag away always leaves in your bag, and how many of each", "收纳背包时总会留在背包里的物品，以及每种留多少")),
        ("putAwayBag", new("Put whole bag away by tag", "按标签收纳整个背包")),
        ("putAwayFloor", new("Put floor items away by tag", "按标签收纳地面物品")),
        ("closeKeys", new("{0} / Esc to close", "按 {0} / Esc 关闭")),
        ("subItems", new("{0} kinds of item across {1} storages and your bag", "{1} 个储物家具和背包里共有 {0} 种物品")),
        ("subRot", new("Food that spoils, soonest first, across {0} storages, your bag and the floor at home", "{0} 个储物家具、背包和家里地面上会腐败的食物，最快腐败的排在前面")),
        ("subKeep", new("Putting the bag away leaves these in your bag", "收纳背包时，这些物品会留在背包里")),
        ("noMatch", new("Nothing matches that search.", "没有符合搜索的物品。")),
        ("noItems", new("No items found.", "没有找到物品。")),
        ("keep", new("Keep", "留")),
        ("keepN", new("Keep {0}", "留 {0}")),
        ("keepOnTip", new("On the keep list: open it to change the amount", "已在「留在背包」清单里：打开清单可以修改数量")),
        ("keepAddTip", new("Add to the keep list: putting the bag away leaves this many in your bag", "加入「留在背包」清单：收纳背包时会在背包里留下这么多")),
        ("keepOff", new("The keep list is turned off in the config (Keep, Enabled), so putting the bag away moves everything.", "「留在背包」清单已在配置中关闭（Keep, Enabled），所以收纳背包会移走所有物品。")),
        ("keepEmpty", new("Nothing on the keep list yet. Find an item on the right, or press the bell on an item in the Items list.", "「留在背包」清单还是空的。在右边找到物品，或在「物品」列表里点物品的铃铛。")),
        ("keepRemoveTip", new("Take off the keep list", "移出「留在背包」清单")),
        ("heldNone", new("{0} in your bag, none kept", "背包里有 {0} 个，不保留")),
        ("held", new("{0} in your bag", "背包里有 {0} 个")),
        ("heldShort", new("{0} in your bag, {1} short", "背包里有 {0} 个，还差 {1} 个")),
        ("keepLess", new("Keep one fewer", "少留一个")),
        ("keepMore", new("Keep one more", "多留一个")),
        ("keepAmountTip", new("How many to keep in your bag", "在背包里留多少")),
        ("keepAddTitle", new("Add to the keep list", "加入「留在背包」清单")),
        ("noName", new("No item has that name.", "没有叫这个名字的物品。")),
        ("bagEmpty", new("Your bag is empty. Type a name above to find any item.", "背包是空的。在上方输入名称可以查找任意物品。")),
        ("moreResults", new("{0} more. Type more of the name to narrow it down.", "还有 {0} 个。多输入一些名称可以缩小范围。")),
        ("hintBag", new("In your bag now. Type a name above to search every item in the game.", "当前背包里的物品。在上方输入名称可以搜索游戏里的所有物品。")),
        ("hintAll", new("Every item in the game", "游戏里的所有物品")),
        ("hintLoading", new("Items you own (the full item list is still loading)", "你拥有的物品（完整物品列表还在载入）")),
        ("inBag", new("{0} in bag", "背包 {0}")),
        ("keeping", new("Keeping {0}", "留 {0}")),
        ("addKeep", new("+ Keep", "+ 留")),
        ("addKeepTip", new("Add to the keep list, keeping {0}", "加入「留在背包」清单，留 {0} 个")),
        ("nothingSpoils", new("Nothing here spoils.", "这里没有会腐败的东西。")),
        ("floorOnlyTip", new("Only on the floor: \"Put floor away\" picks it up", "只在地面上：「收纳地面物品」会把它收起来")),
        ("pickItem", new("Pick an item on the left.", "在左边选一个物品。")),
        ("itemsView", new("Items", "物品")),
        ("goBag", new("Put bag away", "收纳背包")),
        ("goFloor", new("Put floor away", "收纳地面物品")),
        ("allPlaces", new("All places", "所有地方")),
        ("placeTip", new("Show only the items in one place", "只显示某个地方的物品")),
        ("totalPlaces", new("{0} in total, in {1} places", "共 {0} 个，分布在 {1} 处")),
        ("totalPlace", new("{0} in total, in one place", "共 {0} 个，都在一处")),
        ("keepingBag", new("keeping {0} in your bag", "背包里留 {0} 个")),
        ("whereIs", new("Where it is", "存放位置")),
        ("rotSpoiled", new("Spoiled", "已腐败")),
        ("rotDay", new("Under a day", "不到一天")),
        ("rotWeek", new("This week", "一周内")),
        ("rotLater", new("Later", "更久以后")),
        ("takeAll", new("Take all into bag", "全部收进背包")),
        ("putAwayTag", new("Put away by tag", "按标签收纳")),
        ("putAway", new("Put away", "收纳")),
        ("take", new("Take", "取出")),
        ("kindBag", new("Bag", "背包")),
        ("kindStorage", new("Storage", "储物家具")),
        ("kindDrawer", new("Drawer", "抽屉")),
        ("kindBasket", new("Basket", "篮子")),
        ("menuPutAwayTip", new("Move everything in your bag into the storages whose tags want it", "把背包里的所有物品放进标签对应的储物家具")),
        ("menuFloor", new("Put away floor", "收纳地面物品")),
        ("menuFloorTip", new("Move everything lying on the floor at home into the storages whose tags want it, and the rest into your bag", "把家里地面上的所有物品放进标签对应的储物家具，其余的放进背包")),
        ("menuHomeTip", new("Search the storages at home and take items from them", "搜索家中的储物家具并从中取出物品")),
        ("homeBag", new("Bag", "背包")),
        ("homeTab", new("Home", "家中")),
        ("homeTitle", new("Home storage", "家中储物")),
        ("homeSub", new("{0} storages", "{0} 个储物家具")),
        ("homeSearch", new("Search every storage...", "搜索所有储物家具…")),
        ("scopeCooking", new("For cooking", "烹饪可用")),
        ("scopeRatCage", new("For the rat cage", "鼠笼可用")),
        ("scopeBrew", new("For brewing", "酿造可用")),
        ("scopeDye", new("For dyeing", "染色可用")),
        ("scopeShredder", new("For the shredder", "粉碎机可用")),
        ("scopeCompressor", new("For the ration press", "口粮压制机可用")),
        ("scopeTrade", new("For trading", "可交易")),
        ("scopeWorkbench", new("For the workbench", "工作台可用")),
        ("scopeBasket", new("For the basket", "可放进篮子")),
        ("scopeAll", new("Everything", "全部")),
        ("homeOpen", new("Open", "打开")),
        ("homeClose", new("Close", "关闭")),
        ("homeUsable", new("{0} usable", "可用 {0}")),
        ("homeItems", new("{0} items", "{0} 种物品")),
        ("tagUse", new("Use", "使用")),
        ("tagBag", new("To bag", "放进背包")),
        ("tagWish", new("Wanted", "她想要")),
        ("hintStation", new("Use goes into the station. To bag goes into your bag.", "「使用」直接放进这里，「放进背包」放进背包。")),
        ("hintBasket", new("Use and Wanted go into the basket. Wanted is what she asks for now. To bag goes into your bag.", "「使用」和「她想要」直接放进篮子，「她想要」是她现在想要的。「放进背包」放进背包。")),
        ("hintInv", new("Click an item to take it into your bag, or Open a storage beside your bag.", "点击物品收进背包，或点「打开」把储物家具显示在背包旁边。")),
        ("homeNoMatch", new("Nothing in your storages matches that search.", "储物家具里没有符合搜索的物品。")),
        ("homeNone", new("No storage holds anything for this station.", "没有储物家具放着这里能用的物品。")),
        ("homeEmpty", new("Your home storages are empty.", "家中储物家具都是空的。")),
        ("homeLoading", new("Reading your storages...", "正在读取储物家具…")),
        ("hudTip", new("Search every storage at home, take items out, and keep what you need in your bag", "搜索家中所有储物家具，取出物品，并把需要的东西留在背包里")),
        ("itemsTip", new("Everything in your bag and your home storages", "背包和家中储物家具里的所有物品")),
        ("closeTip", new("Close the panel", "关闭面板")),
        ("takeAllTip", new("Take every one from every storage into your bag", "把所有储物家具里的这种物品全部收进背包")),
        ("putAwayTagTip", new("Put the ones in your bag into the storages whose tags want them", "把背包里的这种物品放进标签对应的储物家具")),
        ("takeN", new("Take {0}", "取出 {0}")),
        ("takeSomeTip", new("Take {0} from {1} into your bag", "从{1}取出 {0} 个收进背包")),
        ("takeHere", new("Take all", "全部取出")),
        ("takeHereTip", new("Take every one from {0} into your bag", "把{0}里的全部收进背包")),
        ("amountTip", new("Drag to pick how many to take", "拖动选择取出多少")),
        ("noSplit", new("Taking part of a stack needs the Stacking mod, so each place is taken whole.", "取出一堆中的一部分需要 Stacking 模组，所以每处只能全部取出。")),
        ("fill", new("Fill bag from storage", "从储物家具补满背包")),
        ("fillTip", new("Take what your bag is short of from your home storages (never from a drone)", "从家中储物家具取出背包还缺的物品（不会从无人机取）")),
        ("fillShort", new("{0} short, {1} can be pulled from storage", "缺 {0} 种，可从储物家具补 {1} 种")),
        ("fillDone", new("Your bag holds everything on the list.", "背包里已有清单上的全部物品。")),
        ("stored", new("{0} in storage", "储物 {0}")),
        ("storedTip", new("How many your home storages hold, which Fill can take", "家中储物家具里有多少，补满时可以取出")),
        ("storedNone", new("None in storage", "储物家具里没有")),
        ("storedNoneTip", new("No home storage holds any, so Fill cannot bring it", "家中储物家具里一个都没有，无法补满")),
        ("homeOpenTip", new("Show this storage beside your bag", "把这个储物家具显示在背包旁边")),
        ("homeTabTip", new("Show the tab of this storage", "显示这个储物家具的标签页")),
        ("homeCloseTip", new("Close this storage again", "再次关闭这个储物家具")),
        ("homeShutTip", new("Close the home storage list", "关闭家中储物列表")),
        ("scopeUsableTip", new("Show only what this station can use", "只显示这里能用的物品")),
        ("tabsBackTip", new("Scroll the tabs back", "向前滚动标签页")),
        ("tabsOnTip", new("Scroll to more tabs", "滚动查看更多标签页")),
        ("scopeAllTip", new("Show everything in your storages", "显示储物家具里的所有物品")),
        ("homeItemTip", new("{0} in {1}", "{1}里有 {0} 个")),
    };

    public static IReadOnlyList<Phrase> All { get; } = Collect();

    /// <summary>Sentences in a row: English puts a space between them, Chinese runs them together after the full stop.</summary>
    public static string Sentences(bool isChinese, params string[] sentences) => string.Join(isChinese ? "" : " ", sentences.Where(sentence => sentence.Length > 0));

    /// <summary>A list of names or amounts, at most shown entries long, with how many more there were.</summary>
    public static string List(bool isChinese, IReadOnlyList<string> entries, int shown)
    {
        var listed = string.Join(isChinese ? "、" : ", ", entries.Take(shown));

        return entries.Count > shown ? ListMore.Say(isChinese, listed, entries.Count - shown) : listed;
    }

    private static List<Phrase> Collect()
    {
        var all = new List<Phrase>
        {
            Failed, KeepNotRead, KeepNotSaved, NothingPutAway, NotAvailable, NoItemData, NoWorld, NoBag, OffKeepList, KeepingNone,
            Keeping, NothingOnFloor, NothingToMove, Moved.One, Moved.Many, Nowhere, KeptInBag, ListMore, Units, UnitsTo, UnitsIn,
            DeliveryNothing, DeliveryNoTaker, DeliveryMoved, DeliveryUnread, StayedIn, Stayed,
            Floor, Bag, WorkbenchDrawer, DroneHub, Drone, UnnamedItem, UnnamedStorage,
            Spoiled, UnderAnHour, HoursLeft, DaysLeft,
        };

        foreach (var (_, phrase) in Page) all.Add(phrase);

        return all;
    }
}
