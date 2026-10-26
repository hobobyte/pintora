using System;
using System.IO;
using NUnit.Framework;

namespace Pintora.Avalonia.Tests;

[TestFixture]
internal sealed class BackendDependencyBoundaryTests
{
	[Test]
	public void SkiaBackendDoesNotCarryGtkRuntimeDependencies ()
	{
		Assert.That (ReadDependencies ("src/graphics/Pinta.Graphics.Skia/bin/Release/net10.0/Pinta.Graphics.Skia.deps.json"), Does.Not.Contain ("GirCore."));
	}

	[Test]
	public void CodecBackendDoesNotCarryGtkRuntimeDependencies ()
	{
		Assert.That (ReadDependencies ("src/codecs/Pinta.Codecs/bin/Release/net10.0/Pinta.Codecs.deps.json"), Does.Not.Contain ("GirCore."));
	}

	private static string ReadDependencies (string relativePath)
	{
		string repositoryRoot = Path.GetFullPath (Path.Combine (AppContext.BaseDirectory, "../../../../../"));
		string path = Path.Combine (repositoryRoot, relativePath);
		Assert.That (File.Exists (path), Is.True, $"Missing dependency manifest: {path}");
		return File.ReadAllText (path);
	}
}
