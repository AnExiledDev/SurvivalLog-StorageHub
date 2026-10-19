using System;
using System.Collections.Generic;
using System.Linq;
using StorageHub.Core;
using UnityEngine;

namespace StorageHub.Adapters;

/// <summary>
/// The panel's game side: its open state, the item view it sends to page.js, and the moves the page asks for. Page
/// messages arrive on the web view's thread, so they are queued and handled here, one frame at a time.
/// </summary>
internal sealed class HubSession
{
    private const float RefreshSeconds = 1f;

    private readonly GameStorages _game;
    private readonly WebPage _page;
    private readonly WebKeyboard _keyboard;
    private readonly Action<string> _log;
    private readonly KeepListKeeper _keep;
    private readonly Dictionary<int, string?> _iconPaths = new();
    private readonly StatusLine _status = new();

    private IconIndex? _icons;
    private BesideBagStep _besideBag = new BesideBagStep.Idle();
    private Inventory _inventory = Inventory.Empty;
    private string _shownView = "";
    private float _nextRefreshAt;
    private bool _isOpen;
    private bool _hasLoggedIcons;
    private KeyCode _toggleKey = KeyCode.F8;
    private bool _isVerbose;
    private bool _isKeepOn;
    private bool _isHomeListOn;

    public HubSession(GameStorages game, WebPage page, WebKeyboard keyboard, Action<string> log, KeepListKeeper keep)
    {
        _game = game;
        _page = page;
        _keyboard = keyboard;
        _log = log;
        _keep = keep;
    }

    /// <summary>
    /// Called on the main thread at start and after each change. The page names the new key from its next call, an open panel
    /// shows the keep list's state at its next refresh, and a station asks for its home list after every click.
    /// </summary>
    public void Use(KeyCode toggleKey, bool isVerbose, bool isKeepOn, bool isHomeListOn)
    {
        _toggleKey = toggleKey;
        _isVerbose = isVerbose;
        _isKeepOn = isKeepOn;
        _isHomeListOn = isHomeListOn;
        _page.UseToggleKey(toggleKey.ToString());
    }

    public void Update()
    {
        var now = Time.unscaledTime;
        _page.KeepInstalled(now);

        foreach (var command in CommandBatch.WithoutRepeatedMoves(TakeQueuedCommands())) HandleSafely(command);

        AdvanceBesideBag();

        if (Input.GetKeyDown(_toggleKey))
        {
            if (_isOpen) Close();
            else Open();

            return;
        }

        // The menu is the inventory's top layer, so Esc closes it before the panel or the inventory.
        if (InputGate.IsMenuOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            _page.CloseMenu();
            InputGate.MenuClosed();

            return;
        }

        if (!_isOpen) return;

        // While a page field holds the keyboard, Esc is the page's: it puts a typed amount back before it closes.
        if (!_keyboard.IsClaimed && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();

            return;
        }

        if (now >= _nextRefreshAt) PushView();
    }

    private static List<PageCommand> TakeQueuedCommands()
    {
        var commands = new List<PageCommand>();

        while (PageMessages.TryTake(out var command)) commands.Add(command);

        return commands;
    }

    /// <summary>One command that throws must not cost the rest of the frame its commands or the F8 and Esc checks.</summary>
    private void HandleSafely(PageCommand command)
    {
        try
        {
            Handle(command);
        }
        catch (Exception ex)
        {
            _log($"page command {command} failed: {ex}");
        }
    }

    /// <summary>The view is read before anything is captured, so a failed read leaves the game's input alone.</summary>
    private void Open()
    {
        _keep.TryRead();

        if (!TryReadView("open", out var view, out var notReady))
        {
            GiveUpOnFailedRead();

            return;
        }

        _isOpen = true;
        InputGate.PanelOpened();
        _shownView = view;
        _page.Open(view);
        ShowFreshPanelStatus(notReady);
        _log($"panel opened: {_inventory.Storages.Count} storages, {_inventory.Stacks.Count} stacks");
    }

    private void Close()
    {
        if (!_isOpen) return;

        _isOpen = false;
        _keyboard.Release();
        InputGate.PanelClosed();
        _page.Close();
        _game.Forget();
    }

