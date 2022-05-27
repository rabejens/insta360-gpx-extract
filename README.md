# Insta360 One R (and probably others) GPS Extract Tool

This is a very simple tool based on the works of [ExifTool](https://exiftool.org/) which extracts GPS data from one or more 360 degree video files from an Insta360 One R camera with the GPS remote (and probably others).

For this to work correctly, you must record your videos with the 360-degree module, and use the GPS remote to add the data.

## Building

Use .net 6.0. To build a framework-dependent version, run

```
dotnet publish -c Release Insta360GPXExtract
```

You will find your executable in `Insta360GPXExtract/bin/Release/net6.0/publish`.

You can also build standalone EXEs like so:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true Insta360GPXExtract
```

This, for example, builds a 64-bit Windows version.

To quickly build all common versions, run `build.bat` or `build.sh` scripts. You will then find the framework-dependent version
in `build/fw` and stand-alone runnable versions in the other sub directories of `build`.

## Using

This program is command-line driven. The following options are understood:

 * `-i` - Input path. This can either be a file or a directory. If a directory is given, all files conforming to the naming pattern `VID_yyyyMMdd_HHmmss_00_nnn.insv` are used.
 * `-o` - Output path. How this is used depends on whether only a single file will be written. If multiple files are written, this is always used as a directory.
          If the directory does not already exist, it is created. If only a single file is written, and the path points to a directory that already exists, a file
          with the same name as the input file, with an additional `.gpx` extension, is created. If nothing exists at this path, it is treated as a file.
          If another file already exists at this path, it will be overwritten.
 * `-r` - Recurse through the input directories. This will recursively traverse all directories given with `-i` and use all suitable files which are found.
          If only files are given with `-i`, this is ignored.
 * `-s` - Single file. If given and multiple input files are resolved, they are processed in order, sorted by their timestamp.
          For a correct GPX file, make sure that none of the files overlap.

Example usage:

Let's say you dumped multiple SD cards into the directory `D:\Insta360Videos`, one subdirectory for each card. Then,

```
Insta360GPXExtract -i D:\Insta360Videos -o D:\Insta360Videos\track.gpx -r -s
```

will assemble one `track.gpx` file from all suitable `.insv` files found.

## To do

 * Better approach to find the Insta360 trailer, current one is a bit brute-force
 * Support videos shot with 1-inch module
