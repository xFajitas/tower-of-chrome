# Tower of Chrome

A Unity/C# rewrite of *Dungeon of Chrome*, a turn-based dungeon-crawler RPG
originally written in Python/pygame. This project ports the full game —
combat, classes, abilities, procedural dungeons, loot, leveling, and
save/load — to Unity 6 with UI Toolkit, keyboard- and mouse-driven.

The original Python version lives at
[`Dungeon-of-chrome`](https://github.com/xFajitas/Dungeon-of-chrome) and is
left running/untouched; this repo is a from-scratch rewrite, not a fork.

## Download

Prebuilt Windows installers are published under
[Releases](https://github.com/xFajitas/tower-of-chrome/releases). Grab the
latest `TowerOfChrome-Setup-*.exe`, run it, and launch from the Start Menu.

## What's here

- **6 screens**: Menu, Class Select, Explore (procedural dungeon map),
  Combat, Inventory & Trading, Game Over.
- **16 classes, 89 abilities, 12 enemies, 70 items** — ported 1:1 from the
  Python data files (`data/*.json`), including the subtler combat-formula
  quirks (see `docs/porting-notes.md`).
- Full keyboard **and** mouse controls on every screen.
- First-pass CC0 art (Kenney.nl) for the dungeon map, party/class portraits,
  and UI panels — see `docs/art-credits.md` for sourcing and what's still
  placeholder.
- Save/load against the real Windows user data folder
  (`%AppData%\LocalLow\<Company>\Tower of Chrome\`).

## Project layout

```
TowerOfChrome.sln
src/
  TowerOfChrome.Core/          Unity-agnostic game logic (netstandard2.1) --
                                FSM, entities, combat engine, dungeon
                                generator, loot/inventory, save/load.
  TowerOfChrome.Core.Tests/    xUnit tests for Core, run with plain `dotnet test`.
  TowerOfChrome.Unity/         The Unity project (UI Toolkit screens,
                                PlayMode tests, editor build tooling).
data/                          Canonical game-balance JSON, mirrored into
                                the Unity project's StreamingAssets.
installer/                     Inno Setup script that packages the Windows
                                build into an installer.
docs/
  porting-notes.md             Behavioral decisions made while porting from
                                Python -- read before "fixing" anything that
                                looks odd; most oddities are intentional
                                parity with the original.
  art-credits.md                Art sourcing (Kenney.nl, CC0) and what's
                                still deliberately placeholder.
```

## Building

### Core (no Unity required)

```
cd src
dotnet build TowerOfChrome.Core.sln    # or the top-level TowerOfChrome.sln
dotnet test TowerOfChrome.Core.Tests/TowerOfChrome.Core.Tests.csproj
```

### Unity project

Requires **Unity 6000.0.78f1** (or compatible) with Windows Build Support.
Open `src/TowerOfChrome.Unity` in Unity Hub.

Run the PlayMode test suite headlessly:

```
"<UnityEditor>/Unity.exe" -batchmode -nographics \
  -projectPath src/TowerOfChrome.Unity \
  -runTests -testPlatform PlayMode \
  -testResults results.xml -logFile run.log
```

Rebuild the scene from scratch (screens are wired up in code, not by hand,
via `Assets/Editor/SceneBuilder.cs`):

```
"<UnityEditor>/Unity.exe" -batchmode -nographics \
  -projectPath src/TowerOfChrome.Unity \
  -executeMethod SceneBuilder.BuildMainScene -quit
```

### Windows build + installer

```
"<UnityEditor>/Unity.exe" -batchmode -nographics \
  -projectPath src/TowerOfChrome.Unity \
  -executeMethod BuildScript.BuildWindows -quit

"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\TowerOfChrome.iss
```

Output lands in `installer/Output/`.
