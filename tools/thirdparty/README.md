# Third-party sources

Vendored here so the local compile check in `scripts/smoke-compile.ps1` does not
depend on a temporary directory.

## Why this exists

`smoke-compile.ps1` type-checks the whole client (including everything that uses
`UnityEngine.InputSystem`) by compiling the Input System package's C# sources
alongside the game code, because Unity ships that package as source rather than
as a DLL. Those sources were originally read from `%TEMP%\ispkg`.

That worked until the temp directory got cleaned. Every file that referenced
`Keyboard.current` or `Mouse.current` then failed with

    CS0234: the type or namespace name 'InputSystem' does not exist

and because a declaration-phase error suppresses all method-body errors in
Roslyn, every real type error underneath it was hidden. One `CS1503`
(`FaceTowards` taking a `Transform` where a `Vector3` was required) reached CI
unnoticed. Nothing may be read from `%TEMP%` here for that reason.

## What is vendored

`inputsystem/` is the unpacked tarball of `com.unity.inputsystem` 1.7.0, from

    https://packages.unity.com/com.unity.inputsystem/-/com.unity.inputsystem-1.7.0.tgz

which is the version pinned in `client/Packages/manifest.json`. Licence: see
`inputsystem/package/LICENSE.md`.

## Notes

- This directory sits outside `client/`, so Unity never imports it and it has
  no effect on the player build.
- The smoke script excludes `tools/thirdparty` by exact path when separating
  our diagnostics from third-party noise. Package-internal errors (FieldOffset
  needing an explicit layout, internal visibility) are expected here because
  the smoke build does not pass Unity's defines.
- To refresh, re-download the tarball and unpack it over `inputsystem/`.
