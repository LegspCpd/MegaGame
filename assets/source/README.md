# Asset sources

All third-party art, audio and model files used by MegaGame live **inside this
repository**, under `assets/source/`. Nothing may be downloaded to any other
drive or to a location outside `G:\megame`; assets fetched outside the repo are
invisible to git, to CI and to a fresh clone, and have caused missing-file
failures before.

```
assets/source/
  models/     FBX / OBJ / GLB meshes
  textures/   PNG / TGA image maps
  audio/      WAV / OGG sound effects
```

## Rules

1. **Download only into `assets/source/<category>/`.** Nothing goes to
   `%TEMP%`, another drive, or a tool-specific cache that is not vendored.
2. **Check `git check-ignore -v <path>` before relying on a new folder.**
   `.gitignore` contains `client/Build/`, which git matches
   case-insensitively on Windows and which has already swallowed
   `client/build/` (that is how `client/build/installer/MegaGame.iss` ended up
   missing from the repository and broke the installer for several releases).
   Anything asset-related lives under `assets/`, which nothing ignores.
3. **Record the licence.** Every downloaded asset needs an entry in
   `assets/source/CREDITS.md` with the source URL, author and licence. Open
   assets only (CC0 / CC-BY / MIT / public domain).
4. **Unity must be able to reference it.** This repository ships no `.meta`
   files: GUIDs are generated on first import and differ between machines. A
   model dropped into `client/Assets/` therefore cannot be safely referenced
   from a checked-in scene or prefab. Until a GUID handling story exists,
   content is generated at runtime by `client/Assets/Scripts/Core/MeshBuilder.cs`
   and friends, which needs no asset references at all.

## Current state

`assets/source/` is intentionally empty. All geometry, materials and UI in the
playable build are generated procedurally at runtime.