    /// <summary>
    /// Sends the view only when it changed, so an idle panel costs one inventory read a second and nothing more. A read
    /// that throws shuts the panel, so the game gets its input back instead of a panel that can no longer update.
    /// </summary>
    private void PushView()
    {
        if (!TryReadView("refresh", out var view, out var notReady))
        {
            GiveUpOnFailedRead();

            return;
        }

        ShowReadiness(notReady);

        if (view == _shownView) return;

        _shownView = view;
        _page.Show(view);
    }

    /// <summary>A panel that cannot read the inventory is shut rather than left holding the game's input.</summary>
    private void GiveUpOnFailedRead()
    {
        Close();
        _page.Status(_status.Result(Texts.Failed.Pick(IsChinese)));
    }

    private bool TryReadView(string reason, out string view, out string? notReady)
    {
        try
        {
            view = ReadView(out notReady);

            return true;
        }
        catch (Exception ex)
        {
            _log($"reading the inventory for {reason} failed: {ex}");
            view = "";
            notReady = null;

            return false;
        }
    }

    /// <summary>The page empties its status line whenever it draws the panel.</summary>
    private void ShowFreshPanelStatus(string? notReady)
    {
        _status.Cleared();
        ShowReadiness(notReady);
    }

    private void ShowReadiness(string? notReady)
    {
        if (_status.Refreshed(notReady) is { } status) _page.Status(status);
    }

    /// <summary>The view JSON, and why the inventory could not be read when it could not (the view is then empty).</summary>
    private string ReadView(out string? notReady)
    {
        _nextRefreshAt = Time.unscaledTime + RefreshSeconds;
        notReady = RefreshInventory(out var reason) ? null : NotAvailable(reason);

        var rot = notReady == null ? ReadRottingSoon() : Array.Empty<RotRow>();
        AddNames(_keep.List.Amounts.Keys);
        var view = HubView.Build(_inventory, IconPath, rot, new KeepView(_isKeepOn, BagKeep.Rows(_inventory, _keep.List)));
        LogIconsOnce();

        return view;
    }

    /// <summary>The "Rotting soon" list, floor included; floor kinds no storage holds get their names added first.</summary>
    private IReadOnlyList<RotRow> ReadRottingSoon()
    {
        var floor = _game.ReadFloor();
        AddNames(floor.Select(stack => stack.ConfigId));

        return RottingSoon.Rows(_inventory, floor, _game.DaysLeft);
    }

    /// <summary>Names for items the storage read did not see, such as floor items and kept items the player does not hold.</summary>
    private void AddNames(IEnumerable<int> configIds)
    {
        var names = _inventory.ItemNames.ToDictionary(entry => entry.Key, entry => entry.Value);

        foreach (var configId in configIds) names.TryAdd(configId, _game.NameOf(configId));

        _inventory = _inventory with { ItemNames = names };
    }

    private bool RefreshInventory(out string reason)
    {
        switch (_game.Read())
        {
            case InventoryRead.Ready ready:
                _inventory = ready.Inventory;
                reason = "";

                return true;
            case InventoryRead.NotReady notReady:
                _inventory = Inventory.Empty;
                reason = notReady.Reason;

                return false;
            default:
                throw new InvalidOperationException("unknown inventory read");
        }
    }

