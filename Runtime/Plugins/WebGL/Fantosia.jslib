// Fantosia Games SDK: the browser side of the games protocol, version 1 (README.md).
// The game only exchanges messages with the platform page that frames it: no network, no token.

mergeInto(LibraryManager.library, {

  // True when the build runs inside the platform's iframe.
  FantosiaIsEmbedded: function () {
    return window.parent && window.parent !== window ? 1 : 0;
  },

  // Listens to the page once, then says ready. origin is the platform origin of the build's environment.
  FantosiaConnect: function (originPtr) {
    var origin = UTF8ToString(originPtr);
    window.__fantosiaOrigin = origin;
    if (!window.__fantosiaConnected) {
      window.__fantosiaConnected = true;
      window.addEventListener("message", function (event) {
        var m = event.data;
        if (event.source !== window.parent || event.origin !== origin) return;
        if (!m || typeof m.type !== "string") return;
        // unsupported is read before the version check, so an old build can say "please reload".
        if (m.type !== "unsupported" && m.fantosia !== 1) return;
        SendMessage("FantosiaBridge", "OnFantosiaMessage", m.type);
      });
    }
    window.parent.postMessage({ fantosia: 1, type: "ready", data: {} }, origin);
  },

  FantosiaSend: function (typePtr, dataJsonPtr) {
    if (!window.__fantosiaOrigin) return;
    window.parent.postMessage(
      { fantosia: 1, type: UTF8ToString(typePtr), data: JSON.parse(UTF8ToString(dataJsonPtr)) },
      window.__fantosiaOrigin
    );
  }

});
