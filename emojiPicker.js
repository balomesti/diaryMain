// wwwroot/js/emojiPicker.js
// ─────────────────────────────────────────────────────────────
// Emoji Mart v5 interop helper for Blazor.
//
// Usage in index.html (before </body>):
//   <script type="module" src="https://cdn.jsdelivr.net/npm/emoji-mart@5/dist/browser.js"></script>
//   <script src="js/emojiPicker.js"></script>
// ─────────────────────────────────────────────────────────────

window.emojiPicker = {
  _instances: {},

  show(dotNetRef, anchorId, inputId) {
    const anchor = document.getElementById(anchorId);
    if (!anchor) return;

    // toggle: if already open for this input, close it
    if (this._instances[inputId]) {
      this._instances[inputId].remove();
      delete this._instances[inputId];
      return;
    }

    // close any other open picker first
    Object.keys(this._instances).forEach(key => {
      this._instances[key].remove();
      delete this._instances[key];
    });

    const picker = new EmojiMart.Picker({
      onEmojiSelect: (emoji) => {
        dotNetRef.invokeMethodAsync('OnEmojiSelected', inputId, emoji.native);
        picker.remove();
        delete this._instances[inputId];
      },
      onClickOutside: (e) => {
        // don't close if the click was on the trigger button itself
        if (anchor.contains(e.target)) return;
        picker.remove();
        delete this._instances[inputId];
      },
      theme: 'light',
      previewPosition: 'none',
      skinTonePosition: 'none',
      maxFrequentRows: 2,
    });

    anchor.appendChild(picker);
    this._instances[inputId] = picker;
  }
};