    private void Handle(PageCommand command)
    {
        switch (command)
        {
            case PageCommand.Take take:
                Act("take", () => Take(stack => stack.ConfigId == take.ConfigId && stack.OwnerId == take.OwnerId));
                break;
            case PageCommand.TakeOne takeOne:
                Act("take one", () => TakeOne(takeOne.ConfigId, takeOne.OwnerId));
                break;
            case PageCommand.PutOne putOne:
                Act("put one", () => PutOne(putOne.ConfigId, putOne.OwnerId, putOne.TargetOwnerId));
                break;
            case PageCommand.TakeAll takeAll:
                Act("take all", () => Take(stack => stack.ConfigId == takeAll.ConfigId));
                break;
            case PageCommand.PutAway putAway:
                Act("put away", () => PutAway(stack => stack.ConfigId == putAway.ConfigId));
                break;
            case PageCommand.PutAwayBag:
                Act("put away whole bag", () => PutAway(_ => true));
                break;
            case PageCommand.PutAwayFloor:
                Act("put away floor items", PutAwayFloor);
                break;
            case PageCommand.SetKeep setKeep:
                Act($"keep {setKeep.Amount} of item {setKeep.ConfigId}", () => ChangeKeep(list => list.With(setKeep.ConfigId, setKeep.Amount), setKeep.ConfigId));
                break;
            case PageCommand.RemoveKeep removeKeep:
                Act($"stop keeping item {removeKeep.ConfigId}", () => ChangeKeep(list => list.Without(removeKeep.ConfigId), removeKeep.ConfigId));
                break;
            case PageCommand.SendCatalog:
                SendCatalog();
                break;
            case PageCommand.StationList stationList:
                SendStation(stationList.Kind);
                break;
            case PageCommand.OpenStorage openStorage:
                OpenBesideBag(openStorage.OwnerId);
                break;
            case PageCommand.Close:
                Close();
                break;
            case PageCommand.Focus { IsFocused: true }:
                _keyboard.Claim();
                break;
            case PageCommand.Focus:
                _keyboard.Release();
                break;
            case PageCommand.Menu { IsOpen: true }:
                InputGate.MenuOpened();
                break;
            case PageCommand.Menu:
                InputGate.MenuClosed();
                break;
            case PageCommand.Ready:
                _log("page script ready");
                // A reloaded page has no menu, and one left marked open would hold the game's hotkeys for good.
                InputGate.MenuClosed();
                // Nor does it have a focused field, and a claim left behind would keep Esc from closing the panel.
                _keyboard.Release();
                ReopenAfterReload();
                break;
            case PageCommand.NoScript:
                InputGate.MenuClosed();
                _page.Install();
                break;
            case PageCommand.Unknown unknown:
                _log("page said: " + unknown.Text);
                break;
            default:
                throw new InvalidOperationException("unhandled page command " + command);
        }
    }

    /// <summary>A reloaded web view starts with the panel shut, so a panel the game thinks is open is drawn again.</summary>
    private void ReopenAfterReload()
    {
        if (!_isOpen) return;

        if (!TryReadView("reopen", out var view, out var notReady))
        {
            GiveUpOnFailedRead();

            return;
        }

        _shownView = view;
        _page.Open(view);
        ShowFreshPanelStatus(notReady);
    }

    private void Act(string name, Func<string> action)
    {
        _log(name);
        string status;

        try
        {
            status = action();
        }
        catch (Exception ex)
        {
            status = Texts.Failed.Pick(IsChinese);
            _log(name + " failed: " + ex);
        }
        finally
        {
            _game.Forget();
        }

        _log("  " + status);
        _page.Status(_status.Result(status));

        if (_isOpen) PushView();
    }

    /// <summary>The inventory is read again first: the bag button works with the panel shut, when nothing here is fresh.</summary>
    private string Take(Func<ItemStack, bool> wanted)
    {
        if (!RefreshInventory(out var reason)) return NotAvailable(reason);

        return Execute(StoragePlan.Take(_inventory, _inventory.Stacks.Where(wanted)), MoveAndLog);
    }

    private string TakeOne(int configId, long ownerId)
    {
        if (!RefreshInventory(out var reason)) return NotAvailable(reason);

        return Execute(StoragePlan.TakeOne(_inventory, configId, ownerId), MoveOneAndLog);
    }

    private string PutOne(int configId, long ownerId, long targetOwnerId)
    {
        if (!RefreshInventory(out var reason)) return NotAvailable(reason);

        return Execute(StoragePlan.PutOne(_inventory, configId, ownerId, targetOwnerId), MoveOneAndLog);
    }

    /// <summary>
    /// The keep list holds back its stacks first; what it kept is named after the result of the moves. A keep list that
    /// is there but unread puts nothing away, or the stacks it keeps would go to storage.
    /// </summary>
    private string PutAway(Func<ItemStack, bool> wanted)
    {
        if (_isKeepOn && !_keep.TryRead()) return Texts.Sentences(IsChinese, Texts.KeepNotRead.Pick(IsChinese), Texts.NothingPutAway.Pick(IsChinese));

        if (!RefreshInventory(out var reason)) return NotAvailable(reason);

        var bag = _inventory.Bag;

        if (bag == null) return Texts.NoBag.Pick(IsChinese);

        var split = BagKeep.Split(_inventory.Stacks.Where(stack => stack.OwnerId == bag.OwnerId && wanted(stack)), _isKeepOn ? _keep.List : KeepList.Empty, _game.DaysLeft);
        var kept = BagKeep.Describe(split, _inventory);

        if (_isVerbose) LogKept(split.Kept);

        var moved = Execute(StoragePlan.PutAway(_inventory, split.PutAway, LoggedFacts), MoveAndLog);

        return Texts.Sentences(IsChinese, moved, kept);
    }

