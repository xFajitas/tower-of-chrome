# Tower of Chrome

[![Download](https://img.shields.io/badge/Download-Latest%20Release-2ea44f?style=for-the-badge)](https://github.com/xFajitas/tower-of-chrome/releases/latest)

A Unity/C# rewrite of *Dungeon of Chrome*, a turn-based dungeon-crawler RPG
originally written in Python/pygame.

The original Python version lives at
[`Dungeon-of-chrome`](https://github.com/xFajitas/Dungeon-of-chrome) and is
left running/untouched; this repo is a from-scratch rewrite, not a fork.

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

Output lands in `installer/Output/`. The installer script (`installer/TowerOfChrome.iss`)
detects and silently removes a previous install of the same AppId before
copying new files, so re-running it on a machine with an older version
upgrades cleanly rather than leaving stale files behind.
