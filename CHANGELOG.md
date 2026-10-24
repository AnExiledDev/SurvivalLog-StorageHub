# Changelog

## 0.14.3

- Out exploring, the looting screen's tabs keep the game's own look; the new tab style is only at home.
- Switching storages with A and D scrolls the tab row to the chosen tab.
- The tab row scrolls by whole tabs, so the first tab after the arrow is never cut in half. The arrows move to the next tabs not fully shown, and each mouse wheel notch moves one tab.
- Made for Survival Log 1.1.19611.

## 0.14.2

- The storage tabs at home and at the basket are smaller and sit in one bar, with the open tab filled gold, so more storages fit before the row scrolls.
- Made for Survival Log 1.1.19611.

## 0.14.1

- Fixed: the Storage Hub window (F8) did not open. This broke in 0.13.1.
- Made for Survival Log 1.1.19611.

## 0.14.0

- At home, opening any storage, the neighbor's basket or the trunk puts every home storage in a row of tabs above your bag, with Home after the bag's tab, the way the crafting bench and other stations show them. Clicking an item in Home puts it in the storage that is open.
- At the basket every storage gets a tab, whether or not she has asked for anything yet.
- Out exploring, looting looks the same as before.
- Made for Survival Log 1.1.19611.

## 0.13.1

- Fixed: at the neighbor's basket, a long row of storage tabs widened the bag panel past the left edge of the screen and left a wide gap between the bag and the basket. The tab row now stays as wide as the bag panel and scrolls.
- At the basket, storage tabs are only added for storages holding something she asks for today, and each tab shows its storage's name instead of "Backpack".
- Made for Survival Log 1.1.19131; works on 1.1.19611.

## 0.13.0

- The neighbor's basket can be put away into your storages by tag, the same way as your bag. The button for it is Neighbor Basket's "Put away" on the basket screen (Neighbor Basket 0.2.0 or newer, optional). What no storage wants stays in the basket.
- Made for Survival Log 1.1.19131.

## 0.12.0

- The neighbor's basket screen gets a "Home" tab right after "Character Inventory", and a bag tab for every home storage holding something the basket accepts. Clicking an item in the Home list puts it in the basket. What she asks for today is marked "Wanted" and listed first.
- When there are more tabs than fit, the bag tab and Home stay put and the rest scroll, with arrows at each end. The mouse wheel scrolls them too.
- What is in the basket shows in the panel under its own storage, but nothing is ever put away into the basket or taken back out of it by "Fill bag from storage".
- Fixed: at a station, a Home click that had to switch the storage tab first closed the Home list. It now stays open.
- Made for Survival Log 1.1.19131.

## 0.11.2

- Fixed: pressing A or D while a station's Home tab was showing switched the inventory hidden under it, and the Home list stayed. Now the Home list closes and the inventory you switched to shows.
- Made for Survival Log 1.1.19131.

## 0.11.1

- Fixed the game freezing when taking more than one item from a stack with "Take 5" or "Fill bag from storage".
- Made for Survival Log 1.1.19131.

## 0.11.0

- A chest button at the top of the HUD's tool column opens and closes the panel, with the panel key in its tooltip.
- Each storage row in an item's detail has "Take all", and with Stacking installed a slider to pick how many to take, with a "Take 5" button.
- "Fill bag from storage" on the "Keep in bag" tab takes what your bag is short of from your home storages, never from a drone. Each row says how many storage holds, or "None in storage" in red when your bag is short and storage has none.
- Pointing at a button shows what it does. Pointing at an item shows Info Pack's tooltip with Info Pack 0.8.7 or later, or a short tooltip of its own without it.
- Made for Survival Log 1.1.19131.

## 0.10.1

- Fixed: typing A or D in a search box, the Home tab's included, also switched the game's inventory tabs. The game's keys now wait while you type.
- Made for Survival Log 1.1.19131.

## 0.10.0

