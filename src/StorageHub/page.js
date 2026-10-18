// Storage Hub's web panel. Runs in Root.html, the document every game page iframe sits in, and draws over all of
// them. Styled with the game's own colours and fonts so it reads as part of the game. Also puts the put-away actions
// in the inventory toolbar's Mods menu, the dropdown it shares with other mods (see the slmod-menu block). Talks to
// the plugin through vuplex.postMessage("slmod|storagehub|<verb>|<args>") and is driven by window.__storagehub.
(function () {
  if (window.__storagehub) return;

  // ===== BEGIN slmod-menu v1. Shared block: keep it byte-identical in every mod that carries it. =====
  // Contract "slmod-menu" (window.__slmodmenu): the inventory toolbar's one "Mods" button and its dropdown, shared by
  // every mod that adds an inventory action, so the mods take one button's room between the game's discard and sort
  // buttons instead of one each. Each mod's page.js carries this block and reaches the menu only through slmodMenu,
  // which looks window.__slmodmenu up on every call. The first mod to load creates the menu. A block with a higher
  // VERSION takes it over and keeps every entry and watcher; a lower or equal one only registers. A new version may
  // add methods and entry fields, never drop or change one, and its retire() keeps returning { entries, watchers } in
  // this shape. The game sends the page no keys, so each plugin calls window.__slmodmenu.close() itself on Esc.
  // Carried by sl-storage-hub and sl-unpack-all.
  //
  //   slmodMenu.add(entry)                  adds or replaces the entry with entry.id; false without an id or run
  //   slmodMenu.update(id, changes)         changes some of an entry's fields; false for an unknown id
  //   slmodMenu.remove(id)
  //   slmodMenu.setWanted(id, isWanted)     only wanted entries are listed; with none, the button is taken away
  //   slmodMenu.place(doc)                  makes an inventory page's toolbar match the entries; call it on every
  //                                         poll, since Vue drops foreign nodes when it re-renders the toolbar
  //   slmodMenu.close()                     closes the menu if it is open
  //   slmodMenu.watch(owner, onOpenChange)  onOpenChange(isOpen) runs each time the menu opens or closes
  //
  // entry: { id, label, title, icon, hotkey, order, wanted, run }. icon is the d of an svg path on a 24-unit grid,
  // hotkey is shown as given ('' shows none), rows sort by order and then id, an entry is not wanted until it says
  // so, and run() is called with no arguments once the menu has closed.
  var slmodMenu = (function () {
    var VERSION = 1;
    var SVG_NS = 'http://www.w3.org/2000/svg';
    var GONE_CHECK_MS = 400;
    // Four tiles: room for whatever the mods add.
    var MENU_ICON = 'M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z';
    // The look comes from the game's own category dropdown on this page (category-dropdown-wrapper, -btn,
    // category-dropdown, category-option). These rules only open it upwards, as the toolbar is the panel's last row,
    // point the caret the way it opens, lay out a row's icon, label and hotkey, and give the button the spacing the
    // game gives its toolbar buttons beside the robot switches.
    var CSS = [
      '.slmm-wrap{display:flex}',
      '.toolbar.has-rsw .slmm-wrap{margin-left:auto}',
      '.slmm-wrap .slmm-menu{top:auto;bottom:calc(100% + 4px)}',
      '.slmm-btn::after{transform:rotate(180deg)}',
      '.slmm-btn.open::after{transform:none}',
      '.slmm-row{display:flex;align-items:center;gap:8px}',
      '.slmm-row svg{flex:none;width:14px;height:14px;fill:currentColor;opacity:.8}',
      '.slmm-key{margin-left:auto;padding-left:18px;font-size:11px;opacity:.6}'
    ].join('\n');

    function createHub(snapshot) {
      var entries = {};
      var watchers = {};
      var openMenu = null;
      var isRetired = false;

      function add(entry) {
        if (!entry || typeof entry.id !== 'string' || !entry.id || typeof entry.run !== 'function') return false;

        entries[entry.id] = {
          id: entry.id,
          label: String(entry.label || entry.id),
          title: String(entry.title || ''),
          icon: String(entry.icon || ''),
          hotkey: String(entry.hotkey || ''),
          order: typeof entry.order === 'number' ? entry.order : 0,
          wanted: !!entry.wanted,
          run: entry.run
        };

        return true;
      }

      function update(id, changes) {
        var entry = entries[id];
        var merged = {};

        if (!entry || !changes) return false;

        Object.keys(entry).forEach(function (key) { merged[key] = entry[key]; });
        Object.keys(changes).forEach(function (key) { merged[key] = changes[key]; });
        merged.id = id;

        return add(merged);
      }

      function remove(id) {
        delete entries[id];
      }

      function setWanted(id, isWanted) {
        return update(id, { wanted: !!isWanted });
      }

      function watch(owner, onOpenChange) {
        if (typeof owner !== 'string' || typeof onOpenChange !== 'function') return false;

        watchers[owner] = onOpenChange;

        return true;
      }

      function wantedEntries() {
        return Object.keys(entries)
          .map(function (id) { return entries[id]; })
          .filter(function (entry) { return entry.wanted; })
          .sort(function (a, b) { return a.order - b.order || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0); });
      }

      function place(doc) {
        if (isRetired || !doc) return;

        var bar = doc.querySelector('.bag-slot .toolbar');
        var rows = wantedEntries();
        var existing = doc.querySelector('.slmm-wrap');
        var isWanted = !!bar && rows.length > 0;
        var isInPlace = !!existing && existing.parentNode === bar && existing.getAttribute('data-slmm-version') === String(VERSION);

        if (isWanted && isInPlace) {
          if (openMenu && openMenu.wrap === existing) showRows(rows);

          return;
        }

        if (openMenu && openMenu.doc === doc) close();
        if (existing) existing.remove();
        if (!isWanted) return;

        addStyle(doc);
        bar.insertBefore(menuButton(doc), firstGameButton(bar));
      }

      // The game's sort button, so the menu sits between it and the discard button.
      function firstGameButton(bar) {
        for (var node = bar.firstElementChild; node; node = node.nextElementSibling) {
          if (node.classList.contains('action-btn') && !node.classList.contains('discard-btn')) return node;
        }

        return null;
      }

      function menuButton(doc) {
        var wrap = doc.createElement('div');
        var trigger = doc.createElement('button');

        wrap.className = 'category-dropdown-wrapper slmm-wrap';
        wrap.setAttribute('data-slmm-version', String(VERSION));
        trigger.className = 'action-btn unity-interactive category-dropdown-btn slmm-btn';
        trigger.title = 'Mod actions';
        trigger.appendChild(icon(doc, MENU_ICON));
        trigger.appendChild(text(doc, 'btn-txt', 'Mods'));
        trigger.addEventListener('click', function (e) {
          e.stopPropagation();
          toggle(doc, wrap);
        });
        wrap.appendChild(trigger);

        return wrap;
      }

      function toggle(doc, wrap) {
        if (isRetired) return;

        var wasOpenHere = !!openMenu && openMenu.wrap === wrap;

        close();

        if (!wasOpenHere) open(doc, wrap);
      }

      function open(doc, wrap) {
        var win = doc.defaultView;
        var rows = wantedEntries();

        if (!win || rows.length === 0) return;

        var menu = doc.createElement('div');

        menu.className = 'category-dropdown slmm-menu';
        menu.addEventListener('click', onMenuClick);
        wrap.appendChild(menu);
        wrap.querySelector('.slmm-btn').classList.add('open');
        openMenu = { doc: doc, win: win, wrap: wrap, menu: menu, shown: '', timer: setInterval(closeIfGone, GONE_CHECK_MS) };
        showRows(rows);
        win.addEventListener('mousedown', onOutsideDown, true);
        win.addEventListener('keydown', onKeyDown, true);
        notify(true);
      }

      function close() {
        if (!openMenu) return;

        var closing = openMenu;
        var trigger = closing.wrap.querySelector('.slmm-btn');
        openMenu = null;
        clearInterval(closing.timer);

        try {
          closing.win.removeEventListener('mousedown', onOutsideDown, true);
          closing.win.removeEventListener('keydown', onKeyDown, true);
        } catch (e) {}

        closing.menu.remove();

        if (trigger) trigger.classList.remove('open');

        notify(false);
      }

      // Rebuilt only when what it lists changed, so a poll never redraws the row under the pointer.
      function showRows(rows) {
        var shown = JSON.stringify(rows.map(function (entry) { return [entry.id, entry.label, entry.title, entry.icon, entry.hotkey]; }));

        if (shown === openMenu.shown) return;

        openMenu.shown = shown;
        openMenu.menu.textContent = '';
        rows.forEach(function (entry) { openMenu.menu.appendChild(row(openMenu.doc, entry)); });
      }

      function row(doc, entry) {
        var node = doc.createElement('div');
        node.className = 'category-option slmm-row unity-interactive';
        node.setAttribute('data-slmm-id', entry.id);

        if (entry.title) node.title = entry.title;

        node.appendChild(icon(doc, entry.icon));
        node.appendChild(text(doc, 'slmm-label', entry.label));

        if (entry.hotkey) node.appendChild(text(doc, 'slmm-key', entry.hotkey));

        return node;
      }

      // A row without an icon still gets the empty svg, so every label starts at the same place.
      function icon(doc, d) {
        var svg = doc.createElementNS(SVG_NS, 'svg');
        svg.setAttribute('viewBox', '0 0 24 24');

        if (!d) return svg;

        var path = doc.createElementNS(SVG_NS, 'path');
        path.setAttribute('d', d);
        path.setAttribute('fill-rule', 'evenodd');
        svg.appendChild(path);

        return svg;
      }

      function text(doc, className, value) {
        var span = doc.createElement('span');
        span.className = className;
        span.textContent = value;

        return span;
      }

      // A click on the menu's own padding keeps it open; a row closes it first, so a throwing run() cannot leave it up.
      function onMenuClick(e) {
        var target = e.target && e.target.closest ? e.target.closest('[data-slmm-id]') : null;
        var entry = target ? entries[target.getAttribute('data-slmm-id')] : null;

        e.stopPropagation();

        if (!target) return;

        close();

        if (!entry) return;

        try {
          entry.run();
        } catch (err) {}
      }

      // A press anywhere else closes the menu and still does what it would have, as the game's own dropdown does.
      function onOutsideDown(e) {
        if (openMenu && !openMenu.wrap.contains(e.target)) close();
      }

      // Only reached on a page the game does send keys to; elsewhere the plugins close the menu on Esc.
      function onKeyDown(e) {
        if (!openMenu || e.key !== 'Escape') return;

        e.stopPropagation();
        e.preventDefault();
        close();
      }

      // The game hides or drops the inventory page without telling anyone. A menu left open past it would keep the
      // plugins holding back the game's hotkeys, so it closes with the page.
      function closeIfGone() {
        if (!openMenu) return;

        var frame = null;

        try {
          frame = openMenu.win.frameElement;
        } catch (e) {}

        var isShown = openMenu.wrap.isConnected && !!frame && frame.isConnected && frame.style.display !== 'none';

        if (!isShown) close();
      }

      function notify(isOpen) {
        Object.keys(watchers).forEach(function (owner) {
          try {
            watchers[owner](isOpen);
          } catch (e) {}
        });
      }

      function retire() {
        close();
        isRetired = true;

        return {
          entries: Object.keys(entries).map(function (id) { return entries[id]; }),
          watchers: Object.keys(watchers).map(function (owner) { return { owner: owner, onOpenChange: watchers[owner] }; })
        };
      }

      if (snapshot) {
        (snapshot.entries || []).forEach(function (entry) { add(entry); });
        (snapshot.watchers || []).forEach(function (watcher) { watch(watcher.owner, watcher.onOpenChange); });
      }

      return { version: VERSION, add: add, update: update, remove: remove, setWanted: setWanted, place: place, close: close, watch: watch, retire: retire };
    }

    // The style outlives a dropped menu button, so a rebuild must not stack another copy of it.
    function addStyle(doc) {
      var id = 'slmm-style-v' + VERSION;

      if (doc.getElementById(id)) return;

      var style = doc.createElement('style');
      style.id = id;
      style.textContent = CSS;
      doc.head.appendChild(style);
    }

    function install() {
      var current = window.__slmodmenu;
      var isHub = !!current && typeof current.version === 'number';
      var snapshot = null;

      if (isHub && current.version >= VERSION) return;

      if (isHub && typeof current.retire === 'function') {
        try {
          snapshot = current.retire();
        } catch (e) {}
      }

      window.__slmodmenu = createHub(snapshot);
    }

    function forward(name) {
      return function () {
        var hub = window.__slmodmenu;

        return hub && typeof hub[name] === 'function' ? hub[name].apply(hub, arguments) : undefined;
      };
    }

    install();

    return { add: forward('add'), update: forward('update'), remove: forward('remove'), setWanted: forward('setWanted'), place: forward('place'), close: forward('close'), watch: forward('watch') };
  })();
  // ===== END slmod-menu v1 =====

  var PREFIX = 'slmod|storagehub|';
  var KIND_WORD = { bag: 'kindBag', storage: 'kindStorage', drawer: 'kindDrawer' };
  var BUSY_TIMEOUT_MS = 5000;

  var TIMES = '\u00d7';
  var MINUS = '\u2212';
  var MAX_KEEP = 9999;
  // A run of +/- clicks is saved once, this long after the last one.
  var KEEP_SAVE_DELAY_MS = 350;
  var MAX_KEEP_RESULTS = 60;

  // view is 'items', 'rot' (Rotting soon) or 'keep' (Keep in bag).
  var state = { data: { storages: [], items: [], rot: [], keep: { on: true, rows: [] } }, query: '', selected: null, isOpen: false, view: 'items', catalog: [], isCatalogAsked: false };
  // Amounts the player has changed and the game has not yet answered, by item id: { amount, timer, isSent }.
  var pendingKeep = {};
  var ui = null;
  var toastTimer = 0;
  var isBusy = false;
  var busyTimer = 0;
  // A redraw between a mouse press and its click would take the pressed button away and lose the click, so redraws
  // asked for in between wait for the mouse to come up.
  var isPointerDown = false;
  var isRedrawHeld = false;

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
    keepEmpty: 'Nothing on the keep list yet. Find an item on the right, or press Keep on an item in the main list.',
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
    floorOnlyTip: 'Only on the floor: "Put floor items away by tag" picks it up',
    pickItem: 'Pick an item on the left.',
    total: '{0} in total',
    takeAll: 'Take all into bag',
    putAwayTag: 'Put away by tag',
    putAway: 'Put away',
    take: 'Take',
    kindBag: 'Bag',
    kindStorage: 'Storage',
    kindDrawer: 'Drawer',
    menuPutAwayTip: 'Move everything in your bag into the storages whose tags want it',
    menuFloor: 'Put away floor',
    menuFloorTip: 'Move everything lying on the floor at home into the storages whose tags want it, and the rest into your bag',
    homeTitle: 'Home storage',
    homeSub: '{0} storages',
    homeSearch: 'Search every storage...',
    scopeCooking: 'For cooking',
    scopeRatCage: 'For the rat cage',
    scopeAll: 'Everything',
    homeOpen: 'Open',
    homeClose: 'Close',
    homeUsable: '{0} usable',
    homeItems: '{0} items',
    tagUse: 'Use',
    tagBag: 'To bag',
    hintStation: 'Click: usable items go into the station, anything else into your bag.',
    hintInv: 'Click an item to take it into your bag, or Open a storage beside your bag.',
    homeNoMatch: 'Nothing in your storages matches that search.',
    homeNone: 'No storage holds anything for this station.',
    homeEmpty: 'Your home storages are empty.',
    homeLoading: 'Reading your storages...'
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

  var CSS = [
    '#sh-root{position:fixed;inset:0;z-index:2147483000;display:none;align-items:center;justify-content:center;background:rgba(0,0,0,.4);font-family:"Microsoft YaHei","PingFang SC",sans-serif;color:#eee;user-select:none}',
    '#sh-root *{box-sizing:border-box}',
    '.sh-plate{width:min(1180px,92vw);height:min(800px,86vh);display:flex;flex-direction:column;gap:12px;padding:18px 20px;border-radius:10px;border:1px solid rgba(255,255,255,.14);background:linear-gradient(180deg,rgba(30,30,33,.92),rgba(18,18,20,.94));box-shadow:0 10px 40px rgba(0,0,0,.6)}',
    '.sh-head{display:flex;align-items:baseline;gap:14px}',
    '.sh-title{font-size:18px;font-weight:600;letter-spacing:1px;text-shadow:0 2px 6px rgba(0,0,0,.6)}',
    '.sh-sub{font-size:13px;color:rgba(238,238,238,.6);flex:1}',
    '.sh-btn.sh-x{width:30px;height:30px;padding:0;font-size:18px;line-height:28px}',
    '.sh-bar{display:flex;gap:10px}',
    '.sh-search{flex:1;height:36px;padding:0 12px;font:inherit;font-size:14px;color:#eee;background:rgba(10,10,12,.72);border:1px solid rgba(255,255,255,.14);border-radius:8px;outline:none;user-select:text}',
    '.sh-search:focus{border-color:rgba(255,193,7,.75)}',
    '.sh-body{flex:1;display:flex;gap:12px;min-height:0}',
    '.sh-list,.sh-detail{background:rgba(10,10,12,.72);border:1px solid rgba(255,255,255,.14);border-radius:8px;min-height:0}',
    '.sh-list{width:46%;overflow-y:auto;padding:6px}',
    '.sh-detail{flex:1;display:flex;flex-direction:column;padding:14px;gap:12px}',
    '.sh-row{display:flex;align-items:center;gap:10px;padding:4px 8px;border-radius:7px;border:1px solid transparent;cursor:pointer}',
    '.sh-row:hover{background:rgba(255,255,255,.06)}',
    '.sh-row.sh-active{background:rgba(255,193,7,.24);border-color:rgba(255,193,7,.75)}',
    '.sh-row.sh-still{cursor:default}',
    '.sh-row.sh-still:hover{background:none}',
    '.sh-name{flex:1;font-size:14px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}',
    '.sh-count{font-size:14px;font-weight:600;color:#EBA308}',
    '.sh-icon{flex:none;display:flex;align-items:center;justify-content:center;border-radius:6px;background:rgba(255,255,255,.05);overflow:hidden;color:rgba(238,238,238,.7);font-weight:600}',
    '.sh-icon img{width:100%;height:100%;object-fit:contain}',
    '.sh-i38{width:38px;height:38px;font-size:16px}',
    '.sh-i64{width:64px;height:64px;font-size:26px}',
    '.sh-dhead{display:flex;align-items:center;gap:14px}',
    '.sh-dname{font-size:18px;font-weight:600;letter-spacing:1px;text-shadow:0 2px 6px rgba(0,0,0,.6)}',
    '.sh-dtotal{font-size:14px;color:#EBA308;margin-top:4px}',
    '.sh-actions{display:flex;gap:10px}',
    '.sh-locs{flex:1;overflow-y:auto;display:flex;flex-direction:column;gap:6px}',
    '.sh-loc{display:flex;align-items:center;gap:10px;padding:6px 8px;border-radius:7px;background:rgba(255,255,255,.04)}',
    '.sh-badge{font-size:11px;padding:2px 7px;border-radius:5px;background:rgba(255,255,255,.1);color:rgba(238,238,238,.8)}',
    '.sh-badge-bag{background:rgba(255,193,7,.24);color:#EBA308}',
    '.sh-where{flex:none;max-width:34%;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}',
    '.sh-left{flex:none;min-width:58px;text-align:right;font-size:13px;color:rgba(238,238,238,.8)}',
    '.sh-left-urgent{color:#e0614f;font-weight:600}',
    '.sh-empty{padding:20px;color:rgba(238,238,238,.55);font-size:14px;text-align:center}',
    '.sh-btn{font:inherit;font-size:13px;color:#dde2f0;padding:7px 11px;border-radius:7px;border:1px solid rgba(255,255,255,.14);background:linear-gradient(180deg,rgba(58,58,60,.9),rgba(38,38,40,.92));cursor:pointer;white-space:nowrap}',
    '.sh-btn:hover{background:linear-gradient(180deg,rgba(72,66,48,.92),rgba(50,46,32,.95));border-color:rgba(255,193,7,.75)}',
    '.sh-btn.sh-on{background:rgba(255,193,7,.24);border-color:rgba(255,193,7,.75);color:#EBA308}',
    '.sh-btn,.sh-row{transition:background-color .12s,border-color .12s,color .12s,opacity .12s}',
    '.sh-btn.sh-mini{font-size:12px;padding:3px 9px;opacity:.7}',
    '.sh-row:hover .sh-btn.sh-mini,.sh-btn.sh-mini.sh-on{opacity:1}',
    '.sh-row.sh-keep{cursor:default;padding:5px 8px}',
    '.sh-kname{flex:1;min-width:0;display:flex;flex-direction:column;gap:2px}',
    '.sh-held{font-size:12px;color:rgba(238,238,238,.6)}',
    '.sh-held-short{color:#e0614f}',
    '.sh-keep-zero .sh-name{color:rgba(238,238,238,.55)}',
    '.sh-step{flex:none;display:flex;align-items:center;border:1px solid rgba(255,255,255,.14);border-radius:7px;overflow:hidden;background:rgba(10,10,12,.72)}',
    '.sh-step .sh-btn{width:30px;padding:6px 0;border:none;border-radius:0;text-align:center;font-size:15px;line-height:18px}',
    '.sh-amount{width:58px;height:30px;padding:0 4px;text-align:center;font:inherit;font-size:14px;font-weight:600;color:#EBA308;background:transparent;border:none;border-left:1px solid rgba(255,255,255,.14);border-right:1px solid rgba(255,255,255,.14);outline:none;user-select:text}',
    '.sh-amount:focus{background:rgba(255,193,7,.14)}',
    '.sh-amount::-webkit-outer-spin-button,.sh-amount::-webkit-inner-spin-button{-webkit-appearance:none;margin:0}',
    '.sh-btn.sh-del{flex:none;width:28px;height:28px;padding:0;font-size:16px;line-height:26px;text-align:center}',
    '.sh-btn.sh-del:hover{border-color:#e0614f;color:#e0614f;background:rgba(224,97,79,.14)}',
    '.sh-banner{margin:2px 2px 8px;padding:8px 10px;border-radius:7px;border:1px solid rgba(224,97,79,.6);background:rgba(224,97,79,.12);font-size:13px;color:#f0b5ab}',
    '.sh-hint{font-size:13px;color:rgba(238,238,238,.6)}',
    '.sh-pick:hover{background:rgba(255,255,255,.08)}',
    '.sh-foot{display:flex;font-size:13px;color:rgba(238,238,238,.7)}',
    '.sh-status{flex:1}',
    '#sh-toast{position:fixed;left:50%;bottom:120px;transform:translateX(-50%);z-index:2147483001;pointer-events:none;display:none;padding:10px 18px;border-radius:8px;border:1px solid rgba(255,193,7,.75);background:linear-gradient(180deg,rgba(30,30,33,.94),rgba(18,18,20,.96));color:#eee;font-family:"Microsoft YaHei","PingFang SC",sans-serif;font-size:14px;box-shadow:0 6px 20px rgba(0,0,0,.6)}'
  ].join('\n');

  function build() {
    if (ui && document.body.contains(ui.root)) return;

    addStyle();

    var root = el('div');
    root.id = 'sh-root';
    var plate = el('div', 'sh-plate');
    var head = el('div', 'sh-head');
    var sub = el('div', 'sh-sub');
    head.appendChild(el('div', 'sh-title', 'Storage Hub'));
    head.appendChild(sub);
    head.appendChild(button(TIMES, 'close', 'sh-x'));

    var bar = el('div', 'sh-bar');
    var search = el('input', 'sh-search');
    search.type = 'text';
    var rotButton = button('', 'rotView');
    var keepButton = button('', 'keepView');
    var bagButton = button('', 'putAwayBag');
    var floorButton = button('', 'putAwayFloor');
    bar.appendChild(search);
    bar.appendChild(rotButton);
    bar.appendChild(keepButton);
    bar.appendChild(bagButton);
    bar.appendChild(floorButton);

    var body = el('div', 'sh-body');
    var list = el('div', 'sh-list');
    var detail = el('div', 'sh-detail');
    body.appendChild(list);
    body.appendChild(detail);

    var foot = el('div', 'sh-foot');
    var status = el('div', 'sh-status');
    var closeKeys = el('div');
    foot.appendChild(status);
    foot.appendChild(closeKeys);

    plate.appendChild(head);
    plate.appendChild(bar);
    plate.appendChild(body);
    plate.appendChild(foot);
    root.appendChild(plate);

    var toast = el('div');
    toast.id = 'sh-toast';

    document.body.appendChild(root);
    document.body.appendChild(toast);

    root.addEventListener('click', onClick);
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

    ui = { root: root, sub: sub, search: search, rotButton: rotButton, keepButton: keepButton, bagButton: bagButton, floorButton: floorButton, closeKeys: closeKeys, list: list, detail: detail, status: status, toast: toast };
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
  function addStyle() {
    if (document.getElementById('sh-style')) return;

    var style = el('style');
    style.id = 'sh-style';
    style.textContent = CSS;
    document.head.appendChild(style);
  }

  function onClick(e) {
    var target = e.target.closest('[data-act],[data-item]');

    if (!target) return;

    e.stopPropagation();
    var item = target.getAttribute('data-item');

    if (item != null) {
      state.selected = item;
      renderList();
      renderDetail();

      return;
    }

    var act = target.getAttribute('data-act');
    var args = target.getAttribute('data-args');

    // Switching lists is the page's own business; the game is only asked for the item catalog the keep view searches.
    if (act === 'rotView' || act === 'keepView') {
      showView(act === 'rotView' ? 'rot' : 'keep');

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

    sendAction(args ? act + '|' + args : act);
  }

  // A view's button opens it, and pressed again goes back to the item list.
  function showView(view) {
    state.view = state.view === view ? 'items' : view;
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

    return state.data.items.filter(function (item) { return matches(item, words); });
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
    ui.sub.textContent = subtitles[state.view];
    ui.search.placeholder = say(state.view === 'keep' ? 'searchKeep' : 'search');
    ui.rotButton.textContent = say('rotView');
    ui.rotButton.title = say('rotTip');
    ui.rotButton.className = 'sh-btn' + (state.view === 'rot' ? ' sh-on' : '');
    ui.keepButton.textContent = say('keepView');
    ui.keepButton.title = say('keepTip');
    ui.keepButton.className = 'sh-btn' + (state.view === 'keep' ? ' sh-on' : '');
    ui.bagButton.textContent = say('putAwayBag');
    ui.floorButton.textContent = say('putAwayFloor');
    ui.closeKeys.textContent = say('closeKeys', window.__storagehubToggleKey || 'F8');
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
      ui.list.appendChild(el('div', 'sh-empty', say(state.query ? 'noMatch' : 'noItems')));

      return;
    }

    var fragment = document.createDocumentFragment();

    items.forEach(function (item) {
      var row = el('div', 'sh-row' + (String(item.id) === state.selected ? ' sh-active' : ''));
      row.setAttribute('data-item', String(item.id));
      row.appendChild(icon(item, 'sh-i38'));
      row.appendChild(el('div', 'sh-name', item.name));
      row.appendChild(quickKeepButton(item.id));
      row.appendChild(el('div', 'sh-count', TIMES + item.total));
      fragment.appendChild(row);
    });

    ui.list.appendChild(fragment);
    ui.list.scrollTop = scroll;
  }

  function quickKeepButton(id) {
    var isOnList = isKept(id);
    var node = withArgs(button(isOnList ? say('keepN', keptAmount(id)) : say('keep'), 'quickKeep', 'sh-mini' + (isOnList ? ' sh-on' : '')), id);
    node.title = say(isOnList ? 'keepOnTip' : 'keepAddTip');

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

    keepRows().forEach(function (kept) { fragment.appendChild(keepRowNode(kept)); });

    ui.list.appendChild(fragment);
    ui.list.scrollTop = scroll;
  }

  function keepRowNode(kept) {
    var amount = keptAmount(kept.id);
    var row = el('div', 'sh-row sh-keep' + (amount === 0 ? ' sh-keep-zero' : ''));
    var names = el('div', 'sh-kname');
    names.appendChild(el('div', 'sh-name', kept.name));
    names.appendChild(heldNode(kept.held, amount));
    row.appendChild(icon(kept, 'sh-i38'));
    row.appendChild(names);
    row.appendChild(stepperNode(kept.id, amount));

    var remove = withArgs(button(TIMES, 'keepRemove', 'sh-del'), kept.id);
    remove.title = say('keepRemoveTip');
    row.appendChild(remove);

    return row;
  }

  function heldNode(held, amount) {
    if (amount === 0) return el('div', 'sh-held', say('heldNone', held));

    var isShort = held < amount;

    return el('div', 'sh-held' + (isShort ? ' sh-held-short' : ''), isShort ? say('heldShort', held, amount - held) : say('held', held));
  }

  function stepperNode(id, amount) {
    var step = el('div', 'sh-step');
    var less = withArgs(button(MINUS, 'keepStep'), id + '|-1');
    var more = withArgs(button('+', 'keepStep'), id + '|1');
    less.title = say('keepLess');
    more.title = say('keepMore');
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
    field.title = say('keepAmountTip');
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

    var head = el('div');
    head.appendChild(el('div', 'sh-dname', say('keepAddTitle')));
    head.appendChild(el('div', 'sh-hint', keepAddHint(words.length > 0)));
    ui.detail.appendChild(head);

    if (found.length === 0) {
      ui.detail.appendChild(el('div', 'sh-empty', say(words.length > 0 ? 'noName' : 'bagEmpty')));

      return;
    }

    var picks = el('div', 'sh-locs');

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
    var pick = el('div', 'sh-loc sh-pick');
    var held = bagCount(item.id);
    pick.appendChild(icon(item, 'sh-i38'));
    pick.appendChild(el('div', 'sh-name', item.name));

    if (held > 0) pick.appendChild(el('span', 'sh-badge sh-badge-bag', say('inBag', held)));

    if (isKept(item.id)) {
      pick.appendChild(el('span', 'sh-badge', say('keeping', keptAmount(item.id))));
    } else {
      var add = withArgs(button(say('addKeep'), 'keepAdd'), item.id);
      add.title = say('addKeepTip', suggestedKeep(item.id));
      pick.appendChild(add);
    }

    return pick;
  }

  // One row per kind, place and hour left; clicking one shows that item's detail beside the list, as in the item list.
  // A kind that lies only on the floor has no detail (the item list holds bags and storages only), so its rows do not click.
  function renderRot() {
    var scroll = ui.list.scrollTop;
    var rows = visibleRot();
    ui.list.textContent = '';

    if (rows.length === 0) {
      ui.list.appendChild(el('div', 'sh-empty', say(state.query ? 'noMatch' : 'nothingSpoils')));

      return;
    }

    var fragment = document.createDocumentFragment();

    rows.forEach(function (rot) {
      var hasDetail = findItem(rot.id) != null;
      var isSelected = hasDetail && String(rot.id) === state.selected;
      var row = el('div', 'sh-row' + (hasDetail ? '' : ' sh-still') + (isSelected ? ' sh-active' : ''));

      if (hasDetail) row.setAttribute('data-item', String(rot.id));
      else row.title = say('floorOnlyTip');

      row.appendChild(icon(rot, 'sh-i38'));
      row.appendChild(el('div', 'sh-name', rot.name));
      row.appendChild(el('span', 'sh-badge sh-where', rot.where));
      row.appendChild(el('div', 'sh-count', TIMES + rot.count));
      row.appendChild(el('div', 'sh-left' + (rot.urgent ? ' sh-left-urgent' : ''), rot.left));
      fragment.appendChild(row);
    });

    ui.list.appendChild(fragment);
    ui.list.scrollTop = scroll;
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

    var head = el('div', 'sh-dhead');
    var titles = el('div');
    titles.appendChild(el('div', 'sh-dname', item.name));
    titles.appendChild(el('div', 'sh-dtotal', say('total', item.total)));
    head.appendChild(icon(item, 'sh-i64'));
    head.appendChild(titles);
    ui.detail.appendChild(head);

    var hasBag = item.at.some(function (at) { return storageOf(at[0]).kind === 'bag'; });
    var hasElsewhere = item.at.some(function (at) { return storageOf(at[0]).kind !== 'bag'; });
    var actions = el('div', 'sh-actions');

    if (hasElsewhere) actions.appendChild(withArgs(button(say('takeAll'), 'takeAll'), item.id));
    if (hasBag) actions.appendChild(withArgs(button(say('putAwayTag'), 'putAway'), item.id));

    ui.detail.appendChild(actions);

    var locs = el('div', 'sh-locs');

    item.at.forEach(function (at) {
      var storage = storageOf(at[0]);
      var isBag = storage.kind === 'bag';
      var loc = el('div', 'sh-loc');
      loc.appendChild(el('span', 'sh-badge' + (isBag ? ' sh-badge-bag' : ''), KIND_WORD[storage.kind] ? say(KIND_WORD[storage.kind]) : storage.kind));
      loc.appendChild(el('div', 'sh-name', storage.name));
      loc.appendChild(el('div', 'sh-count', TIMES + at[1]));
      loc.appendChild(isBag ? withArgs(button(say('putAway'), 'putAway'), item.id) : withArgs(button(say('take'), 'take'), item.id + '|' + storage.id));
      locs.appendChild(loc);
    });

    ui.detail.appendChild(locs);
    locs.scrollTop = scroll;
  }

  function withArgs(node, args) {
    node.setAttribute('data-args', String(args));

    return node;
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

  function status(text) {
    build();
    clearBusy();
    settleSentKeeps();

    if (state.isOpen) {
      ui.status.textContent = text;

      return;
    }

    // An empty status only clears the line; as a toast it would be an empty box.
    if (!text) {
      ui.toast.style.display = 'none';

      return;
    }

    ui.toast.textContent = text;
    ui.toast.style.display = 'block';
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { ui.toast.style.display = 'none'; }, 3500);
  }

  // The inventory screen: "Put away by tag" and "Put away floor" in the toolbar's Mods menu, on the bag side's item
  // tab only. The page is a same-origin iframe, so its document can be reached from here; polled because the game
  // creates and drops that iframe whenever it likes.
  var hasReportedBackpackAccess = false;
  var hasReportedTabFallback = false;
  // A tag with its string hole, and an arrow lifting off the floor.
  var PUT_AWAY_ICON = 'M3 3h8l10 10-8 8L3 11zM7.5 6a1.5 1.5 0 1 0 0 3a1.5 1.5 0 1 0 0-3z';
  var FLOOR_ICON = 'M3 19h18v2H3zM12 3l6 6h-4v7h-4V9H6z';

  var menuEntries = [
    { id: 'storagehub.putaway', labelWord: 'putAwayTag', titleWord: 'menuPutAwayTip', icon: PUT_AWAY_ICON, order: 100, run: function () { sendAction('putAwayBag'); } },
    { id: 'storagehub.floor', labelWord: 'menuFloor', titleWord: 'menuFloorTip', icon: FLOOR_ICON, order: 110, run: function () { sendAction('putAwayFloor'); } }
  ];
  var menuWords = null;

  function menuEntry(entry) {
    return { id: entry.id, label: say(entry.labelWord), title: say(entry.titleWord), icon: entry.icon, order: entry.order, run: entry.run };
  }

  function relabelMenu() {
    if (menuWords === words()) return;

    menuWords = words();
    menuEntries.forEach(function (entry) { slmodMenu.update(entry.id, { label: say(entry.labelWord), title: say(entry.titleWord) }); });
  }

  // Reads the page's Vue state, which a game update can rename; failing open shows the buttons on every tab, so the
  // first failure is reported.
  function backpackTabWantsButton(doc) {
    try {
      var setup = doc.getElementById('app')._vnode.component.setupState;

      return setup.curATab === 0 && !setup.isPrologue;
    } catch (e) {
      if (!hasReportedTabFallback) send('diag|cannot read the inventory tab state, showing the buttons on every tab: ' + e);
      hasReportedTabFallback = true;

      return true;
    }
  }

  function patchBackpack() {
    var frames = document.querySelectorAll('iframe');

    for (var i = 0; i < frames.length; i++) {
      if (!/BackpackUI\.html/.test(frames[i].src || '')) continue;

      var doc = null;

      try {
        doc = frames[i].contentDocument;
      } catch (e) {}

      if (!doc) {
        if (!hasReportedBackpackAccess) send('diag|cannot reach the inventory page document (root ' + location.protocol + ', iframe ' + frames[i].src + ')');
        hasReportedBackpackAccess = true;
        continue;
      }

      patchToolbar(doc);
    }
  }

  // Each entry keeps the rule its old toolbar button had: the bag side's item tab only.
  function patchToolbar(doc) {
    var bar = doc.querySelector('.bag-slot .toolbar');
    var isWanted = !!bar && backpackTabWantsButton(doc);

    relabelMenu();
    menuEntries.forEach(function (entry) { slmodMenu.setWanted(entry.id, isWanted); });
    slmodMenu.place(doc);
  }

  menuEntries.forEach(function (entry) { slmodMenu.add(menuEntry(entry)); });
  menuWords = words();
  // The game sends the page no keys, so the plugin holds the game's hotkeys back while the menu is open and closes
  // it on Esc itself.
  slmodMenu.watch('storagehub', function (isOpen) { send('menu|' + (isOpen ? '1' : '0')); });

  // The home storage list docked beside the stove, the rat cage and the inventory. The plugin sends the storages with
  // an ok flag per item, decided by the station's own rules; the list is drawn inside the game's iframe, beside its
  // panels. At a station, a usable item goes in the way the game's own click does it: its storage's bag tab is shown,
  // then the item is clicked there. Anything else, or an item whose storage has no tab, is taken into the bag.
  var STATION_PAGES = [
    { kind: 'cooking', page: /Cooking\.html/, host: '.cooking-system' },
    { kind: 'ratcage', page: /RatCage\.html/, host: '.generator-system' },
    { kind: 'inventory', page: /BackpackUI\.html/, host: '.bag-panel .panel' }
  ];
  var STATION_ASK_MS = 3000;
  var STATION_AFTER_CLICK_MS = [400, 1200];
  var TAB_WAIT_MS = 2000;
  var TAB_POLL_MS = 80;
  var HOME_WIDTH = 300;
  var HOME_GAP = 24;
  var SEARCH_ICON = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#fcfcfc" stroke-width="2.4"><circle cx="11" cy="11" r="7"/><path d="M20 20l-4-4"/></svg>';
  var HOME_CSS = [
    '.slmh-home{display:flex;flex-direction:column;width:' + HOME_WIDTH + 'px;min-height:0;flex:none}',
    '.slmh-home .panel-title{margin-bottom:10px}',
    '.slmh-home .panel-title .sub{margin-left:8px;font-size:12px;font-weight:400;color:#c0c0c0;letter-spacing:0}',
    '.slmh-search{position:relative}',
    '.slmh-search input{width:100%;box-sizing:border-box;padding:7px 10px 7px 30px;background:rgba(20,15,12,.85);border:1px solid rgba(255,255,255,.12);border-radius:6px;color:#fcfcfc;font-size:13px;outline:none;font-family:inherit}',
    '.slmh-search input:focus{border-color:rgba(251,176,52,.7)}',
    '.slmh-search svg{position:absolute;left:9px;top:50%;transform:translateY(-50%);opacity:.6}',
    '.slmh-scope{display:flex;gap:4px;margin-top:8px}',
    '.slmh-scope button{flex:1;padding:5px 8px;font-size:12.5px;border-radius:6px;border:1px solid rgba(255,255,255,.12);background:rgba(20,15,12,.55);color:#c0c0c0;cursor:pointer;font-family:inherit}',
    '.slmh-scope button.on{border-color:rgba(251,176,52,.7);color:#fbb034;background:linear-gradient(180deg,rgba(90,70,30,.55),rgba(60,45,20,.6))}',
    '.slmh-list{flex:1;min-height:0;overflow-y:auto;margin-top:8px;padding-right:4px}',
    '.slmh-list::-webkit-scrollbar{width:6px}',
    '.slmh-list::-webkit-scrollbar-thumb{background:rgba(251,176,52,.35);border-radius:3px}',
    '.slmh-store{display:flex;align-items:center;gap:6px;padding:6px 8px;margin-top:5px;background:rgba(20,15,12,.6);border:1px solid rgba(255,255,255,.12);border-radius:6px;cursor:pointer;font-size:13px;color:#fcfcfc;user-select:none}',
    '.slmh-store:hover{border-color:rgba(251,176,52,.45)}',
    '.slmh-store.on{border-color:rgba(251,176,52,.75)}',
    '.slmh-car{width:10px;font-size:9px;color:#fbb034}',
    '.slmh-store .cnt{margin-left:auto;color:#c0c0c0;font-size:12px}',
    '.slmh-open{padding:2px 9px;font-size:12px;border-radius:5px;border:1px solid rgba(251,176,52,.55);background:linear-gradient(180deg,rgba(90,70,30,.85),rgba(60,45,20,.9));color:#ffd77a;cursor:pointer;font-family:inherit}',
    '.slmh-open.on{background:rgba(251,176,52,.85);color:#2a1d08}',
    '.slmh-row{display:flex;align-items:center;gap:8px;padding:3px 6px 3px 16px;cursor:pointer;border-radius:5px;font-size:12.5px;color:#e8e8e8}',
    '.slmh-row:hover{background:rgba(251,176,52,.14)}',
    '.slmh-ic{width:32px;height:32px;flex:none;background:linear-gradient(135deg,rgba(70,60,55,.95),rgba(45,38,34,.95));border:1px solid rgba(255,255,255,.14);border-radius:4px;display:flex;align-items:center;justify-content:center;color:#c0c0c0;font-size:13px}',
    '.slmh-ic img{max-width:28px;max-height:28px}',
    '.slmh-row .n{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.slmh-row .x{color:#c0c0c0;font-size:12px;flex:none}',
    '.slmh-tag{margin-left:auto;flex:none;font-size:11px;padding:1px 6px;border-radius:4px}',
    '.slmh-tag.st{color:#9fe6a8;background:rgba(60,110,60,.35)}',
    '.slmh-tag.bag{color:#c0c0c0;background:rgba(255,255,255,.07)}',
    '.slmh-empty{padding:16px 6px;color:#c0c0c0;font-size:12.5px;text-align:center}',
    '.slmh-hint{margin-top:8px;font-size:12px;color:#c0c0c0}',
    '.slmh-inv{position:fixed;z-index:50;box-sizing:border-box;padding:14px 16px 12px;border-radius:14px;background:linear-gradient(168deg,rgba(34,34,36,.94),rgba(20,20,22,.96));border:1px solid rgba(255,255,255,.15);box-shadow:0 12px 40px rgba(0,0,0,.5);font-family:"Microsoft YaHei","PingFang SC",sans-serif}',
    '.slmh-inv .panel-title{font-size:17px;font-weight:700;color:#ffe6a8;letter-spacing:.5px}',
    '.slmh-inv .panel-title .sub{font-size:12px;font-weight:400;color:#999;margin-left:6px}'
  ].join('');

  var home = { data: null, isOff: false, station: null, frame: null, ui: null, askedAt: 0, shownJson: '', query: '', isAllScope: false, folds: {}, openId: null };

  function station(data) {
    if (data === null) {
      home.isOff = true;
      dropHome();

      return;
    }

    if (!data || !home.station || data.kind !== home.station.kind) return;

    // The tabs decide which storages get an Open button, so a change of tabs redraws too.
    var json = JSON.stringify(data) + '|' + tabOwners().join(',');

    home.data = data;

    // Redrawn only when something changed, so a click is never lost to a list rebuilt under the pointer.
    if (home.ui && json === home.shownJson) return;

    home.shownJson = json;

    if (!home.ui) return mountHome(home.frame.contentDocument);

    renderHome();
  }

  function patchStations() {
    if (home.isOff) return;

    var found = findStation();

    if (!found) {
      dropHome();

      return;
    }

    // Mounted when the first list arrives, so a list turned off in the config never flashes up. The game can also
    // redraw its panels and drop ours, which starts it over.
    if (found.frame !== home.frame || (home.ui && !home.ui.root.isConnected)) {
      dropHome();
      home.station = found.station;
      home.frame = found.frame;
      home.askedAt = 0;
    }

    if (home.ui && home.station.kind === 'inventory') placeInventoryHome(found.doc);
    if (Date.now() - home.askedAt >= STATION_ASK_MS) askStation();
  }

  function findStation() {
    var frames = document.querySelectorAll('iframe');

    for (var i = 0; i < frames.length; i++) {
      for (var s = 0; s < STATION_PAGES.length; s++) {
        if (!STATION_PAGES[s].page.test(frames[i].src || '')) continue;

        var doc = null;

        try {
          doc = frames[i].contentDocument;
        } catch (e) {}

        if (doc && doc.querySelector(STATION_PAGES[s].host)) return { station: STATION_PAGES[s], frame: frames[i], doc: doc };
      }
    }

    return null;
  }

  function askStation() {
    home.askedAt = Date.now();
    send('station|' + home.station.kind);
  }

  function askStationSoon() {
    STATION_AFTER_CLICK_MS.forEach(function (delay) {
      setTimeout(function () {
        if (home.station) askStation();
      }, delay);
    });
  }

  function dropHome() {
    if (home.ui && home.ui.root.parentNode) home.ui.root.parentNode.removeChild(home.ui.root);
    if (home.ui && home.ui.isTyping) send('focus|0');

    home.ui = null;
    home.station = null;
    home.frame = null;
    home.data = null;
    home.shownJson = '';
    home.openId = null;
  }

  function make(doc, tag, className, text) {
    var node = doc.createElement(tag);

    if (className) node.className = className;
    if (text != null) node.textContent = text;

    return node;
  }

  function mountHome(doc) {
    var isInventory = home.station.kind === 'inventory';
    var root = make(doc, 'div', isInventory ? 'slmh-home slmh-inv' : 'slmh-home panel');
    var title = make(doc, 'div', 'panel-title', say('homeTitle'));
    var sub = make(doc, 'span', 'sub');
    var search = make(doc, 'div', 'slmh-search');
    var input = make(doc, 'input');
    var scope = make(doc, 'div', 'slmh-scope');
    var list = make(doc, 'div', 'slmh-list');
    var hint = make(doc, 'div', 'slmh-hint', say(isInventory ? 'hintInv' : 'hintStation'));

    addHomeStyle(doc);
    // The inventory page passes clicks through to the game unless the element under the pointer is marked interactive.
    root.setAttribute('data-interactive', '1');
    title.appendChild(sub);
    search.innerHTML = SEARCH_ICON;
    input.placeholder = say('homeSearch');
    input.value = home.query;
    search.appendChild(input);
    root.appendChild(title);
    root.appendChild(search);

    if (!isInventory) {
      scope.appendChild(scopeButton(doc, say(home.station.kind === 'cooking' ? 'scopeCooking' : 'scopeRatCage'), false));
      scope.appendChild(scopeButton(doc, say('scopeAll'), true));
      root.appendChild(scope);
    }

    root.appendChild(list);
    root.appendChild(hint);
    home.ui = { root: root, sub: sub, input: input, scope: scope, list: list, isTyping: false };
    watchHomeSearch(input);
    list.addEventListener('click', onHomeClick);
    (isInventory ? doc.body : doc.querySelector(home.station.host)).appendChild(root);
    renderHome();
  }

  function addHomeStyle(doc) {
    if (doc.getElementById('slmh-style')) return;

    var style = make(doc, 'style');
    style.id = 'slmh-style';
    style.textContent = HOME_CSS;
    doc.head.appendChild(style);
  }

  function scopeButton(doc, text, isAll) {
    var node = make(doc, 'button', isAll === home.isAllScope ? 'on' : '', text);

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
      home.ui.isTyping = true;
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

  // Inside the inventory page the list floats left of the bag, matching the bag panel's top and height.
  function placeInventoryHome(doc) {
    var bag = doc.querySelector(home.station.host);
    var root = home.ui.root;

    root.style.display = backpackTabWantsButton(doc) ? '' : 'none';

    if (!bag) return;

    var box = bag.getBoundingClientRect();

    root.style.width = HOME_WIDTH + 'px';
    root.style.top = box.top + 'px';
    root.style.height = box.height + 'px';
    root.style.left = Math.max(8, box.left - HOME_WIDTH - HOME_GAP) + 'px';
  }

  function renderHome() {
    if (!home.ui) return;

    var ui = home.ui;
    var doc = ui.root.ownerDocument;
    var scrollTop = ui.list.scrollTop;
    var shown = home.data ? homeStorages() : [];

    ui.sub.textContent = home.data ? say('homeSub', home.data.storages.length) : '';
    Array.prototype.forEach.call(ui.scope.children, function (node, index) { node.className = (index === 1) === home.isAllScope ? 'on' : ''; });
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

  function isUsableScope() {
    return home.station.kind !== 'inventory' && !home.isAllScope;
  }

  // Each storage with the rows the scope and the search leave; one with none left is not shown.
  function homeStorages() {
    var query = home.query.trim().toLowerCase().split(/\s+/).filter(Boolean);
    var isUsableOnly = isUsableScope();
    var tabs = home.station.kind === 'inventory' ? [] : tabOwners();

    return home.data.storages.map(function (storage) {
      var rows = storage.items.filter(function (item) {
        var name = String(item.name).toLowerCase();

        return (!isUsableOnly || item.ok) && query.every(function (word) { return name.indexOf(word) >= 0; });
      });

      return { storage: storage, rows: rows, hasTab: tabs.indexOf(storage.id) >= 0 };
    }).filter(function (entry) {
      return entry.rows.length > 0 || (!query.length && !isUsableOnly && home.station.kind === 'inventory');
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
    var isInventory = home.station.kind === 'inventory';

    head.setAttribute('data-fold', entry.storage.id);
    head.appendChild(make(doc, 'span', 'slmh-car', isFold ? '\u25b6' : '\u25bc'));
    head.appendChild(make(doc, 'span', '', entry.storage.name));
    head.appendChild(make(doc, 'span', 'cnt', isUsableScope() ? say('homeUsable', entry.rows.length) : say('homeItems', entry.rows.length)));

    if (isInventory || entry.hasTab) head.appendChild(openButton(doc, entry.storage.id));

    box.appendChild(head);

    if (!isFold) entry.rows.forEach(function (item) { box.appendChild(homeRow(doc, entry.storage.id, item)); });

    return box;
  }

  function openButton(doc, id) {
    var isOpenHere = home.openId === id;
    var node = make(doc, 'button', 'slmh-open' + (isOpenHere ? ' on' : ''), say(isOpenHere ? 'homeClose' : 'homeOpen'));

    node.setAttribute('data-open', id);

    return node;
  }

  function homeRow(doc, storageId, item) {
    var row = make(doc, 'div', 'slmh-row');
    var box = make(doc, 'div', 'slmh-ic');

    row.setAttribute('data-storage', storageId);
    row.setAttribute('data-item', item.item);

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

    if (home.station.kind !== 'inventory') row.appendChild(make(doc, 'span', 'slmh-tag ' + (item.ok ? 'st' : 'bag'), say(item.ok ? 'tagUse' : 'tagBag')));

    return row;
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

  // On the inventory screen the storage opens beside the bag, the way walking up to it does; at a station its tab is shown.
  function openStorage(id) {
    if (home.station.kind !== 'inventory') {
      switchTab(id);

      return;
    }

    home.openId = home.openId === id ? null : id;
    send('openStorage|' + (home.openId === null ? '-1' : id));
    renderHome();
  }

  function clickHomeItem(storageId, itemId) {
    var storage = findHomeStorage(storageId);
    var item = storage && storage.items.filter(function (it) { return it.item === itemId; })[0];

    if (!item) return;

    if (home.station.kind !== 'inventory' && item.ok && tabOwners().indexOf(storageId) >= 0) {
      putInStation(storageId, itemId);
    } else {
      send('take|' + item.cfg + '|' + storageId);
    }

    askStationSoon();
  }

  function findHomeStorage(id) {
    return home.data ? home.data.storages.filter(function (storage) { return storage.id === id; })[0] : null;
  }

  function pageCore() {
    var win = home.frame && home.frame.contentWindow;

    return win && win.core && win.core.UnitySendEvent ? win.core : null;
  }

  function switchTab(ownerId) {
    var core = pageCore();

    if (core && activeOwner() !== ownerId) core.UnitySendEvent('SWITCH_BAG', { ownerId: Number(ownerId) });
  }

  function putInStation(ownerId, itemId) {
    var startedAt = Date.now();

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

    core.UnitySendEvent('ITEM_CLICK', { itemId: Number(itemId), rightClick: false });
    askStationSoon();
  }

  // The rat cage keeps its tabs in State; the stove keeps the owner on each tab element.
  function activeOwner() {
    var win = home.frame && home.frame.contentWindow;
    var tab = win && win.document.querySelector('.bag-tab.active');

    if (win && win.State && win.State.bagOwnerId != null) return String(win.State.bagOwnerId);

    return tab && tab._ownerId != null ? String(tab._ownerId) : null;
  }

  function tabOwners() {
    var win = home.frame && home.frame.contentWindow;

    if (!win) return [];

    if (win.State && win.State.bagTabs) {
      return win.State.bagTabs.filter(function (t) { return !t.locked; }).map(function (t) { return String(t.ownerId); });
    }

    return Array.prototype.filter.call(win.document.querySelectorAll('.bag-tab'), function (t) {
      return t._ownerId != null && !t.classList.contains('locked');
    }).map(function (t) { return String(t._ownerId); });
  }

  setInterval(function () {
    try {
      patchBackpack();
    } catch (e) {}

    try {
      patchStations();
    } catch (e) {}
  }, 400);

  window.__storagehub = { open: open, setData: setData, close: close, status: status, catalog: catalog, station: station };
  send('ready');
})();
