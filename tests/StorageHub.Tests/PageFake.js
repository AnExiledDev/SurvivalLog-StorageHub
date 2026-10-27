// Just enough of Root.html for page.js to run in Jint: the root document, an inventory page in a frame, a Mods menu
// that keeps what it is handed and a fake Exiled's Toolkit that records the panels handed to it. Test code only;
// nothing here ships.
var window = this;

var location = { protocol: 'https:' };
var innerWidth = 1920;
var innerHeight = 1080;

var intervals = [];

function setInterval(callback) {
  intervals.push(callback);

  return intervals.length;
}

function clearInterval() {}

// One run of every interval the script set, which is how its 400 ms page check runs.
function poll() {
  intervals.slice().forEach(function (callback) { callback(); });
}

// Held until a test runs the ones of one delay, say the tip's wait.
var timers = {};
var nextTimer = 1;

function setTimeout(callback, delay) {
  timers[nextTimer] = { callback: callback, delay: delay || 0 };

  return nextTimer++;
}

function clearTimeout(id) {
  delete timers[id];
}

function runTimers(delay) {
  Object.keys(timers).forEach(function (id) {
    var timer = timers[id];

    if (!timer || timer.delay !== delay) return;

    delete timers[id];
    timer.callback();
  });
}

function addEventListener() {}

var FakeElement = class {
  constructor(tagName, ownerDocument) {
    var element = this;

    // Browsers report HTML tag names in capitals, and page.js checks them that way.
    this.tagName = tagName.charAt(0) === '#' ? tagName : tagName.toUpperCase();
    this.ownerDocument = ownerDocument;
    this.id = '';
    this.parentElement = null;
    this.children = [];
    this.text = '';
    this.attrs = {};
    this.classes = [];
    this.listeners = {};
    this.style = {};
    this.dataset = {};
    this.value = '';
    this.scrollTop = 0;
    // Set by a test: the element's box on screen and its own unzoomed size.
    this.rect = { left: 0, top: 0, width: 0, height: 0 };
    this.offsetWidth = 0;
    this.offsetHeight = 0;
    this.classList = {
      contains: function (name) { return element.classes.indexOf(name) >= 0; },
      add: function (name) { if (element.classes.indexOf(name) < 0) element.classes.push(name); },
      remove: function (name) { element.classes = element.classes.filter(function (c) { return c !== name; }); },
      toggle: function (name, isOn) {
        if (isOn === undefined) isOn = element.classes.indexOf(name) < 0;
        if (isOn) element.classList.add(name);
        else element.classList.remove(name);

        return isOn;
      }
    };
  }

  get parentNode() { return this.parentElement; }

  get firstChild() { return this.children.length ? this.children[0] : null; }

  get lastChild() { return this.children.length ? this.children[this.children.length - 1] : null; }

  // In the page while some ancestor is a document's head or body.
  get isConnected() {
    for (var element = this; element; element = element.parentElement) {
      if (element.isRoot) return true;
    }

    return false;
  }

  get className() { return this.classes.join(' '); }

  set className(value) { this.classes = String(value).split(' ').filter(function (name) { return name !== ''; }); }

  // As in a browser: an element's text is all of its descendants' text, and setting it replaces the children.
  get textContent() {
    return this.text + this.children.map(function (child) { return child.textContent; }).join('');
  }

  set textContent(value) {
    this.children.slice().forEach(function (child) { this.removeChild(child); }, this);
    this.text = value == null ? '' : String(value);
  }

  // Markup is kept as text only; the script sets it for icons and to empty a list.
  get innerHTML() { return this.text; }

  set innerHTML(value) { this.textContent = value; }

  getBoundingClientRect() {
    var r = this.rect;

    return { left: r.left, top: r.top, width: r.width, height: r.height, right: r.left + r.width, bottom: r.top + r.height };
  }

  hasAttribute(name) { return Object.prototype.hasOwnProperty.call(this.attrs, name); }

  getAttribute(name) { return this.hasAttribute(name) ? this.attrs[name] : null; }

  setAttribute(name, value) { this.attrs[name] = String(value); }

  removeAttribute(name) { delete this.attrs[name]; }

  addEventListener(type, listener) {
    (this.listeners[type] = this.listeners[type] || []).push(listener);
  }

  removeEventListener() {}

  // Runs the element's own listeners for type with the event given.
  fire(type, event) {
    (this.listeners[type] || []).slice().forEach(function (listener) { listener(event); });
  }

  focus() { if (this.ownerDocument) this.ownerDocument.activeElement = this; }

  blur() { if (this.ownerDocument && this.ownerDocument.activeElement === this) this.ownerDocument.activeElement = null; }

  select() {}

  appendChild(child) {
    if (child.parentElement) child.parentElement.removeChild(child);

    child.parentElement = this;
    this.children.push(child);

    return child;
  }

  insertBefore(child, before) {
    if (!before) return this.appendChild(child);

    if (child.parentElement) child.parentElement.removeChild(child);

    child.parentElement = this;
    this.children.splice(this.children.indexOf(before), 0, child);

    return child;
  }

  removeChild(child) {
    this.children = this.children.filter(function (c) { return c !== child; });
    child.parentElement = null;

    return child;
  }

  remove() {
    if (this.parentElement) this.parentElement.removeChild(this);
  }

  contains(other) {
    for (var element = other; element; element = element.parentElement) {
      if (element === this) return true;
    }

    return false;
  }

  matches(selector) {
    var element = this;

    return selector.split(',').some(function (one) { return matchesChain(element, one.trim().split(/\s+/)); });
  }

  closest(selector) {
    for (var element = this; element; element = element.parentElement) {
      if (element.matches && element.matches(selector)) return element;
    }

    return null;
  }

  querySelectorAll(selector) {
    return descendantsOf([this]).filter(function (element) { return element.matches(selector); });
  }

  querySelector(selector) {
    return this.querySelectorAll(selector)[0] || null;
  }
};

