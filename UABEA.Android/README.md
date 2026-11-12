# UABEA Mobile (Android port - first milestone)

This folder contains the Android application host for the UABEA fork.

## Current status

- Separate Android application project; the original desktop project is not modified.
- Avalonia Android 11.0.1 and .NET 8 Android target.
- ARM64 APK target for modern Android phones (minimum Android 12 / API 31).
- Android Storage Access Framework file picker; selected content is copied into the app cache before parsing.
- Opens supported UnityFS, UnityRaw, and UnityWeb bundles and displays their directory entries.
- Selecting a bundle entry marked as a serialized file loads it from the bundle and displays its asset list.
- Opens standalone serialized assets files and displays their asset list.
- Asset lists show Path ID, Type ID, and data size. Selecting an asset shows its basic metadata and file offsets.
- Bundle entries and asset lists have text filtering and a Back button for navigation.
- Non-serialized bundle entries (including resource streams) are displayed as entries with basic metadata; their contents are not decoded.

## Not implemented yet

This milestone is a read-only browser, not feature parity with desktop UABEA. It does not edit assets, decode serialized object fields, preview textures/audio/fonts, import/export resources, save changes, resolve external resource files automatically, or provide dependency browsing, plugins, Cpp2IL, and desktop multi-window workflows. Files with other bundle signatures or unsupported serialized formats may fail to open.

## Build

Install the .NET 8 SDK, Android SDK/JDK, and the .NET Android workload, then run:

```bash
dotnet workload install android
dotnet publish UABEA.Android/UABEA.Android.csproj -c Release -f net8.0-android31.0 -p:AndroidPackageFormat=apk -p:PublishTrimmed=false -p:RunAOTCompilation=false
```

The Android build workflow runs on pushes to the `android-port` branch and uploads one artifact named `uabea-mobile-apk` containing only `UABEA-Mobile.apk`. GitHub Actions still wraps downloaded artifacts in a ZIP; the ZIP contains only the APK. A persistent release keystore must be configured in repository Actions secrets: `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, and `ANDROID_KEY_PASSWORD`. Keep this keystore backed up and reuse it for every build so future APK updates retain the same signing identity.

You can also start the same build manually from the repository's Actions tab using the **Android Build** workflow.

## Licensing

UABEA and this fork retain the upstream MIT license. Keep the root license and all third-party license notices with redistributed builds.
