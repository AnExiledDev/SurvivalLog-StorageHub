// Storage Hub's web panel. Runs in Root.html, the document every game page iframe sits in, and draws over all of
// them. Styled with the game's own colours and fonts so it reads as part of the game. Talks to the plugin through
// vuplex.postMessage("slmod|storagehub|<verb>|<args>") and is driven by window.__storagehub.
(function () {
  if (window.__storagehub) return;

  var PREFIX = 'slmod|storagehub|';
  var KIND_LABEL = { bag: 'Bag', storage: 'Storage', drawer: 'Drawer' };
  var BUSY_TIMEOUT_MS = 5000;

  var state = { data: { storages: [], items: [] }, query: '', selected: null, isOpen: false };
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
    '.sh-empty{padding:20px;color:rgba(238,238,238,.55);font-size:14px;text-align:center}',
    '.sh-btn{font:inherit;font-size:13px;color:#dde2f0;padding:7px 11px;border-radius:7px;border:1px solid rgba(255,255,255,.14);background:linear-gradient(180deg,rgba(58,58,60,.9),rgba(38,38,40,.92));cursor:pointer;white-space:nowrap}',
    '.sh-btn:hover{background:linear-gradient(180deg,rgba(72,66,48,.92),rgba(50,46,32,.95));border-color:rgba(255,193,7,.75)}',
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
    head.appendChild(button('×', 'close', 'sh-x'));

    var bar = el('div', 'sh-bar');
    var search = el('input', 'sh-search');
    search.type = 'text';
    search.placeholder = 'Search items...';
    bar.appendChild(search);
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

    ui = { root: root, sub: sub, search: search, list: list, detail: detail, status: status, toast: toast };
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

  function visibleItems() {
    var words = state.query.toLowerCase().split(/\s+/).filter(Boolean);

    return state.data.items.filter(function (item) { return matches(item, words); });
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
    ui.sub.textContent = state.data.items.length + ' kinds of item across ' + homes + ' storages and your bag';
  }

  function renderList() {
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
      row.appendChild(el('div', 'sh-count', '×' + item.total));
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
      loc.appendChild(el('div', 'sh-count', '×' + at[1]));
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

  // The inventory screen: a "Put away by tag" button next to the game's own sort button, on the bag side's item
  // tab only. The page is a same-origin iframe, so its document can be reached from here; polled because the game
  // creates and drops that iframe whenever it likes.
  var hasReportedBackpackAccess = false;
  var hasReportedTabFallback = false;

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

  var toolbarButtons = [
    { className: 'sh-putaway-btn', text: 'Put away by tag', title: 'Move everything in your bag into the storages whose tags want it', action: 'putAwayBag' },
    { className: 'sh-floor-btn', text: 'Put away floor', title: 'Move everything lying on the floor at home into the storages whose tags want it, and the rest into your bag', action: 'putAwayFloor' }
  ];

  function patchToolbar(doc) {
    var bar = doc.querySelector('.bag-slot .toolbar');
    var isWanted = !!bar && backpackTabWantsButton(doc);

    for (var i = 0; i < toolbarButtons.length; i++) placeToolbarButton(doc, bar, isWanted, toolbarButtons[i]);
  }

  function placeToolbarButton(doc, bar, isWanted, spec) {
    var existing = doc.querySelector('.' + spec.className);

    if (!isWanted) {
      if (existing) existing.remove();

      return;
    }

    if (existing && existing.parentNode === bar) return;
    if (existing) existing.remove();

    var node = doc.createElement('button');
    node.className = 'action-btn unity-interactive ' + spec.className;
    node.textContent = spec.text;
    node.title = spec.title;
    node.addEventListener('click', function (e) {
      e.stopPropagation();
      sendAction(spec.action);
    });

    bar.insertBefore(node, firstGameButton(bar));
  }

  // The game's sort button, so ours sit just before it; the discard button and our own are skipped.
  function firstGameButton(bar) {
    var buttons = bar.querySelectorAll('.action-btn');

    for (var j = 0; j < buttons.length; j++) {
      var isOurs = buttons[j].classList.contains('sh-putaway-btn') || buttons[j].classList.contains('sh-floor-btn');

      if (!buttons[j].classList.contains('discard-btn') && !isOurs) return buttons[j];
    }

    return null;
  }

  setInterval(function () {
    try {
      patchBackpack();
    } catch (e) {}
  }, 400);

  window.__storagehub = { open: open, setData: setData, close: close, status: status };
  send('ready');
})();