- A new look for the panel. "Items", "Rotting soon" and "Keep in bag" are tabs along the top, each with a count.
- Item rows show where the item sits under its name, your bag first. An "All places" drop-down beside the search box shows only the items in one storage.
- The item's detail says how many places hold it, and has a Keep button.
- The Keep button on item rows is a bell that shows when you point at the row, and stays lit with the amount once the item is kept.
- "Rotting soon" is split into Spoiled, Under a day, This week and Later.
- Keep list rows have a bar that fills as your bag gets to the amount to keep.
- "Put bag away" is the big button at the bottom right, with "Put floor away" beside it.
- The status line is green when everything moved, amber when something was left behind and red when something failed.
- The Home tab at stations and the Home storage window have the same new look. The "usable" and "Everything" switch shows how many items each side holds.
- With Info Pack 0.8.4 or later, pointing at an item in the Home tab or the Home storage window shows its item tooltip.
- Made for Survival Log 1.1.19131.

## 0.9.5

- After a game update, a hook that can no longer be placed is logged as an error, so the Mod Manager can mark the mod as maybe needing an update.
- Made for Survival Log 1.1.19131.

## 0.9.4

- The Auto Organize button in the drone trade tidies your bag the same way as the inventory's Sort button, and tops up partial stacks of the same item. Stacks you have offered keep their count.

## 0.9.3

- In the drone trade, dragging an offered item from the drone onto the Home tab takes it back off the offer. With Stacking installed, Shift or Ctrl held while you let go takes back only part of it. Clicking a stack in the Home tab offers one unit (with Stacking installed).

## 0.9.2

- At the workbench, clicking an item in the Home tab puts one unit on the bench, not the whole stack, the same as at the shredder. Without Stacking installed it still moves the whole stack.

## 0.9.1

- At the shredder, clicking an item in the Home tab puts one unit in the hopper, not the whole stack. Without Stacking installed it still moves the whole stack.

## 0.9.0

- The Mods screen links this changelog.
- On the Mods screen, settings that only matter while another setting is on stay hidden until it is.
- On the Mods screen, the diagnostics settings sit in the folded Advanced group.
- Settings changed on the Mod Manager's Mods screen take effect at once.
- The Mods screen shows the mod's name, description, category and links, and lists Stacking as an optional companion.
- Exiled's Toolkit 0.4.0 or newer is optional. With it, settings changed on the Mods screen take effect without a restart; without it, at the next game start.

## 0.8.3

- The "Home" tab and the home storage tabs now also show at the workbench, with a "For the workbench" filter for anything a recipe uses. The workbench takes recipe materials from every storage tab it lists, so a home storage tab there also feeds crafting.
- At a station, clicking an item in the "Home" tab that goes to your bag now moves one unit instead of the whole stack. This needs Stacking 0.5.0 or later; without it the whole stack moves as before.
- The "Open" button in the "Home" tab now works at the drone trade.

## 0.8.2

- The "Home" tab and the home storage tabs now also show at the brewing barrel, dye vat, shredder, ration press and drone trade. Each has its own filter ("For brewing", "For dyeing", "For the shredder", "For the ration press", "For trading"). At the drone trade, clicking a tradable item puts it on your side of the trade. The trade's tabs only show when it has more than one, so the "Home" tab needs at least one storage tab there too.

## 0.8.1

- Stack merging works again for items that never spoil, such as wire, dyes, fuel and seeds. They merge whenever they were picked up and whether they were found on the map, made or bought.
- Food found on the map no longer differs from other food by the shelf factor it came with; the time left decides, as before.
- When partial stacks of one item stay apart, the log says why (which trait differs, or freshness or uses), once per item per session.

## 0.8.0

- A searchable home storage list. At the stove and the rat cage it is a "Home" tab on the bag panel; on the inventory screen it is the "Home storage" window in the Mods menu. At a station, "For cooking" or "For the rat cage" shows only what it can use, and clicking a usable item puts it into the station; anything else goes to your bag. In the window, "Open" shows a storage beside your bag. `Esc` or the close button shuts it. New setting `HomeStorageList`.
- The stove and the rat cage get a bag tab for every home storage holding something they can use. New setting `StorageTabs`.
- At the rat cage, the mouse slots scroll past three rows, so a big cage no longer shrinks the whole screen.

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