// "A B": the element matches B and some element around it matches A.
function matchesChain(element, parts) {
  if (!matchesCompound(element, parts[parts.length - 1])) return false;
  if (parts.length === 1) return true;

  for (var around = element.parentElement; around; around = around.parentElement) {
    if (matchesChain(around, parts.slice(0, -1))) return true;
  }

  return false;
}

// A tag, classes, ids and attributes (".bag-tab[data-owner]", "#sh-tip", "[data-tip]"); the script asks the page for
// nothing else along the paths the tests take.
function matchesCompound(element, selector) {
  var steps = selector.match(/^[a-z]+|\.[\w-]+|#[\w-]+|\[[\w-]+\]/g) || [];

  if (steps.join('') !== selector) throw new Error('the fake page does not know the selector "' + selector + '"');

  return steps.every(function (step) {
    switch (step.charAt(0)) {
      case '.':
        return element.classList.contains(step.slice(1));
      case '#':
        return element.id === step.slice(1);
      case '[':
        return element.hasAttribute(step.slice(1, -1));
      default:
        return element.tagName === step.toUpperCase();
    }
  });
}

function descendantsOf(roots) {
  var found = [];

  roots.forEach(function (root) {
    root.children.forEach(function walk(child) {
      found.push(child);
      child.children.forEach(walk);
    });
  });

  return found;
}

var FakeDocument = class {
  constructor(view) {
    this.defaultView = view;
    this.head = new FakeElement('head', this);
    this.body = new FakeElement('body', this);
    this.head.isRoot = true;
    this.body.isRoot = true;
    this.documentElement = this.body;
    this.activeElement = null;
    this.frames = [];
  }

  createElement(tagName) { return new FakeElement(tagName, this); }

  createElementNS(namespace, tagName) { return new FakeElement(tagName, this); }

  createTextNode(text) {
    var node = new FakeElement('#text', this);

    node.textContent = text;

    return node;
  }

  addEventListener() {}

  removeEventListener() {}

  // The same list every call, as a browser's is live: a frame a test adds later is in it.
  getElementsByTagName(tagName) {
    if (tagName !== 'iframe') throw new Error('the fake page lists only iframes');

    return this.frames;
  }

  getElementById(id) {
    return descendantsOf([this.head, this.body]).filter(function (element) { return element.id === id; })[0] || null;
  }

  querySelectorAll(selector) {
    if (selector === 'iframe') return this.frames;

    return descendantsOf([this.head, this.body]).filter(function (element) { return element.matches(selector); });
  }

  querySelector(selector) {
    return this.querySelectorAll(selector)[0] || null;
  }
};

// Root.html's own document, which holds the page iframes.
var document = new FakeDocument(window);

// What the script posts to the plugin.
var posted = [];

window.vuplex = {
  postMessage: function (message) { posted.push(message); }
};

// A Mods menu already in Root.html with a higher version than the block in page.js, so that block only registers with
// it. The menu itself is tested in Exiled's Toolkit.
var menuEntries = {};

window.__slmodmenu = {
  version: 99,
  add: function (entry) {
    menuEntries[entry.id] = entry;

    return true;
  },
  update: function (id, changes) {
    if (!menuEntries[id]) return false;

    for (var key in changes) menuEntries[id][key] = changes[key];

    return true;
  },
  remove: function () {},
  setWanted: function () {},
  place: function () {},
  placeAll: function () {},
  close: function () {},
  watch: function () {}
};

// Exiled's Toolkit's UI size, recording what is handed to it. A panel's zoom is what a test writes on its style, and
// zoomAt multiplies them up the tree as the real one multiplies the zooms it wrote.
var handed = [];

window.ExiledUi = {
  version: 1,
  scale: 1,
  size: function (el, options) { handed.push({ el: el, options: options }); },
  zoomAt: function (el) {
    var zoom = 1;

    for (var node = el; node; node = node.parentElement) {
      if (node.style && node.style.zoom) zoom *= Number(node.style.zoom);
    }

    return zoom;
  }
};

function handedWith(className) {
  return handed.filter(function (entry) { return entry.el.classList.contains(className); });
}

function handedId(id) {
  return handed.filter(function (entry) { return entry.el.id === id; });
}

// The inventory screen in a frame: its bag panel, the bag side's item tab toolbar and two panels side by side, 700 to
// 1300 across and 200 to 900 down.
var gameWindow = { innerWidth: 1920, innerHeight: 1080 };
var gameDoc = new FakeDocument(gameWindow);

gameWindow.document = gameDoc;

function gameEl(parent, tag, className) {
  var node = gameDoc.createElement(tag);

  node.className = className;
  parent.appendChild(node);

  return node;
}

function openInventory() {
  var app = gameEl(gameDoc.body, 'div', '');
  var bag = gameEl(app, 'div', 'bag-slot');
  var bagPanel = gameEl(bag, 'div', 'bag-panel');
  var panel = gameEl(bagPanel, 'div', 'panel');
  var other = gameEl(bag, 'div', 'panel');

  app.id = 'app';
  app._vnode = { component: { setupState: { curATab: 0, isPrologue: false } } };
  gameEl(bag, 'div', 'toolbar');
  panel.rect = { left: 700, top: 200, width: 300, height: 700 };
  other.rect = { left: 1000, top: 200, width: 300, height: 700 };
  document.frames.push({ src: 'file:///D:/Game/WebUI/BackpackUI.html', contentDocument: gameDoc, contentWindow: gameWindow });
  poll();
}

// The Storage Hub window on the inventory screen, opened from the Mods menu as a player does.
function openPop() {
  openInventory();
  menuEntries['storagehub.home'].run();

  return gameDoc.querySelector('.slmh-pop');
}

// A button with a tip inside the Storage Hub panel, at 600 to 700 across and 350 to 400 down, with the plate zoomed to
// zoom; the tip it shows measures 200 by 50 unzoomed. Returns the tip once its wait has run.
function tipAt(zoom) {
  // An empty status line builds the panel without drawing a list the fake page could not hold.
  window.__storagehub.status('', '');

  var plate = document.querySelector('.sh-plate');
  var target = document.createElement('button');

  plate.style.zoom = zoom === 1 ? '' : String(zoom);
  target.setAttribute('data-tip', 'Put everything away');
  target.rect = { left: 600, top: 350, width: 100, height: 50 };
  plate.appendChild(target);
  document.getElementById('sh-root').fire('mouseover', { target: target });

  var tip = null;

  // The tip is made when its wait runs and measured at once, so its size is set as it is put on the page.
  var append = document.body.appendChild;

  document.body.appendChild = function (child) {
    if (child.id === 'sh-tip') {
      child.offsetWidth = 200;
      child.offsetHeight = 50;
      tip = child;
    }

    return append.call(this, child);
  };
  runTimers(300);
  document.body.appendChild = append;

  return tip;
}
