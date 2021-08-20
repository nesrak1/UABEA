![](UABEAvalonia/Assets/logo.png)

## [Latest Build](https://nightly.link/nesrak1/UABEA/workflows/dotnet-desktop/master/uabea-windows.zip)

[![GitHub issues](https://img.shields.io/github/issues/nesrak1/UABEA?logo=GitHub&style=flat-square)](https://github.com/nesrak1/UABEA/issues) [![discord](https://img.shields.io/discord/862035581491478558?label=discord&logo=discord&logoColor=FFFFFF&style=flat-square)](https://discord.gg/hd9VdswwZs)

## Why UABEAvalonia

[UABE](https://github.com/DerPopo/UABE) has currently not been updated in a while and does not support newer Unity versions. The goal of UABEA is to remake UABE to keep it up to date. All of the UI has been kept the same for those familiar with UABE. At the moment, implementing base features of UABE is still in progress. Also, it is built with Avalonia so it (should be) cross platform, no Wine needed.

## New features

There's not much in the way of new features as of yet (I wasn't even planning on doing this). There have been many requested features such as sprite importing/exporting, better command line support, batch import/export from gui, etc. I do not have a lot of time, but I do hope to get to these someday.

## Exporting assets

UABEA can export textures and asset dumps, but that's about it. If you're trying to dump anything else, try [AssetStudio](https://github.com/Perfare/AssetStudio) or [AssetRipper](https://github.com/ds5678/AssetRipper) (uTinyRipper), but these tools cannot import again. 

## Scripting

If you're doing something that requires scripting such as dumping all of the fields from a MonoBehaviour, importing multiple text files or textures, etc. without interacting with the gui, try using [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) instead. UABEA can be a good way to figure out how the file is laid out, but the script can be written with AssetsTools. If AssetsTools is too complicated, you can also try [UnityPy](https://github.com/K0lb3/UnityPy) which has a simpler api with the cost of supporting less assets.

## Differences between UABE and UABEA

|                                     | UABE           | UABEA                                 |
| ----------------------------------- | -------------- | ------------------------------------- |
| Supported versions                  | Unity 4-2019.3 | Unity 5-2020.3                        |
| Class data editor                   | Yes            | No                                    |
| Standalone .exe creator             | Yes            | No                                    |
| Package creator                     | Yes            | Yes, but bundles aren't supported yet |
| Bundle > compress to memory         | No             | Yes                                   |
| Assets info > Dependencies          | Yes            | No                                    |
| Assets info > Containers            | Yes            | No                                    |
| Plugins > AudioClip                 | Yes            | No                                    |
| Plugins > Mesh                      | Yes            | No                                    |
| Plugins > MovieTexture              | Yes            | No                                    |
| Plugins > SubstanceArchive          | Yes            | No                                    |
| Plugins > TerrainData               | Yes            | No                                    |
| Plugins > UMAMesh                   | Yes            | No                                    |
| And more I probably forgot about... |                |                                       |

## Libraries

* [Avalonia](https://github.com/AvaloniaUI/Avalonia) for UI
* [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) for assets reading/writing which uses [detex](https://github.com/hglm/detex) for DXT decoding
* [ISPC](https://github.com/GameTechDev/ISPCTextureCompressor) for DXT encoding
* [crnlib](https://github.com/Unity-Technologies/crunch/tree/unity) (crunch) for crunch decompressing and compressing
* [PVRTexLib](https://developer.imaginationtech.com/downloads/) (PVRTexTool) for all other texture decoding and encoding