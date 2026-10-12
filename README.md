# Storage Hub

A BepInEx 6 (IL2CPP) plugin for Survival Log: one searchable list of every item in your bag and all your storages, plus buttons that put things away into the storage whose tags fit them.

## What it does

- `F8` opens a panel in the game's own UI style listing every item across your bag, storages and drawers, with a search box.
- Per item: take it from a storage into your bag, take all of it into your bag, or put it away by tag.
- "Put whole bag away by tag" sends each item in your bag to the best-matching tagged storage.
- "Put floor items away by tag" picks up items lying on the floor inside the house and stores them the same way; anything with no matching storage goes into your bag.
- Both put-away actions are also in the Mods menu on the game's inventory screen, the button next to the game's Sort button on your bag's item tab. Other mods that use the same menu (Unpack All does) list their actions there too. Esc or a click elsewhere closes it.
- "Rotting soon" (a button next to the search box) switches the list to food that spoils: everything in your bag, your storages and on the floor at home, soonest first, with where it sits and the time left ("spoiled", "under 1h", "5h", "2d 3h"). Items that never spoil, and food the game has stopped spoiling, are left out. The search box filters it too, and clicking a row shows that item on the right. Press the button again for the full item list. The time left is the game's own figure, the one its inventory tooltip shows.

Moves go through the game's own quick-move and floor pickup, so freshness and durability are kept.

## Drone deliveries

When a drone comes home and the game drops its cargo into the drone hub (or into the drone itself when the hub is full), Storage Hub puts each delivered stack into the storage whose tags fit it, the same way "Put whole bag away by tag" does. What no storage takes stays where the game put it. Each delivery writes one line to the log and shows it as a toast (in the panel's status line while the panel is open), for example "Drone delivery: 12 Wood to Wooden Box, 3 Red Apple to Fridge. Stayed in Drone hub: 2 Rope."

Known limits:

- It runs for every drone trip that ends through the game's return settlement, trade, foraging and loot trips alike.
- A delivered stack that topped up a stack already in the hub moves whole, including what was there before.
- When the hub is full the game leaves the cargo in the drone, and its own auto-unload later moves the drone's whole bag into the hub once it all fits, then ends the trip. Whatever the put-away left in the drone goes along, and that later move is not put away.
- No drone and no drone hub is ever a put-away target, not even one that already holds the item.
- Two drones landing on one hub at once are put away together, as one delivery with one log line.
- Nothing runs before the prologue is over.

## Organize

- Opening a storage tidies it a moment after the screen appears. Only the storage side is tidied; your own bag is never rearranged on open.
- With `CompactSmallItems` on, big items are packed in from the bottom-right corner and small items (2 cells or fewer by default) go into the smallest square in the top-left corner that holds them. The free space stays in one piece, so a big item still fits later. If that layout cannot hold everything, the game's own Sort layout is used; if neither can, nothing moves.
- The game's Sort button packs the same way while `CompactSmallItems` is on: the game sorts first, then the side you sorted is repacked on the next frame. That includes your own bag when you press Sort on it.
- A storage that is already tidy is left alone, so reopening it writes nothing. A storage is tidied once per opening, so whatever you move by hand stays put until you close and reopen it.
- With `MergeStacks` on, every tidy first tops up partial stacks of one kind from each other, so a storage holding 7, 5 and 4 berries with a stack limit of 10 ends up with 10 and 6. Food merges only when the two stacks spoil within an hour of each other, the rule the Stacking mod uses. The units poured in take the spoil time of the stack they join, so they can end up as much as an hour older or fresher than they were. Items that never spoil merge when their uses left match. Stacks also have to agree on everything else the item carries (mold, packaging, burn value, weight). The stack limit is the game's own, so a limit raised by Stacking is used. Works with or without Stacking installed. After the prologue only.

Known limits:

- Material racks, cooking furniture, compost bins, hot pots, locked bags and the game's synthetic bags (the vehicle trunk and boxes, the workbench drawer, the shredder and compressor trays, the trap store) are never touched, because the game places items in them by its own rules. Furniture with item slots and baskets are tidied: the game only checks their total load or which items they take, never which cell an item sits on.
- Mold cells are kept clear. No layout puts an item on one, an item already on one is moved off it, and a storage that would only fit by using a moldy cell is left as it is. If the mold cannot be read, the storage is left alone, with one line in the log.
- A bag whose grid on screen differs from its real grid is left alone, with one line in the log.
- After Sort, the game's own layout shows for one frame before the compact one replaces it.
- If the game refuses a move, every item is put back where it was and one line goes to the log. Nothing is shown in game. The exception is an item the game had saved off the grid (a load repair can leave one at -1,y): writing that cell back would hand it to the game's repair again, which may move or store it elsewhere, so it stays where it is, or takes the first free cell if it is in another item's way. Each such item gets its own log line.
- Items are never rotated; the game has no rotation.
- A stack sitting on a mold cell is never merged into or out of. If the game refuses part of a merge, both stacks are put back to their counts and merging stops for that tidy, with one line in the log.

## Config

`BepInEx/config/com.dev.survivallog.storagehub.cfg`:

- `Panel/ToggleKey` (default `F8`): key that opens and closes the panel.
- `Diagnostics/Verbose` (default `false`): also log every planned move and every storage's tag ranking.
- `Organize/OrganizeOnOpen` (default `true`): tidy a storage each time you open it.
- `Organize/CompactSmallItems` (default `true`): pack small items into a square in the top-left corner and big ones from the bottom-right, both when a storage opens and when you press Sort. Off: opening a storage tidies it the way the game's Sort does, and the Sort button is left alone.
- `Organize/SmallItemMaxArea` (default `2`): items covering this many cells or fewer count as small.
- `Organize/MergeStacks` (default `true`): top up partial stacks of one kind from each other when a storage is tidied. It rides on the tidies above, so it does nothing while both `OrganizeOnOpen` and `CompactSmallItems` are off.
- `Delivery/AutoPutAway` (default `true`): put drone deliveries away into tagged storages when the drone lands.

Log: `BepInEx/storage-hub.log`.

## Install

Download `StorageHub.dll` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-StorageHub/releases/latest) and copy it into `BepInEx/plugins`. Needs BepInEx 6 (IL2CPP build).

## Build and test

```sh
dotnet build src/StorageHub -c Release -p:GameDir="D:\path\to\Survival Log"
dotnet test tests/StorageHub.Tests
```

The build references BepInEx and the game's interop assemblies under `GameDir`, so BepInEx must be installed and the game launched once. The tests compile only the pure `Core` code and need nothing from the game.

## License

MIT, see [LICENSE](LICENSE).
