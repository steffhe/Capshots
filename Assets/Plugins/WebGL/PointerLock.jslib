// Asks the browser directly whether the mouse is locked to the game.
// Unity's Cursor.lockState only reports what the game last asked for, so it can't tell
// when the browser releases the mouse on Escape.
mergeInto(LibraryManager.library, {
  IsBrowserPointerLocked: function () {
    return document.pointerLockElement ? 1 : 0;
  }
});
