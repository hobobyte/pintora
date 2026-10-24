# Pintora

Pintora is a free, open-source raster graphics editor for creating and editing images. It is being
developed toward the familiar workflow of desktop paint applications, with layers, selection tools,
brushes, shapes, and image adjustments.

> **Alpha notice:** Pintora `0.0.1-alpha` is an early preview. Features and file workflows are still
> incomplete, and bugs or data loss are possible. Keep backups of important artwork and project
> files; do not rely on this alpha as the only copy of your work.

## Get Pintora

The `v0.0.1-alpha` release pipeline builds self-contained packages for Windows x64, Linux x64, and
macOS (Intel and Apple silicon):

- Windows installer (`.msi`) and portable `.zip`;
- Linux AppImage, Debian package (`.deb`), and portable `.tar.gz`;
- macOS disk images (`.dmg`) and portable `.tar.gz` for both architectures.

After a successful push to `master`, the workflow publishes a prerelease with all platform packages
attached. Packages are unsigned; macOS builds are not notarized and may display a security warning
on first launch.

See the [Pintora changelog](CHANGELOG.md) for the fork baseline, alpha contents, and known limitations.

## Build and run

Install the .NET 10 SDK. From the repository root:

```sh
dotnet run --project Pintora.Avalonia/Pintora.Avalonia.csproj
```

To build a Release version:

```sh
dotnet build Pintora.Avalonia/Pintora.Avalonia.csproj --configuration Release
```

## Tests

Run the editor test suite with:

```sh
dotnet test tests/Pintora.Avalonia.Tests/Pintora.Avalonia.Tests.csproj
```

## Contributing and reporting issues

Bug reports, feature suggestions, and contributions are welcome through the
[Pintora GitHub repository](https://github.com/hobobyte/pintora). When reporting a bug, include
your operating system, steps to reproduce it, and a sample file only if it is safe to share.

## License and credits

Pintora-specific contributions are licensed under MIT; see [`LICENSE.md`](LICENSE.md). Upstream
Pinta and Paint.NET code retain their original notices in
[`third-party/pinta/LICENSE.md`](third-party/pinta/LICENSE.md) and
[`third-party/paint.net/LICENSE.md`](third-party/paint.net/LICENSE.md). Icon sources and attributions are listed in
[`Pinta.Resources/icons/pinta-icons.md`](Pinta.Resources/icons/pinta-icons.md).
