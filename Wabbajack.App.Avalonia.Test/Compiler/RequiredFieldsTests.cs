using System;
using System.IO;
using Wabbajack.App.Avalonia.ViewModels.Compiler;
using Wabbajack.Paths;
using Xunit;

namespace Wabbajack.App.Avalonia.Test.Compiler;

/// <summary>
///     What has to be filled in before Compile can be pressed. It is the button's only gate and its tooltip, so a
///     field left out of it lets a compile start that fails minutes in, and a field wrongly in it blocks one.
/// </summary>
public class RequiredFieldsTests : IDisposable
{
    private readonly string _image = Path.Combine(Path.GetTempPath(), $"wj-required-{Guid.NewGuid():N}.png");

    public RequiredFieldsTests()
    {
        File.WriteAllBytes(_image, new byte[] {1});
    }

    public void Dispose()
    {
        File.Delete(_image);
    }

    private CompilerSettingsVM Complete()
    {
        return new CompilerSettingsVM
        {
            ModListName = "My List",
            ModListAuthor = "Me",
            ModListDescription = "A list",
            ModListImage = _image.ToAbsolutePath(),
            Downloads = Path.Combine(Path.GetTempPath(), "downloads").ToAbsolutePath(),
            OutputFile = Path.Combine(Path.GetTempPath(), "My List.wabbajack").ToAbsolutePath(),
            Version = "1.2.0"
        };
    }

    [Fact]
    public void Complete_NeedsNothing()
    {
        Assert.Empty(CompilerMainVM.MissingRequiredFields(Complete()));
    }

    [Theory]
    [InlineData("title")]
    [InlineData("author")]
    [InlineData("description")]
    [InlineData("an image that exists")]
    [InlineData("downloads folder")]
    [InlineData("export destination ending in .wabbajack")]
    [InlineData("a version such as 1.0.0")]
    public void EachMissingField_IsNamed(string field)
    {
        var settings = Complete();
        switch (field)
        {
            case "title": settings.ModListName = "  "; break;
            case "author": settings.ModListAuthor = ""; break;
            case "description": settings.ModListDescription = ""; break;
            case "an image that exists": settings.ModListImage = (_image + ".missing").ToAbsolutePath(); break;
            case "downloads folder": settings.Downloads = default; break;
            case "export destination ending in .wabbajack":
                settings.OutputFile = Path.Combine(Path.GetTempPath(), "My List.zip").ToAbsolutePath(); break;
            case "a version such as 1.0.0": settings.Version = "first"; break;
        }

        Assert.Equal(new[] {field}, CompilerMainVM.MissingRequiredFields(settings));
    }
}
