# UABEA Mobile (Android port - first milestone)

This folder contains the first Android application host for the UABEA fork.

## Current status

- Separate Android application project; the original desktop project is not modified.
- Avalonia Android 11.0.1 and .NET 8 Android target.
- ARM64 APK target for modern Android phones (minimum Android 12 / API 31, matching the pinned Avalonia Android 11.0.1 package).
- Android Storage Access Framework file picker; selected content is copied into the app cache before parsing.
- Initial read-only inspection of UnityFS/UnityRaw/UnityWeb bundles and serialized assets files using the existing AssetsTools.NET assembly.

## Not implemented yet

This is an initial Android port milestone, not feature parity with desktop UABEA. Editing, import/export, saving, dependency browsing, plugins, Cpp2IL, texture/audio/font processing, and the desktop multi-window workflows still need Android-specific work and validation. In particular, desktop plugins currently depend on native libraries that are not built for Android ARM64.

## Build

Install the .NET 8 SDK, Android SDK/JDK, and the .NET Android workload, then run:

```bash
dotnet workload install android
dotnet publish UABEA.Android/UABEA.Android.csproj -c Release -f net8.0-android34.0 -p:AndroidPackageFormat=apk -p:PublishTrimmed=false -p:RunAOTCompilation=false
```

The Android build workflow runs on pushes to the `android-port` branch and uploads the APK plus a ZIP archive of the complete repository source.

## Licensing

UABEA and this fork retain the upstream MIT license. Keep the root license and all third-party license notices with redistributed builds.