    private void LogKept(IEnumerable<ItemStack> kept)
    {
        foreach (var stack in kept) _log($"  keep {Describe(stack)}");
    }

    private string ChangeKeep(Func<KeepList, KeepList> edit, int configId)
    {
        switch (_keep.Change(edit))
        {
            case KeepEdit.Saved:
                return KeptStatus(configId);
            case KeepEdit.NotRead:
                return Texts.KeepNotRead.Pick(IsChinese);
            case KeepEdit.NotSaved:
                return Texts.KeepNotSaved.Pick(IsChinese);
            default:
                throw new InvalidOperationException("unknown keep edit");
        }
    }

    private string KeptStatus(int configId)
    {
        var name = _game.NameOf(configId);

        if (!_keep.List.Has(configId)) return Texts.OffKeepList.Say(IsChinese, name);

        var amount = _keep.List.AmountOf(configId);

        return amount == 0 ? Texts.KeepingNone.Say(IsChinese, name) : Texts.Keeping.Say(IsChinese, amount, name);
    }

    private static bool IsChinese => GameLanguage.IsChinese();

    private static string NotAvailable(string reason) => Texts.NotAvailable.Say(IsChinese, reason);

    /// <summary>A failed catalog is answered with an empty one, which the page takes as "ask again later".</summary>
    private void SendCatalog()
    {
        try
        {
            var catalog = _game.Catalog();
            _page.Catalog(HubView.Catalog(catalog, IconPath));
            _log($"keep list catalog sent: {catalog.Count} items");
        }
        catch (Exception ex)
        {
            _log($"reading the item catalog failed, the page will ask again: {ex}");
            _page.Catalog("[]");
        }
    }

    /// <summary>
    /// The home storage list beside a station, read fresh since the page asks after every click. An inventory that
    /// cannot be read yet sends nothing, and the page asks again.
    /// </summary>
    private void SendStation(StationKind kind)
    {
        if (!_isHomeListOn)
        {
            _page.Station("null");

            return;
        }

        try
        {
            if (!RefreshInventory(out _)) return;

            _page.Station(StationView.Build(_inventory, kind, _game.Usable(kind), IconPath));
        }
        finally
        {
            _game.Forget();
        }
    }

    private void OpenBesideBag(long ownerId)
    {
        _log(ownerId == -1 ? "close the storage beside the bag" : $"open {_inventory.StorageName(ownerId)} beside the bag");
        Run(BesideBag.Start(ownerId, _game.IsBackpackShowing()));
    }

    /// <summary>Run once a frame after the frame's commands, so a close sent on one frame is seen on the next.</summary>
    private void AdvanceBesideBag()
    {
        if (_besideBag is BesideBagStep.Idle) return;

        try
        {
            Run(BesideBag.Advance(_besideBag, _game.IsBackpackShowing()));
        }
        catch (Exception ex)
        {
            _besideBag = new BesideBagStep.Idle();
            _log($"showing a storage beside the bag failed: {ex}");
        }
    }

    private void Run(BesideBagTick tick)
    {
        _besideBag = tick.Next;

        switch (tick.Command)
        {
            case BesideBagCommand.CloseBackpack:
                _game.CloseBackpack();
                break;
            case BesideBagCommand.AskForBags:
                if (!_game.AskForBags(tick.OwnerId)) _page.Status(_status.Result(NotAvailable(Texts.NoWorld.Pick(IsChinese))));
                break;
            case BesideBagCommand.ReportStuck:
                _log($"the bag screen did not close within {BesideBag.CloseFrameLimit} frames, so the storage was not shown");
                break;
            case BesideBagCommand.None:
                break;
            default:
                throw new InvalidOperationException("unhandled beside-bag command " + tick.Command);
        }
    }

