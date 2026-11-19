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
  }
});
