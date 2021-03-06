RecompressZip
=============

[![Test status](https://ci.appveyor.com/api/projects/status/x1jhkjn2phi3gh5e/branch/main?svg=true)](https://ci.appveyor.com/project/koturn/recompresszip/branch/main "AppVeyor | koturn/RecompressPng")

Zip archive recompressor with Google's zopfli library.

Implementation of [komiya-atsushi/zipzop](https://github.com/komiya-atsushi/zipzop "komiya-atsushi/zipzop") in C#.


## Differences from the original

- Parallel processing of recompression for each file.
- Available all options of zopfli.
- Able to handle zip files with password (ZipCrypto only).
- Able to handle zip files with Data Descriptor.
- Also supports recompression of gzip and PNG files.


## Usage

```shell
> RecompressZip.exe [Option]... [Zip Archive or Directory]...
```


## Build

First, pull all submodules.

```shell
> git submodule update --init
```

Second, build whole project.

```shell
> msbuild /nologo /m /t:restore /p:Configuration=Release;Platform="Any CPU" RecompressZip.sln
> msbuild /nologo /m /p:Configuration=Release;Platform="Any CPU" RecompressZip.sln
```

If you use x86 environment, please run the following command instead.

```shell
> msbuild /nologo /m /t:restore /p:Configuration=Release;Platform="x86" RecompressZip.sln
> msbuild /nologo /m /p:Configuration=Release;Platform="x86" RecompressZip.sln
```


## Depedent Libraries

The following libraries are managed as submodules.

- [google/zopfli](https://github.com/google/zopfli "google/zopfli")
- [koturn/ArgumentParserSharp](https://github.com/koturn/ArgumentParserSharp "koturn/ArgumentParserSharp")
- [koturn/NativeCodeSharp](https://github.com/koturn/NativeCodeSharp "koturn/NativeCodeSharp")
- [koturn/ZopfliSharp](https://github.com/koturn/NativeCodeSharp "koturn/ZopfliSharp")


## LICENSE

This software is released under the MIT License, see [LICENSE](LICENSE "LICENSE").
