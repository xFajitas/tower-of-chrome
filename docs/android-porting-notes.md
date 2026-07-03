# Android Porting Notes

Living doc of platform-porting decisions for the Android build target, added on top of the
existing Windows Standalone build. Unlike `porting-notes.md` (Python -> C# behavioral parity),
this covers Unity-platform concerns only — no `TowerOfChrome.Core` gameplay logic changes.

## Single project, second build target

Android is a second `BuildTarget` inside the existing `src/TowerOfChrome.Unity` project, not a
separate copied project. `TowerOfChrome.Core`, all game data, and the UI Toolkit screens are
shared unchanged between Windows and Android. `Assets/Editor/BuildScript.cs` now has
`BuildAndroid()` alongside `BuildWindows()`, invoked the same way:
`Unity.exe -batchmode -nographics -projectPath <path> -executeMethod BuildScript.BuildAndroid -quit`,
output to `Builds/Android/TowerOfChrome.apk`.

## StreamingAssets loading

`Assets/Scripts/Data/UnityStreamingAssetsDataSource.cs` read the 7 JSON data files via plain
`System.IO.File.ReadAllText`, which works on Windows Standalone/Editor but not on Android —
StreamingAssets live inside the compressed APK there and aren't filesystem-accessible that way.
`ReadText` now branches on `UNITY_ANDROID`: the Android path uses `UnityWebRequest.Get` and
blocks on `isDone`. This stays synchronous (rather than becoming a coroutine) because
`GameManager.Awake()` constructs all Core registries synchronously — a local jar:/APK-asset read
completes off a native callback rather than a frame tick, so spin-waiting the main thread doesn't
deadlock waiting on its own next `Update()`.

## Orientation

Landscape-only. The UI Toolkit panels (`Assets/UI/DefaultPanelSettings.asset`) use a 1200x800
reference resolution, closer to landscape than portrait, and reworking every screen's layout for
a narrow portrait aspect ratio was out of scope for this pass. `defaultScreenOrientation` stays
`AutoRotation` but `allowedAutorotateToPortrait`/`allowedAutorotateToPortraitUpsideDown` are now
`0`, leaving both landscape directions enabled — the standard Unity landscape-lock pattern, so the
UI stays upright whichever way the player holds the device rather than force-locking to one
specific landscape direction.

## IL2CPP + System.Text.Json AOT risk

`AndroidTargetArchitectures` is already ARM64-only (`2`), which requires the IL2CPP scripting
backend — Mono can't target ARM64 on Android. `BuildScript.BuildAndroid()` now sets this
explicitly via `PlayerSettings.SetScriptingBackend` rather than relying on
`ProjectSettings.asset`, which had no per-platform value recorded for it at all.

This project has never been built with IL2CPP before (Windows Standalone uses Mono), and
`Assets/Plugins/TowerOfChrome.Core/System.Text.Json.dll`'s `JsonSerializer.Deserialize<T>` calls
are reflection-based, not source-generated — a known IL2CPP AOT stripping risk. Mitigated for now
with `Assets/link.xml`, which preserves the whole `TowerOfChrome.Core` assembly from stripping.
**If a device build still throws a `MissingMethodException`/similar during data loading**, the
proper fix is switching to a source-generated `System.Text.Json.Serialization.JsonSerializerContext`
covering the DTO types in `TowerOfChrome.Core.Data.DataModels` — avoids reflection entirely, but
touches `TowerOfChrome.Core`'s data layer, so it wasn't done preemptively in this pass.

Also worth a one-time check: `Assets/Plugins/TowerOfChrome.Core/*.dll` `.meta` files currently
carry no explicit per-platform `PluginImporter` data (just `fileFormatVersion`/`guid`). The first
time this project is opened in the Unity Editor GUI and Android is selected as the active
platform, spot-check each DLL's Inspector to confirm Android compatibility is enabled.

## Signing

`AndroidUseCustomKeystore` is left at `0` (Unity auto-debug-signs), which is enough to sideload
and test on a device but not to publish. A real upload keystore + Play App Signing setup is a
follow-up before any Play Store release — consistent with this project's existing precedent of
not code-signing the Windows installer either (see the README's SmartScreen section).

## Non-goals this pass

- No on-screen touch equivalents for the existing keyboard-only shortcuts (arrow-key nav,
  number-key 1-4 class select, Enter/Escape in `ClassSelectScreenView`/`ExploreScreenView`/
  `CombatScreenView`/`GameOverScreenView`). UI Toolkit's `ClickEvent` handlers already receive
  touch taps as pointer events, so basic click-driven play works without new touch-input code —
  but any action reachable *only* via a keyboard shortcut has no touch path yet.
- Portrait support.
- Play Store release signing/listing.
