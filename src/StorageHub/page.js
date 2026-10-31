// Storage Hub's web panel. Runs in Root.html, the document every game page iframe sits in, and draws over all of
// them. Styled with the game's own colours and fonts so it reads as part of the game. Talks to the plugin through vuplex.postMessage("slmod|storagehub|<verb>|<args>") and is driven by window.__storagehub.
(function () {
  if (window.__storagehub) return;

  var PREFIX = 'slmod|storagehub|';
  var HUD_CLASS = 'sh-hud-btn';
  // Storage Hub's place in the HUD's tool column when Exiled's Toolkit lays it out in columns of four.
  var DOCK_ORDER = '60';
  var SVG_NS = 'http://www.w3.org/2000/svg';
  // A chest with its lid and clasp, on the 24-unit grid the game's HUD icons use.
  var CHEST_PATH = 'M4 4h16a1 1 0 0 1 1 1v4H3V5a1 1 0 0 1 1-1zm-1 7h7v2h4v-2h7v8a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1z';
  var TIP_ID = 'sh-tip';
  var TIP_DELAY_MS = 300;
  // Styled inline, so the tip looks the same in any page it is drawn in, the Home tab's station pages included.
  var TIP_STYLE = 'position:fixed;z-index:2147483600;display:none;max-width:320px;padding:8px 12px;border-radius:4px;border:1px solid rgba(255,200,58,.45);background:linear-gradient(180deg,#3a3129,#241e18);box-shadow:0 8px 24px rgba(0,0,0,.6);font-family:"Microsoft YaHei","PingFang SC",sans-serif;font-size:13px;line-height:1.45;color:#f0e8d8;white-space:pre-line;text-align:left;pointer-events:none';
  var TIP_TITLE_STYLE = 'display:block;margin-bottom:2px;font-size:14px;font-weight:700;color:#FFC83A';
  var KIND_WORD = { bag: 'kindBag', storage: 'kindStorage', drawer: 'kindDrawer', basket: 'kindBasket' };
  var BUSY_TIMEOUT_MS = 5000;

  var TIMES = '\u00d7';
  var MINUS = '\u2212';
  var MAX_KEEP = 9999;
  // A run of +/- clicks is saved once, this long after the last one.
  var KEEP_SAVE_DELAY_MS = 350;
  var MAX_KEEP_RESULTS = 60;

  // view is 'items', 'rot' (Rotting soon) or 'keep' (Keep in bag).
  var state = { data: { storages: [], items: [], rot: [], keep: { on: true, rows: [] } }, query: '', selected: null, place: null, isPlaceOpen: false, isOpen: false, view: 'items', catalog: [], isCatalogAsked: false };
  // Amounts the player has changed and the game has not yet answered, by item id: { amount, timer, isSent }.
  var pendingKeep = {};
  var ui = null;
  var isBusy = false;
  var busyTimer = 0;
  // A redraw between a mouse press and its click would take the pressed button away and lose the click, so redraws
  // asked for in between wait for the mouse to come up.
  var isPointerDown = false;
  var isRedrawHeld = false;
  // The take slider's last amount per item and storage ("item|storage"), so a refresh does not reset it.
  var takeAmounts = {};
  // Whether a save is loaded, so the HUD button shows, and the panel key its tip names.
  var hud = { isLive: false, key: '', button: null };

  // The panel's words, used until the plugin sends them in the game's language (window.__storagehubWords) before each call.
  var ENGLISH = {
    search: 'Search items...',
    searchKeep: 'Search every item to add it to the keep list...',
    rotView: 'Rotting soon',
    rotTip: 'Food in your bag, your storages and on the floor at home, soonest to spoil first',
    keepView: 'Keep in bag',
    keepTip: 'Items putting the bag away always leaves in your bag, and how many of each',
    putAwayBag: 'Put whole bag away by tag',
    putAwayFloor: 'Put floor items away by tag',
    closeKeys: '{0} / Esc to close',
    escClose: 'Esc to close',
    subItems: '{0} kinds of item across {1} storages and your bag',
    subRot: 'Food that spoils, soonest first, across {0} storages, your bag and the floor at home',
    subKeep: 'Putting the bag away leaves these in your bag',
    noMatch: 'Nothing matches that search.',
    noItems: 'No items found.',
    keep: 'Keep',
    keepN: 'Keep {0}',
    keepOnTip: 'On the keep list: open it to change the amount',
    keepAddTip: 'Add to the keep list: putting the bag away leaves this many in your bag',
    keepOff: 'The keep list is turned off in the config (Keep, Enabled), so putting the bag away moves everything.',
    keepEmpty: 'Nothing on the keep list yet. Find an item on the right, or press the bell on an item in the Items list.',
    keepRemoveTip: 'Take off the keep list',
    heldNone: '{0} in your bag, none kept',
    held: '{0} in your bag',
    heldShort: '{0} in your bag, {1} short',
    keepLess: 'Keep one fewer',
    keepMore: 'Keep one more',
    keepAmountTip: 'How many to keep in your bag',
    keepAddTitle: 'Add to the keep list',
    noName: 'No item has that name.',
    bagEmpty: 'Your bag is empty. Type a name above to find any item.',
    moreResults: '{0} more. Type more of the name to narrow it down.',
    hintBag: 'In your bag now. Type a name above to search every item in the game.',
    hintAll: 'Every item in the game',
    hintLoading: 'Items you own (the full item list is still loading)',
    inBag: '{0} in bag',
    keeping: 'Keeping {0}',
    addKeep: '+ Keep',
    addKeepTip: 'Add to the keep list, keeping {0}',
    nothingSpoils: 'Nothing here spoils.',
    floorOnlyTip: 'Only on the floor: "Put floor away" picks it up',
    pickItem: 'Pick an item on the left.',
    itemsView: 'Items',
    goBag: 'Put bag away',
    goFloor: 'Put floor away',
    allPlaces: 'All places',
    placeTip: 'Show only the items in one place',
    totalPlaces: '{0} in total, in {1} places',
    totalPlace: '{0} in total, in one place',
    keepingBag: 'keeping {0} in your bag',
    whereIs: 'Where it is',
    rotSpoiled: 'Spoiled',
    rotDay: 'Under a day',
    rotWeek: 'This week',
    rotLater: 'Later',
    takeAll: 'Take all into bag',
    putAwayTag: 'Put away by tag',
    putAway: 'Put away',
    take: 'Take',
    kindBag: 'Bag',
    kindStorage: 'Storage',
    kindDrawer: 'Drawer',
    kindBasket: 'Basket',
    menuPutAwayTip: 'Move everything in your bag into the storages whose tags want it',
    menuFloor: 'Put away floor',
    menuFloorTip: 'Move everything lying on the floor at home into the storages whose tags want it, and the rest into your bag',
    menuHomeTip: 'Search the storages at home and take items from them',
    homeBag: 'Bag',
    homeTab: 'Home',
    homeTitle: 'Home storage',
    homeSub: '{0} storages',
    homeSearch: 'Search every storage...',
    scopeCooking: 'For cooking',
    scopeRatCage: 'For the rat cage',
    scopeBrew: 'For brewing',
    scopeDye: 'For dyeing',
    scopeShredder: 'For the shredder',
    scopeCompressor: 'For the ration press',
    scopeTrade: 'For trading',
    scopeWorkbench: 'For the workbench',
    scopeBasket: 'For the basket',
    scopeStorage: 'For this storage',
    scopeAll: 'Everything',
    homeOpen: 'Open',
    homeClose: 'Close',
    homeUsable: '{0} usable',
    homeItems: '{0} items',
    tagUse: 'Use',
    tagBag: 'To bag',
    tagWish: 'Wanted',
    hintStation: 'Use goes into the station. To bag goes into your bag.',
    hintBasket: 'Use and Wanted go into the basket. Wanted is what she asks for now. To bag goes into your bag.',
    hintStorage: 'Click an item to put it in the open storage. Its own tab is beside Home.',
    hintInv: 'Click an item to take it into your bag, or Open a storage beside your bag.',
    homeNoMatch: 'Nothing in your storages matches that search.',
    homeNone: 'No storage holds anything for this station.',
    homeEmpty: 'Your home storages are empty.',
    homeLoading: 'Reading your storages...',
    hudTip: 'Search every storage at home, take items out, and keep what you need in your bag',
    itemsTip: 'Everything in your bag and your home storages',
    closeTip: 'Close the panel',
    takeAllTip: 'Take every one from every storage into your bag',
    putAwayTagTip: 'Put the ones in your bag into the storages whose tags want them',
    takeN: 'Take {0}',
    takeSomeTip: 'Take {0} from {1} into your bag',
    takeHere: 'Take all',
    takeHereTip: 'Take every one from {0} into your bag',
    amountTip: 'Drag to pick how many to take',
    noSplit: 'Taking part of a stack needs the Stacking mod, so each place is taken whole.',
    fill: 'Fill bag from storage',
    fillTip: 'Take what your bag is short of from your home storages (never from a drone)',
    fillShort: '{0} short, {1} can be pulled from storage',
    fillDone: 'Your bag holds everything on the list.',
    stored: '{0} in storage',
    storedTip: 'How many your home storages hold, which Fill can take',
    storedNone: 'None in storage',
    storedNoneTip: 'No home storage holds any, so Fill cannot bring it',
    homeOpenTip: 'Show this storage beside your bag',
    homeTabTip: 'Show the tab of this storage',
    homeCloseTip: 'Close this storage again',
    homeShutTip: 'Close the home storage list',
    scopeUsableTip: 'Show only what this station can use',
    tabsBackTip: 'Scroll the tabs back',
    tabsOnTip: 'Scroll to more tabs',
    scopeAllTip: 'Show everything in your storages',
    homeItemTip: '{0} in {1}'
  };

  function words() {
    return window.__storagehubWords || ENGLISH;
  }

  // say('held', 3) fills {0} with 3; a key the plugin's words lack falls back to English.
  function say(key) {
    var args = arguments;
    var text = words()[key] || ENGLISH[key] || key;

    return text.replace(/\{(\d)\}/g, function (all, index) { return String(args[Number(index) + 1]); });
  }

  function send(message) {
    try {
      if (window.vuplex) window.vuplex.postMessage(PREFIX + message);
    } catch (e) {}
  }

  // One move at a time: a double click would otherwise queue the same move twice. Every move is answered by a status
  // line, which frees the buttons; the timer frees them anyway if that answer never comes.
  function sendAction(message) {
    if (isBusy) return;

    isBusy = true;
    clearTimeout(busyTimer);
    busyTimer = setTimeout(clearBusy, BUSY_TIMEOUT_MS);
    send(message);
  }

  function clearBusy() {
    isBusy = false;
    clearTimeout(busyTimer);
  }

  // Hands a panel to Exiled's Toolkit to be drawn at the player's UI size. Before the Toolkit's script has loaded it
  // waits in a list the Toolkit reads; without the Toolkit nothing happens.
  function uiSize(node, options) {
    options = Object.assign({ mod: 'StorageHub' }, options);
    var toolkit = window.ExiledUi;

    if (toolkit) {
      toolkit.size(node, options);
      return;
    }

    var waiting = window.__exiledUiWait = window.__exiledUiWait || [];
    waiting.push([node, options]);
    if (waiting.length > 32) waiting.shift();
  }

  // The zoom node is drawn at by the panels the Toolkit sizes that hold it, its own included; 1 without the Toolkit.
  function uiZoom(node) {
    var toolkit = window.ExiledUi;

    if (!toolkit || typeof toolkit.zoomAt !== 'function') return 1;

    try {
      return Number(toolkit.zoomAt(node)) || 1;
    } catch (e) {
      return 1;
    }
  }

  function el(tag, className, text) {
    var node = document.createElement(tag);

    if (className) node.className = className;
    if (text != null) node.textContent = text;

    return node;
  }

  function button(text, action, extraClass) {
    var node = el('button', 'sh-btn' + (extraClass ? ' ' + extraClass : ''), text);
    node.setAttribute('data-act', action);

    return node;
  }

  function actButton(className, text, action) {
    var node = el('button', className, text);
    node.setAttribute('data-act', action);

    return node;
  }
  var BELL_ICON = '<svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M12 2a6 6 0 0 0-6 6v4l-2 3v1h16v-1l-2-3V8a6 6 0 0 0-6-6zm-2 16a2 2 0 0 0 4 0z"/></svg>';
  var LOOK_ICON = '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#a89a82" stroke-width="2.2"><circle cx="11" cy="11" r="7"/><path d="m20 20-4-4"/></svg>';
  var TONE_ICON = {
    ok: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3"><path d="m5 12 5 5 9-10"/></svg>',
    warn: '<svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M12 3 2 21h20zm-1 6h2v6h-2zm0 8h2v2h-2z"/></svg>',
    bad: '<svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M12 3 2 21h20zm-1 6h2v6h-2zm0 8h2v2h-2z"/></svg>'
  };
  var DOT = ' \u00b7 ';
  var DAY_HOURS = 24;
  var WEEK_HOURS = 168;

  var CSS = [
    '#sh-root{--accent:#FFC83A;--glow:rgba(255,200,58,.35);--text:#f0e8d8;--text2:#cbbfa8;--text3:#a89a82;--text4:#7a6e5a;--line:rgba(220,188,120,.2);--soft:rgba(220,188,120,.11);--card:rgba(70,58,44,.55);--ok:#8cc56a;--warn:#e9b955;--no:#e0876a}',
    '#sh-root{position:fixed;inset:0;z-index:2147483000;display:none;align-items:center;justify-content:center;background:rgba(10,6,2,.5);font-family:"Microsoft YaHei","PingFang SC",sans-serif;color:var(--text);user-select:none}',
    '#sh-root *{box-sizing:border-box}',
    '.sh-plate{position:relative;width:min(1180px,92vw);height:min(800px,86vh);display:flex;flex-direction:column;padding:20px 22px 16px;border-radius:4px;border:1px solid rgba(220,188,120,.28);background:radial-gradient(ellipse at 15% 0%,rgba(150,100,40,.28),transparent 55%),linear-gradient(180deg,#3a3129 0%,#2b241d 55%,#241e18 100%);box-shadow:0 30px 90px rgba(0,0,0,.7),inset 0 1px 0 rgba(255,230,180,.08)}',
    '.sh-plate::before{content:"";position:absolute;top:0;left:0;right:0;height:2px;background:linear-gradient(90deg,transparent 10%,var(--accent) 50%,transparent 90%);opacity:.6}',
    '.sh-head{display:flex;align-items:center;gap:14px;flex:none}',
    '.sh-title{display:flex;align-items:center;font-size:22px;font-weight:700;letter-spacing:5px;text-transform:uppercase;color:#fff}',
    '.sh-title::before{content:"";width:4px;height:20px;margin-right:12px;background:var(--accent);box-shadow:0 0 10px var(--glow)}',
    '.sh-sub{flex:1;min-width:0;font-size:13px;color:var(--text3);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.sh-x{width:30px;height:30px;padding:0;border-radius:4px;border:1px solid var(--line);background:none;color:var(--text3);font:inherit;font-size:18px;line-height:28px;cursor:pointer}',
    '.sh-x:hover{color:var(--accent);border-color:var(--accent)}',
    '.sh-tabs{display:flex;gap:8px;margin-top:14px;padding-bottom:12px;border-bottom:1px solid var(--soft);flex:none}',
    '.sh-tab{display:inline-flex;align-items:center;gap:7px;padding:7px 16px;border-radius:4px;border:1px solid var(--line);background:rgba(255,240,210,.04);color:var(--text2);font:inherit;font-size:14px;cursor:pointer}',
    '.sh-tab:hover{border-color:rgba(255,200,58,.6);color:#fff}',
    '.sh-tab.sh-on{color:var(--accent);background:rgba(255,200,58,.13);border-color:var(--accent);box-shadow:0 0 12px rgba(255,200,58,.16)}',
    '.sh-n{min-width:18px;padding:0 5px;border-radius:8px;text-align:center;font-size:11px;background:rgba(255,240,210,.1);color:var(--text2)}',
    '.sh-tab.sh-on .sh-n{background:var(--accent);color:#2a1f10;font-weight:700}',
    '.sh-tab .sh-n.sh-bad{background:var(--no);color:#2a1208;font-weight:700}',
    '.sh-body{flex:1;min-height:0;display:flex;gap:14px;margin-top:14px}',
    '.sh-col{display:flex;flex-direction:column;min-height:0;padding:14px 16px;border-radius:6px;background:rgba(30,24,18,.45);border:1px solid var(--soft)}',
    '.sh-left{width:48%;flex:none}',
    '.sh-right{flex:1;min-width:0}',
    '.sh-detail{flex:1;min-height:0;display:flex;flex-direction:column}',
    '.sh-tools{display:flex;gap:8px;flex:none}',
    '.sh-slot{display:flex;flex:none}','.sh-slot:empty{display:none}','.sh-slot .sh-tools{flex:1;min-width:0}',
    '.sh-find{flex:1;display:flex;align-items:center;gap:8px;height:36px;padding:0 12px;border-radius:4px;border:1px solid var(--line);background:rgba(20,14,8,.5)}',
    '.sh-find:focus-within{border-color:rgba(255,200,58,.7)}',
    '.sh-search{flex:1;min-width:0;height:100%;padding:0;font:inherit;font-size:14px;color:var(--text);background:none;border:none;outline:none;user-select:text}',
    '.sh-search::placeholder{color:var(--text4)}',
    '.sh-sel{position:relative;flex:none;max-width:42%;height:36px;display:flex;align-items:center;gap:8px;padding:0 12px;border-radius:4px;border:1px solid var(--line);background:rgba(20,14,8,.5);color:var(--text2);font:inherit;font-size:13px;cursor:pointer;white-space:nowrap}',
    '.sh-sel:hover,.sh-sel.sh-on{border-color:rgba(255,200,58,.6)}',
    '.sh-sel span{overflow:hidden;text-overflow:ellipsis}',
    '.sh-menu{position:absolute;right:0;top:40px;z-index:3;min-width:200px;max-height:360px;overflow-y:auto;padding:4px;border-radius:4px;border:1px solid var(--line);background:#2b241d;box-shadow:0 10px 30px rgba(0,0,0,.6);text-align:left}',
    '.sh-menu div{padding:6px 10px;border-radius:3px;color:var(--text2);overflow:hidden;text-overflow:ellipsis}',
    '.sh-menu div:hover{background:rgba(255,200,58,.1);color:#fff}',
    '.sh-menu div.sh-on{color:var(--accent)}',
    '.sh-list{flex:1;min-height:0;overflow-y:auto;margin-top:10px;padding-right:6px;display:flex;flex-direction:column;gap:5px}',
    '.sh-list>*{flex:none}',
    '.sh-list::-webkit-scrollbar{width:5px}',
    '.sh-list::-webkit-scrollbar-thumb{background:rgba(255,200,58,.3);border-radius:3px}',
    '.sh-row{display:flex;align-items:center;gap:12px;padding:6px 10px;border-radius:4px;background:var(--card);border:1px solid var(--soft);cursor:pointer}',
    '.sh-row:hover{background:rgba(84,70,52,.7);border-color:rgba(255,200,58,.35)}',
    '.sh-row.sh-active{border-color:var(--accent);box-shadow:inset 0 0 0 1px rgba(255,200,58,.25)}',
    '.sh-row.sh-still{cursor:default;opacity:.75}',
    '.sh-row.sh-still:hover{background:var(--card);border-color:var(--soft)}',
    '.sh-icon{flex:none;display:flex;align-items:center;justify-content:center;border-radius:4px;background:rgba(10,8,6,.55);border:1px solid rgba(0,0,0,.5);overflow:hidden;color:var(--text3);font-weight:600}',
    '.sh-icon img{width:86%;height:86%;object-fit:contain}',
    '.sh-i32{width:32px;height:32px;font-size:14px}',
    '.sh-i42{width:42px;height:42px;font-size:17px}',
    '.sh-i72{width:72px;height:72px;font-size:28px}',
    '.sh-main{flex:1;min-width:0}',
    '.sh-name{font-size:15px;font-weight:700;color:#fff;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.sh-where{font-size:12px;color:var(--text3);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.sh-where b{color:var(--text2);font-weight:600}',
    '.sh-where .sh-bagat,.sh-where .sh-bagat b{color:var(--accent)}',
    '.sh-count{min-width:40px;text-align:right;font-size:16px;font-weight:700;color:var(--accent)}',
    '.sh-bell{position:relative;flex:none;width:28px;height:28px;display:flex;align-items:center;justify-content:center;gap:6px;padding:0;border-radius:4px;border:1px solid var(--line);background:none;color:var(--text4);font:inherit;cursor:pointer;opacity:0}',
    '.sh-row:hover .sh-bell,.sh-bell.sh-on,.sh-bell.sh-wide{opacity:1}',
    '.sh-bell:hover{border-color:rgba(255,200,58,.6);color:var(--text2)}',
    '.sh-bell.sh-on{color:var(--accent);border-color:rgba(255,200,58,.5)}',
    '.sh-bell em{position:absolute;top:-7px;right:-7px;padding:0 4px;border-radius:3px;font-style:normal;font-size:10px;font-weight:700;background:var(--accent);color:#2a1f10}',
    '.sh-bell.sh-wide{width:auto;height:32px;margin-left:auto;padding:0 12px;font-size:13px;color:var(--text2)}',
    '.sh-bell.sh-wide.sh-on{color:var(--accent)}',
    '.sh-dhead{display:flex;align-items:center;gap:16px;flex:none}',
    '.sh-dname{font-size:20px;font-weight:700;color:#fff}',
    '.sh-dsub{margin-top:3px;font-size:13px;color:var(--text3)}',
    '.sh-actions{display:flex;align-items:center;gap:8px;margin-top:14px;flex:none}',
    '.sh-h2{display:flex;align-items:center;gap:8px;margin:18px 0 8px;font-size:12px;font-weight:700;letter-spacing:2px;text-transform:uppercase;color:var(--text2);flex:none}',
    '.sh-h2::after{content:"";flex:1;height:1px;background:linear-gradient(90deg,var(--line),transparent)}',
    '.sh-slot+.sh-h2{margin-top:14px}',
    '.sh-locs{flex:1;min-height:0;overflow-y:auto;display:flex;flex-direction:column;gap:5px;padding-right:6px}',
    '.sh-locs>*{flex:none}',
    '.sh-locs::-webkit-scrollbar{width:5px}',
    '.sh-locs::-webkit-scrollbar-thumb{background:rgba(255,200,58,.3);border-radius:3px}',
    '.sh-loc{display:grid;grid-template-columns:70px minmax(0,1fr) 50px 110px;align-items:center;gap:10px;padding:7px 10px;border-radius:4px;background:var(--card);border:1px solid var(--soft)}',
    '.sh-kind{font-size:11px;font-weight:700;letter-spacing:1px;text-transform:uppercase;color:var(--text3)}',
    '.sh-loc.sh-bag{border-color:rgba(255,200,58,.35);background:rgba(255,200,58,.07)}',
    '.sh-loc.sh-bag .sh-kind{color:var(--accent)}',
    '.sh-loc.sh-drawer .sh-kind{color:#c9a77a}',
    '.sh-loc .sh-lname{font-size:14px;color:#fff;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.sh-loc .sh-lcount{text-align:right;font-weight:700;color:var(--text)}',
    '.sh-loc .sh-add{width:100%}',
    '.sh-amt{grid-column:2/-1;display:grid;grid-template-columns:minmax(0,1fr) 110px;align-items:center;gap:10px}',
    '.sh-range{width:100%;height:18px;margin:0;accent-color:#FFC83A;cursor:pointer}',
    '.sh-fill{display:flex;align-items:center;gap:10px;padding:8px 10px;border-radius:4px;border:1px solid rgba(255,200,58,.3);background:rgba(255,200,58,.06)}',
    '.sh-fill .sh-hint{flex:1;min-width:0}',
    '.sh-add.sh-dim{opacity:.5}',
    '.sh-chip.sh-none{color:#2a1208;background:var(--no);font-weight:700}',
    '.sh-add{height:30px;padding:0 14px;border-radius:4px;border:1px solid rgba(255,200,58,.6);background:rgba(255,200,58,.12);color:var(--accent);font:inherit;font-size:13px;font-weight:700;cursor:pointer;white-space:nowrap}',
    '.sh-add:hover{background:rgba(255,200,58,.22)}',
    '.sh-add.sh-sm{height:26px;padding:0 10px;font-size:12px}',
    '.sh-btn{height:34px;padding:0 14px;border-radius:4px;border:1px solid var(--line);background:linear-gradient(180deg,rgba(92,78,60,.9),rgba(66,55,42,.92));color:var(--text);font:inherit;font-size:13px;cursor:pointer;white-space:nowrap}',
    '.sh-btn.sh-sm{height:30px}',
    '.sh-btn:hover{border-color:var(--accent)}',
    '.sh-go{height:46px;padding:0 26px;border-radius:4px;border:1px solid #ffd36a;background:linear-gradient(180deg,#ffd25a,#e8a91c);color:#2a1f10;font:inherit;font-size:16px;font-weight:800;letter-spacing:2px;text-transform:uppercase;box-shadow:0 0 16px rgba(255,200,58,.25);cursor:pointer;white-space:nowrap}',
    '.sh-go:hover{background:linear-gradient(180deg,#ffdc78,#f0b42c)}',
    '.sh-tab,.sh-row,.sh-btn,.sh-add,.sh-bell,.sh-sel{transition:background-color .12s,border-color .12s,color .12s,opacity .12s}',
    '.sh-foot{display:flex;align-items:center;gap:10px;margin-top:14px;flex:none}',
    '.sh-status{flex:1;min-width:0;display:flex;align-items:center;gap:8px;font-size:13px;color:var(--text3)}',
    '.sh-status svg{flex:none}',
    '.sh-status span{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.sh-status.sh-ok{color:var(--ok)}',
    '.sh-status.sh-warn{color:var(--warn)}',
    '.sh-status.sh-bad{color:var(--no)}',
    '.sh-keys{margin-right:6px;font-size:12px;color:var(--text4);white-space:nowrap}',
    '.sh-empty{margin:auto;padding:20px;text-align:center;font-size:13px;color:var(--text3)}',
    '.sh-hint{flex:none;font-size:13px;color:var(--text3)}',
    '.sh-g{display:flex;align-items:center;gap:8px;padding:8px 2px 2px;font-size:12px;font-weight:700;letter-spacing:2px;text-transform:uppercase;color:var(--text2)}',
    '.sh-g::after{content:"";flex:1;height:1px;background:linear-gradient(90deg,var(--line),transparent)}',
    '.sh-g .sh-gc{order:2;font-weight:400;letter-spacing:0;color:var(--text4)}',
    '.sh-g:first-child{padding-top:0}',
    '.sh-ttl{flex:none;min-width:72px;padding:3px 8px;border-radius:3px;text-align:center;font-size:12px;font-weight:700;color:var(--text2);background:rgba(255,240,210,.07)}',
    '.sh-ttl.sh-warn{color:var(--warn);background:rgba(233,185,85,.12)}',
    '.sh-ttl.sh-bad{color:#2a1208;background:var(--no)}',
    '.sh-row.sh-keep{cursor:default;padding:7px 10px}',
    '.sh-row.sh-keep:hover{background:var(--card);border-color:var(--soft)}',
    '.sh-keep-zero .sh-name{color:var(--text3)}',
    '.sh-held{font-size:12px;color:var(--text3)}',
    '.sh-held-short{color:var(--warn)}',
    '.sh-meter{height:4px;margin-top:5px;border-radius:2px;background:rgba(255,240,210,.08);overflow:hidden}',
    '.sh-meter i{display:block;height:100%;background:var(--ok)}',
    '.sh-meter i.sh-low{background:var(--warn)}',
    '.sh-step{flex:none;display:flex;align-items:center;height:30px;border:1px solid var(--line);border-radius:4px;overflow:hidden;background:rgba(20,14,8,.5)}',
    '.sh-step .sh-stepb{width:28px;height:100%;padding:0;border:none;background:rgba(255,240,210,.06);color:var(--text2);font:inherit;font-size:15px;cursor:pointer}',
    '.sh-step .sh-stepb:hover{color:var(--accent);background:rgba(255,200,58,.12)}',
    '.sh-amount{width:52px;height:100%;padding:0 4px;text-align:center;font:inherit;font-size:14px;font-weight:700;color:var(--accent);background:transparent;border:none;outline:none;user-select:text}',
    '.sh-amount:focus{background:rgba(255,200,58,.14)}',
    '.sh-amount::-webkit-outer-spin-button,.sh-amount::-webkit-inner-spin-button{-webkit-appearance:none;margin:0}',
    '.sh-del{flex:none;width:26px;height:26px;padding:0;border:none;border-radius:4px;background:none;color:var(--text4);font:inherit;font-size:16px;cursor:pointer}',
    '.sh-del:hover{color:var(--no);background:rgba(224,135,106,.12)}',
    '.sh-banner{flex:none;padding:8px 10px;border-radius:4px;border:1px solid rgba(224,135,106,.6);background:rgba(224,135,106,.12);font-size:13px;color:#f0c0ab}',
    '.sh-pick{display:flex;align-items:center;gap:10px;padding:5px 8px;border-radius:4px;border:1px solid transparent}',
    '.sh-pick:hover{background:rgba(255,240,210,.05);border-color:var(--soft)}',
    '.sh-pick .sh-pname{flex:1;min-width:0;font-size:14px;color:#fff;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.sh-chip{flex:none;padding:1px 7px;border-radius:3px;font-size:11px;color:var(--text3);background:rgba(255,240,210,.07)}',
    '.sh-chip.sh-on{color:var(--accent);background:rgba(255,200,58,.12)}'
  ].join('\n');

  function build() {
    if (ui && document.body.contains(ui.root)) return;

    addPanelStyle();

    var root = el('div');
    root.id = 'sh-root';
    var plate = el('div', 'sh-plate');
    var head = el('div', 'sh-head');
    var sub = el('div', 'sh-sub');
    head.appendChild(el('div', 'sh-title', 'Storage Hub'));
    head.appendChild(sub);
    var x = el('button', 'sh-x', TIMES);
    x.setAttribute('data-act', 'close');
    head.appendChild(x);

    var tabs = el('div', 'sh-tabs');
    var tabNodes = {};
    ['items', 'rot', 'keep'].forEach(function (view) {
      var tab = withArgs(el('button', 'sh-tab'), view);
      tab.setAttribute('data-act', 'view');
      tab.appendChild(el('span'));
      tab.appendChild(el('span', 'sh-n'));
      tabs.appendChild(tab);
      tabNodes[view] = tab;
    });

    // The search and the place filter sit above the list, or above the add list on the right in the keep view.
    var tools = el('div', 'sh-tools');
    var find = el('div', 'sh-find');
    find.innerHTML = LOOK_ICON;
    var search = el('input', 'sh-search');
    search.type = 'text';
    find.appendChild(search);
    var place = el('div', 'sh-sel');
    tools.appendChild(find);
    tools.appendChild(place);

    var body = el('div', 'sh-body');
    var left = el('div', 'sh-col sh-left');
    var leftTools = el('div', 'sh-slot');
    var list = el('div', 'sh-list');
    var right = el('div', 'sh-col sh-right');
    var rightTools = el('div', 'sh-slot');
    var detail = el('div', 'sh-detail');
    leftTools.appendChild(tools);
    left.appendChild(leftTools);
    left.appendChild(list);
    right.appendChild(rightTools);
    right.appendChild(detail);
    body.appendChild(left);
    body.appendChild(right);

    var foot = el('div', 'sh-foot');
    var status = el('div', 'sh-status');
    var closeKeys = el('span', 'sh-keys');
    var floorButton = button('', 'putAwayFloor');
    var bagButton = el('button', 'sh-go');
    bagButton.setAttribute('data-act', 'putAwayBag');
    foot.appendChild(status);
    foot.appendChild(closeKeys);
    foot.appendChild(floorButton);
    foot.appendChild(bagButton);

    plate.appendChild(head);
    plate.appendChild(tabs);
    plate.appendChild(body);
    plate.appendChild(foot);
    root.appendChild(plate);

    document.body.appendChild(root);
    // The mask fills the screen at any size, so the plate on it is what follows the UI size and has to fit.
    uiSize(plate);

    root.addEventListener('click', onClick);
    watchTips(document, root);
    // Clicks inside the plate must not reach the game pages underneath; the mask itself closes nothing on purpose.
    root.addEventListener('mousedown', function (e) {
      isPointerDown = true;
      e.stopPropagation();
    });
    // The same function every build, so a rebuilt panel does not add a second listener.
    document.addEventListener('mouseup', releasePointer, true);
    search.addEventListener('input', function () {
      state.query = search.value;

      if (state.view === 'keep') renderDetail();
      else renderList();
    });
    search.addEventListener('focus', function () { send('focus|1'); });
    search.addEventListener('blur', function () { send('focus|0'); });
    search.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') send('close');
      e.stopPropagation();
    });
    // A broken icon path swaps to the first-letter placeholder; error events do not bubble, so capture them.
    root.addEventListener('error', function (e) {
      var img = e.target;

      if (img && img.tagName === 'IMG' && img.parentNode) img.parentNode.textContent = img.getAttribute('data-letter') || '?';
    }, true);

    ui = { root: root, x: x, sub: sub, tabs: tabNodes, tools: tools, leftTools: leftTools, rightTools: rightTools, search: search, place: place, bagButton: bagButton, floorButton: floorButton, closeKeys: closeKeys, list: list, detail: detail, status: status };
  }

  // The click comes straight after mouseup, so the held redraw runs once it is through.
  function releasePointer() {
    if (!isPointerDown) return;

    isPointerDown = false;
    setTimeout(redrawHeld, 0);
  }

  function redrawHeld() {
    if (!isRedrawHeld) return;

    isRedrawHeld = false;

    if (state.isOpen) render();
  }

  // The style outlives a lost panel root, so a rebuild must not stack another copy of it.
  function addPanelStyle() {
    if (document.getElementById('sh-style')) return;

    var style = el('style');
    style.id = 'sh-style';
    style.textContent = CSS;
    document.head.appendChild(style);
  }

  function onClick(e) {
    var target = e.target.closest('[data-act],[data-item]');
    var act = target ? target.getAttribute('data-act') : null;

    // Any click outside the place menu closes it.
    if (state.isPlaceOpen && act !== 'placeMenu' && act !== 'placePick') {
      state.isPlaceOpen = false;
      renderPlace();
    }

    if (!target) return;

    e.stopPropagation();
    var item = target.getAttribute('data-item');

    if (item != null) {
      state.selected = item;
      renderList();
      renderDetail();

      return;
    }

    var args = target.getAttribute('data-args');

    // Switching lists is the page's own business; the game is only asked for the item catalog the keep view searches.
    if (act === 'view') {
      showView(args);

      return;
    }

    if (act === 'placeMenu') {
      state.isPlaceOpen = !state.isPlaceOpen;
      renderPlace();

      return;
    }

    if (act === 'placePick') {
      state.place = args || null;
      state.isPlaceOpen = false;
      ui.list.scrollTop = 0;
      renderPlace();
      renderList();

      return;
    }

    if (Object.prototype.hasOwnProperty.call(KEEP_ACTIONS, act)) {
      KEEP_ACTIONS[act](args);

      return;
    }

    if (act === 'close') {
      send(act);

      return;
    }

    if (act === 'keepFill') flushPendingKeeps();

    sendAction(args ? act + '|' + args : act);
  }

  function showView(view) {
    state.view = view;
    state.isPlaceOpen = false;
    ui.list.scrollTop = 0;

    if (state.view === 'keep') askForCatalog();

    render();
  }

  function askForCatalog() {
    if (state.isCatalogAsked) return;

    state.isCatalogAsked = true;
    send('catalog');
  }

  // Keep list edits are not moves, so they skip the one-move-at-a-time lock and a run of +/- clicks stays responsive.
  var KEEP_ACTIONS = {
    keepAdd: function (id) { setKeep(id, suggestedKeep(id)); },
    keepStep: function (args) {
      var parts = String(args).split('|');
      var id = parts[0];

      setKeep(id, keptAmount(id) + Number(parts[1]));
    },
    keepRemove: function (id) {
      dropPendingKeep(id);
      send('keepRemove|' + id);
    },
    // The Keep button on an item list row: adds the item, or once it is on the list, opens the list.
    quickKeep: function (id) {
      if (keepRow(id)) {
        showView('keep');

        return;
      }

      setKeep(id, suggestedKeep(id));
    }
  };

  function setKeep(id, amount) {
    recordKeep(id, amount);
    render();
  }

  // Shown at once and saved once the clicks stop, so the game writes the file once per run of clicks.
  function recordKeep(id, amount) {
    var clamped = Math.max(0, Math.min(MAX_KEEP, Math.floor(amount) || 0));

    dropPendingKeep(id);
    pendingKeep[id] = { amount: clamped, isSent: false, timer: setTimeout(function () { sendKeep(id); }, KEEP_SAVE_DELAY_MS) };
  }

  function sendKeep(id) {
    var pending = pendingKeep[id];

    if (!pending) return;

    pending.isSent = true;
    send('keepSet|' + id + '|' + pending.amount);
  }

  function flushPendingKeeps() {
    Object.keys(pendingKeep).forEach(function (id) {
      if (pendingKeep[id].isSent) return;

      clearTimeout(pendingKeep[id].timer);
      sendKeep(id);
    });
  }

  function dropPendingKeep(id) {
    if (pendingKeep[id]) clearTimeout(pendingKeep[id].timer);

    delete pendingKeep[id];
  }

  // Every edit is answered by a status line and then the new view; once both are in, the view is the truth again,
  // which also puts back an amount the game could not save.
  function settleSentKeeps() {
    setTimeout(function () {
      var isChanged = false;

      Object.keys(pendingKeep).forEach(function (id) {
        if (!pendingKeep[id].isSent) return;

        delete pendingKeep[id];
        isChanged = true;
      });

      if (isChanged && state.isOpen) render();
    }, 300);
  }

  function keepRows() {
    return (state.data.keep && state.data.keep.rows) || [];
  }

  function keepRow(id) {
    var rows = keepRows();

    for (var i = 0; i < rows.length; i++) {
      if (String(rows[i].id) === String(id)) return rows[i];
    }

    return null;
  }

  function keptAmount(id) {
    if (pendingKeep[id]) return pendingKeep[id].amount;

    var row = keepRow(id);

    return row ? row.amount : 0;
  }

  function isKept(id) {
    return keepRow(id) != null || pendingKeep[id] != null;
  }

  // What the bag holds now, which is what the player most likely wants to keep; 1 when the bag has none.
  function suggestedKeep(id) {
    return bagCount(id) || 1;
  }

  function bagCount(id) {
    var row = keepRow(id);

    if (row) return row.held;

    var item = findItem(id);

    if (!item) return 0;

    return item.at.reduce(function (sum, at) { return storageOf(at[0]).kind === 'bag' ? sum + at[1] : sum; }, 0);
  }

  function matches(item, words) {
    var name = String(item.name).toLowerCase();

    for (var i = 0; i < words.length; i++) {
      if (name.indexOf(words[i]) < 0) return false;
    }

    return true;
  }

  function searchWords() {
    return state.query.toLowerCase().split(/\s+/).filter(Boolean);
  }

  function visibleItems() {
    var words = searchWords();
    var place = state.place;

    return state.data.items.filter(function (item) {
      return matches(item, words) && (place == null || item.at.some(function (at) { return String(at[0]) === place; }));
    });
  }

  function visibleRot() {
    var words = searchWords();

    return (state.data.rot || []).filter(function (row) { return matches(row, words); });
  }

  function icon(item, sizeClass) {
    var box = el('div', 'sh-icon ' + sizeClass);
    var letter = String(item.name || '?').charAt(0).toUpperCase();

    if (item.icon) {
      var img = el('img');
      img.setAttribute('data-letter', letter);
      img.src = item.icon;
      box.appendChild(img);
    } else {
      box.textContent = letter;
    }

    return box;
  }

  function findItem(id) {
    var items = state.data.items;

    for (var i = 0; i < items.length; i++) {
      if (String(items[i].id) === String(id)) return items[i];
    }

    return null;
  }

  function storageOf(id) {
    var storages = state.data.storages;

    for (var i = 0; i < storages.length; i++) {
      if (storages[i].id === id) return storages[i];
    }

    return { id: id, name: 'Storage ' + id, kind: 'storage' };
  }

  function renderHeader() {
    var homes = state.data.storages.filter(function (s) { return s.kind !== 'bag'; }).length;
    var subtitles = {
      items: say('subItems', state.data.items.length, homes),
      rot: say('subRot', homes),
      keep: say('subKeep')
    };
    var rot = state.data.rot || [];
    ui.sub.textContent = subtitles[state.view];
    ui.search.placeholder = say(state.view === 'keep' ? 'searchKeep' : 'search');
    renderTab('items', say('itemsView'), say('itemsTip'), state.data.items.length, false);
    renderTab('rot', say('rotView'), say('rotTip'), rot.length, rot.some(function (row) { return row.urgent; }));
    renderTab('keep', say('keepView'), say('keepTip'), keepRows().length, false);
    ui.bagButton.textContent = say('goBag');
    tipFor(ui.bagButton, say('putAwayBag'));
    ui.floorButton.textContent = say('goFloor');
    tipFor(ui.floorButton, say('putAwayFloor'));
    tipFor(ui.x, say('closeTip'));
    ui.closeKeys.textContent = window.__storagehubToggleKey ? say('closeKeys', window.__storagehubToggleKey) : say('escClose');

    // The keep view searches every item for the add list on the right, so the search box moves over there.
    var slot = state.view === 'keep' ? ui.rightTools : ui.leftTools;

    if (ui.tools.parentNode !== slot) slot.appendChild(ui.tools);

    renderPlace();
  }

  function renderTab(view, label, tip, count, isUrgent) {
    var tab = ui.tabs[view];
    tab.className = 'sh-tab' + (state.view === view ? ' sh-on' : '');
    tipFor(tab, tip);
    tab.firstChild.textContent = label;
    tab.lastChild.textContent = String(count);
    tab.lastChild.className = 'sh-n' + (isUrgent ? ' sh-bad' : '');
  }

  // A drop-down of the places that hold anything, drawn by the page so it matches the panel; the items view only.
  function renderPlace() {
    var place = ui.place;
    var places = state.data.storages;

    // A storage that went away since it was picked shows everything again.
    if (state.place != null && !places.some(function (s) { return s.id === state.place; })) state.place = null;

    place.style.display = state.view === 'items' ? '' : 'none';
    place.className = 'sh-sel' + (state.isPlaceOpen ? ' sh-on' : '');
    place.setAttribute('data-act', 'placeMenu');
    tipFor(place, say('placeTip'));
    place.textContent = '';
    place.appendChild(el('span', '', state.place == null ? say('allPlaces') : storageOf(state.place).name));
    place.appendChild(el('span', '', '\u25be'));

    if (!state.isPlaceOpen) return;

    var menu = el('div', 'sh-menu');
    menu.appendChild(placeChoice('', say('allPlaces'), state.place == null));

    places.forEach(function (s) { menu.appendChild(placeChoice(String(s.id), s.name, String(s.id) === state.place)); });

    place.appendChild(menu);
  }

  function placeChoice(id, name, isOn) {
    var choice = withArgs(el('div', isOn ? 'sh-on' : '', name), id);
    choice.setAttribute('data-act', 'placePick');

    return choice;
  }

  function renderList() {
    if (state.view === 'rot') {
      renderRot();

      return;
    }

    if (state.view === 'keep') {
      renderKeep();

      return;
    }

    var scroll = ui.list.scrollTop;
    var items = visibleItems();
    ui.list.textContent = '';

    if (items.length === 0) {
      ui.list.appendChild(el('div', 'sh-empty', say(state.query || state.place != null ? 'noMatch' : 'noItems')));

      return;
    }

    var fragment = document.createDocumentFragment();

    items.forEach(function (item) {
      var row = el('div', 'sh-row' + (String(item.id) === state.selected ? ' sh-active' : ''));
      var main = el('div', 'sh-main');
      main.appendChild(el('div', 'sh-name', item.name));
      main.appendChild(whereLine(item));
      row.setAttribute('data-item', String(item.id));
      row.appendChild(itemTip(icon(item, 'sh-i42'), item.id, item.name, itemTipBody(item)));
      row.appendChild(itemTip(main, item.id, item.name, itemTipBody(item)));
      row.appendChild(quickKeepButton(item.id));
      row.appendChild(el('div', 'sh-count', String(item.total)));
      fragment.appendChild(row);
    });

    ui.list.appendChild(fragment);
    ui.list.scrollTop = scroll;
  }

  // The bag first, then the places holding the most.
  function placesOf(item) {
    return item.at.slice().sort(function (a, b) {
      var bagFirst = (storageOf(b[0]).kind === 'bag') - (storageOf(a[0]).kind === 'bag');

      return bagFirst || b[1] - a[1];
    });
  }

  // "Bag 4, Fridge 12, Wooden Box 3" with a middle dot between places
  function whereLine(item) {
    var line = el('div', 'sh-where');

    placesOf(item).forEach(function (at, index) {
      var storage = storageOf(at[0]);
      var part = el('span', storage.kind === 'bag' ? 'sh-bagat' : '', storage.name + ' ');
      part.appendChild(el('b', '', String(at[1])));

      if (index > 0) line.appendChild(document.createTextNode(DOT));

      line.appendChild(part);
    });

    return line;
  }

  // The bell on a list row: shows once the row is hovered, and stays lit with the amount once the item is kept.
  function quickKeepButton(id) {
    var isOnList = isKept(id);
    var node = withArgs(el('button', 'sh-bell' + (isOnList ? ' sh-on' : '')), id);
    node.setAttribute('data-act', 'quickKeep');
    node.innerHTML = BELL_ICON;
    tipFor(node, say(isOnList ? 'keepOnTip' : 'keepAddTip'));

    if (isOnList) node.appendChild(el('em', '', String(keptAmount(id))));

    return node;
  }

  // The keep list, by name: how many to keep, with - and + and a number field, and how many the bag holds now.
  // A field being typed into is left alone, so the refresh every second cannot wipe what the player is typing.
  function renderKeep() {
    if (isTypingAmount()) return;

    var scroll = ui.list.scrollTop;
    var keep = state.data.keep || { on: true, rows: [] };
    ui.list.textContent = '';

    if (!keep.on) ui.list.appendChild(el('div', 'sh-banner', say('keepOff')));

    if (keepRows().length === 0) {
      ui.list.appendChild(el('div', 'sh-empty', say('keepEmpty')));

      return;
    }

    var fragment = document.createDocumentFragment();
    fragment.appendChild(fillBar());

    keepRows().forEach(function (kept) { fragment.appendChild(keepRowNode(kept)); });

    ui.list.appendChild(fragment);
    ui.list.scrollTop = scroll;
  }

  function keepRowNode(kept) {
    var amount = keptAmount(kept.id);
    var row = el('div', 'sh-row sh-keep' + (amount === 0 ? ' sh-keep-zero' : ''));
    var main = el('div', 'sh-main');
    main.appendChild(el('div', 'sh-name', kept.name));
    main.appendChild(heldNode(kept.held, amount));
    main.appendChild(meterNode(kept.held, amount));
    row.appendChild(itemTip(icon(kept, 'sh-i42'), kept.id, kept.name, say('held', kept.held)));
    row.appendChild(itemTip(main, kept.id, kept.name, say('held', kept.held)));

    if (amount > 0) row.appendChild(storedChip(kept, amount));

    row.appendChild(stepperNode(kept.id, amount));

    var remove = withArgs(el('button', 'sh-del', TIMES), kept.id);
    remove.setAttribute('data-act', 'keepRemove');
    tipFor(remove, say('keepRemoveTip'));
    row.appendChild(remove);

    return row;
  }

  // How many the home storages hold for Fill to take, red when the bag is short and none are there to pull.
  function storedChip(kept, amount) {
    var count = storedCount(kept.id);

    if (count > 0) return tipFor(el('span', 'sh-chip', say('stored', count)), say('storedTip'));

    return tipFor(el('span', 'sh-chip' + (kept.held < amount ? ' sh-none' : ''), say('storedNone')), say('storedNoneTip'));
  }

  // Units in home storages, which is where Fill takes from: not the bag, never a drone, and never the neighbor's basket.
  function storedCount(id) {
    var item = findItem(id);

    if (!item) return 0;

    return item.at.reduce(function (sum, at) {
      var storage = storageOf(at[0]);

      return storage.kind === 'bag' || storage.kind === 'basket' || storage.drone ? sum : sum + at[1];
    }, 0);
  }

  // The button that brings the bag up to the list, with how many items are short and how many storage can cover.
  function fillBar() {
    var short = 0;
    var pullable = 0;

    keepRows().forEach(function (kept) {
      if (kept.held >= keptAmount(kept.id)) return;

      short++;

      if (storedCount(kept.id) > 0) pullable++;
    });

    var bar = el('div', 'sh-fill');
    var go = tipFor(actButton('sh-add' + (pullable === 0 ? ' sh-dim' : ''), say('fill'), 'keepFill'), say('fillTip'));
    bar.appendChild(el('div', 'sh-hint', short === 0 ? say('fillDone') : say('fillShort', short, pullable)));
    bar.appendChild(go);

    return bar;
  }

  function heldNode(held, amount) {
    if (amount === 0) return el('div', 'sh-held', say('heldNone', held));

    var isShort = held < amount;

    return el('div', 'sh-held' + (isShort ? ' sh-held-short' : ''), isShort ? say('heldShort', held, amount - held) : say('held', held));
  }

  // How much of the amount to keep the bag holds now: full and green when it has enough, amber while short.
  function meterNode(held, amount) {
    var meter = el('div', 'sh-meter');
    var fill = el('i', held < amount ? 'sh-low' : '');
    fill.style.width = (amount === 0 ? 0 : Math.min(100, Math.round(held / amount * 100))) + '%';
    meter.appendChild(fill);

    return meter;
  }

  function stepperNode(id, amount) {
    var step = el('div', 'sh-step');
    var less = withArgs(el('button', 'sh-stepb', MINUS), id + '|-1');
    var more = withArgs(el('button', 'sh-stepb', '+'), id + '|1');
    less.setAttribute('data-act', 'keepStep');
    more.setAttribute('data-act', 'keepStep');
    tipFor(less, say('keepLess'));
    tipFor(more, say('keepMore'));
    step.appendChild(less);
    step.appendChild(amountField(id, amount));
    step.appendChild(more);

    return step;
  }

  // Enter saves; Esc puts the shown amount back and closes the panel, as Esc in the search box does. The arrow keys
  // step the typed number on purpose, like - and +; the mouse wheel does not, so scrolling the list cannot change it.
  function amountField(id, amount) {
    var field = el('input', 'sh-amount');
    field.type = 'number';
    field.min = '0';
    field.max = String(MAX_KEEP);
    field.value = String(amount);
    field.setAttribute('data-shown', String(amount));
    tipFor(field, say('keepAmountTip'));
    field.addEventListener('focus', function () {
      send('focus|1');
      field.select();
    });
    field.addEventListener('blur', function () {
      send('focus|0');
      // Saved here rather than on change, so a refresh held back while typing is drawn either way. A button clicked to
      // leave the field acts on top of the typed amount, since render waits for that click.
      if (field.value !== String(amount) && field.value !== '') recordKeep(id, Number(field.value));

      render();
    });
    field.addEventListener('keydown', function (e) {
      e.stopPropagation();

      if (e.key === 'Enter') field.blur();

      if (e.key === 'Escape') {
        field.value = String(amount);
        field.blur();
        send('close');
      }
    });
    field.addEventListener('wheel', function (e) {
      if (document.activeElement === field) e.preventDefault();
    }, { passive: false });

    return field;
  }

  function isTypingAmount() {
    var active = document.activeElement;

    return !!active && active.className === 'sh-amount' && ui.list.contains(active);
  }
  // The right side of the keep view: the search box finds any item in the game; with no search, what the bag holds.
  function renderKeepAdd() {
    var scrollBox = ui.detail.querySelector('.sh-locs');
    var scroll = scrollBox ? scrollBox.scrollTop : 0;
    var words = searchWords();
    var source = words.length > 0 && state.catalog.length > 0 ? state.catalog : state.data.items;
    var found = source.filter(function (item) { return words.length > 0 ? matches(item, words) : bagCount(item.id) > 0; });
    ui.detail.textContent = '';
    ui.detail.appendChild(el('div', 'sh-h2', say('keepAddTitle')));
    ui.detail.appendChild(el('div', 'sh-hint', keepAddHint(words.length > 0)));

    if (found.length === 0) {
      ui.detail.appendChild(el('div', 'sh-empty', say(words.length > 0 ? 'noName' : 'bagEmpty')));

      return;
    }

    var picks = el('div', 'sh-locs');
    picks.style.marginTop = '10px';

    found.slice(0, MAX_KEEP_RESULTS).forEach(function (item) { picks.appendChild(keepPickNode(item)); });

    if (found.length > MAX_KEEP_RESULTS) picks.appendChild(el('div', 'sh-empty', say('moreResults', found.length - MAX_KEEP_RESULTS)));

    ui.detail.appendChild(picks);
    picks.scrollTop = scroll;
  }

  function keepAddHint(isSearching) {
    if (!isSearching) return say('hintBag');

    return say(state.catalog.length > 0 ? 'hintAll' : 'hintLoading');
  }

  function keepPickNode(item) {
    var pick = el('div', 'sh-pick');
    var held = bagCount(item.id);
    pick.appendChild(itemTip(icon(item, 'sh-i32'), item.id, item.name, say('held', held)));
    pick.appendChild(itemTip(el('div', 'sh-pname', item.name), item.id, item.name, say('held', held)));

    if (held > 0) pick.appendChild(el('span', 'sh-chip', say('inBag', held)));

    if (isKept(item.id)) {
      pick.appendChild(el('span', 'sh-chip sh-on', say('keeping', keptAmount(item.id))));
    } else {
      var add = withArgs(el('button', 'sh-add sh-sm', say('addKeep')), item.id);
      add.setAttribute('data-act', 'keepAdd');
      tipFor(add, say('addKeepTip', suggestedKeep(item.id)));
      pick.appendChild(add);
    }

    return pick;
  }

  // Food grouped by how long it has left, soonest first; clicking a row shows that item's detail beside the list, as in
  // the item list. A kind that lies only on the floor has no detail (the item list holds bags and storages only), so
  // its rows do not click.
  function renderRot() {
    var scroll = ui.list.scrollTop;
    var rows = visibleRot();
    ui.list.textContent = '';

    if (rows.length === 0) {
      ui.list.appendChild(el('div', 'sh-empty', say(state.query ? 'noMatch' : 'nothingSpoils')));

      return;
    }

    var fragment = document.createDocumentFragment();
    var groups = [];

    rows.forEach(function (rot) {
      var name = rotGroup(rot.hours);
      var group = groups.length > 0 ? groups[groups.length - 1] : null;

      if (!group || group.name !== name) {
        group = { name: name, rows: [] };
        groups.push(group);
      }

      group.rows.push(rot);
    });

    groups.forEach(function (group) {
      var head = el('div', 'sh-g', group.name);
      head.appendChild(el('span', 'sh-gc', String(group.rows.length)));
      fragment.appendChild(head);

      group.rows.forEach(function (rot) { fragment.appendChild(rotRowNode(rot)); });
    });

    ui.list.appendChild(fragment);
    ui.list.scrollTop = scroll;
  }

  function rotGroup(hours) {
    if (hours < 0) return say('rotSpoiled');
    if (hours < DAY_HOURS) return say('rotDay');
    if (hours < WEEK_HOURS) return say('rotWeek');

    return say('rotLater');
  }

  function rotRowNode(rot) {
    var hasDetail = findItem(rot.id) != null;
    var isSelected = hasDetail && String(rot.id) === state.selected;
    var row = el('div', 'sh-row' + (hasDetail ? '' : ' sh-still') + (isSelected ? ' sh-active' : ''));
    var main = el('div', 'sh-main');
    var tone = rot.hours < 1 ? ' sh-bad' : rot.hours < DAY_HOURS ? ' sh-warn' : '';

    if (hasDetail) row.setAttribute('data-item', String(rot.id));
    else tipFor(row, say('floorOnlyTip'));

    main.appendChild(el('div', 'sh-name', rot.name));
    main.appendChild(el('div', 'sh-where', rot.where));
    row.appendChild(itemTip(icon(rot, 'sh-i42'), rot.id, rot.name, rot.where + DOT + rot.left));
    row.appendChild(itemTip(main, rot.id, rot.name, rot.where + DOT + rot.left));
    row.appendChild(el('div', 'sh-count', String(rot.count)));
    row.appendChild(el('div', 'sh-ttl' + tone, rot.left));

    return row;
  }

  function renderDetail() {
    if (state.view === 'keep') {
      renderKeepAdd();

      return;
    }

    var scrollBox = ui.detail.querySelector('.sh-locs');
    var scroll = scrollBox ? scrollBox.scrollTop : 0;
    var item = state.selected == null ? null : findItem(state.selected);
    ui.detail.textContent = '';

    if (!item) {
      ui.detail.appendChild(el('div', 'sh-empty', say('pickItem')));

      return;
    }

    var places = placesOf(item);
    var kept = keptAmount(item.id);
    var line = places.length === 1 ? say('totalPlace', item.total) : say('totalPlaces', item.total, places.length);
    var head = el('div', 'sh-dhead');
    var titles = el('div', 'sh-main');
    titles.appendChild(el('div', 'sh-dname', item.name));
    titles.appendChild(el('div', 'sh-dsub', kept > 0 ? line + DOT + say('keepingBag', kept) : line));
    head.appendChild(itemTip(icon(item, 'sh-i72'), item.id, item.name, itemTipBody(item)));
    head.appendChild(titles);
    ui.detail.appendChild(head);

    var hasBag = item.at.some(function (at) { return storageOf(at[0]).kind === 'bag'; });
    var hasElsewhere = item.at.some(function (at) { return storageOf(at[0]).kind !== 'bag'; });
    var actions = el('div', 'sh-actions');

    if (hasElsewhere) actions.appendChild(tipFor(withArgs(actButton('sh-add', say('takeAll'), 'takeAll'), item.id), say('takeAllTip')));
    if (hasBag) actions.appendChild(tipFor(withArgs(button(say('putAwayTag'), 'putAway', 'sh-sm'), item.id), say('putAwayTagTip')));

    actions.appendChild(keepWideButton(item.id));
    ui.detail.appendChild(actions);
    ui.detail.appendChild(el('div', 'sh-h2', say('whereIs')));

    if (!canSplit() && places.some(function (at) { return storageOf(at[0]).kind !== 'bag' && at[1] > 1; })) ui.detail.appendChild(el('div', 'sh-hint', say('noSplit')));

    var locs = el('div', 'sh-locs');

    places.forEach(function (at) {
      var storage = storageOf(at[0]);
      var isBag = storage.kind === 'bag';
      var loc = el('div', 'sh-loc' + (isBag ? ' sh-bag' : storage.kind === 'drawer' ? ' sh-drawer' : ''));
      loc.appendChild(el('div', 'sh-kind', KIND_WORD[storage.kind] ? say(KIND_WORD[storage.kind]) : storage.kind));
      loc.appendChild(el('div', 'sh-lname', storage.name));
      loc.appendChild(el('div', 'sh-lcount', String(at[1])));
      if (isBag) {
        loc.appendChild(tipFor(withArgs(actButton('sh-add sh-sm', say('putAway'), 'putAway'), item.id), say('putAwayTagTip')));
      } else {
        var isPartial = canSplit() && at[1] > 1;
        loc.appendChild(tipFor(withArgs(actButton('sh-add sh-sm', say(isPartial ? 'takeHere' : 'take'), 'take'), item.id + '|' + storage.id), say('takeHereTip', storage.name)));

        if (isPartial) loc.appendChild(amountRow(item, storage, at[1]));
      }

      locs.appendChild(loc);
    });

    ui.detail.appendChild(locs);
    locs.scrollTop = scroll;
  }

  // Without Stacking the game moves stacks whole, so part of one cannot be taken.
  function canSplit() {
    return state.data.canSplit !== false;
  }

  // A slider from 1 to what the place holds, and a button taking that many. Taking all of them is the plain take.
  function amountRow(item, storage, count) {
    var key = item.id + '|' + storage.id;
    var amount = Math.max(1, Math.min(count, takeAmounts[key] || 1));
    var row = el('div', 'sh-amt');
    var range = el('input', 'sh-range');
    var take = el('button', 'sh-add sh-sm');
    range.type = 'range';
    range.min = '1';
    range.max = String(count);
    range.step = '1';
    range.value = String(amount);
    tipFor(range, say('amountTip'));
    takeSomeButton(take, item, storage, amount, count);
    range.addEventListener('input', function () {
      takeAmounts[key] = Number(range.value);
      takeSomeButton(take, item, storage, Number(range.value), count);
    });
    row.appendChild(range);
    row.appendChild(take);

    return row;
  }

  function takeSomeButton(node, item, storage, amount, count) {
    var isAll = amount >= count;
    node.textContent = say('takeN', amount);
    node.setAttribute('data-act', isAll ? 'take' : 'takeSome');
    withArgs(node, item.id + '|' + storage.id + (isAll ? '' : '|' + amount));
    tipFor(node, say('takeSomeTip', amount, storage.name));
  }

  // The bell with words, beside the detail's moves: adds the item to the keep list, or opens the list once it is on it.
  function keepWideButton(id) {
    var isOnList = isKept(id);
    var node = withArgs(el('button', 'sh-bell sh-wide' + (isOnList ? ' sh-on' : '')), id);
    node.setAttribute('data-act', 'quickKeep');
    node.innerHTML = BELL_ICON;
    node.appendChild(el('span', '', isOnList ? say('keepN', keptAmount(id)) : say('keep')));
    tipFor(node, say(isOnList ? 'keepOnTip' : 'keepAddTip'));

    return node;
  }

  function withArgs(node, args) {
    node.setAttribute('data-args', String(args));

    return node;
  }

  // A button's hover tip, drawn by the page itself (see watchTips); an empty text takes it away.
  function tipFor(node, text, title) {
    if (text) node.setAttribute('data-tip', text);
    else node.removeAttribute('data-tip');

    if (title) node.setAttribute('data-tip-title', title);

    return node;
  }

  // An item's hover tip. Info Pack shows its own over anything carrying the config id once it watches the page; the
  // name and body here are the tip drawn when it does not.
  function itemTip(node, configId, name, body) {
    node.setAttribute('data-slip-item', String(configId));
    node.setAttribute('data-item-name', name);
    node.setAttribute('data-item-tip', body);

    return node;
  }

  // "12 in total, in 2 places" and where they are, for an item's own tip.
  function itemTipBody(item) {
    var places = placesOf(item);
    var line = places.length === 1 ? say('totalPlace', item.total) : say('totalPlaces', item.total, places.length);

    return line + '\n' + places.map(function (at) { return storageOf(at[0]).name + ' ' + at[1]; }).join(DOT);
  }

  // One floating tip per page, shown a moment after the pointer rests on anything with a tip. The game's web view is
  // not relied on for native title tips, so the page draws its own in the panel's look.
  function watchTips(doc, area) {
    if (area.__shTips) return;

    var timer = 0;
    var shown = null;
    area.__shTips = true;
    area.addEventListener('mouseover', function (e) {
      var target = tipTarget(doc, e.target);

      if (target === shown) return;

      clearTimeout(timer);
      hideTip(doc);
      shown = target;

      if (target) timer = setTimeout(function () { showTip(doc, target); }, TIP_DELAY_MS);
    });
    area.addEventListener('mouseleave', function () {
      clearTimeout(timer);
      shown = null;
      hideTip(doc);
    });
    area.addEventListener('mousedown', function () {
      clearTimeout(timer);
      hideTip(doc);
    }, true);
  }

  function tipTarget(doc, node) {
    var target = node && node.closest ? node.closest('[data-tip],[data-item-tip]') : null;

    if (!target || node.closest('.sh-menu')) return null;

    // Info Pack draws the item tip in a page it watches, so this one stands back there.
    if (!target.hasAttribute('data-tip') && isInfoPackWatching(doc)) return null;

    return target;
  }

  function isInfoPackWatching(doc) {
    var win = doc.defaultView;

    return !!(win && win.__slipTagged);
  }

  function showTip(doc, target) {
    if (!doc.body || !doc.body.contains(target)) return;

    var tip = doc.getElementById(TIP_ID);
    var isItem = !target.hasAttribute('data-tip');
    var title = target.getAttribute(isItem ? 'data-item-name' : 'data-tip-title');

    if (!tip) {
      tip = doc.createElement('div');
      tip.id = TIP_ID;
      tip.style.cssText = TIP_STYLE;
      doc.body.appendChild(tip);
    }

    tip.textContent = '';

    if (title) {
      var head = doc.createElement('b');
      head.style.cssText = TIP_TITLE_STYLE;
      head.textContent = title;
      tip.appendChild(head);
    }

    tip.appendChild(doc.createTextNode(target.getAttribute(isItem ? 'data-item-tip' : 'data-tip') || ''));

    // The tip sits on the page's body, outside the panel, so it takes the zoom of the panel its target is in.
    var zoom = uiZoom(target);

    tip.style.zoom = zoom === 1 ? '' : String(zoom);
    tip.style.display = 'block';
    placeTip(doc, tip, target.getBoundingClientRect(), zoom);
  }

  // Below the target, or above it when there is no room below, and never off the side of the page. The target's box
  // and the window are in screen pixels; the tip's left and top are in its own zoomed pixels.
  function placeTip(doc, tip, rect, zoom) {
    var win = doc.defaultView;
    var width = tip.offsetWidth * zoom;
    var height = tip.offsetHeight * zoom;
    var gap = 8 * zoom;
    var left = Math.max(6, Math.min(rect.left + rect.width / 2 - width / 2, win.innerWidth - width - 6));
    var top = rect.bottom + gap;

    if (top + height > win.innerHeight - 6) top = Math.max(6, rect.top - height - gap);

    tip.style.left = Math.round(left / zoom) + 'px';
    tip.style.top = Math.round(top / zoom) + 'px';
  }

  function hideTip(doc) {
    var tip = doc && doc.getElementById(TIP_ID);

    if (tip) tip.style.display = 'none';
  }

  function render() {
    if (isPointerDown) {
      isRedrawHeld = true;

      return;
    }

    renderHeader();
    renderList();
    renderDetail();
  }

  function open(data) {
    build();
    state.data = data || state.data;
    state.isOpen = true;
    isPointerDown = false;
    ui.status.textContent = '';
    ui.root.style.display = 'flex';
    render();
    ui.search.focus();
    ui.search.select();
  }

  function setData(data) {
    state.data = data || state.data;

    if (state.isOpen) render();
  }

  // Every named item in the game, for the keep view's search. An empty answer means the game had not loaded its item
  // table yet, so the next visit to the keep view asks again.
  function catalog(items) {
    state.catalog = items || [];

    if (state.catalog.length === 0) state.isCatalogAsked = false;

    if (state.isOpen && state.view === 'keep') render();
  }

  function close() {
    state.isOpen = false;
    isPointerDown = false;

    if (!ui) return;

    putBackTypedAmount();
    hideTip(document);
    ui.root.style.display = 'none';

    if (document.activeElement === ui.search) ui.search.blur();
  }

  // Closing is a cancel: a number half typed into an amount field is put back before the blur would save it.
  function putBackTypedAmount() {
    var active = document.activeElement;

    if (!active || active.className !== 'sh-amount') return;

    active.value = active.getAttribute('data-shown');
    active.blur();
  }

  // tone is "ok", "warn", "bad" or "", worked out by the plugin from what happened.
  function status(text, tone) {
    build();
    clearBusy();
    settleSentKeeps();
    var toneClass = TONE_ICON[tone] ? ' sh-' + tone : '';

    // With the panel closed the plugin shows the line as an Exiled's Toolkit toast instead.
    if (!state.isOpen) return;

    ui.status.className = 'sh-status' + toneClass;
    ui.status.innerHTML = text && TONE_ICON[tone] ? TONE_ICON[tone] : '';
    ui.status.appendChild(el('span', '', text));
  }

  // The inventory's Mods menu, with this mod's rows in it, comes from Exiled's Toolkit; the plugin adds them.
  var hasReportedTabFallback = false;

  // Reads the page's Vue state, which a game update can rename; failing open keeps the window on every tab, so the
  // first failure is reported.
  function backpackTabWantsButton(doc) {
    try {
      var setup = doc.getElementById('app')._vnode.component.setupState;

      return setup.curATab === 0 && !setup.isPrologue;
    } catch (e) {
      if (!hasReportedTabFallback) send('diag|cannot read the inventory tab state, keeping the window on every tab: ' + e);
      hasReportedTabFallback = true;

      return true;
    }
  }

  // The home storage list. At a station it is a tab in the bag panel's own tab bar: choosing it
  // shows the list in the bag grid's place, at the same size, so the page's layout and zoom never change. On the
  // inventory screen it is a window opened from the Mods menu. The plugin sends the storages with an ok flag per
  // item, decided by the station's own rules. At a station, a usable item goes in the way the game's own click does
  // it: its storage's bag tab is shown underneath, then the item is clicked there. Anything else, or an item whose
  // storage has no tab, is taken into the bag.
  //
  // Each station page names its tab bar (tabs), the box inside it that holds the first tabs (pin; none means the bar
  // itself), the class and element of a tab, the bag panel around the bar, and where the page keeps its tabs (model):
  // 'bag' pages keep an owner per tab and switch with SWITCH_BAG, 'cart' pages keep a tab list in State and switch by
  // index, the drone trade keeps its tabs in Vue, so only the drawn tabs tell their owners, and the workbench
  // ('tooltable') keeps its last bag message in a script variable, tab 0 the bag, 1 the drawer and 2 on its cabinets.
  // quick is the right click that moves an item straight into the station.
  var BAG_TABS = { tabs: '#bagTabs', pin: '#bagTabPinned', tab: 'bag-tab', tag: 'div', model: 'bag' };
  var CART_TABS = { tab: 'left-tab', tag: 'button', panel: '.cart', model: 'cart', quick: true };
  var STATION_PAGES = [
    withTabs({ kind: 'cooking', page: /Cooking\.html/, host: '.cooking-system', scope: 'scopeCooking', panel: '.panel', quick: false }, BAG_TABS),
    withTabs({ kind: 'ratcage', page: /RatCage\.html/, host: '.generator-system', scope: 'scopeRatCage', panel: '.panel', quick: false }, BAG_TABS),
    withTabs({ kind: 'brew', page: /BrewPanel\.html/, host: '.brew-system', scope: 'scopeBrew', panel: '.col', quick: true }, BAG_TABS),
    withTabs({ kind: 'dye', page: /DyePanel\.html/, host: '.dye-system', scope: 'scopeDye', panel: '.col', quick: true }, BAG_TABS),
    withTabs({ kind: 'shredder', page: /Shredder\.html/, host: '.cart', scope: 'scopeShredder', tabs: '.cart > .left-tabs', pin: '#tabPinned' }, CART_TABS),
    withTabs({ kind: 'compressor', page: /Compressor\.html/, host: '.cart', scope: 'scopeCompressor', tabs: '#tabs', pin: null }, CART_TABS),
    { kind: 'trade', page: /TradeUI\.html/, host: '#app .cols', scope: 'scopeTrade', tabs: '.cols > .col > .bag-tabs', pin: '.bag-tab-pinned', tab: 'bag-tab', tag: 'button', panel: '.col', model: 'trade', quick: true },
    { kind: 'workbench', page: /ToolTable\.html/, host: '.backpack-panel', scope: 'scopeWorkbench', tabs: '.backpack-panel > .left-tabs', pin: '.left-tab-pinned', tab: 'left-tab', tag: 'button', panel: '.backpack-panel', model: 'tooltable', quick: true },
    // The basket screen is the bag screen with the basket beside the bag, so it is matched before the inventory. Its bag
    // side's tabs ('atab') are Vue's, switched by index; the plugin names the storage behind each, and Home goes after
    // the bag's tab with the storage tabs scrolling past it.
    { kind: 'basket', page: /BackpackUI\.html/, host: '.bag-panel.is-basket-panel', scope: 'scopeBasket', tabs: '.bag-slot.is-anchored .bag-tabs', pin: null, tab: 'bag-tab', tag: 'div', panel: '.bag-slot.is-anchored > .bag-panel', model: 'atab', quick: true, homeAfterFirst: true, scrollTabs: true },
    // Any other storage open beside the bag works the same way at home. Out exploring the plugin answers that it is
    // away, and the screen keeps the inventory's window instead of a Home tab.
    { kind: 'storage', page: /BackpackUI\.html/, host: '.bag-panel.is-storage', scope: 'scopeStorage', tabs: '.bag-slot.is-anchored .bag-tabs', pin: null, tab: 'bag-tab', tag: 'div', panel: '.bag-slot.is-anchored > .bag-panel', model: 'atab', quick: true, homeAfterFirst: true, scrollTabs: true },
    { kind: 'inventory', page: /BackpackUI\.html/, host: '.bag-panel .panel' }
  ];
  // A wheel notch moves the storage tabs one tab; an arrow pages to the first tab not wholly shown.
  var WHEEL_NOTCH = 50;
  // How long a smooth scroll to a tab runs; until then the next step counts from where it is going.
  var TAB_SCROLL_MS = 600;
  // The bag panel beside the basket or a storage is sized by its widest child, so a long row of storage tabs widened it past the screen
  // and pushed the grid to the middle of it. The header gets no say in the width, the same way the game keeps a long
  // tag name from widening a panel, and the tabs scroll inside it. Added as soon as the screen is found, so it holds
  // even before the plugin answers.
  var TAB_CAP_CSS = [
    '.bag-slot.is-anchored .header{width:0;min-width:100%;box-sizing:border-box}',
    '.bag-slot.is-anchored .bag-tabs{min-width:0;overflow-x:auto;scrollbar-width:none}',
    '.bag-slot.is-anchored .bag-tabs::-webkit-scrollbar{display:none}',
    '.bag-slot.is-anchored .bag-tabs>.bag-tab{flex:1 0 auto;white-space:nowrap}',
    // One segmented bar: small tabs split by hairlines, the chosen one filled gold, so more storages fit. Only on a bar
    // the Home tab is in, so looting out exploring keeps the game's own tabs.
    '.bag-slot.is-anchored .bag-tabs.slmh-scroll{gap:0;background:rgba(10,10,12,.72);border:1px solid rgba(255,255,255,.14);border-radius:6px}',
    '.bag-slot.is-anchored .bag-tabs.slmh-scroll>.bag-tab{flex:0 0 auto;padding:4px 11px 5px;font-size:13px;letter-spacing:.3px;background:none;border:0;border-right:1px solid rgba(255,255,255,.1);border-radius:0;color:rgba(255,255,255,.55)}',
    '.bag-slot.is-anchored .bag-tabs.slmh-scroll>.bag-tab:hover{color:#fff;background-color:rgba(255,255,255,.06)}',
    '.bag-slot.is-anchored .bag-tabs.slmh-scroll>.bag-tab.active{color:#1a1408;background-color:#FFC83A;text-shadow:none;font-weight:700}',
    // The pinned tabs cover the storage tabs scrolling behind them with their own solid fill. The ::before cover would
    // paint over the Home icon and the gold of a chosen pinned tab, since a sticky tab is its own stacking context.
    '.bag-slot.is-anchored .bag-tabs.slmh-scroll>.slmh-pin::before{display:none}',
    '.bag-slot.is-anchored .bag-tabs.slmh-scroll>.slmh-pin{background-color:#111113}',
    '.bag-slot.is-anchored .bag-tabs.slmh-scroll>.slmh-pin:not(.active):hover{background-color:#1c1c1f}',
    '.bag-slot.is-anchored .bag-tabs.slmh-scroll>.slmh-tab{padding-left:28px;color:#FFC83A;background-image:url("' + homeIcon('#FFC83A') + '");background-repeat:no-repeat;background-position:10px 50%;background-size:13px}',
    '.bag-slot.is-anchored .bag-tabs.slmh-scroll>.slmh-tab.active{color:#1a1408;background-image:url("' + homeIcon('#1a1408') + '")}',
    '.bag-slot.is-anchored .bag-tabs.slmh-scroll>.slmh-arrow{width:22px;border:0;border-radius:0;background:#111113;font-size:15px}'
  ].join('');

  function homeIcon(colour) {
    return 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="' + colour +
      '" stroke-width="2.6" stroke-linejoin="round"><path d="M3 11l9-7 9 7v9h-6v-6H9v6H3z"/></svg>');
  }
  var STATION_ASK_MS = 3000;
  var STATION_AFTER_CLICK_MS = [400, 1200];
  var TAB_WAIT_MS = 2000;
  // An Open closes the bag screen and opens it again with the storage beside it, which drops the window; it is put
  // back once the screen is up again, for this long at most.
  var REOPEN_MS = 3000;
  var TAB_POLL_MS = 80;
  var PANE_TAB_GAP = 8;
  var POP_WIDTH = 320;
  var POP_GAP = 24;
  var POP_EDGE = 16;
  var POP_MIN_HEIGHT = 360;
  var SEARCH_ICON = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#fcfcfc" stroke-width="2.4"><circle cx="11" cy="11" r="7"/><path d="M20 20l-4-4"/></svg>';
  var HOME_CSS = [
    '.slmh-host{position:relative}',
    // While the list is shown the bag panel keeps its own content and size, only hidden, so nothing reflows.
    '.slmh-on>:not(.panel-title):not(.col-t):not(.bag-tabs):not(.left-tabs):not(.slmh-pane){visibility:hidden}',
    // With only the bag the game hides its tab bar, so the bag gets a tab of its own beside ours.
    '.slmh-forced .bag-tabs{display:flex!important}',
    '.slmh-forced #bagPanelTitle{display:none!important}',
    // The basket screen keeps its tab bar in a header the list would otherwise hide.
    '.slmh-on>.header-slot{visibility:visible}',
    // Many storage tabs scroll sideways behind the bag's tab, Home and the arrows, as the stove's tabs do.
    '.slmh-scroll{min-width:0;overflow-x:hidden;scroll-behavior:smooth}',
    '.slmh-scroll>.bag-tab{flex:1 0 auto;white-space:nowrap}',
    '.slmh-scroll>.slmh-pin{position:sticky;z-index:2}',
    '.slmh-scroll>.slmh-pin::before{content:"";position:absolute;inset:-1px;z-index:-1;border-radius:8px;background:#141416}',
    '.slmh-arrow{position:sticky;z-index:3;flex:none;width:26px;display:none;align-items:center;justify-content:center;background:#141416;border:1px solid rgba(255,255,255,.14);border-radius:6px;color:rgba(255,255,255,.75);font-size:18px;font-weight:bold;cursor:pointer;user-select:none}',
    '.slmh-arrow.on{display:flex}',
    '.slmh-arrow.off{opacity:.3;cursor:default}',
    '.slmh-arrow.r{right:0}',
    '.slmh-tag.wish{color:#FFC83A}',
    '.slmh-tag.wish::before{background:#FFC83A;box-shadow:0 0 6px rgba(255,200,58,.6)}',
    '.slmh-pane{position:absolute;z-index:5;display:flex;flex-direction:column;min-height:0;font-family:"Microsoft YaHei","PingFang SC",sans-serif}',
    '.slmh-pop{position:fixed;z-index:50;box-sizing:border-box;display:flex;flex-direction:column;padding:14px 16px 12px;border-radius:4px;background:radial-gradient(ellipse at 15% 0%,rgba(150,100,40,.28),transparent 55%),linear-gradient(180deg,#3a3129 0%,#2b241d 55%,#241e18 100%);border:1px solid rgba(220,188,120,.28);box-shadow:0 12px 40px rgba(0,0,0,.6);font-family:"Microsoft YaHei","PingFang SC",sans-serif}',
    '.slmh-pop::before{content:"";position:absolute;top:0;left:0;right:0;height:2px;background:linear-gradient(90deg,transparent 10%,#FFC83A 50%,transparent 90%);opacity:.6}',
    '.slmh-head{display:flex;align-items:center;margin-bottom:10px}',
    '.slmh-title{display:flex;align-items:center;font-size:15px;font-weight:700;color:#fff;letter-spacing:3px;text-transform:uppercase}',
    '.slmh-title::before{content:"";width:3px;height:15px;margin-right:9px;background:#FFC83A;box-shadow:0 0 8px rgba(255,200,58,.35)}',
    '.slmh-sub{margin-left:10px;font-size:12px;color:#a89a82}',
    '.slmh-x{margin-left:auto;width:26px;height:26px;padding:0;border-radius:4px;border:1px solid rgba(220,188,120,.2);background:none;color:#a89a82;font-size:16px;line-height:1;cursor:pointer;font-family:inherit}',
    '.slmh-x:hover{border-color:#FFC83A;color:#FFC83A}',
    '.slmh-search{position:relative}',
    '.slmh-search input{width:100%;box-sizing:border-box;height:32px;padding:0 10px 0 30px;background:rgba(20,14,8,.6);border:1px solid rgba(220,188,120,.2);border-radius:4px;color:#f0e8d8;font-size:13px;outline:none;font-family:inherit}',
    '.slmh-search input::placeholder{color:#7a6e5a}',
    '.slmh-search input:focus{border-color:rgba(255,200,58,.7)}',
    '.slmh-search svg{position:absolute;left:9px;top:50%;transform:translateY(-50%);opacity:.6}',
    '.slmh-scope{display:flex;margin-top:8px;border:1px solid rgba(220,188,120,.2);border-radius:4px;overflow:hidden;background:rgba(20,14,8,.5)}',
    '.slmh-scope button{flex:1;display:flex;align-items:center;justify-content:center;gap:6px;padding:5px 8px;font-size:12.5px;border:none;background:none;color:#cbbfa8;cursor:pointer;font-family:inherit}',
    '.slmh-scope button+button{border-left:1px solid rgba(220,188,120,.2)}',
    '.slmh-scope button:hover{color:#fff}',
    '.slmh-scope button.on{color:#FFC83A;background:rgba(255,200,58,.13)}',
    '.slmh-scope b{min-width:16px;padding:0 5px;border-radius:7px;font-size:11px;font-weight:700;background:rgba(255,240,210,.1);color:#cbbfa8}',
    '.slmh-scope button.on b{background:#FFC83A;color:#2a1f10}',
    '.slmh-list{flex:1;min-height:0;overflow-y:auto;margin-top:8px;padding-right:4px}',
    '.slmh-list::-webkit-scrollbar{width:5px}',
    '.slmh-list::-webkit-scrollbar-thumb{background:rgba(255,200,58,.3);border-radius:3px}',
    '.slmh-store{display:flex;align-items:center;gap:7px;padding:9px 2px 4px;cursor:pointer;font-size:11.5px;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;color:#cbbfa8;user-select:none;border-bottom:1px solid rgba(220,188,120,.11)}',
    '.slmh-list>div:first-child>.slmh-store{padding-top:2px}',
    '.slmh-store:hover{color:#fff}',
    '.slmh-store.on{color:#FFC83A}',
    '.slmh-car{width:9px;font-size:8px;color:#FFC83A}',
    '.slmh-store .cnt{font-weight:400;letter-spacing:0;text-transform:none;color:#7a6e5a}',
    '.slmh-open{margin-left:auto;padding:1px 9px;font-size:11px;font-weight:700;letter-spacing:0;text-transform:none;border-radius:3px;border:1px solid rgba(255,200,58,.55);background:rgba(255,200,58,.1);color:#FFC83A;cursor:pointer;font-family:inherit}',
    '.slmh-open:hover{background:rgba(255,200,58,.22)}',
    '.slmh-open.on{background:#FFC83A;color:#2a1f10}',
    '.slmh-row{display:flex;align-items:center;gap:9px;margin-top:4px;padding:3px 8px 3px 4px;cursor:pointer;border-radius:4px;background:rgba(70,58,44,.55);border:1px solid rgba(220,188,120,.11);font-size:12.5px;color:#f0e8d8}',
    '.slmh-row:hover{background:rgba(84,70,52,.7);border-color:rgba(255,200,58,.35)}',
    '.slmh-ic{width:32px;height:32px;flex:none;background:rgba(10,8,6,.55);border:1px solid rgba(0,0,0,.5);border-radius:4px;display:flex;align-items:center;justify-content:center;color:#a89a82;font-size:13px}',
    '.slmh-ic img{max-width:28px;max-height:28px}',
    '.slmh-row .n{min-width:0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.slmh-row .x{color:#FFC83A;font-weight:700;font-size:12px;flex:none}',
    '.slmh-tag{margin-left:auto;flex:none;display:flex;align-items:center;gap:5px;font-size:11px;color:#a89a82}',
    '.slmh-tag::before{content:"";width:6px;height:6px;border-radius:50%;background:#7a6e5a}',
    '.slmh-tag.st{color:#8cc56a}',
    '.slmh-tag.st::before{background:#8cc56a;box-shadow:0 0 6px rgba(140,197,106,.6)}',
    '.slmh-empty{padding:16px 6px;color:#a89a82;font-size:12.5px;text-align:center}',
    '.slmh-hint{display:flex;align-items:center;gap:7px;margin-top:8px;font-size:12px;color:#a89a82}',
    '.slmh-hint::before{content:"";flex:none;width:5px;height:5px;border-radius:50%;background:#FFC83A;opacity:.7}'
  ].join('');

  // isShown: the station's home tab is chosen, or the inventory's window is open.
  // switchingUntil: a Home click is switching the game's tab until then, so that switch does not close the list.
  var home = { data: null, isOff: false, isAway: false, station: null, frame: null, doc: null, ui: null, tab: null, back: null, arrows: null, panel: null, tabsEl: null, observer: null, askedAt: 0, shownJson: '', shownTabs: '', query: '', isAllScope: false, folds: {}, openId: null, isShown: false, underTab: -1, switchingUntil: 0, reopen: null };

  function station(data) {
    if (data === null) {
      home.isOff = true;
      dropHome();

      return;
    }

    if (!data || !home.station || data.kind !== home.station.kind) return;

    // Out exploring the Home tab goes, and the inventory's window takes its place.
    if (!!data.away !== home.isAway) {
      home.isAway = !!data.away;
      removeHomeTab();
    }

    // The tabs decide which storages get an Open button, so a change of tabs redraws too.
    var owners = tabOwners().join(',');
    var json = JSON.stringify(data) + '|' + owners;

    home.data = data;
    home.shownTabs = owners;

    if (!isInventory()) addHomeTab();

    // Redrawn only when something changed, so a click is never lost to a list rebuilt under the pointer.
    if (home.ui && json === home.shownJson) return;

    home.shownJson = json;
    renderHome();
  }

  // found: this poll's findStation, looked up once for the cage slots and the stations.
  function patchStations(found) {
    if (home.isOff) return;

    if (!found) {
      dropHome();

      return;
    }

    // The bag screen stays one page while it moves between the bag alone, a storage and the basket.
    if (found.doc !== home.doc || found.station !== home.station) {
      dropHome();
      home.station = found.station;
      home.frame = found.frame;
      home.doc = found.doc;
      home.askedAt = 0;
    }

    if (home.station.scrollTabs && !home.isAway) addStyle(home.doc, 'slmh-tabcap', TAB_CAP_CSS);
    if (isInventory()) keepPop();
    else keepHomeTab();

    if (home.reopen && isInventory()) resumePop();

    // One ask learns whether the list is turned off; after that the list is kept fresh only while it shows.
    if (home.askedAt === 0 || (home.isShown && Date.now() - home.askedAt >= STATION_ASK_MS)) askStation();
  }

  // Out exploring, a storage beside the bag gets the inventory's window, as before there were storage tabs.
  function isInventory() {
    return !!home.station && (home.station.kind === 'inventory' || home.isAway);
  }

  function withTabs(own, shared) {
    var merged = {};

    Object.keys(shared).forEach(function (key) { merged[key] = shared[key]; });
    Object.keys(own).forEach(function (key) { merged[key] = own[key]; });

    return merged;
  }

  // Live, so the poll reads the page's frames without a fresh list each time.
  var allFrames = document.getElementsByTagName('iframe');

  // The station pages a frame address can be, worked out once per address instead of every poll.
  var stationPagesBySrc = {};

  function stationPagesFor(src) {
    var known = stationPagesBySrc[src];

    if (known) return known;

    known = STATION_PAGES.filter(function (station) { return station.page.test(src); });
    stationPagesBySrc[src] = known;

    return known;
  }

  function findStation() {
    var frames = allFrames;

    for (var i = 0; i < frames.length; i++) {
      var pages = stationPagesFor(frames[i].src || '');

      for (var s = 0; s < pages.length; s++) {

        var doc = null;

        try {
          doc = frames[i].contentDocument;
        } catch (e) {}

        if (doc && doc.querySelector(pages[s].host)) return { station: pages[s], frame: frames[i], doc: doc };
      }
    }

    return null;
  }

  // The trailing 1 tells the plugin this page still holds the list it last sent for these tabs, so an unchanged list
  // is not sent again.
  function askStation() {
    var hasList = !!home.data && home.shownTabs === tabOwners().join(',');

    home.askedAt = Date.now();
    send('station|' + home.station.kind + (hasList ? '|1' : ''));
  }

  // A run of clicks asks once soon after the first and once after the last, not twice for every click.
  var stationAskSoon = { first: 0, last: 0 };

  function askStationSoon() {
    if (!stationAskSoon.first) {
      stationAskSoon.first = setTimeout(function () {
        stationAskSoon.first = 0;
        if (home.station) askStation();
      }, STATION_AFTER_CLICK_MS[0]);
    }

    clearTimeout(stationAskSoon.last);
    stationAskSoon.last = setTimeout(function () {
      stationAskSoon.last = 0;
      if (home.station) askStation();
    }, STATION_AFTER_CLICK_MS[1]);
  }

  function dropHome() {
    var wasPopOpen = isPopOpen();

    removeHomeUi();
    removeHomeTab();
    home.station = null;
    home.frame = null;
    home.doc = null;
    home.data = null;
    home.shownJson = '';
    home.shownTabs = '';
    home.openId = null;
    home.isShown = false;
    home.isAway = false;

    if (wasPopOpen) holdKeys();
  }

  function removeHomeUi() {
    if (!home.ui) return;
    if (home.ui.root.parentNode) home.ui.root.parentNode.removeChild(home.ui.root);
    if (home.ui.isTyping) send('focus|0');

    home.ui = null;
  }

  function make(doc, tag, className, text) {
    var node = doc.createElement(tag);

    if (className) node.className = className;
    if (text != null) node.textContent = text;

    return node;
  }

  function addHomeStyle(doc) {
    addStyle(doc, 'slmh-style', HOME_CSS);
  }

  function addStyle(doc, id, css) {
    if (doc.getElementById(id)) return;

    var style = make(doc, 'style');
    style.id = id;
    style.textContent = css;
    doc.head.appendChild(style);
  }

  // The search, the scope buttons at a station, the list and the hint, added to box; returns the ui handles.
  function buildHomeBody(doc, root, box, sub) {
    var search = make(doc, 'div', 'slmh-search');
    var input = make(doc, 'input');
    var scope = make(doc, 'div', 'slmh-scope');
    var list = make(doc, 'div', 'slmh-list');
    var hint = make(doc, 'div', 'slmh-hint', say(isInventory() ? 'hintInv' : home.station.kind === 'basket' ? 'hintBasket' : home.station.kind === 'storage' ? 'hintStorage' : 'hintStation'));

    addHomeStyle(doc);
    // The inventory page passes clicks through to the game unless the element under the pointer is marked interactive.
    root.setAttribute('data-interactive', '1');
    search.innerHTML = SEARCH_ICON;
    input.placeholder = say('homeSearch');
    input.value = home.query;
    search.appendChild(input);
    box.appendChild(search);

    if (!isInventory()) {
      scope.appendChild(tipFor(scopeButton(doc, say(home.station.scope), false), say('scopeUsableTip')));
      scope.appendChild(tipFor(scopeButton(doc, say('scopeAll'), true), say('scopeAllTip')));
      box.appendChild(scope);
    }

    box.appendChild(list);
    box.appendChild(hint);
    home.ui = { root: root, sub: sub, input: input, scope: scope, list: list, isTyping: false };
    watchHomeSearch(input);
    list.addEventListener('click', onHomeClick);
    watchTips(doc, root);
  }

  // One half of the two-part switch: the words, then how many rows that half shows.
  function scopeButton(doc, text, isAll) {
    var node = make(doc, 'button', isAll === home.isAllScope ? 'on' : '');

    node.appendChild(make(doc, 'span', '', text));
    node.appendChild(make(doc, 'b'));

    node.addEventListener('click', function () {
      home.isAllScope = isAll;
      home.folds = {};
      renderHome();
    });

    return node;
  }

  // The game sends keys to the page only while the plugin holds them, and its own hotkeys must not see the typing.
  function watchHomeSearch(input) {
    input.addEventListener('focus', function () {
      if (home.ui) home.ui.isTyping = true;
      send('focus|1');
    });
    input.addEventListener('blur', function () {
      if (home.ui) home.ui.isTyping = false;
      send('focus|0');
    });
    input.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') input.blur();
      e.stopPropagation();
    });
    input.addEventListener('input', function () {
      home.query = input.value;
      home.folds = {};
      renderHome();
    });
  }

  // ---- The station's home tab ----

  // The game redraws its tabs whenever the bag or a storage changes, so the tab is put back on every redraw.
  function keepHomeTab() {
    if (!home.data) return;

    addHomeTab();

    if (!home.isShown) return;
    if (!home.ui || !home.ui.root.isConnected) mountPane();

    placePane();
  }

  function addHomeTab() {
    var doc = home.doc;
    var st = home.station;
    var tabs = doc && doc.querySelector(st.tabs);
    var pinned = tabs && (st.pin ? tabs.querySelector(st.pin) : tabs);
    var panel = tabs && tabs.closest(st.panel);

    // The drone trade draws no tab bar for the bag alone, so it has no home tab then.
    if (!pinned || !panel) {
      dropHomeTab();

      return;
    }

    if (panel !== home.panel || tabs !== home.tabsEl) adoptPanel(doc, tabs, panel);

    var hasGameTab = !!pinned.querySelector(gameTabSelector());

    // A page that redraws the panel may drop the classes, so they are put back on every pass.
    panel.classList.add('slmh-host');
    panel.classList.toggle('slmh-on', home.isShown);
    panel.classList.toggle('slmh-forced', !hasGameTab);

    if (hasGameTab && home.back.parentNode) home.back.parentNode.removeChild(home.back);
    if (!hasGameTab) showBackTab(doc, pinned);
    placeHomeTab(pinned);
    if (isSwitchedByKey()) leaveForSwitchedTab();

    markActiveTab();

    if (home.station.scrollTabs) keepTabScroll(doc, tabs);
  }

  // After the pinned tabs, or right after the bag's own tab where the storage tabs follow in the same bar.
  function placeHomeTab(pinned) {
    var first = home.station.homeAfterFirst ? pinned.querySelector(gameTabSelector()) : null;

    if (!first) {
      if (pinned.lastElementChild !== home.tab) pinned.appendChild(home.tab);

      return;
    }

    if (first.nextElementSibling !== home.tab) pinned.insertBefore(home.tab, first.nextElementSibling);
  }

  // The bag's tab and Home stay put at the left, the storage tabs scroll behind them, and an arrow at each end shows
  // while there are more tabs than fit. The arrows sit in the bar itself, so they stick to its ends as it scrolls.
  function keepTabScroll(doc, tabs) {
    var first = tabs.querySelector(gameTabSelector());

    if (!first) return;
    if (!home.arrows) home.arrows = makeArrows(doc, tabs);

    addHomeStyle(doc);

    var gap = parseFloat(doc.defaultView.getComputedStyle(tabs).columnGap) || 0;

    setClass(tabs, 'slmh-scroll', true);
    setClass(first, 'slmh-pin', true);
    setClass(home.tab, 'slmh-pin', true);
    setStyle(first, 'left', '0px');
    setStyle(home.tab, 'left', first.offsetWidth + gap + 'px');
    setStyle(home.arrows.back, 'left', first.offsetWidth + home.tab.offsetWidth + 2 * gap + 'px');

    if (home.tab.nextElementSibling !== home.arrows.back) tabs.insertBefore(home.arrows.back, home.tab.nextElementSibling);
    if (tabs.lastElementChild !== home.arrows.on) tabs.appendChild(home.arrows.on);

    showArrows(tabs);
    padLastStop(tabs);
    showArrows(tabs);
    revealChosenTab(tabs);
  }

  // The storage tabs scroll by whole tabs. Each stop lines one tab's left edge up with the back arrow, so no tab is
  // cut at the start; only the end of the row is cut. Offsets between siblings ignore the page's own scaling.
  function scrollingTabs(tabs) {
    return Array.prototype.filter.call(tabs.children, function (tab) {
      return tab.matches(gameTabSelector()) && !tab.classList.contains('slmh-pin');
    });
  }

  function tabStops(list) {
    return list.map(function (tab) { return tab.offsetLeft - list[0].offsetLeft; });
  }

  function arrowWidth(arrow, gap) {
    return arrow.classList.contains('on') ? arrow.offsetWidth + gap : 0;
  }

  function gapOf(tabs) {
    return parseFloat(tabs.ownerDocument.defaultView.getComputedStyle(tabs).columnGap) || 0;
  }

  // The width that shows storage tabs: the bar less the pinned tabs and both arrows.
  function tabView(tabs) {
    var gap = gapOf(tabs);
    var first = tabs.querySelector(gameTabSelector());

    return tabs.clientWidth - first.offsetWidth - home.tab.offsetWidth - 2 * gap -
      arrowWidth(home.arrows.back, gap) - arrowWidth(home.arrows.on, gap);
  }

  // Room after the last tab so the farthest scroll is a stop as well, and the last tab can show whole.
  function padLastStop(tabs) {
    var list = scrollingTabs(tabs);
    var now = parseFloat(home.arrows.on.style.marginLeft) || 0;
    var pad = 0;

    if (list.length && home.arrows.on.classList.contains('on')) {
      var end = tabs.scrollWidth - tabs.clientWidth - now;
      var stops = tabStops(list);

      for (var i = 0; end > 0 && i < stops.length; i++) {
        if (stops[i] >= end - 1) {
          pad = Math.max(0, stops[i] - end);
          break;
        }
      }
    }

    setStyle(home.arrows.on, 'marginLeft', pad ? pad + 'px' : '');
  }

  // Where the bar is, or where it is still sliding to after a step.
  function tabScrollAt(tabs) {
    return Date.now() < (tabs.slmhUntil || 0) ? tabs.slmhTarget : tabs.scrollLeft;
  }

  function scrollTabsTo(tabs, x) {
    var end = Math.max(0, tabs.scrollWidth - tabs.clientWidth);

    tabs.slmhTarget = Math.max(0, Math.min(end, x));
    tabs.slmhUntil = Date.now() + TAB_SCROLL_MS;
    tabs.scrollLeft = tabs.slmhTarget;
  }

  function stepTabs(tabs, by) {
    var list = scrollingTabs(tabs);

    if (!list.length) return;

    var stops = tabStops(list);
    var near = nearestStop(stops, tabScrollAt(tabs));

    scrollTabsTo(tabs, stops[Math.max(0, Math.min(stops.length - 1, near + by))]);
  }

  // Forward to the first tab cut off on the right; back as far as keeps the tab now at the start in view. At least
  // one tab either way, so a tab wider than the view is not stuck on.
  function pageTabs(tabs, way) {
    var list = scrollingTabs(tabs);

    if (!list.length) return;

    var stops = tabStops(list);
    var at = tabScrollAt(tabs);
    var view = tabView(tabs);
    var near = nearestStop(stops, at);
    var to = near + way;
    var i;

    if (way > 0) {
      for (i = near + 1; i < list.length && stops[i] + list[i].offsetWidth <= at + view + 1; i++) to = i + 1;
    } else {
      for (i = near - 1; i >= 0 && stops[near] + list[near].offsetWidth - stops[i] <= view + 1; i--) to = i;
    }

    scrollTabsTo(tabs, stops[Math.max(0, Math.min(stops.length - 1, to))]);
  }

  function nearestStop(stops, at) {
    var near = 0;

    for (var i = 1; i < stops.length; i++) {
      if (Math.abs(stops[i] - at) < Math.abs(stops[near] - at)) near = i;
    }

    return near;
  }

  // A and D move the game's chosen tab, maybe off the visible part of the row, so the row follows it to the
  // nearest stop that shows it whole. Only a new choice moves the row, so the player can still scroll away from it.
  function revealChosenTab(tabs) {
    var list = scrollingTabs(tabs);
    var chosen = list.filter(function (tab) {
      return tab.classList.contains('active') || tab.classList.contains('slmh-was');
    })[0];

    // Back on the bag tab, the next storage tab chosen is a new choice again.
    if (!chosen) {
      tabs.slmhChosen = null;

      return;
    }

    if (chosen === tabs.slmhChosen) return;

    tabs.slmhChosen = chosen;

    var stops = tabStops(list);
    var index = list.indexOf(chosen);
    var at = tabScrollAt(tabs);
    var right = stops[index] + chosen.offsetWidth - tabView(tabs);

    if (stops[index] < at - 1) return scrollTabsTo(tabs, stops[index]);
    if (right <= at + 1) return;

    for (var i = 0; i <= index; i++) {
      if (stops[i] >= right - 1) return scrollTabsTo(tabs, stops[i]);
    }

    // A tab wider than the view shows from its start.
    scrollTabsTo(tabs, stops[index]);
  }

  function makeArrows(doc, tabs) {
    var back = make(doc, 'div', 'slmh-arrow', '<');
    var on = make(doc, 'div', 'slmh-arrow r', '>');

    tipFor(back, say('tabsBackTip'));
    tipFor(on, say('tabsOnTip'));
    back.addEventListener('click', function () { pageTabs(tabs, -1); });
    on.addEventListener('click', function () { pageTabs(tabs, 1); });
    if (tabs.slmhScrolls) return { back: back, on: on };

    // The bar outlives its arrows when Home is taken off and put back, so it is hooked only once.
    tabs.slmhScrolls = true;
    tabs.addEventListener('scroll', function () { if (home.arrows) showArrows(tabs); });
    tabs.addEventListener('wheel', function (e) {
      if (!home.arrows || tabs.scrollWidth <= tabs.clientWidth + 1) return;

      e.preventDefault();
      // A notch is one tab; a touchpad's small deltas add up to one.
      tabs.slmhWheel = (tabs.slmhWheel || 0) + (e.deltaY || e.deltaX);
      if (Math.abs(tabs.slmhWheel) < WHEEL_NOTCH) return;
      stepTabs(tabs, tabs.slmhWheel > 0 ? 1 : -1);
      tabs.slmhWheel = 0;
    }, { passive: false });

    return { back: back, on: on };
  }

  function showArrows(tabs) {
    var isOverflowing = tabs.scrollWidth > tabs.clientWidth + 1;
    var end = tabs.scrollWidth - tabs.clientWidth - 1;

    setClass(home.arrows.back, 'on', isOverflowing);
    setClass(home.arrows.on, 'on', isOverflowing);
    setClass(home.arrows.back, 'off', tabs.scrollLeft <= 0);
    setClass(home.arrows.on, 'off', tabs.scrollLeft >= end);
  }

  // Only a real change is written: the tab bar's observer sees every write and would run again for nothing.
  function setClass(node, className, isOn) {
    if (node.classList.contains(className) !== isOn) node.classList.toggle(className, isOn);
  }

  function setStyle(node, name, value) {
    if (node.style[name] !== value) node.style[name] = value;
  }

  // A and D switch the game's tab with no click, and the game then marks the new tab active. A game tab marked active
  // while the list shows, other than the one the list was opened over, is that switch.
  function isSwitchedByKey() {
    var now = gameTabIndex('active');

    if (!home.isShown || now < 0 || now === home.underTab) return false;

    // A Home click switched it to put an item in: the list stays, over the new tab.
    if (Date.now() < home.switchingUntil) {
      home.underTab = now;

      return false;
    }

    return true;
  }

  // The tab the list was opened over is not the game's chosen one any more, so it does not get its mark back.
  function leaveForSwitchedTab() {
    Array.prototype.forEach.call(home.panel.querySelectorAll(gameTabSelector()), function (tab) {
      tab.classList.remove('slmh-was');
    });
    hideHome();
  }

  function gameTabIndex(className) {
    var tabs = home.panel.querySelectorAll(gameTabSelector());

    for (var i = 0; i < tabs.length; i++) {
      if (tabs[i].classList.contains(className)) return i;
    }

    return -1;
  }

  function dropHomeTab() {
    if (!home.panel) return;

    hideHome();
    removeHomeTab();
  }

  function gameTabSelector() {
    // Crafting Hub's bench tab shares the workbench's tab class but is not one of the game's storages.
    return '.' + home.station.tab + ':not(.slmh-tab):not(.slmh-back):not(.cc-tab)';
  }

  function adoptPanel(doc, tabs, panel) {
    var tabClass = '.' + home.station.tab;

    removeHomeTab();
    addHomeStyle(doc);
    home.panel = panel;
    home.tabsEl = tabs;
    home.tab = makeTab(doc, 'slmh-tab', say('homeTab'));
    home.back = makeTab(doc, 'slmh-back');
    home.tab.addEventListener('click', showHome);
    panel.classList.add('slmh-host');
    // Any other tab leaves the list; the game's own click on that tab then does the rest.
    tabs.addEventListener('click', function (e) {
      var tab = e.target.closest(tabClass);

      if (tab && !tab.classList.contains('slmh-tab') && !tab.classList.contains('locked') && !tab.disabled) hideHome();
    }, true);
    // A burst of the game's changes is answered by one pass on the next frame, and the changes that pass makes itself
    // are dropped afterwards, so it does not wake itself again.
    var isPassDue = false;
    var observer = new doc.defaultView.MutationObserver(function (records) {
      if (isPassDue || !records.some(isGameMutation)) return;

      isPassDue = true;
      doc.defaultView.requestAnimationFrame(function () {
        isPassDue = false;

        if (home.observer !== observer) return;

        try {
          addHomeTab();
        } catch (e) {}

        if (home.observer) home.observer.takeRecords();
      });
    });
    home.observer = observer;
    // Classes too: the drone trade's Vue rewrites a tab's class when the chosen tab changes, without a redraw.
    home.observer.observe(tabs, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });

    // The same function every time, so a page adopted again does not add a second listener. Capture on the document
    // runs before the trade page's own mouseup listener on its window.
    if (home.station.model === 'trade') doc.addEventListener('mouseup', takeBackOnHomeDrop, true);
  }

  // Our own tabs and arrows changing, or only being put in, is no news; anything else, a removal of ours included, is.
  function isGameMutation(record) {
    if (isHomeTabNode(record.target)) return false;
    if (record.type !== 'childList' || record.removedNodes.length || !record.addedNodes.length) return true;

    for (var i = 0; i < record.addedNodes.length; i++) {
      if (!isHomeTabNode(record.addedNodes[i])) return true;
    }

    return false;
  }

  function isHomeTabNode(node) {
    return !!node && (node === home.tab || node === home.back || (!!home.arrows && (node === home.arrows.back || node === home.arrows.on)));
  }

  // A drag off the drone let go over the Home list takes that offer back, as a right click on the drone does, and
  // Stacking turns a Shift or Ctrl held then into taking back part of it.
  function takeBackOnHomeDrop(e) {
    var drag = home.isShown && home.ui ? tradeDrag() : null;
    var core;

    if (e.button !== 0 || !drag || !drag.dragging || !drag.moved || drag.fromGrid !== 'drone' || !drag.item) return;
    if (!isOverHomePane(e)) return;

    core = pageCore();

    if (!core) return;

    leaveDragOffGrids();
    core.UnitySendEvent('CLICK_ITEM', { itemKey: String(drag.item.id), fromGrid: 'drone', rightClick: true });
    askStationSoon();
  }

  // The trade page keeps its drag in Vue setup state.
  function tradeDrag() {
    var setup = vueSetup();

    return setup ? setup.dragState : null;
  }

  // The Vue pages keep their state in the mounted app's setup state, reached through its container.
  function vueSetup() {
    var app = home.doc && home.doc.querySelector('#app');
    var instance = app && app._vnode && app._vnode.component;

    return instance && instance.setupState ? instance.setupState : null;
  }

  function isOverHomePane(e) {
    var rect = home.ui.root.getBoundingClientRect();

    return e.clientX >= rect.left && e.clientX <= rect.right && e.clientY >= rect.top && e.clientY <= rect.bottom;
  }

  // The bag grid is only hidden under the list, so the trade page would drop the item on it. A last move far off
  // every grid leaves the drop nowhere, and the page puts the item back where it was and ends the drag itself.
  function leaveDragOffGrids() {
    var win = home.doc.defaultView;

    win.dispatchEvent(new win.MouseEvent('mousemove', { clientX: -100000, clientY: -100000 }));
  }

  function makeTab(doc, marker, text) {
    var node = make(doc, home.station.tag, home.station.tab + ' ' + marker, text);

    if (home.station.tag === 'button') node.type = 'button';

    return node;
  }

  // Named after the panel's own title, which is the bag's name; set only on a change, as setting it is itself a redraw.
  function showBackTab(doc, pinned) {
    var title = doc.getElementById('bagTitleText') || doc.getElementById('bagPanelTitle');
    var name = (title && title.textContent.trim()) || say('homeBag');

    if (home.back.textContent !== name) home.back.textContent = name;
    if (pinned.firstElementChild !== home.back) pinned.insertBefore(home.back, pinned.firstElementChild);
  }

  // While the list shows, the game's chosen tab is only marked, so the game still knows which bag sits underneath.
  function markActiveTab() {
    var tabs = home.panel.querySelectorAll(gameTabSelector());

    home.tab.classList.toggle('active', home.isShown);
    home.back.classList.toggle('active', !home.isShown);
    Array.prototype.forEach.call(tabs, function (tab) {
      if (home.isShown && tab.classList.contains('active')) {
        tab.classList.remove('active');
        tab.classList.add('slmh-was');
      } else if (!home.isShown && tab.classList.contains('slmh-was')) {
        tab.classList.remove('slmh-was');
        tab.classList.add('active');
      }
    });
  }

  function removeHomeTab() {
    if (home.observer) home.observer.disconnect();
    if (home.tab && home.tab.parentNode) home.tab.parentNode.removeChild(home.tab);
    if (home.back && home.back.parentNode) home.back.parentNode.removeChild(home.back);
    if (home.arrows) removeArrows();

    if (home.panel) {
      home.panel.classList.remove('slmh-host', 'slmh-on', 'slmh-forced');
      Array.prototype.forEach.call(home.panel.querySelectorAll('.slmh-was'), function (tab) {
        tab.classList.remove('slmh-was');
        tab.classList.add('active');
      });
    }

    home.observer = null;
    home.tab = null;
    home.back = null;
    home.arrows = null;
    home.panel = null;
    home.tabsEl = null;
  }

  function removeArrows() {
    [home.arrows.back, home.arrows.on].forEach(function (arrow) {
      if (arrow.parentNode) arrow.parentNode.removeChild(arrow);
    });

    if (!home.tabsEl) return;

    home.tabsEl.classList.remove('slmh-scroll');
    Array.prototype.forEach.call(home.tabsEl.querySelectorAll('.slmh-pin'), function (tab) {
      tab.classList.remove('slmh-pin');
      tab.style.left = '';
    });
  }

  function showHome() {
    if (home.isShown || !home.panel) return;

    home.isShown = true;
    mountPane();
    placePane();
    markActiveTab();
    home.underTab = gameTabIndex('slmh-was');
    askStation();
  }

  function hideHome() {
    if (!home.isShown) return;

    home.isShown = false;
    removeHomeUi();

    if (!home.panel) return;

    home.panel.classList.remove('slmh-on');
    markActiveTab();
  }

  function mountPane() {
    var doc = home.doc;
    var root = make(doc, 'div', 'slmh-pane');

    removeHomeUi();
    buildHomeBody(doc, root, root, make(doc, 'span'));
    home.panel.appendChild(root);
    home.panel.classList.add('slmh-on');
    // Never shrunk, as it stays inside the bag panel at any size: a bigger size shows bigger rows in the same box.
    // Placed again for each new size.
    uiSize(root, { fit: false, onZoom: function () { if (home.ui && home.ui.root === root) placePane(); } });
    renderHome();
  }

  // The pane covers the panel below its tab bar, inside the panel's padding. Those are the panel's own pixels, and
  // the pane's zoom scales its offsets, so they are written over its zoom to keep the same box at any size.
  function placePane() {
    var root = home.ui.root;
    var tabs = home.tabsEl;
    var style = home.doc.defaultView.getComputedStyle(home.panel);
    var zoom = uiZoom(root);
    var inset = function (px) { return Math.round((parseFloat(px) || 0) / zoom) + 'px'; };
    var place = {
      top: inset(offsetTopIn(tabs, home.panel) + tabs.offsetHeight + PANE_TAB_GAP),
      left: inset(style.paddingLeft),
      right: inset(style.paddingRight),
      bottom: inset(style.paddingBottom)
    };

    Object.keys(place).forEach(function (side) {
      if (root.style[side] !== place[side]) root.style[side] = place[side];
    });
  }

  // How far below the panel's top a node sits; the basket screen's tab bar sits inside a positioned header.
  function offsetTopIn(node, panel) {
    var top = 0;

    for (var at = node; at && at !== panel; at = at.offsetParent) top += at.offsetTop;

    return top;
  }

  // ---- The inventory's window ----

  function isPopOpen() {
    return home.isShown && isInventory();
  }

  // The game sends the page no keys, so the plugin holds the game's hotkeys back while the window is open, and
  // closes it on Esc itself.
  function holdKeys() {
    send('menu|' + (isPopOpen() ? '1' : '0'));
  }

  function openPop() {
    if (!isInventory() || home.isShown) return;

    home.isShown = true;
    holdKeys();
    mountPop();
    askStation();
    home.ui.input.focus();
  }

  function closePop() {
    if (!isPopOpen()) return;

    home.isShown = false;
    removeHomeUi();
    holdKeys();
  }

  // The user closing the window also stops an Open from putting it back.
  function shutPop() {
    home.reopen = null;
    closePop();
  }

  // Kept up through the window's whole wait, as the screen may still be on its way down when it is first seen.
  function resumePop() {
    var reopen = home.reopen;

    if (Date.now() > reopen.until) {
      home.reopen = null;

      return;
    }

    if (!home.doc.querySelector('.bag-slot .toolbar') || !backpackTabWantsButton(home.doc)) return;
    if (!home.isShown) openPop();

    if (home.openId !== reopen.id) {
      home.openId = reopen.id;
      renderHome();
    }
  }

  // Shown on the bag side's item tab only, where the Mods menu that opens it shows.
  function keepPop() {
    if (!home.isShown) return;

    if (!home.doc.querySelector('.bag-slot .toolbar') || !backpackTabWantsButton(home.doc)) {
      closePop();

      return;
    }

    if (!home.ui || !home.ui.root.isConnected) mountPop();

    placePop();
  }

  function mountPop() {
    var doc = home.doc;
    var root = make(doc, 'div', 'slmh-pop');
    var head = make(doc, 'div', 'slmh-head');
    var sub = make(doc, 'span', 'slmh-sub');
    var shut = make(doc, 'button', 'slmh-x', TIMES);

    removeHomeUi();
    shut.addEventListener('click', shutPop);
    tipFor(shut, say('homeShutTip'));
    head.appendChild(make(doc, 'span', 'slmh-title', say('homeTitle')));
    head.appendChild(sub);
    head.appendChild(shut);
    root.appendChild(head);
    buildHomeBody(doc, root, root, sub);
    doc.body.appendChild(root);
    // Never shrunk, as placePop keeps it inside the window at any size, and placed again for each new size.
    uiSize(root, { fit: false, onZoom: function () { if (home.ui && home.ui.root === root) placePop(); } });
    placePop();
    renderHome();
  }

  // Beside the inventory's panels, the open storage's included: left if there is room, else right, else at the edge.
  // As tall as the panels, so a bigger UI size shows bigger rows rather than a taller window. The panels' box and the
  // window are in screen pixels; the window's own left, top and height are in its zoomed pixels.
  function placePop() {
    var win = home.doc.defaultView;
    var root = home.ui.root;
    var zoom = uiZoom(root);
    var width = POP_WIDTH * zoom;
    var box = panelsBox(home.doc);
    var height = Math.min(win.innerHeight - 2 * POP_EDGE, Math.max(POP_MIN_HEIGHT * zoom, box.bottom - box.top));
    var top = Math.max(POP_EDGE, Math.min(box.top, win.innerHeight - POP_EDGE - height));
    var left = POP_EDGE;

    if (box.left - POP_GAP - width >= POP_EDGE) left = box.left - POP_GAP - width;
    else if (box.right + POP_GAP + width <= win.innerWidth - POP_EDGE) left = box.right + POP_GAP;

    var place = { left: Math.round(left / zoom) + 'px', top: Math.round(top / zoom) + 'px', width: POP_WIDTH + 'px', height: Math.round(height / zoom) + 'px' };

    Object.keys(place).forEach(function (side) {
      if (root.style[side] !== place[side]) root.style[side] = place[side];
    });
  }

  function panelsBox(doc) {
    var win = doc.defaultView;
    var box = { left: win.innerWidth / 2, right: win.innerWidth / 2, top: POP_EDGE, bottom: win.innerHeight - POP_EDGE };
    var isFirst = true;

    Array.prototype.forEach.call(doc.querySelectorAll('.bag-slot .panel'), function (panel) {
      var rect = panel.getBoundingClientRect();

      if (!rect.width) return;

      box.left = isFirst ? rect.left : Math.min(box.left, rect.left);
      box.right = isFirst ? rect.right : Math.max(box.right, rect.right);
      box.top = isFirst ? rect.top : Math.min(box.top, rect.top);
      box.bottom = isFirst ? rect.bottom : Math.max(box.bottom, rect.bottom);
      isFirst = false;
    });

    return box;
  }

  // ---- The list ----

  function renderHome() {
    if (!home.ui) return;

    var ui = home.ui;
    var doc = ui.root.ownerDocument;
    var scrollTop = ui.list.scrollTop;
    var shown = home.data ? homeStorages() : [];

    ui.sub.textContent = home.data ? say('homeSub', home.data.storages.length) : '';
    var counts = home.data ? scopeCounts() : [0, 0];
    Array.prototype.forEach.call(ui.scope.children, function (node, index) {
      node.className = (index === 1) === home.isAllScope ? 'on' : '';
      node.lastChild.textContent = String(counts[index]);
    });
    ui.list.innerHTML = '';

    if (!shown.length) ui.list.appendChild(make(doc, 'div', 'slmh-empty', homeEmptyLine()));

    shown.forEach(function (entry) { ui.list.appendChild(storageNode(doc, entry)); });
    ui.list.scrollTop = scrollTop;
  }

  function homeEmptyLine() {
    if (!home.data) return say('homeLoading');
    if (home.query.trim()) return say('homeNoMatch');
    if (isUsableScope()) return say('homeNone');

    return say('homeEmpty');
  }

  // How many rows the search leaves usable here, and in all.
  function scopeCounts() {
    var query = homeQuery();
    var counts = [0, 0];

    home.data.storages.forEach(function (storage) {
      storage.items.forEach(function (item) {
        if (!matchesHome(item, query)) return;

        counts[1]++;

        if (item.ok) counts[0]++;
      });
    });

    return counts;
  }

  function homeQuery() {
    return home.query.trim().toLowerCase().split(/\s+/).filter(Boolean);
  }

  function matchesHome(item, query) {
    var name = String(item.name).toLowerCase();

    return query.every(function (word) { return name.indexOf(word) >= 0; });
  }

  function isUsableScope() {
    return !isInventory() && !home.isAllScope;
  }

  // Each storage with the rows the scope and the search leave; one with none left is not shown.
  function homeStorages() {
    var query = homeQuery();
    var isUsableOnly = isUsableScope();
    var tabs = isInventory() ? [] : tabOwners();

    return home.data.storages.map(function (storage) {
      var rows = storage.items.filter(function (item) { return (!isUsableOnly || item.ok) && matchesHome(item, query); });

      return { storage: storage, rows: rows, hasTab: tabs.indexOf(storage.id) >= 0 };
    }).filter(function (entry) {
      return entry.rows.length > 0 || (!query.length && !isUsableOnly && isInventory());
    });
  }

  function isFolded(entry) {
    var choice = home.folds[entry.storage.id];

    if (choice !== undefined) return choice;

    return !(home.query.trim() || isUsableScope());
  }

  function storageNode(doc, entry) {
    var box = make(doc, 'div');
    var head = make(doc, 'div', 'slmh-store' + (home.openId === entry.storage.id ? ' on' : ''));
    var isFold = isFolded(entry);
    var isInventoryList = isInventory();

    head.setAttribute('data-fold', entry.storage.id);
    head.appendChild(make(doc, 'span', 'slmh-car', isFold ? '\u25b6' : '\u25bc'));
    head.appendChild(make(doc, 'span', '', entry.storage.name));
    head.appendChild(make(doc, 'span', 'cnt', isUsableScope() ? say('homeUsable', entry.rows.length) : say('homeItems', entry.rows.length)));

    if (isInventoryList || entry.hasTab) head.appendChild(openButton(doc, entry.storage.id));

    box.appendChild(head);

    if (!isFold) entry.rows.forEach(function (item) { box.appendChild(homeRow(doc, entry.storage.id, item)); });

    return box;
  }

  function openButton(doc, id) {
    var isOpenHere = home.openId === id;
    var node = make(doc, 'button', 'slmh-open' + (isOpenHere ? ' on' : ''), say(isOpenHere ? 'homeClose' : 'homeOpen'));

    node.setAttribute('data-open', id);
    tipFor(node, say(isOpenHere ? 'homeCloseTip' : isInventory() ? 'homeOpenTip' : 'homeTabTip'));

    return node;
  }

  function homeRow(doc, storageId, item) {
    var row = make(doc, 'div', 'slmh-row');
    var box = make(doc, 'div', 'slmh-ic');

    row.setAttribute('data-storage', storageId);
    row.setAttribute('data-item', item.item);
    // Info Pack shows its item tooltip over anything carrying the item's config id.
    row.setAttribute('data-slip-item', item.cfg);
    row.setAttribute('data-item-name', item.name);
    row.setAttribute('data-item-tip', say('homeItemTip', item.count, (findHomeStorage(storageId) || { name: '' }).name));

    if (item.icon) {
      var img = make(doc, 'img');
      img.src = new URL(item.icon, location.href).href;
      box.appendChild(img);
    } else {
      box.textContent = String(item.name || '?').charAt(0).toUpperCase();
    }

    row.appendChild(box);
    row.appendChild(make(doc, 'span', 'n', item.name));
    row.appendChild(make(doc, 'span', 'x', TIMES + item.count));

    if (!isInventory()) row.appendChild(rowTag(doc, item));

    return row;
  }

  // What a click does with the row: into the station, or into the bag. At the basket a row she wants says so.
  function rowTag(doc, item) {
    if (item.ok && item.wish) return make(doc, 'span', 'slmh-tag st wish', say('tagWish'));

    return make(doc, 'span', 'slmh-tag ' + (item.ok ? 'st' : 'bag'), say(item.ok ? 'tagUse' : 'tagBag'));
  }

  function onHomeClick(e) {
    var open = e.target.closest('[data-open]');
    var row = e.target.closest('[data-item]');
    var fold = e.target.closest('[data-fold]');

    if (open) {
      e.stopPropagation();
      openStorage(open.getAttribute('data-open'));
    } else if (row) {
      clickHomeItem(row.getAttribute('data-storage'), row.getAttribute('data-item'));
    } else if (fold) {
      var id = fold.getAttribute('data-fold');
      home.folds[id] = !isFolded({ storage: { id: id } });
      renderHome();
    }
  }

  // On the inventory screen the storage opens beside the bag, the way walking up to it does; at a station the list
  // makes way for the storage's own tab.
  function openStorage(id) {
    if (!isInventory()) {
      hideHome();
      switchTab(id);

      return;
    }

    home.openId = home.openId === id ? null : id;
    home.reopen = { id: home.openId, until: Date.now() + REOPEN_MS };
    send('openStorage|' + (home.openId === null ? '-1' : id));
    renderHome();
  }

  function clickHomeItem(storageId, itemId) {
    var storage = findHomeStorage(storageId);
    var item = storage && storage.items.filter(function (it) { return it.item === itemId; })[0];

    if (!item) return;

    var container = stationContainer();

    if (container && item.ok && tabOwners().indexOf(storageId) >= 0) {
      // The shredder's and workbench's own clicks put a whole stack in; a Home click feeds them one unit at a time.
      send('putOne|' + item.cfg + '|' + storageId + '|' + container);
    } else if (!isInventory() && item.ok && tabOwners().indexOf(storageId) >= 0) {
      putInStation(storageId, itemId);
    } else {
      // At a station a click takes one unit, so a stack is not dragged out whole to feed it one at a time.
      send((isInventory() ? 'take|' : 'takeOne|') + item.cfg + '|' + storageId);
    }

    askStationSoon();
  }

  // The station's own container that a Home click feeds one unit at a time, as the page records it: the shredder's
  // hopper, or the workbench's grid in its last bag message. Owner ids stay well inside what a page number holds exactly.
  function stationContainer() {
    var win = home.frame && home.frame.contentWindow;
    var id = null;

    if (!win) return null;
    if (home.station.kind === 'shredder') id = win.State && win.State.hopperOwnerId;
    if (home.station.kind === 'workbench') id = (pageGlobal(win, 'lastBagMsg') || {}).workbenchOwnerId;

    return id ? String(id) : null;
  }

  function findHomeStorage(id) {
    return home.data ? home.data.storages.filter(function (storage) { return storage.id === id; })[0] : null;
  }

  function pageCore() {
    var win = home.frame && home.frame.contentWindow;

    if (!win) return null;

    var core = win.core || pageGlobal(win, 'core');

    if (core && core.UnitySendEvent) return core;

    // The drone trade keeps its sender inside Vue. Root sends a message on to the game for the page it names, and a
    // page's frame is named after it.
    var pageId = home.frame.id;

    return pageId ? { UnitySendEvent: function (type, data) { window.postMessage({ type: type, data: data, sourcePageId: pageId }, '*'); } } : null;
  }

  // A page's top-level let and const are not window properties, so they are read through the page's own scope.
  function pageGlobal(win, name) {
    try {
      return win.eval('typeof ' + name + ' === "undefined" ? null : ' + name);
    } catch (e) {
      return null;
    }
  }

  function switchTab(ownerId) {
    var core = pageCore();
    var model = home.station.model;

    if (!core || activeOwner() === ownerId) return;

    if (model === 'trade') {
      core.UnitySendEvent('SWITCH_BAG', { ownerId: String(ownerId) });
    } else if (model === 'cart') {
      var index = cartTabs().map(function (t) { return String(t.ownerId); }).indexOf(ownerId);

      if (index >= 0) core.UnitySendEvent('TAB_SWITCH', { tab: index });
    } else if (model === 'atab') {
      switchBagSideTab(core, tabOwners().indexOf(ownerId));
    } else if (model === 'tooltable') {
      var cabinet = toolTableTabs().map(function (t) { return String(t.ownerId); }).indexOf(ownerId);
      var switchLeftTab = pageGlobal(home.frame.contentWindow, 'switchLeftTab');

      if (cabinet < 0) return;
      if (typeof switchLeftTab === 'function') switchLeftTab(2 + cabinet);
      else core.UnitySendEvent('TAB_SWITCH', { tab: 2 + cabinet });
    } else {
      core.UnitySendEvent('SWITCH_BAG', { ownerId: Number(ownerId) });
    }
  }

  // The page's own tab click, so Vue marks the tab at once; the event alone if the page cannot be reached.
  function switchBagSideTab(core, index) {
    var setup = vueSetup();

    if (index < 0) return;
    if (setup && typeof setup.onSwitchATab === 'function') setup.onSwitchATab(index);
    else core.UnitySendEvent('SWITCH_A_TAB', { tab: index });
  }

  function putInStation(ownerId, itemId) {
    var startedAt = Date.now();

    home.switchingUntil = startedAt + TAB_WAIT_MS;
    switchTab(ownerId);

    (function clickWhenShown() {
      if (!home.station) return;
      if (activeOwner() === ownerId) return clickInStation(itemId);
      if (Date.now() - startedAt < TAB_WAIT_MS) setTimeout(clickWhenShown, TAB_POLL_MS);
    })();
  }

  // The same events the station's own item click sends, a live mouse going into a free mouse slot included.
  function clickInStation(itemId) {
    var core = pageCore();
    var pageState = home.frame.contentWindow.State;

    if (!core) return;

    if (home.station.kind === 'ratcage' && pageState && pageState.bagItems && pageState.mice) {
      var held = pageState.bagItems.filter(function (it) { return String(it.id) === itemId; })[0];

      if (held && held.isMouse && pageState.mice.length < pageState.mouseSlotCount) {
        core.UnitySendEvent('MOUSE_ADD', { itemId: Number(itemId) });

        return;
      }
    }

    // The drone trade gives an item with a right click on the bag, and the basket takes one with the bag screen's quick
    // transfer from the bag side; the other stations take it as the page's own click.
    if (home.station.model === 'trade') core.UnitySendEvent('CLICK_ITEM', { itemKey: String(itemId), fromGrid: 'bag', rightClick: true });
    else if (home.station.model === 'atab') core.UnitySendEvent('QUICK_TRANSFER_ITEM', { itemId: Number(itemId) });
    else core.UnitySendEvent('ITEM_CLICK', { itemId: Number(itemId), rightClick: !!home.station.quick });

    askStationSoon();
  }

  function toolTableTabs() {
    var win = home.frame && home.frame.contentWindow;

    return (win && (pageGlobal(win, 'lastBagMsg') || {}).cabinetTabs) || [];
  }

  function cartTabs() {
    var win = home.frame && home.frame.contentWindow;

    return (win && win.State && win.State.cartTabs) || [];
  }

  // The rat cage, brewing barrel and dye vat keep their tabs in State, the shredder and ration press keep a cart in
  // State, the stove keeps the owner on each tab element, and the drone trade on each tab's data-owner.
  function activeOwner() {
    var win = home.frame && home.frame.contentWindow;
    var tabClass = '.' + home.station.tab;
    // While the home tab shows, the game's chosen tab is marked slmh-was instead of active.
    var tab = win && win.document.querySelector(tabClass + '.active:not(.slmh-tab):not(.slmh-back):not(.cc-tab), ' + tabClass + '.slmh-was');

    if (!win) return null;
    if (home.station.model === 'trade') return tab && tab.getAttribute('data-owner');
    if (home.station.model === 'cart') return win.State && win.State.cartOwnerId != null ? String(win.State.cartOwnerId) : null;
    if (home.station.model === 'tooltable') return toolTableOwner(pageGlobal(win, 'activeLeftTab'));
    if (home.station.model === 'atab') return bagSideOwner();
    if (win.State && win.State.bagOwnerId != null) return String(win.State.bagOwnerId);

    return tab && tab._ownerId != null ? String(tab._ownerId) : null;
  }

  // The bag side's owner once the game has sent its items, so a put waits for the switched tab to load.
  function bagSideOwner() {
    var setup = vueSetup();
    var owner = setup && setup.bagA ? setup.bagA.ownerId : null;

    return owner ? String(owner) : null;
  }

  function toolTableOwner(leftTab) {
    var cabinet = typeof leftTab === 'number' && leftTab >= 2 ? toolTableTabs()[leftTab - 2] : null;

    return cabinet ? String(cabinet.ownerId) : null;
  }

  function tabOwners() {
    var win = home.frame && home.frame.contentWindow;

    if (!win) return [];

    if (home.station.model === 'trade') {
      return Array.prototype.map.call(win.document.querySelectorAll('.bag-tab[data-owner]'), function (t) { return t.getAttribute('data-owner'); });
    }

    if (home.station.model === 'atab') return (home.data && home.data.tabs) || [];

    if (home.station.model === 'tooltable') {
      return toolTableTabs().filter(function (t) { return !t.isRack; }).map(function (t) { return String(t.ownerId); });
    }

    if (home.station.model === 'cart') {
      return cartTabs().filter(function (t) { return !t.locked; }).map(function (t) { return String(t.ownerId); });
    }

    if (win.State && win.State.bagTabs) {
      return win.State.bagTabs.filter(function (t) { return !t.locked; }).map(function (t) { return String(t.ownerId); });
    }

    return Array.prototype.filter.call(win.document.querySelectorAll('.bag-tab'), function (t) {
      return t._ownerId != null && !t.classList.contains('locked');
    }).map(function (t) { return String(t._ownerId); });
  }

  // A big rat cage has rows of mouse slots that make the middle panel taller than the screen, and the page then zooms
  // everything down to fit. The slots scroll past three rows instead. Mice are dropped on the slot box as a whole, so
  // a scrolled box takes them the same.
  var CAGE_SLOT_CSS = [
    '#mouseSlots{max-height:' + (3 * 64 + 2 * 8) + 'px;overflow-y:auto;overflow-x:hidden}',
    '#mouseSlots::-webkit-scrollbar{width:6px}',
    '#mouseSlots::-webkit-scrollbar-thumb{background:rgba(164,181,70,.45);border-radius:3px}'
  ].join('');

  function patchCageSlots(found) {
    if (!found || found.station.kind !== 'ratcage' || found.doc.getElementById('slmh-cage-style')) return;

    var style = make(found.doc, 'style');

    style.id = 'slmh-cage-style';
    style.textContent = CAGE_SLOT_CSS;
    found.doc.head.appendChild(style);
    // The page refits its zoom on resize, so the shorter panel takes effect at once.
    found.frame.contentWindow.dispatchEvent(new found.frame.contentWindow.Event('resize'));
  }

  // The key and the tip in the plugin's language: worked out again only when either changed, not every poll.
  var hudStampFor = { key: null, words: null, stamp: '' };

  function hudStamp() {
    if (hudStampFor.key !== hud.key || hudStampFor.words !== words()) {
      hudStampFor.key = hud.key;
      hudStampFor.words = words();
      hudStampFor.stamp = hud.key + '|' + say('hudTip');
    }

    return hudStampFor.stamp;
  }

  // Whether a save is loaded, so the HUD button shows, and the panel key its tip names.
  function live(isLive, key) {
    hud.isLive = !!isLive;
    hud.key = key || '';
    patchHud();
  }

  var hasReportedHudAccess = false;
  var hasReportedHudPlaced = false;

  // The HUD is a same-origin iframe the game creates and drops whenever it likes, so the button is checked every poll.
  function patchHud() {
    var frames = allFrames;

    for (var i = 0; i < frames.length; i++) {
      if ((frames[i].src || '').indexOf('CoreUI1.html') < 0) continue;

      var doc = null;

      try {
        doc = frames[i].contentDocument;
      } catch (e) {}

      if (!doc) {
        if (!hasReportedHudAccess) send('diag|cannot reach the HUD page document (root ' + location.protocol + ', iframe ' + frames[i].src + ')');
        hasReportedHudAccess = true;
        continue;
      }

      placeHudButton(doc);
    }
  }

  // First in the tool column, as the other mods' buttons are: the column is anchored at the bottom, so the first child
  // sits on top and the game's own buttons do not move. Drawn again only when the key or the language changed, so the
  // game's hover state on it is not reset every poll.
  function placeHudButton(doc) {
    var stamp = hudStamp();

    // The button this page last placed, still in place and current, needs no lookups.
    if (hud.isLive && hud.button && hud.button.ownerDocument === doc && hud.button.isConnected && hud.button.getAttribute('data-stamp') === stamp) return;

    var column = doc.querySelector('.vertical-tools');
    var existing = doc.querySelector('.' + HUD_CLASS);

    if (!column || !hud.isLive) {
      if (existing) existing.remove();

      return;
    }

    if (existing && existing.parentNode === column && existing.getAttribute('data-stamp') === stamp) {
      hud.button = existing;

      return;
    }

    if (existing) existing.remove();

    var node = hudButton(doc);
    hud.button = node;
    node.setAttribute('data-stamp', stamp);
    node.setAttribute('data-etk-dock', DOCK_ORDER);
    column.insertBefore(node, column.firstChild);

    if (!hasReportedHudPlaced) send('diag|HUD button placed in the tool column');
    hasReportedHudPlaced = true;
  }

  // The same markup as the game's bag button: its classes, an svg icon, and the hover tip its stylesheet shows. The
  // HUD page watches its body for new buttons and tells the game where they are, so a click on this one reaches it.
  function hudButton(doc) {
    var node = doc.createElement('button');
    var svg = doc.createElementNS(SVG_NS, 'svg');
    var path = doc.createElementNS(SVG_NS, 'path');
    node.className = 'ui-btn icon-sq interactive-btn has-entry-tip ' + HUD_CLASS;
    node.setAttribute('data-interactive', '');
    svg.setAttribute('class', 'icon-svg');
    svg.setAttribute('viewBox', '0 0 24 24');
    path.setAttribute('d', CHEST_PATH);
    svg.appendChild(path);
    node.appendChild(svg);
    node.appendChild(entryTip(doc));
    node.addEventListener('click', function (e) {
      e.stopPropagation();
      send('toggle');
    });

    return node;
  }

  function entryTip(doc) {
    var tip = doc.createElement('div');
    var title = doc.createElement('div');
    var body = doc.createElement('div');
    tip.className = 'entry-tip';
    title.className = 'entry-tip-title';
    title.textContent = 'Storage Hub';
    body.className = 'entry-tip-body';
    body.textContent = say('hudTip');

    if (hud.key) {
      var key = doc.createElement('span');
      key.className = 'entry-tip-key';
      key.textContent = hud.key;
      title.appendChild(key);
    }

    tip.appendChild(title);
    tip.appendChild(body);

    return tip;
  }

  setInterval(function () {
    try {
      patchHud();
    } catch (e) {}

    var found = null;

    try {
      found = findStation();
    } catch (e) {
      return;
    }

    try {
      patchCageSlots(found);
    } catch (e) {}

    try {
      patchStations(found);
    } catch (e) {}
  }, 400);

  window.__storagehub = { open: open, setData: setData, close: close, status: status, catalog: catalog, station: station, openPop: openPop, closePop: shutPop, live: live };
  send('ready');
})();
