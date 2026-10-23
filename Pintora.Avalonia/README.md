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

The first alpha release is `0.0.1-alpha`. Release downloads include self-contained portable builds
for each supported platform, a Windows MSI, Linux AppImage and Debian package, and macOS DMGs for
Intel and Apple silicon. The matching `v0.0.1-alpha` tag creates a draft prerelease after CI and all
platform packages pass. Packages are not code-signed; macOS DMGs are not notarized and may show a
security warning.
