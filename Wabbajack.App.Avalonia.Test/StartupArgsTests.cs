using System;
using System.IO;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.Paths;
using Xunit;

namespace Wabbajack.App.Avalonia.Test;

/// <summary>What the app makes of the argument a launcher or a file manager starts it with.</summary>
public class StartupArgsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "wj-startup-" + Guid.NewGuid().ToString("N"));
    private readonly string _file;

    public StartupArgsTests()
    {
        Directory.CreateDirectory(_folder);
        _file = Path.Combine(_folder, "My List.wabbajack");
        File.WriteAllText(_file, "");
    }

    public void Dispose()
    {
        Directory.Delete(_folder, true);
    }

    [Fact]
    public void APath_NamesTheFile()
    {
        Assert.Equal((AbsolutePath) _file, StartupChecks.WabbajackFile(_file));
    }

    /// <summary>A .desktop entry's %u lets a file manager pass a local file as a file:// URL, escaped.</summary>
    [Fact]
    public void AFileUrl_NamesTheFile()
    {
        Assert.Equal((AbsolutePath) _file, StartupChecks.WabbajackFile(new Uri(_file).AbsoluteUri));
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("file://")]
    [InlineData("wabbajack://halgari/halgaris_helper")]
    public void AnythingElse_IsNoFile(string arg)
    {
        Assert.Null(StartupChecks.WabbajackFile(arg));
    }
}
