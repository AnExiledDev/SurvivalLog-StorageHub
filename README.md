# Storage Hub

A BepInEx 6 (IL2CPP) plugin for Survival Log: one searchable list of every item in your bag and all your storages, plus buttons that put things away into the storage whose tags fit them.

## What it does

- `F8` opens a panel in the game's own UI style listing every item across your bag, storages and drawers, with a search box.
- Per item: take it from a storage into your bag, take all of it into your bag, or put it away by tag.
- "Put whole bag away by tag" sends each item in your bag to the best-matching tagged storage.
- "Put floor items away by tag" picks up items lying on the floor inside the house and stores them the same way; anything with no matching storage goes into your bag.
- The put-away buttons also appear on the game's inventory screen.

Moves go through the game's own quick-move and floor pickup, so freshness and durability are kept.

## Organize

- Opening a storage tidies it a moment after the screen appears. Only the storage side is tidied; your own bag is never rearranged on open.
- With `CompactSmallItems` on, big items are packed in from the bottom-right corner and small items (2 cells or fewer by default) go into the smallest square in the top-left corner that holds them. The free space stays in one piece, so a big item still fits later. If that layout cannot hold everything, the game's own Sort layout is used; if neither can, nothing moves.
- The game's Sort button packs the same way while `CompactSmallItems` is on: the game sorts first, then the side you sorted is repacked on the next frame. That includes your own bag when you press Sort on it.
- A storage that is already tidy is left alone, so reopening it writes nothing. A storage is tidied once per opening, so whatever you move by hand stays put until you close and reopen it.

Known limits:

- Material racks, cooking furniture, compost bins, hot pots, locked bags and the game's synthetic bags (the vehicle trunk and boxes, the workbench drawer, the shredder and compressor trays, the trap store) are never touched, because the game places items in them by its own rules. Furniture with item slots and baskets are tidied: the game only checks their total load or which items they take, never which cell an item sits on.
- Mold cells are kept clear. No layout puts an item on one, an item already on one is moved off it, and a storage that would only fit by using a moldy cell is left as it is. If the mold cannot be read, the storage is left alone, with one line in the log.
- A bag whose grid on screen differs from its real grid is left alone, with one line in the log.
- After Sort, the game's own layout shows for one frame before the compact one replaces it.
- If the game refuses a move, every item is put back where it was and one line goes to the log. Nothing is shown in game. The exception is an item the game had saved off the grid (a load repair can leave one at -1,y): writing that cell back would hand it to the game's repair again, which may move or store it elsewhere, so it stays where it is, or takes the first free cell if it is in another item's way. Each such item gets its own log line.
- Items are never rotated; the game has no rotation.

## Config

`BepInEx/config/com.dev.survivallog.storagehub.cfg`:

- `Panel/ToggleKey` (default `F8`): key that opens and closes the panel.
- `Diagnostics/Verbose` (default `false`): also log every planned move and every storage's tag ranking.
- `Organize/OrganizeOnOpen` (default `true`): tidy a storage each time you open it.
- `Organize/CompactSmallItems` (default `true`): pack small items into a square in the top-left corner and big ones from the bottom-right, both when a storage opens and when you press Sort. Off: opening a storage tidies it the way the game's Sort does, and the Sort button is left alone.
- `Organize/SmallItemMaxArea` (default `2`): items covering this many cells or fewer count as small.

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
