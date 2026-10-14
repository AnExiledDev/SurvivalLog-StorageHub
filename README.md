# Storage Hub

One searchable list of everything in your bag and your storages at home, buttons that put things away where their tags say they belong, a keep list for what you always carry, tidier storages, and drone deliveries that put themselves away.

Survival Log makes you open storage after storage to find something, and again to put it back. Storage Hub adds a panel, opened with `F8`, that lists every item in your bag, in every storage at home and in the workbench drawer, with a search box. From there you can take items into your bag, or put your bag away with each item going to the storage whose tags fit it. It can leave the items you always want on you in your bag, tidy a storage each time you open it, and put a drone's cargo away when it lands. Every move goes through the game's own quick-move and pickup, so freshness and durability are kept.

## Features

- **The panel**: `F8` opens a list of every kind of item in your bag, your storages at home and the workbench drawer, with icons, totals and a search box. Pick an item to see where it sits and how many are in each place, and take it or put it away from there.
- **Put away by tag**: "Put whole bag away by tag" sends everything in your bag to the storages whose tags want it. "Put floor items away by tag" does the same for what is lying on the floor at home, and puts anything no storage takes into your bag.
- **Mods menu**: both put-away actions are also in the Mods menu on the game's inventory screen, so you don't need the panel for them.
- **Keep in bag**: a list of items you always want on you, and how many of each. Putting your bag away leaves that many in your bag and stores the rest.
- **Rotting soon**: switches the list to food that spoils, soonest first, with where it sits and how long it has left.
- **Drone deliveries**: when a drone comes home, what it brought is put away into the storages tagged for it.
- **Tidy storages**: opening a storage tidies it. Big items go toward the bottom-right and small ones usually toward the top-left, in whichever of several layouts leaves the fewest empty cells that only a small item could fill and the most room for big items. Items of the same kind stay together when that costs no room. Partial stacks of the same item are topped up from each other first. The game's Sort button packs the same way.
- **A log file**: what the mod did and anything that went wrong is written to `BepInEx\storage-hub.log`.

The keep list, drone deliveries, tidying and stack merging each have their own setting, so you can turn off the ones you don't want (see [CONFIG.md](CONFIG.md)).

Storage Hub keeps nothing of its own in your save and does not edit game files. The items it moves stay where it put them, so removing the mod undoes nothing.

**Back up your saves before trying a new version of this mod.** It moves real items around in your save, and a new version can have new bugs.

## The panel

`F8` opens and closes it (the key can be changed in the settings). `Esc` and the `x` in the top-right corner close it too. While the panel is open, the game's hotkeys are paused, so typing in the search box does not also open a game menu.

- The search box filters the list by name. With several words, an item has to match all of them.
- Click an item on the left to see it on the right: its total, then one row per place that holds it (Bag, Storage or Drawer) with how many are there.
- "Take" on a storage's row moves that storage's stacks of the item into your bag. "Take all into bag" takes it from every place that holds it.
- "Put away" on the Bag row, or "Put away by tag" at the top, puts your bag's stacks of that one item away.
- "Put whole bag away by tag" and "Put floor items away by tag" sit next to the search box.
- The result shows on the status line at the bottom, for example `Moved 4 stacks.` or `Moved 3 stacks. 1 had nowhere to go: Rope.` When the panel is closed, results show as a short message near the bottom of the screen instead.
- Storages with the same name are numbered, like `Wooden Box` and `Wooden Box 2`, so you can tell them apart.
- The list refreshes every second while the panel is open.

Before a save is loaded, the panel opens empty and says `Not available right now`.

### Rotting soon

The "Rotting soon" button next to the search box switches the list to food that spoils: everything in your bag, your storages and on the floor at home, soonest first, with where it sits and the time left (`spoiled`, `under 1h`, `5h`, `2d 3h`). Spoiled food and food with under an hour left are shown in red. Items that never spoil, and food the game has stopped spoiling, are left out. The time left is the game's own figure, the one its inventory tooltip shows.

The search box filters this list too, and clicking a row shows that item on the right. Food that is only on the floor has no detail to show, so its rows don't click. Press "Rotting soon" again to go back to the full item list.

### Mods menu

On the game's inventory screen, a "Mods" button sits next to the game's Sort button on your bag's item tab. It holds "Put away by tag" (your whole bag) and "Put away floor". Other mods that use the same menu, such as Unpack All, list their actions there too. `Esc` or a click elsewhere closes it. The menu does not show during the prologue.

## Putting things away

Each stack in your bag is offered to your home storages in this order:

1. Storages whose tag rule wants the item. The closest-fitting rule goes first, and between equal fits, a storage that already holds that item goes first. Tag rules that care about freshness (a fridge, say) see each stack's real time left.

   A material rack has no tags, so it counts as tagged Materials and is ranked the way the game's own robots rank it: one step closer than a storage tagged Materials and nothing else. It only takes the materials it has a slot for.
2. Storages that already hold some of that item, even with no tag for it.
3. Nothing else. A stack no storage wants stays in your bag, and the status line names it.

If a storage is full, the next one in line gets a turn. A whole stack moves at once; the game cannot split one.

