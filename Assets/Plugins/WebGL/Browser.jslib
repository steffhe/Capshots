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

  // The first connected controller, read straight from the browser's Gamepad API (BrowserGamepad.cs):
  // its 4 stick axes, then its first 17 buttons (0 to 1; the triggers in between). Returns 0 if
  // none is connected, 1 for one with the browser's "standard" layout, 2 for any other.
  ReadBrowserGamepad: function (values) {
    if (!navigator.getGamepads) return 0;
    var pads = navigator.getGamepads();
    for (var i = 0; i < pads.length; i++) {
      var pad = pads[i];
      if (!pad || !pad.connected) continue;
      var base = values >> 2;
      for (var a = 0; a < 4; a++) HEAPF32[base + a] = a < pad.axes.length ? pad.axes[a] : 0;
      for (var b = 0; b < 17; b++) {
        var button = b < pad.buttons.length ? pad.buttons[b] : null;
        var value = button ? button.value : 0;
        // Some controllers report a pressed button with no value; the triggers keep theirs
        if (button && button.pressed && b !== 6 && b !== 7) value = 1;
        HEAPF32[base + 4 + b] = value;
      }
      return pad.mapping === "standard" ? 1 : 2;
    }
    return 0;
  },

  // The name the browser gives that controller, for the console
  GetBrowserGamepadName: function () {
    var name = "";
    if (navigator.getGamepads) {
      var pads = navigator.getGamepads();
      for (var i = 0; i < pads.length; i++) {
        if (pads[i] && pads[i].connected) { name = pads[i].id + " (mapping: " + (pads[i].mapping || "none") + ")"; break; }
      }
    }
    var size = lengthBytesUTF8(name) + 1;
    var buffer = _malloc(size);
    stringToUTF8(name, buffer, size);
    return buffer;
  }
});
