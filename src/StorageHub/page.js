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
  var KIND_LABEL = { bag: 'Bag', storage: 'Storage', drawer: 'Drawer' };
  var BUSY_TIMEOUT_MS = 5000;

  var TIMES = '\u00d7';
  // Labels the list draws in warning colour: food that has turned, or will within the hour.
  var URGENT_LEFT = { 'spoiled': true, 'under 1h': true };

  var state = { data: { storages: [], items: [], rot: [] }, query: '', selected: null, isOpen: false, isRotView: false };
  var ui = null;
  var toastTimer = 0;
  var isBusy = false;
  var busyTimer = 0;

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
    search.placeholder = 'Search items...';
    var rotButton = button('Rotting soon', 'rotView');
    rotButton.title = 'Food in your bag, your storages and on the floor at home, soonest to spoil first';
    bar.appendChild(search);
    bar.appendChild(rotButton);
    bar.appendChild(button('Put whole bag away by tag', 'putAwayBag'));
    bar.appendChild(button('Put floor items away by tag', 'putAwayFloor'));

    var body = el('div', 'sh-body');
    var list = el('div', 'sh-list');
    var detail = el('div', 'sh-detail');
    body.appendChild(list);
    body.appendChild(detail);

    var foot = el('div', 'sh-foot');
    var status = el('div', 'sh-status');
    foot.appendChild(status);
    foot.appendChild(el('div', '', 'F8 / Esc to close'));

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
    root.addEventListener('mousedown', function (e) { e.stopPropagation(); });
    search.addEventListener('input', function () {
      state.query = search.value;
      renderList();
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

    ui = { root: root, sub: sub, search: search, rotButton: rotButton, list: list, detail: detail, status: status, toast: toast };
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

    // Switching lists is the page's own business; the game is not asked.
    if (act === 'rotView') {
      state.isRotView = !state.isRotView;
      ui.list.scrollTop = 0;
      render();

      return;
    }

    if (act === 'close') {
      send(act);

      return;
    }

    sendAction(args ? act + '|' + args : act);
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
    ui.sub.textContent = state.isRotView
      ? 'Food that spoils, soonest first, across ' + homes + ' storages, your bag and the floor at home'
      : state.data.items.length + ' kinds of item across ' + homes + ' storages and your bag';
    ui.rotButton.className = 'sh-btn' + (state.isRotView ? ' sh-on' : '');
  }

  function renderList() {
    if (state.isRotView) {
      renderRot();

      return;
    }

    var scroll = ui.list.scrollTop;
    var items = visibleItems();
    ui.list.textContent = '';

    if (items.length === 0) {
      ui.list.appendChild(el('div', 'sh-empty', state.query ? 'Nothing matches that search.' : 'No items found.'));

      return;
    }

    var fragment = document.createDocumentFragment();

    items.forEach(function (item) {
      var row = el('div', 'sh-row' + (String(item.id) === state.selected ? ' sh-active' : ''));
      row.setAttribute('data-item', String(item.id));
      row.appendChild(icon(item, 'sh-i38'));
      row.appendChild(el('div', 'sh-name', item.name));
      row.appendChild(el('div', 'sh-count', TIMES + item.total));
      fragment.appendChild(row);
    });

    ui.list.appendChild(fragment);
    ui.list.scrollTop = scroll;
  }

  // One row per kind, place and hour left; clicking one shows that item's detail beside the list, as in the item list.
  function renderRot() {
    var scroll = ui.list.scrollTop;
    var rows = visibleRot();
    ui.list.textContent = '';

    if (rows.length === 0) {
      ui.list.appendChild(el('div', 'sh-empty', state.query ? 'Nothing matches that search.' : 'Nothing here spoils.'));

      return;
    }

    var fragment = document.createDocumentFragment();

    rows.forEach(function (rot) {
      var row = el('div', 'sh-row' + (String(rot.id) === state.selected ? ' sh-active' : ''));
      row.setAttribute('data-item', String(rot.id));
      row.appendChild(icon(rot, 'sh-i38'));
      row.appendChild(el('div', 'sh-name', rot.name));
      row.appendChild(el('span', 'sh-badge sh-where', rot.where));
      row.appendChild(el('div', 'sh-count', TIMES + rot.count));
      row.appendChild(el('div', 'sh-left' + (URGENT_LEFT[rot.left] ? ' sh-left-urgent' : ''), rot.left));
      fragment.appendChild(row);
    });

    ui.list.appendChild(fragment);
    ui.list.scrollTop = scroll;
  }

  function renderDetail() {
    var scrollBox = ui.detail.querySelector('.sh-locs');
    var scroll = scrollBox ? scrollBox.scrollTop : 0;
    var item = state.selected == null ? null : findItem(state.selected);
    ui.detail.textContent = '';

    if (!item) {
      ui.detail.appendChild(el('div', 'sh-empty', 'Pick an item on the left.'));

      return;
    }

    var head = el('div', 'sh-dhead');
    var titles = el('div');
    titles.appendChild(el('div', 'sh-dname', item.name));
    titles.appendChild(el('div', 'sh-dtotal', item.total + ' in total'));
    head.appendChild(icon(item, 'sh-i64'));
    head.appendChild(titles);
    ui.detail.appendChild(head);

    var hasBag = item.at.some(function (at) { return storageOf(at[0]).kind === 'bag'; });
    var hasElsewhere = item.at.some(function (at) { return storageOf(at[0]).kind !== 'bag'; });
    var actions = el('div', 'sh-actions');

    if (hasElsewhere) actions.appendChild(withArgs(button('Take all into bag', 'takeAll'), item.id));
    if (hasBag) actions.appendChild(withArgs(button('Put away by tag', 'putAway'), item.id));

    ui.detail.appendChild(actions);

    var locs = el('div', 'sh-locs');

    item.at.forEach(function (at) {
      var storage = storageOf(at[0]);
      var isBag = storage.kind === 'bag';
      var loc = el('div', 'sh-loc');
      loc.appendChild(el('span', 'sh-badge' + (isBag ? ' sh-badge-bag' : ''), KIND_LABEL[storage.kind] || storage.kind));
      loc.appendChild(el('div', 'sh-name', storage.name));
      loc.appendChild(el('div', 'sh-count', TIMES + at[1]));
      loc.appendChild(isBag ? withArgs(button('Put away', 'putAway'), item.id) : withArgs(button('Take', 'take'), item.id + '|' + storage.id));
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
    renderHeader();
    renderList();
    renderDetail();
  }

  function open(data) {
    build();
    state.data = data || state.data;
    state.isOpen = true;
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

  function close() {
    state.isOpen = false;

    if (!ui) return;

    ui.root.style.display = 'none';

    if (document.activeElement === ui.search) ui.search.blur();
  }

  function status(text) {
    build();
    clearBusy();

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
    { id: 'storagehub.putaway', label: 'Put away by tag', title: 'Move everything in your bag into the storages whose tags want it', icon: PUT_AWAY_ICON, order: 100, run: function () { sendAction('putAwayBag'); } },
    { id: 'storagehub.floor', label: 'Put away floor', title: 'Move everything lying on the floor at home into the storages whose tags want it, and the rest into your bag', icon: FLOOR_ICON, order: 110, run: function () { sendAction('putAwayFloor'); } }
  ];

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

    menuEntries.forEach(function (entry) { slmodMenu.setWanted(entry.id, isWanted); });
    slmodMenu.place(doc);
  }

  menuEntries.forEach(function (entry) { slmodMenu.add(entry); });
  // The game sends the page no keys, so the plugin holds the game's hotkeys back while the menu is open and closes
  // it on Esc itself.
  slmodMenu.watch('storagehub', function (isOpen) { send('menu|' + (isOpen ? '1' : '0')); });

  setInterval(function () {
    try {
      patchBackpack();
    } catch (e) {}
  }, 400);

  window.__storagehub = { open: open, setData: setData, close: close, status: status };
  send('ready');
})();