    /// <summary>Floor items are not in the storage read, so their names are added before anything is logged.</summary>
    private string PutAwayFloor()
    {
        if (!RefreshInventory(out var reason)) return NotAvailable(reason);

        var floor = _game.ReadFloor();

        if (floor.Count == 0) return Texts.NothingOnFloor.Pick(IsChinese);

        AddNames(floor.Select(stack => stack.ConfigId));

        return Execute(StoragePlan.PutAwayFloor(_inventory, floor, LoggedFacts), PickUpAndLog);
    }

    private string Execute(IReadOnlyList<MoveStep> steps, Func<ItemStack, long, bool> tryMove)
    {
        if (_isVerbose) LogPlan(steps);

        return MoveRunner.Describe(MoveRunner.Run(steps, tryMove), _inventory);
    }

    private void LogPlan(IReadOnlyList<MoveStep> steps)
    {
        foreach (var step in steps) _log($"  plan {Describe(step.Stack)} -> {(step.Targets.Count == 0 ? "no storage wants it" : string.Join(", ", step.Targets.Select(_inventory.StorageName)))}");
    }

    private bool MoveAndLog(ItemStack stack, long targetOwnerId)
    {
        var isMoved = _game.Move(stack, targetOwnerId);
        _log($"  move {Describe(stack)} -> {_inventory.StorageName(targetOwnerId)}: {(isMoved ? "ok" : "refused")}");

        return isMoved;
    }

    private bool MoveOneAndLog(ItemStack stack, long targetOwnerId)
    {
        var moved = _game.MoveOne(stack, targetOwnerId);
        var how = moved switch
        {
            UnitMove.One => "ok",
            UnitMove.WholeStack => "ok, whole stack (Stacking is not installed or could not split it)",
            _ => "refused",
        };

        _log($"  move one {Describe(stack)} -> {_inventory.StorageName(targetOwnerId)}: {how}");

        return moved != UnitMove.Refused;
    }

    private bool PickUpAndLog(ItemStack stack, long targetOwnerId)
    {
        var isPickedUp = _game.PickUp(stack, targetOwnerId);
        _log($"  pick up {Describe(stack)} -> {_inventory.StorageName(targetOwnerId)}: {(isPickedUp ? "ok" : "refused or partial")}");

        return isPickedUp;
    }

    private PutFacts LoggedFacts => new(Logged(_game.Rank), Logged(_game.RackRank));

    private Func<ItemStack, Storage, PutRank?> Logged(Func<ItemStack, Storage, PutRank?> rankOf) => (stack, storage) =>
    {
        var rank = rankOf(stack, storage);

        if (_isVerbose && rank.HasValue) _log($"  rank {Describe(stack)} in {storage.Name}: level {rank.Value.Level}, specificity {rank.Value.Specificity}");

        return rank;
    };

    private string Describe(ItemStack stack) => $"{_inventory.ItemName(stack.ConfigId)} x{stack.Count} (item {stack.ItemId}) from {_inventory.StorageName(stack.OwnerId)}";

    private string? IconPath(int configId)
    {
        if (_iconPaths.TryGetValue(configId, out var cached)) return cached;

        _icons ??= WebIcons.Load(_log);

        return _iconPaths[configId] = _icons.Resolve(_game.IconName(configId));
    }

    /// <summary>The icon name format is a guess from the file names, so the first view logs how well it matched.</summary>
    private void LogIconsOnce()
    {
        if (_hasLoggedIcons || _iconPaths.Count == 0) return;

        _hasLoggedIcons = true;
        var missing = _iconPaths.Where(entry => entry.Value == null).Select(entry => entry.Key).ToList();
        var sample = _iconPaths.Keys.Take(3).Select(configId => $"{configId}: \"{_game.IconName(configId)}\" -> {_iconPaths[configId] ?? "none"}");

        _log($"icons: {_iconPaths.Count - missing.Count} of {_iconPaths.Count} items matched a web icon; samples {string.Join("; ", sample)}");

        if (missing.Count > 0) _log($"  first unmatched: {string.Join("; ", missing.Take(3).Select(configId => $"{configId}: \"{_game.IconName(configId)}\""))}");
    }
}
