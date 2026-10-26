Name: Storage Hub
Summary: Find anything you own in seconds: F8 opens one searchable list of everything in your bag and home storages. Put your whole bag away into the right tagged storages in one click, keep chosen items on you, and get a Home tab at the stove, workbench and other stations. Drone deliveries put themselves away and storages tidy themselves.

Description (BBCode):
Survival Log makes you open storage after storage to find something, and again to put it back. Storage Hub adds one panel that lists every item in your bag, your storages at home and the workbench drawer, in the game's own style, and puts things away for you. Every move goes through the game's own quick-move, so freshness and durability are kept.

[size=5]Main features[/size]
[list]
[*][b]The panel[/b]: press F8, or the chest button in the HUD, to search every item you own at home, see where each one sits, and take it into your bag or put it away. Take a whole storage's worth, or with [url=https://www.nexusmods.com/survivallog/mods/74]Stacking[/url] (optional) pick how many with a slider. Every button says what it does when you point at it.[/*]
[*][b]Put away by tag[/b]: send your whole bag, or everything lying on the floor at home, into the storages whose tags want it. Also in the Mods menu on the inventory screen and on most station screens. Material racks count as tagged Materials and take only what they have a slot for. With Neighbor Basket 0.2.0 or newer (optional), its "Put away" button empties the neighbor's basket the same way.[/*]
[*][b]Keep in bag[/b]: pick items by name and set how many to keep. Putting your bag away leaves that many in your bag and stores the rest, and "Fill bag from storage" pulls in what your bag is short of. Each row shows how many storage holds, and marks items storage has none of.[/*]
[*][b]Rotting soon[/b]: all your food that spoils, soonest first, with where it sits and the time left.[/*]
[*][b]Home tab at stations[/b]: at the stove, rat cage, brewing barrel, dye vat, shredder, ration press, workbench and drone trade, and at home whenever you open a storage, the neighbor's basket or the car trunk, the bag panel gets a searchable "Home" tab listing everything in your home storages, filtered to what that station uses. Click an item to put it in (one unit of a stack at the shredder, workbench and drone trade, with Stacking). In the drone trade, drag an offered item back onto the tab to take it off the offer. At an open storage or the trunk a click puts the item in it. At the basket, what the neighbor asks for today is marked "Wanted" and listed first. The same list is the "Home storage" window in the Mods menu. With [url=https://www.nexusmods.com/survivallog/mods/81]Info Pack[/url] (optional; 0.8.4 or newer here, 0.8.7 or newer in the panel), pointing at an item shows its tooltip.[/*]
[*][b]Storage tabs at stations[/b]: those stations also get a bag tab for every home storage holding something they can use, next to the game's own storage tabs, and the workbench crafts from them too.[/*]
[*][b]Storage tabs at home[/b]: at home, opening a storage, the neighbor's basket or the car trunk gives every home storage a tab above your bag, empty or not, so you can move things between any of them. A long row scrolls by whole tabs with the arrows or the mouse wheel, and A and D bring the chosen tab into view. Out exploring, looting looks as the game made it.[/*]
[*][b]Drone deliveries[/b]: when a drone comes home, its cargo is put away into the tagged storages. A drone left empty no longer asks you to retrieve supplies, and the delivery counts toward events that ask you to retrieve them. A setting leaves out material racks whose robot put switch is off.[/*]
[*][b]Tidy storages[/b]: opening a storage packs big items toward the bottom-right and small ones usually toward the top-left, in whichever of several layouts leaves the fewest empty cells only a small item could fill and the most room for big items. Items of the same kind stay together when that costs no room. It also tops up partial stacks of the same item. The Sort button packs the same way, and so does Auto Organize in the drone trade (offered stacks keep their count).[/*]
[/list]
The keep list, deliveries, tidying and stack merging can each be turned off in the config.

[size=5]Keep in bag[/size]
The game can't split a stack, so the keep list keeps whole stacks: the smallest stack that covers the amount, or else the fullest stacks until it is covered, the fresher one first between stacks of the same size. You often keep a few more than you asked for.

The list is saved in [font=Courier New]BepInEx\config\com.dev.survivallog.storagehub.keep.json[/font]. [b]For now it is one list for all your saves.[/b] If that file can't be read, the mod refuses to put your bag away or change the list rather than overwrite it, and a file this version can't understand is copied aside with a .broken- name before starting fresh.

Storage Hub keeps nothing of its own in your save and does not edit game files.

Works in English and Chinese.

[size=5]Known issues[/size]
[list]
[*]No put-away ever fills a drone or the workbench drawer. "Take all into bag" does take from a drone, so check before taking all of something a drone is carrying.[/*]
[*]Racks, cooking furniture, compost bins, hot pots, locked bags and the game's special bags are never tidied.[/*]
[*]After Sort, the game's own layout shows for one frame before the compact one replaces it.[/*]
[*]Drone deliveries and stack merging only start after the prologue.[/*]
[/list]

[center][img]https://sldb.core-directive.com/img/mods-banner.jpg[/img]
[size=4][url=https://www.nexusmods.com/survivallog/mods/73]Get Mod Manager[/url] to set up all your mods in game  |  [url=https://sldb.core-directive.com/en/]Open the Survival Log wiki[/url] for items, recipes and maps[/size][/center]

[size=5]Requirements[/size]
[list]
[*]Survival Log 1.1.19611. A game update can break it until the mod is updated.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]. Install it first.[/*]
[*]Optional: [url=https://www.nexusmods.com/survivallog/mods/74]Stacking[/url], to take part of a stack.[/*]
[*]Optional: [url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.4.0 or newer. With it, settings changed on the Mod Manager's Mods screen take effect without restarting the game. Without it, they take effect at the next game start.[/*]
[/list]

[size=5]Installation[/size]
[list=1]
[*]Install [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i](if no other mods were installed before, start the game once so BepInEx finishes setup, then quit)[/i].[/*]
[*]Optional, to change settings without a restart: install Exiled's Toolkit the same way. Download its zip and extract it into the game folder.[/*]
[*]Download the zip from the Files tab.[/*]
[*]Extract it into the game folder (the folder with [font=Courier New]SurvivalLog.exe[/font]). The zip holds a BepInEx folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\StorageHub.dll[/code][/*]
[/list]

[size=5]Uninstall[/size]
Delete [font=Courier New]StorageHub.dll[/font] from the [font=Courier New]BepInEx\plugins[/font] folder. The items it moved stay where they are.

[size=5]Configuration[/size]
To change the panel key or turn off any piece, edit [font=Courier New]BepInEx\config\com.dev.survivallog.storagehub.cfg[/font] while the game is closed, or do it in game on the Mod Manager's Mods screen, where changes take effect at once (see [url=https://github.com/AnExiledDev/SurvivalLog-StorageHub/blob/main/CONFIG.md]CONFIG.md[/url] for every setting; the cfg file explains each setting in English and Chinese). Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start instead.

[size=5]Feedback[/size]
Found a bug or have an idea? Come say hi on the [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url], or post in this page's Posts tab. For bugs, please include [font=Courier New]BepInEx\LogOutput.log[/font] and [font=Courier New]BepInEx\storage-hub.log[/font].
To reach me directly, message [b]exiled.dev[/b] on Discord; you can find me in the [url=https://discord.gg/MZGg4gQAUe]official Survival Log Discord[/url]. I'm also on X as [url=https://x.com/AnExiledDev]@AnExiledDev[/url].

[size=5]Source and license[/size]
Open source under MIT: [url=https://github.com/AnExiledDev/SurvivalLog-StorageHub]github.com/AnExiledDev/SurvivalLog-StorageHub[/url]

[line]
[size=5]简体中文[/size]

在《Survival Log》里，找一件东西要打开一个又一个储物家具，放回去还要再来一遍。Storage Hub 加了一个面板，用游戏自己的风格列出背包、家里所有储物家具和工作台抽屉里的每件物品，并替你收纳。每次移动都走游戏自己的快速移动，所以新鲜度和耐久都会保留。

[size=5]主要功能[/size]
[list]
[*][b]面板[/b]：按 F8 或点 HUD 里的箱子按钮，搜索家里拥有的所有物品，查看每件物品在哪里，把它取进背包或收纳起来。可以把一个储物家具里的全部取出；装了 [url=https://www.nexusmods.com/survivallog/mods/74]Stacking[/url]（可选）时，还能用滑块选择取出多少。鼠标指向任何按钮都会说明它的作用。[/*]
[*][b]按标签收纳[/b]：把整个背包，或家里地面上的所有物品，放进标签对应的储物家具。物品栏界面和大部分设施界面的 Mods 菜单里也有这两个按钮。材料架算作标签为「材料」的储物家具，只接收它有空位的物品。装了 Neighbor Basket 0.2.0 或更新版本（可选）时，它的「收纳」按钮会用同样的方式清空邻居的篮子。[/*]
[*][b]留在背包[/b]：按名称选择物品并设定保留数量。收纳背包时会在背包里留下这么多，其余的收纳起来；「从储物家具补满背包」会取出背包还缺的物品。每一行都显示储物家具里有多少，并标出储物家具里没有的物品。[/*]
[*][b]即将腐败[/b]：列出所有会腐败的食物，最快腐败的排在前面，并显示所在位置和剩余时间。[/*]
[*][b]设施旁的「家中」标签[/b]：在灶台、鼠笼、酿酒桶、染料桶、粉碎机、口粮压制机、工作台和无人机交易界面，以及在家里打开储物家具、邻居的篮子或小汽车后备箱时，背包面板多一个可搜索的「家中」标签，列出家里所有储物家具里的物品，并按这个设施能用的筛选。点击物品即可放进去（装了 Stacking 时，在粉碎机、工作台和无人机交易里每次放一件）。在无人机交易里，把已交出的物品拖回这个标签即可撤回。打开储物家具或后备箱时，点击物品会放进它里面。在篮子界面，邻居今天想要的物品标为「她想要」并排在最前。物品栏界面 Mods 菜单里的「家中储物」窗口也是同一个列表。装了 [url=https://www.nexusmods.com/survivallog/mods/81]Info Pack[/url]（可选；这里需要 0.8.4 或更新版本，面板里需要 0.8.7 或更新版本）时，鼠标指向物品会显示它的提示框。[/*]
[*][b]设施旁的储物标签[/b]：这些设施还会为每个放着它能用的物品的家中储物家具加一个背包标签，和游戏自带的储物标签并列，工作台也会用这些标签里的材料制作。[/*]
[*][b]家中的储物标签[/b]：在家里打开储物家具、邻居的篮子或小汽车后备箱时，每个家中储物家具都会在背包上方有一个标签，空的也有，方便在它们之间搬东西。标签太多时，用箭头或鼠标滚轮按整个标签滚动，按 A 和 D 切换时会自动滚到选中的标签。外出探索时，搜刮界面保持游戏原样。[/*]
[*][b]无人机送货[/b]：无人机回家时，它带回的物资会被放进对应标签的储物家具。卸空的无人机不会再提示你取回物资，这次送货也会计入要求取回物资的事件。有一个设置可以跳过机器人放置开关关闭的材料架。[/*]
[*][b]整理储物家具[/b]：打开储物家具时，大件物品会被排到右下方，小件通常排到左上方。模组会在几种排法里选出只有小件才能填的空格最少、给大件留空间最多的那种。同类物品在不占用额外空间时会放在一起。它还会把同种物品未满的堆叠补满。「整理」按钮也按同样的方式排列，无人机交易里的「一键整理」也一样（已交出的堆叠数量不变）。[/*]
[/list]
「留在背包」清单、无人机送货、整理和合并堆叠都可以在配置中单独关闭。

[size=5]留在背包[/size]
游戏无法拆分堆叠，所以「留在背包」清单按整堆保留：优先留下刚好够数量的最小一堆，否则从最满的堆开始留，直到够数；大小相同的堆先留更新鲜的。所以实际留下的常常比你设定的多一些。

清单保存在 [font=Courier New]BepInEx\config\com.dev.survivallog.storagehub.keep.json[/font]。[b]目前所有存档共用同一个清单。[/b]如果这个文件读不出来，模组会拒绝收纳背包和修改清单，而不是覆盖它；当前版本无法识别的文件会被另存为带 .broken- 的名字，然后重新开始一个空清单。

Storage Hub 不在你的存档里保存任何自己的数据，也不修改游戏文件。

[size=5]已知问题[/size]
[list]
[*]任何收纳都不会把物品放进无人机或工作台抽屉。「全部收进背包」会从无人机里取东西，所以在全部取出无人机正在运送的物品之前请先确认。[/*]
[*]架子、烹饪家具、堆肥箱、火锅、上锁的包和游戏的特殊背包永远不会被整理。[/*]
[*]点击「整理」后，游戏自己的排列会先显示一帧，然后才被紧凑的排列替换。[/*]
[*]无人机送货和合并堆叠要在序章结束后才会开始工作。[/*]
[/list]

[url=https://www.nexusmods.com/survivallog/mods/73]获取 Mod Manager[/url]，在游戏里设置所有模组。[url=https://sldb.core-directive.com/zh/]打开 Survival Log 维基[/url]，物品、配方、地图一查就有。

[size=5]前置要求[/size]
[list]
[*]Survival Log 1.1.19611。游戏更新后模组可能暂时失效，直到模组更新为止。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]，请先安装。[/*]
[*]可选：[url=https://www.nexusmods.com/survivallog/mods/74]Stacking[/url]，用于取出一堆中的一部分。[/*]
[*]可选：[url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.4.0 或更新版本。装了它，在 Mod Manager 的「模组」界面改动的设置无需重启游戏即可生效；没装的话，改动在下次启动游戏时生效。[/*]
[/list]

[size=5]安装[/size]
[list=1]
[*]安装 [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i]（如果之前没有装过其他模组，先启动一次游戏，让 BepInEx 完成初始化，然后退出）[/i]。[/*]
[*]可选（改动设置无需重启游戏）：用同样的方式安装 Exiled's Toolkit，下载它的压缩包并解压到游戏目录。[/*]
[*]在「文件」页下载压缩包。[/*]
[*]把压缩包解压到游戏目录（[font=Courier New]SurvivalLog.exe[/font] 所在的文件夹）。压缩包里带有 BepInEx 文件夹，DLL 会自动放到正确位置。如果提示合并文件夹，选择合并。解压后应为：
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\StorageHub.dll[/code][/*]
[/list]

[size=5]卸载[/size]
从 [font=Courier New]BepInEx\plugins[/font] 文件夹中删除 [font=Courier New]StorageHub.dll[/font]。模组移动过的物品会留在原处。

[size=5]配置[/size]
如需修改面板按键或关闭任何一项功能，可以在游戏内 Mod Manager 的「模组」界面修改（改动立即生效），也可以在游戏关闭时编辑 [font=Courier New]BepInEx\config\com.dev.survivallog.storagehub.cfg[/font]。配置文件中每项设置都有中文说明，完整说明见 [url=https://github.com/AnExiledDev/SurvivalLog-StorageHub/blob/main/CONFIG.md]CONFIG.md[/url]（英文）。没装 Exiled's Toolkit 的话，在「模组」界面的改动改为在下次启动游戏时生效。

[size=5]反馈[/size]
发现 bug 或有好点子？欢迎来 [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url] 打个招呼，或在本页的「Posts」标签里发帖。报告 bug 时，请附上 [font=Courier New]BepInEx\LogOutput.log[/font] 和 [font=Courier New]BepInEx\storage-hub.log[/font]。
想直接联系我，可以在 Discord 上给 [b]exiled.dev[/b] 发消息，我在 [url=https://discord.gg/MZGg4gQAUe]Survival Log 官方 Discord[/url] 里。也可以在 X 上找我：[url=https://x.com/AnExiledDev]@AnExiledDev[/url]。

[size=5]源代码与许可[/size]
基于 MIT 许可开源：[url=https://github.com/AnExiledDev/SurvivalLog-StorageHub]github.com/AnExiledDev/SurvivalLog-StorageHub[/url]