No put-away ever fills a drone, since a drone's bag can hold goods it is about to trade away, and nothing is ever put into the workbench drawer. A drone hub takes put-aways like any other storage.

Floor pickup uses the same order, then falls back to your bag. Items the game's own drone collects after a wave are left for the drone.

## Keep in bag

The keep list names items you always want on you, and how many of each. Putting your bag away leaves that many in your bag and stores the rest. That covers "Put whole bag away by tag", "Put away by tag" and "Put away" on one item in the panel, and "Put away by tag" in the Mods menu. Floor pickup and drone deliveries ignore the list.

- "Keep in bag" (next to "Rotting soon") opens the list on the left. Each row shows the item's icon and name, how many to keep, and how many your bag holds now. When your bag holds fewer than that, the count turns red and says how many you are short.
- Use `-` and `+` to change the amount, or type a number and press Enter. `Esc` while typing puts the old number back and closes the panel. Amounts go from 0 to 9999.
- An amount of 0 keeps the item on the list but keeps none of it, so you can turn one item off without losing its place.
- The `x` at the end of a row takes the item off the list.
- The right side adds items. With the search box empty it shows what is in your bag; type a name to search every item in the game (while that list is still loading, it searches the items you own). "+ Keep" adds the item, keeping as many as your bag holds now, or 1 if it holds none.
- On the main item list, each row has a "Keep" button that adds the item the same way. Once the item is on the list, the button shows the amount (for example "Keep 5"), and pressing it opens the list.
- Changes are saved a moment after you stop clicking, and the status line confirms them, for example `Keeping 5 Red Apple in your bag.`
- After a put-away, the status line says what stayed, for example `Moved 4 stacks. Kept in your bag: 6 Red Apple.`

### Whole stacks only

The game moves a stack whole and cannot split one, so the keep list keeps whole stacks. It keeps at least the amount you set when your bag has that many, and often a few more:

- When one stack is enough, the smallest stack that covers the amount stays.
- When no single stack is enough, the fullest stacks stay until the amount is covered.
- Between two stacks of the same size, the fresher one stays.

For example, keeping 5 apples with stacks of 20 and 6 keeps the 6. With one stack of 10, all 10 stay. If your bag holds fewer than the amount, everything of that item stays.

### Where the list is saved

The list is saved to `BepInEx\config\com.dev.survivallog.storagehub.keep.json`.

**For now this is one list for all your saves.** Changing it in one save changes it in every save.

The mod is careful with that file:

- If the file is not a keep list this version can read (it was edited by hand and broken, say), it is copied to a name like `com.dev.survivallog.storagehub.keep.json.broken-20261012-091400` (the date and time), the list starts empty, and the log says why. Your old list stays in that copy.
- If the file is there but cannot be opened at all (another program has it locked, say), it is left alone. Until it can be read, changes to the list and bag put-aways refuse and say so on the status line, rather than overwrite it or put away items you meant to keep. The mod tries again each time you open the panel, change the list or put your bag away.

With the keep list turned off in the settings, putting away moves everything, and the list stays saved for when you turn it back on. The list's page then shows a note saying it is off.

## Drone deliveries

