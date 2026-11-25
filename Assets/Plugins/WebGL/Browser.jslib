// Small helpers for the browser build. Called from Assets/Scripts/Browser.cs.
mergeInto(LibraryManager.library, {
  // Whether the browser has the mouse locked to the game. Unity's Cursor.lockState only reports
  // what the game last asked for, so it can't tell when the browser releases the mouse on Escape.
  IsBrowserPointerLocked: function () {
    return document.pointerLockElement ? 1 : 0;
  },

  // While on, closing or reloading the tab asks "Leave site?" first, so a stray Ctrl+W or
  // Ctrl+R doesn't drop the player out of a match.
  SetBrowserLeaveWarning: function (enabled) {
    if (!Module.leaveWarning) {
      Module.leaveWarning = function (e) { e.preventDefault(); e.returnValue = ""; };
    }
    window.removeEventListener("beforeunload", Module.leaveWarning);
    if (enabled) window.addEventListener("beforeunload", Module.leaveWarning);
  },

  // Phones and tablets: the main pointer is a finger. Touchscreen laptops have a mouse or
  // trackpad as their main pointer, so they keep the keyboard and mouse controls.
  IsBrowserTouchDevice: function () {
    return window.matchMedia && window.matchMedia("(pointer: coarse)").matches ? 1 : 0;
  },

  // The kind of pointer used last: 0 none yet, 1 mouse or trackpad, 2 finger or pen.
  // Pointer events say which it was; the mouse events browsers make up for taps don't count.
  GetBrowserPointerType: function () {
    if (!Module.pointerTypeListener) {
      Module.lastPointerType = 0;
      Module.pointerTypeListener = function (e) {
        Module.lastPointerType = e.pointerType === "mouse" ? 1 : 2;
      };
      document.addEventListener("pointerdown", Module.pointerTypeListener, true);
      document.addEventListener("pointermove", Module.pointerTypeListener, true);
    }
    return Module.lastPointerType;
  },

  // Takes ?code= (an invite link's room code) out of the address bar once it's been used, so
  // coming back to the menu or reloading doesn't join that match again
  ClearBrowserUrlQuery: function () {
    if (window.history && window.history.replaceState) {
      window.history.replaceState(null, "", window.location.pathname + window.location.hash);
    }
  },

  // Copies text to the clipboard (an invite link). Browsers allow it shortly after a click.
  CopyBrowserText: function (text) {
    var value = UTF8ToString(text);
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(value).catch(function () {});
      return;
    }
    var area = document.createElement("textarea");
    area.value = value;
    document.body.appendChild(area);
    area.select();
    try { document.execCommand("copy"); } catch (e) {}
    document.body.removeChild(area);
  },

  // The controller in use, read straight from the browser's Gamepad API (BrowserGamepad.cs): its 4
  // stick axes, then its first 17 buttons (0 to 1; the triggers in between). Returns 0 if none is
  // connected, 1 for one with the browser's "standard" layout, 2 for any other.
  //
  // Chrome on Android lays out every controller in the standard order, even one it doesn't know,
  // which it reports as having no mapping ("") all the same. So there every controller counts as
  // standard: otherwise an unknown one (some Bluetooth pads) would be read as a joystick, and
  // nothing in the game would answer it.
  //
  // Phones often list other devices as controllers too (Android's fingerprint sensor shows up as
  // one in Chrome), so the first in the list isn't always the real one. The controller in use is
  // the last one a button was pressed on; until then, the likeliest: one with the standard layout
  // and a full set of buttons and sticks.
  ReadBrowserGamepad: function (values) {
    if (!navigator.getGamepads) return 0;
    var pads = navigator.getGamepads();
    var android = /Android/i.test(navigator.userAgent);
    var standard = function (p) { return p.mapping === "standard" || android; };
    // Which controllers had a button down last time: only a new press counts, so a device that
    // always reports a button held can't take over
    var held = Module.gamepadHeld || (Module.gamepadHeld = {});
    var pressedOn = -1, pressedScore = -1, best = -1, bestScore = -1;
    for (var i = 0; i < pads.length; i++) {
      var p = pads[i];
      if (!p || !p.connected) { held[i] = false; continue; }
      var down = false;
      for (var k = 0; k < p.buttons.length; k++) {
        if (p.buttons[k] && (p.buttons[k].pressed || p.buttons[k].value > 0.5)) { down = true; break; }
      }
      var score = (standard(p) ? 4 : 0) + (p.buttons.length >= 16 ? 2 : 0) + (p.axes.length >= 4 ? 1 : 0);
      if (down && !held[i] && (pressedOn < 0 || score > pressedScore)) { pressedOn = i; pressedScore = score; }
      held[i] = down;
      if (score > bestScore) { bestScore = score; best = i; }
    }
    if (best < 0) { Module.gamepadIndex = -1; return 0; }
    var current = Module.gamepadIndex;
    var currentPad = current !== undefined && current >= 0 ? pads[current] : null;
    if (!currentPad || !currentPad.connected) { current = best; currentPad = null; }
    // A press moves to that controller, unless it has fewer buttons and sticks than the one in use
    // (touching the fingerprint sensor mustn't take over from the real controller)
    if (pressedOn >= 0 && pressedOn !== current && (!currentPad || pressedScore >= Module.gamepadScore)) current = pressedOn;
    var chosen = pads[current];
    Module.gamepadScore = (standard(chosen) ? 4 : 0) + (chosen.buttons.length >= 16 ? 2 : 0) + (chosen.axes.length >= 4 ? 1 : 0);
    Module.gamepadIndex = current;

    var pad = pads[current];
    var base = values >> 2;
    for (var a = 0; a < 4; a++) HEAPF32[base + a] = a < pad.axes.length ? pad.axes[a] : 0;
    for (var b = 0; b < 17; b++) {
      var button = b < pad.buttons.length ? pad.buttons[b] : null;
      var value = button ? button.value : 0;
      // Some controllers report a pressed button with no value; the triggers keep theirs
      if (button && button.pressed && b !== 6 && b !== 7) value = 1;
      HEAPF32[base + 4 + b] = value;
    }
    return standard(pad) ? 1 : 2;
  },

  // Which of the browser's controllers ReadBrowserGamepad reads, or -1 for none
  GetBrowserGamepadIndex: function () {
    return Module.gamepadIndex === undefined ? -1 : Module.gamepadIndex;
  },

  // How many controllers the browser lists, the real one and any others
  GetBrowserGamepadCount: function () {
    if (!navigator.getGamepads) return 0;
    var pads = navigator.getGamepads(), count = 0;
    for (var i = 0; i < pads.length; i++) if (pads[i] && pads[i].connected) count++;
    return count;
  },

  // The name the browser gives the controller in use, for the console and the on-screen notice
  GetBrowserGamepadName: function () {
    var name = "";
    var i = Module.gamepadIndex;
    if (navigator.getGamepads && i !== undefined && i >= 0) {
      var pad = navigator.getGamepads()[i];
      if (pad && pad.connected) name = pad.id + " (mapping: " + (pad.mapping || "none") + ")";
    }
    var size = lengthBytesUTF8(name) + 1;
    var buffer = _malloc(size);
    stringToUTF8(name, buffer, size);
    return buffer;
  }
});
