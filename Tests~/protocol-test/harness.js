// A test game for the Fantosia games protocol, version 1. It speaks the protocol by hand so the
// platform can be tested in a browser without Unity: start, a chosen score, a too short run, too
// many points a second, a second run:end, an old version. Buttons for people; window.protocolTest
// for automated tests (Chrome DevTools Protocol). Development only: served on dev.fantosia.games.
(function () {
  // The platform page that frames this test, by the games host it is served from.
  var ORIGINS = {
    "dev.fantosia.games": "https://platform.fantosia.app",
    "staging.fantosia.games": "https://platform.fantosia.net",
    "localhost": "http://localhost:3000",
  };
  var origin = ORIGINS[location.hostname];
  var log = document.getElementById("log");
  var status = document.getElementById("status");
  var received = [];

  function write(line) {
    log.textContent += new Date().toISOString().slice(11, 23) + " " + line + "\n";
  }

  function send(type, data, version) {
    if (!origin) return write("no platform origin for " + location.hostname);
    var message = { fantosia: version === undefined ? 1 : version, type: type, data: data || {} };
    window.parent.postMessage(message, origin);
    write("→ " + JSON.stringify(message));
  }

  window.addEventListener("message", function (event) {
    if (event.source !== window.parent || event.origin !== origin) return;
    var m = event.data;
    if (!m || typeof m.type !== "string") return;
    received.push(m.type);
    write("← " + JSON.stringify(m));
    status.textContent = "last message from the platform: " + m.type;
  });

  function score() { return parseInt(document.getElementById("score").value, 10) || 0; }

  var api = {
    received: received,
    start: function () { send("run:start"); },
    end: function (value) { send("run:end", { score: value === undefined ? score() : value }); },
    endTwice: function (value) { var s = value === undefined ? score() : value; send("run:end", { score: s }); send("run:end", { score: s * 10 }); },
    score: function (value) { send("run:score", { score: value === undefined ? score() : value }); },
    error: function (message) { send("error", { message: message || "test error from the protocol test page" }); },
    oldVersion: function () { send("ready", {}, 0); },
  };
  window.protocolTest = api;

  document.querySelectorAll("button[data-action]").forEach(function (button) {
    button.addEventListener("click", function () { api[button.dataset.action](); });
  });

  status.textContent = origin ? "ready sent to " + origin : "unknown games host: nothing sent";
  send("ready");
})();
