# Storage Hub

A BepInEx 6 (IL2CPP) plugin for Survival Log: one searchable list of every item in your bag and all your storages, plus buttons that put things away into the storage whose tags fit them.

## What it does

- `F8` opens a panel in the game's own UI style listing every item across your bag, storages and drawers, with a search box.
- Per item: take it from a storage into your bag, take all of it into your bag, or put it away by tag.
- "Put whole bag away by tag" sends each item in your bag to the best-matching tagged storage.
- "Put floor items away by tag" picks up items lying on the floor inside the house and stores them the same way; anything with no matching storage goes into your bag.
- The put-away buttons also appear on the game's inventory screen.

Moves go through the game's own quick-move and floor pickup, so freshness and durability are kept.

## Config

`BepInEx/config/com.dev.survivallog.storagehub.cfg`:

- `Panel/ToggleKey` (default `F8`): key that opens and closes the panel.
- `Diagnostics/Verbose` (default `false`): also log every planned move and every storage's tag ranking.

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
