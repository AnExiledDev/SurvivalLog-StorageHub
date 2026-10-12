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
| `Enabled` | `true` | Leave the items on the "Keep in bag" list in your bag whenever you put your bag away, whole or one item at a time. Off: putting away moves everything, and the list stays saved for when you turn it back on. |

## Delivery

| Setting | Default | What it does |
| --- | --- | --- |
| `AutoPutAway` | `true` | When a drone lands, put what it brought into the storages tagged for it. What no storage takes stays where the game put it. |

## Organize

| Setting | Default | What it does |
| --- | --- | --- |
| `OrganizeOnOpen` | `true` | Tidy a storage each time you open it. Your own bag is never tidied on open. |
| `CompactSmallItems` | `true` | Pack small items into a square in the top-left corner and big items from the bottom-right, so the free space stays in one piece. Also applies to the game's Sort button. Off: storages are tidied the way the game's Sort does it, and Sort is left alone. |
| `SmallItemMaxArea` | `2` | Items covering this many cells or fewer count as small. `0` means no item counts as small. |
| `MergeStacks` | `true` | When a storage is tidied, top up partial stacks of the same item from each other first. Food only merges when the stacks spoil within an hour of each other, and stacks on mold are left alone. |

`MergeStacks` rides on the tidies above, so it does nothing while both `OrganizeOnOpen` and `CompactSmallItems` are off. With those two off, the mod leaves your storages and the Sort button exactly as the game makes them.

## Diagnostics

| Setting | Default | What it does |
| --- | --- | --- |
| `Verbose` | `false` | Also log every planned move, every stack the keep list held back, and how each storage's tags ranked an item, for working out why an item went where it did. |

The log is `BepInEx\storage-hub.log`. Leave `Verbose` off unless you are chasing a problem; it makes the log grow much faster.
