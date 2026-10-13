# Storage Hub settings

Settings live in `BepInEx\config\com.dev.survivallog.storagehub.cfg`, created the first time the game starts with the mod. Each heading below is a section in that file, like `[Panel]`. Edit the file while the game is closed; changes apply at the next game start. Delete the file to get the defaults back.

The keep list itself is not a setting. Change it in the panel ("Keep in bag"); it is saved in `BepInEx\config\com.dev.survivallog.storagehub.keep.json`.

## Panel

| Setting | Default | What it does |
| --- | --- | --- |
| `ToggleKey` | `F8` | The key that opens and closes the Storage Hub panel. Use a Unity key name such as `F7` or `Insert`; the settings file lists every accepted name. |

## Keep

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | When you put your bag away, leave the amounts on the "Keep in bag" list in your bag. This counts for the whole bag and for one item at a time. Off: putting away moves everything, and the list stays saved for when you turn this back on. |

## Delivery

| Setting | Default | What it does |
| --- | --- | --- |
| `AutoPutAway` | `true` | When a drone comes home, put what it brought into the storages tagged for it, the same way "Put whole bag away by tag" does. Anything no storage takes stays where the game put it. Also counts each delivery as retrieving the supplies, so drone trade events can finish. |

## Organize

| Setting | Default | What it does |
| --- | --- | --- |
| `OrganizeOnOpen` | `true` | Tidy a storage each time you open it. Your own bag is never tidied when you open a storage. |
| `CompactSmallItems` | `true` | Pack big items toward the bottom-right and small items usually toward the top-left. Several layouts are tried, and the one kept leaves the fewest empty cells that only a small item could fill and the most room for big items. Items of the same kind stay together when that costs no room. The game's Sort button packs this way too. Off: storages are tidied the way the game's Sort does it, and the Sort button works as normal. |
| `SmallItemMaxArea` | `2` | Items that take up this many cells or fewer count as small when `CompactSmallItems` packs a storage. `0` means no item counts as small. |
| `MergeStacks` | `true` | When a storage is tidied, first top up part-full stacks of the same item from each other. Food only merges when the stacks spoil within an hour of each other. Stacks on moldy cells are left alone. |

`MergeStacks` only runs as part of a tidy, so it does nothing while both `OrganizeOnOpen` and `CompactSmallItems` are off. With those two off, the mod leaves your storages and the Sort button exactly as the game makes them.

## Diagnostics

| Setting | Default | What it does |
| --- | --- | --- |
| `Verbose` | `false` | Write extra detail to the log: every planned move, what the keep list left in your bag, and how well each storage's tags matched an item. Turn it on to find out why an item went where it did. |

The log is `BepInEx\storage-hub.log`. Leave `Verbose` off unless you are chasing a problem; it makes the log grow much faster.
