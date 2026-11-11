# Android Port Status

Branch: `android-port`

## Implemented in the first milestone

- Added a separate Avalonia Android application host under `UABEA.Android/`.
- Added an Android activity and application manifest.
- Added an ARM64 Android target using .NET 8 and Avalonia 11.0.1.
- Added a touch-oriented initial screen and Android Storage Access Framework file picker.
- Added initial parsing and listing for UnityFS/UnityRaw/UnityWeb bundles and serialized assets files through the existing AssetsTools.NET assembly.
- Added a GitHub Actions workflow for Android builds and source archives.

## Known limitations

This is not yet a complete mobile replacement for desktop UABEA. Editing, import/export, saving, dependency handling, multi-window workflows, and desktop plugins are not yet ported. Native texture/audio dependencies need Android ARM64 implementations or managed replacements.

The Android APK must pass CI and be tested on a physical device before this milestone can be considered build-verified.
