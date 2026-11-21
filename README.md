# Fantosia Games SDK for Unity

Connects a Unity WebGL game to the Fantosia platform. The game runs in an iframe on the games host
(`fantosia.games`) and only exchanges a few messages with the platform page that frames it. It
makes no network request, holds no token and receives nothing about the player. The platform
measures the run's time, saves the score, checks it on the server and shows its own result screen.

Unity 6 (6000.0 or later), WebGL. Version 1.0.0 speaks games protocol version 1.

## Install

Unity: *Window > Package Manager > + > Add package from git URL*:

```
https://github.com/zonixgames-com/game-sdk-unity.git#v1.0.0
```

or in `Packages/manifest.json`:

```json
"com.fantosia.games-sdk": "https://github.com/zonixgames-com/game-sdk-unity.git#v1.0.0"
```

## Use

```csharp
using FantosiaGames;

void OnEnable()
{
    Fantosia.RunStarted += BeginRun;   // the platform's go: start the run now
    Fantosia.RunRefused += ShowStart;  // the platform explains why to the player
    Fantosia.Paused += Pause;          // the player pressed pause in the platform's header
    Fantosia.Resumed += Resume;
}

void OnDisable()
{
    Fantosia.RunStarted -= BeginRun;
    Fantosia.RunRefused -= ShowStart;
    Fantosia.Paused -= Pause;
    Fantosia.Resumed -= Resume;
}

public void OnStartButton() => Fantosia.RequestRunStart(); // do not start the run yet
void Update() { if (running) Fantosia.ReportScore(score); } // optional, at most once a second
void OnGameOver() => Fantosia.EndRun(score);                // once, with the final score
```

The SDK says `ready` by itself when the build loads and mutes or unmutes `AudioListener` on the
platform's sound button (`Fantosia.HandleSound = false` to do it yourself through `Muted` and
`Unmuted`). Outside an iframe (the editor, a standalone build) `Fantosia.Embedded` is false:
`RequestRunStart` raises `RunStarted` at once and the other calls do nothing, so the game plays on
its own.

## Build per environment

A build talks only to the platform of the environment it is made for. Set one scripting define
(*Project Settings > Player > Scripting Define Symbols*, or `-define:` in `Assets/csc.rsp`):

| Define | Platform (only page allowed to frame the build) |
|---|---|
| none | `https://platform.fantosia.app` (development) |
| `FANTOSIA_STAGING` | `https://platform.fantosia.net` |
| `FANTOSIA_PRODUCTION` | `https://platform.fantosia.com` |

## Rules for the game

- **One page load is one run.** After `EndRun` the game freezes its final score. It may play a
  short end animation, but shows no *Play again*, game-over menu, leaderboard or sign-in of its own.
- **Begin the run only on `RunStarted`.** The platform's clock starts then. The run has a time limit
  set per game on the server; a game with no natural end ends the run by itself before it.
- **The score** is a whole number, 0 or more; higher is better. The server checks it against the
  game's limits (time, maximum score, score per second): it is a claim, never trusted as is.
- **No network** except the build's own files, no third-party SDK, no analytics. Turn off Unity
  Analytics and Cloud Diagnostics (*Project Settings > Services*).
- **Nothing about the player in browser storage** (`PlayerPrefs`): several members can play on one
  browser, and every game on the games host shares that storage. Settings such as sound can stay,
  prefixed with the game's name.
- **No inline script or style in the web page template.** The games host forbids them (Content
  Security Policy): keep CSS and JavaScript in files under `TemplateData/`.

## Protocol version 1

Messages go through `window.postMessage`, only between the game and the platform page of the
build's environment. Each is `{ fantosia: 1, type, data }`.

| Game to page | When | `data` |
|---|---|---|
| `ready` | The build is loaded (sent by the SDK). | `{}` |
| `run:start` | `RequestRunStart`. | `{}` |
| `run:score` | `ReportScore`, optional, at most once a second. | `{ score }` |
| `run:end` | `EndRun`: the score is final. | `{ score }` |
| `error` | `Error`: the build cannot continue. | `{ message }`, at most 200 characters |

| Page to game | Event | |
|---|---|---|
| `init` | | Answer to `ready`. |
| `unsupported` | `Unsupported` | The page does not speak this version: the build is out of date. |
| `run:started` | `RunStarted` | The run clock starts. |
| `run:refused` | `RunRefused` | The run cannot start. |
| `pause`, `resume` | `Paused`, `Resumed` | |
| `mute`, `unmute` | `Muted`, `Unmuted` | |

The page reacts to the first `ready`, `run:start` and `run:end` only, and never accepts a
`run:end` without a `run:started` before it. There is no answer to `run:end`.

A change an existing build would misread gets a new protocol version and a new major SDK version.

## Protocol test page

`Tests~/protocol-test/` (ignored by Unity) is a test game that speaks the protocol by hand, with
buttons and `window.protocolTest` for automated tests: start a run, end it with a chosen score,
end it twice, send `run:score`, `error`, or a `ready` with an old version. It is served on the
development games host only, to test the platform without a Unity build.

