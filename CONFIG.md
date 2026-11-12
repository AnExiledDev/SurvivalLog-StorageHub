# Storage Hub settings

Settings live in `BepInEx\config\com.dev.survivallog.storagehub.cfg`, created the first time the game starts with the mod. Each heading below is a section in that file, like `[Panel]`. Change them in game on the Mod Manager's Mods screen, where they take effect at once, or edit the file while the game is closed. Delete the file to get the defaults back.

The keep list itself is not a setting. Change it in the panel ("Keep in bag"). It is saved in `BepInEx\config\com.dev.survivallog.storagehub.keep.json`.

## Panel

| Setting | Default | What it does |
| --- | --- | --- |
| `ToggleKey` | `None` | The key that opens and closes the Storage Hub panel. It is unbound by default, since the chest button in the HUD opens the panel. Set a Unity key name such as `F7` or `Insert` to open it from the keyboard too. The settings file lists every key name you can use. The key does nothing while you type in a text box. |

## Keep

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | When you put your bag away, the items on the panel's "Keep in bag" list stay in your bag, as many as the list says. This works when you put away the whole bag and when you put away one item. When off, putting away moves everything, and the list stays saved for when you turn this back on. |

## Stations

| Setting | Default | What it does |
| --- | --- | --- |
| `HomeStorageList` | `true` | Adds a searchable list of everything in your home storages. It shows as a "Home" tab on the bag panel at the stove, rat cage, brewing barrel, dye vat, shredder, ration press, drone trade and workbench, and at home when you open a storage, the neighbor's basket or the car trunk. On the inventory screen it is the "Home storage" window in the Mods menu. At a station, clicking an item the station can use puts it into the station (at the drone trade, onto your side of the trade), and anything else goes to your bag. At an open storage, the basket or the trunk, clicking an item puts it in there. |
| `StorageTabs` | `true` | At the stove, rat cage, brewing barrel, dye vat, shredder, ration press, drone trade and workbench, add a bag tab for every home storage holding something the station can use, next to the game's storage tabs. At home, opening a storage, the neighbor's basket or the car trunk also gives every home storage a tab above your bag, empty or not. |

## Delivery

| Setting | Default | What it does |
| --- | --- | --- |
| `AutoPutAway` | `false` | When a drone comes home, put what it brought into the storages tagged for it, the same way the "Put bag away" button does. Anything no storage takes stays where the game put it. The delivery also counts as retrieving the supplies, so events that ask you to trade with a drone and retrieve the supplies can finish. With this off, those events can't finish when the drone hub unloads the drone for you. |
| `SkipRacksWithRobotPutOff` | `false` | When on, drone deliveries leave out any material rack whose robot put switch is off, the way the game's robots do. When off, deliveries fill every rack that has a slot for the item. |

## Organize

| Setting | Default | What it does |
| --- | --- | --- |
| `OrganizeOnOpen` | `false` | Tidy a storage each time you open it. Your own bag is never tidied when you open a storage. |
| `CompactSmallItems` | `true` | Pack big items toward the bottom-right and small items toward the top-left, leaving as few gaps and as much room for big items as possible. Items of the same kind stay together when that costs no room. The game's Sort button packs this way too. When off, storages are tidied the same way the game's Sort button does it, and the Sort button works as normal. |
| `SmallItemMaxArea` | `2` | Items that take up this many cells or fewer count as small when `CompactSmallItems` packs a storage. `0` means no item counts as small. |
| `MergeStacks` | `true` | When a storage is tidied, first top up part-full stacks of the same item from each other. Food only merges when the stacks spoil within an hour of each other. Stacks on moldy cells are left alone. |

`MergeStacks` only runs as part of a tidy, so it does nothing while both `OrganizeOnOpen` and `CompactSmallItems` are off. With those two off, the mod leaves your storages and the Sort button exactly as the game makes them.

## Interface

| Setting | Default | What it does |
| --- | --- | --- |
| `UiScale` | `0` | How big Storage Hub's own windows, panels and messages are drawn, in percent, from 75 to 200. At `0` they follow the UI size set in Exiled's Toolkit (its `UiScale`). |

## Diagnostics

| Setting | Default | What it does |
| --- | --- | --- |
| `Verbose` | `false` | Write extra detail to the log: every planned move, what the keep list left in your bag, and how well each storage's tags matched an item. Turn it on to find out why an item went where it did. |

The log is `BepInEx\storage-hub.log`. Leave `Verbose` off unless you are tracking down a problem, since it makes the log grow much faster.
