# Changelog

## 0.8.0

- A searchable home storage list sits beside your bag on the inventory screen, the stove and the rat cage. At a station, "For cooking" or "For the rat cage" shows only what it can use, and clicking a usable item puts it into the station; anything else goes to your bag. On the inventory screen, "Open" shows a storage beside your bag. New setting `HomeStorageList`.
- The stove and the rat cage get a bag tab for every home storage holding something they can use. New setting `StorageTabs`.

## 0.7.4

Changes since 0.3.2:

- "Rotting soon" switches the list to food that spoils, soonest first, with where it sits and the time left.
- A keep list names items you always carry; putting your bag away leaves that many in your bag.
- Put-away actions moved into a shared Mods menu.
- Storages tidy when you open them: a compact layout packs small items, stacks merge, and moldy bags are tidied around their mold.
- Drone deliveries are put away where their tags say, fill material racks, and count toward retrieve-supplies events.
- Material racks are ranked the way the game's own robots rank them. New setting `SkipRacksWithRobotPutOff` leaves out racks with their robot put switch off.
- The panel, menu entries and messages are in Simplified Chinese too, and every config entry is explained in plain English and Chinese.
- The panel footer names the key you set.
- Fixes: no lost units, clicks or lists; an emptied drone stops offering a retrieve; the mod only reads game objects after the game has built them.
