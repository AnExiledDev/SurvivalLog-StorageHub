# Storage Hub settings

Settings live in `BepInEx\config\com.dev.survivallog.storagehub.cfg`, created the first time the game starts with the mod. Each heading below is a section in that file, like `[Panel]`. Change them in game on the Mod Manager's Mods screen, where they take effect at once, or edit the file while the game is closed. Delete the file to get the defaults back. Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start instead.

The keep list itself is not a setting. Change it in the panel ("Keep in bag"); it is saved in `BepInEx\config\com.dev.survivallog.storagehub.keep.json`.

## Panel

| Setting | Default | What it does |
| --- | --- | --- |
| `ToggleKey` | `F8` | The key that opens and closes the Storage Hub panel. Use a Unity key name such as `F7` or `Insert`; the settings file lists every accepted name. |

## Keep

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | When you put your bag away, leave the amounts on the "Keep in bag" list in your bag. This counts for the whole bag and for one item at a time. Off: putting away moves everything, and the list stays saved for when you turn this back on. |

## Stations

| Setting | Default | What it does |
| --- | --- | --- |
| `HomeStorageList` | `true` | Add a searchable list of your home storages: a "Home" tab on the bag panel at the stove, rat cage, brewing barrel, dye vat, shredder, ration press, drone trade and workbench and, at home, beside an open storage, the neighbor's basket or the car trunk, and a "Home storage" window in the Mods menu on the inventory screen. At the stove or rat cage, clicking an item the station takes puts it into the station, and anything else goes to your bag. Beside a storage, the basket or the trunk, a click puts the item in it. |
| `StorageTabs` | `true` | At the stove, rat cage, brewing barrel, dye vat, shredder, ration press, drone trade and workbench, add a bag tab for every home storage holding something the station takes, next to the game's own storage tabs. At home, opening a storage, the neighbor's basket or the car trunk also gives every home storage a tab above your bag, empty or not. |

## Delivery

| Setting | Default | What it does |
| --- | --- | --- |
| `AutoPutAway` | `true` | When a drone comes home, put what it brought into the storages tagged for it, the same way "Put whole bag away by tag" does. Anything no storage takes stays where the game put it. It also lets events that ask you to trade with a drone and pick up the supplies finish. |
| `SkipRacksWithRobotPutOff` | `false` | When on, drone deliveries leave out any material rack whose robot put switch is off, the way the game's robots do. When off, deliveries fill every rack that has a slot for the item. |

## Organize

| Setting | Default | What it does |
| --- | --- | --- |
| `OrganizeOnOpen` | `true` | Tidy a storage each time you open it. Your own bag is never tidied when you open a storage. |
| `CompactSmallItems` | `true` | Pack big items toward the bottom-right and small items toward the top-left, leaving as few gaps as it can and as much room as it can for big items. Items of the same kind stay together when that costs no room. The game's Sort button packs this way too. Off: storages are tidied the way the game's Sort does it, and the Sort button works as normal. |
| `SmallItemMaxArea` | `2` | Items that take up this many cells or fewer count as small when `CompactSmallItems` packs a storage. `0` means no item counts as small. |
| `MergeStacks` | `true` | When a storage is tidied, first top up part-full stacks of the same item from each other. Food only merges when the stacks spoil within an hour of each other. Stacks on moldy cells are left alone. |

`MergeStacks` only runs as part of a tidy, so it does nothing while both `OrganizeOnOpen` and `CompactSmallItems` are off. With those two off, the mod leaves your storages and the Sort button exactly as the game makes them.

## Interface

| Setting | Default | What it does |
| --- | --- | --- |
| `UiScale` | `0` | How big Storage Hub's own windows, panels and messages are drawn, in percent, from 75 to 200. At `0` they follow the UI size set in Exiled's Toolkit (its `UiScale`). Needs Exiled's Toolkit 0.11.0 or newer. |

## Diagnostics

| Setting | Default | What it does |
| --- | --- | --- |
| `Verbose` | `false` | Write extra detail to the log: every planned move, what the keep list left in your bag, and how well each storage's tags matched an item. Turn it on to find out why an item went where it did. |

The log is `BepInEx\storage-hub.log`. Leave `Verbose` off unless you are chasing a problem; it makes the log grow much faster.
