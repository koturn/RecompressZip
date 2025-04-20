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
> git submodule update --init --recursive
```

Second, build whole project.

```shell
> nmake
```

## Depedent Libraries

The following libraries are managed as submodules.

- [koturn/Koturn.CommandLine](https://github.com/koturn/Koturn.CommandLine "koturn/Koturn.CommandLine")
- [koturn/Koturn.Zopfli](https://github.com/koturn/NativeCodeSharp "koturn/Koturn.Zopfli")
    - [google/zopfli](https://github.com/google/zopfli "google/zopfli")
- [koturn/NativeCodeSharp](https://github.com/koturn/NativeCodeSharp "koturn/NativeCodeSharp")


## LICENSE

This software is released under the MIT License, see [LICENSE](LICENSE "LICENSE").