When a drone comes home and the game drops its cargo into the drone hub (or into the drone itself when the hub is full), Storage Hub puts each delivered stack away the same way "Put whole bag away by tag" does. What no storage takes stays where the game put it. Each delivery writes one line to the log and shows it as a short message on screen (on the panel's status line while the panel is open), for example `Drone delivery: 12 Wood to Wooden Box, 3 Red Apple to Fridge. Stayed in Drone hub: 2 Rope.`

- It works for every drone trip that brings cargo home.
- No drone and no drone hub is ever a target for a delivery, not even one that already holds the item.
- Material racks are filled even when their robot put switch is off. Turn on `SkipRacksWithRobotPutOff` to leave those racks out.
- Two drones landing on one hub at once are put away together, as one delivery with one line.
- When the cargo landed in the drone and all of it was put away, the drone is done: it no longer offers "Retrieve supplies" and its marker goes away, the same as if you had retrieved it yourself. If anything is left in the drone, it still offers "Retrieve supplies" so you can take the rest.
- A delivery that ends up out of the drone, whether the game unloaded it into the hub or Storage Hub emptied the drone, counts as retrieving the supplies for events like "Conduct Drone trade and retrieve supplies". Such an event also needs a completed trade, and an event already stuck clears on the next trade trip home. A drone that still waits for you to retrieve it counts when you do. With `AutoPutAway` off nothing is counted.
- Nothing happens before the prologue is over.

## Tidying storages

This is how tidying works with the default settings. You can turn off the tidy on open, the compact layout or stack merging on their own (see [CONFIG.md](CONFIG.md)).

- Opening a storage tidies it a moment after the screen appears. Only the storage side is tidied; your own bag is never rearranged when you open a storage.
- Big items are packed toward the bottom-right corner and small items (2 cells or fewer by default) usually toward the top-left. Several layouts are built. One fills the smallest square in the top-left corner with the small items; each of the others sets the items down one at a time, each where it touches the most of the bag's edges, mold and items already placed. The one whose free space is best is kept. Best means, in order: the fewest empty cells that only a small item could fill, the most room for big items, the biggest free block, and the fewest separate free areas. On a tie, the layout with more items of the same kind side by side wins. The same storage always packs the same way.
- If no compact layout can hold everything, the game's own Sort layout is used; if neither can, nothing moves.
- The game's Sort button packs the same way: the game sorts first, then the side you sorted is repacked. That includes your own bag when you press Sort on it.
- A storage that is already tidy is left alone. A storage is tidied once per opening, so whatever you move by hand stays put until you close and reopen it.
- Items are never rotated; the game has no rotation.
- Moldy cells are kept clear: no item is placed on one, and an item already sitting on one is moved off it. A storage that would only fit by using a moldy cell is left as it is.

### Merging stacks

Before a tidy, partial stacks of the same item are topped up from each other. A storage holding 7, 5 and 4 berries with a stack limit of 10 ends up with 10 and 6.

- Food merges only when the two stacks spoil within an hour of each other, the same rule the Stacking mod uses. The food poured in takes the spoil time of the stack it joins, so it can end up as much as an hour older or fresher than it was.
- Items that never spoil merge when their uses left match.
- Stacks also have to match on everything else the item carries (mold, packaging, burn value, weight).
- The stack limit is the game's own, so a limit raised by the Stacking mod is used. Works with or without Stacking installed.
- A stack sitting on a moldy cell is never merged.
- Merging only happens after the prologue.

## Known issues and limits

Moving items around is finicky. The mod checks every move and puts things back when the game refuses one, but if an item ends up somewhere strange, goes missing or doubles, please tell me (see [Feedback](#feedback-and-bug-reports)), even if you are not sure the mod caused it. If one piece misbehaves, turn just that one off in the settings.

- "Take all into bag" takes from every place listed for the item, a drone included. Check where an item sits before taking all of it if a drone holds trade goods.
- Material racks, cooking furniture, compost bins, hot pots, locked bags and the game's special bags (the vehicle trunk and boxes, the workbench drawer, the shredder and compressor trays, the trap store) are never tidied, because the game places items in them by its own rules.
- A storage whose grid on screen differs from its real grid is left alone, with one line in the log. So is a storage whose mold cannot be read.
- After Sort, the game's own layout shows for one frame before the compact one replaces it.
- If the game refuses a tidy move, every item is put back where it was and one line goes to the log. Nothing is shown in game. In rare cases an item the game had saved outside the grid is not put back on its old cell; the log names it.
- A delivered stack that topped up a stack already in the hub moves whole, including what was there before.
- When the hub is full, the game leaves the cargo in the drone. Anything the game moves out of the drone later is not put away.
- The keep list is one list for all saves.
- The mod's own text is in English. Item and storage names come from the game, in your language.
- `BepInEx\storage-hub.log` is never trimmed. Delete it now and then if it grows large.

## Requirements

- Survival Log 1.1.18317. Built for this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Download `StorageHub.dll` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-StorageHub/releases/latest) and put it in the `BepInEx\plugins` folder inside the game folder (the folder with the game .exe). Full path example: `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\StorageHub.dll`

## Uninstall

Delete `StorageHub.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete these from `BepInEx\config`: `com.dev.survivallog.storagehub.cfg`, `com.dev.survivallog.storagehub.keep.json` and any `.broken-` copies of it. The log is `BepInEx\storage-hub.log`.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.storagehub.cfg`, created the first time the game starts with the mod. Edit it while the game is closed; changes apply at the next game start. See [CONFIG.md](CONFIG.md) for every setting.

The keep list is not in that file. Change it in the panel ("Keep in bag").

## Troubleshooting

Built against BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for a line starting `===== Storage Hub 0.7.2 started`. It names the toggle key and whether verbose logging and the keep list are on. The lines after it say which game hooks went in, how the tidy and delivery settings are set, and, when a keep list was found, how many items it holds. A hook that failed gets a line starting `could not hook`, which says what will not work.

The same lines are in `BepInEx\storage-hub.log`, which survives restarts, along with every move the mod makes.

If the status line says `That failed, see BepInEx/storage-hub.log.`, the log has the details. To see why an item went where it did, turn on `Verbose` (see [CONFIG.md](CONFIG.md)): the log then lists every planned move and how each storage's tags ranked the item.

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . You can also leave a comment on the Nexus page or [open an issue](https://github.com/AnExiledDev/SurvivalLog-StorageHub/issues). When reporting a bug, attach `BepInEx\LogOutput.log` and `BepInEx\storage-hub.log`.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against BepInEx and the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. Those assemblies come from the game and are not part of this repo. The .NET 8 SDK is required.

```
dotnet build src/StorageHub -c Release -p:GameDir="C:\path\to\Survival Log"
```

`GameDir` is the game folder. The default in the project file is a local path you will almost certainly need to override. The output DLL is at `src\StorageHub\bin\Release\StorageHub.dll`.

The unit tests cover the pure rules (put-away choice, the keep list, packing, stack merging, deliveries and the rotting list) and need nothing from the game:

```
dotnet test tests/StorageHub.Tests
```

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
