# Pintora

This project contains Pintora's cross-platform desktop editor. It is an early alpha and is still
growing toward feature parity with established raster graphics applications.

## Build and run

Install the .NET 10 SDK, then from the repository root:

```sh
dotnet run --project Pintora.Avalonia/Pintora.Avalonia.csproj
```

Run the editor test suite with:

```sh
dotnet test tests/Pintora.Avalonia.Tests/Pintora.Avalonia.Tests.csproj
```

The first alpha release is `0.0.1-alpha`. A successful push to `master` publishes a prerelease
after CI and all platform packages pass. Release downloads include self-contained portable builds
for each supported platform, a Windows MSI, Linux AppImage and Debian package, and macOS DMGs for
Intel and Apple silicon. Packages are not code-signed; macOS DMGs are not notarized and may show a
security warning.

The editor can open PNG, JPEG, WebP, TIFF, BMP, GIF, and OpenRaster (`.ora`) documents. It can
export PNG, JPEG, WebP, TIFF, and OpenRaster files.
