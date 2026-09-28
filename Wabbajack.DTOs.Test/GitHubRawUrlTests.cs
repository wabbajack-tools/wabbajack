using System;
using Wabbajack.Networking.WabbajackClientApi;
using Xunit;

namespace Wabbajack.DTOs.Test;

public class GitHubRawUrlTests
{
    [Theory]
    [InlineData("https://raw.githubusercontent.com/JohnDoe/my-lists/refs/heads/main/modlists.json",
        "https://raw.githubusercontent.com/JohnDoe/my-lists/main/modlists.json")]
    [InlineData("https://raw.githubusercontent.com/JohnDoe/my-lists/refs/heads/master/modlists.json",
        "https://raw.githubusercontent.com/JohnDoe/my-lists/master/modlists.json")]
    [InlineData("https://raw.githubusercontent.com/JohnDoe/my-lists/refs/heads/feature/lists/modlists.json",
        "https://raw.githubusercontent.com/JohnDoe/my-lists/feature/lists/modlists.json")]
    [InlineData("https://raw.githubusercontent.com/JohnDoe/my-lists/refs/tags/v1.0/modlists.json",
        "https://raw.githubusercontent.com/JohnDoe/my-lists/v1.0/modlists.json")]
    [InlineData("https://raw.githubusercontent.com/JohnDoe/my-lists/REFS/HEADS/main/modlists.json",
        "https://raw.githubusercontent.com/JohnDoe/my-lists/main/modlists.json")]
    [InlineData("https://raw.githubusercontent.com/JohnDoe/my-lists/refs/heads/main/modlists.json?token=abc",
        "https://raw.githubusercontent.com/JohnDoe/my-lists/main/modlists.json?token=abc")]
    public void RefsHeadsUrlsAreRewrittenToPlainRefUrls(string input, string expected)
    {
        Assert.Equal(expected, GitHubRawUrl.Normalize(new Uri(input)).OriginalString);
    }

    [Theory]
    [InlineData("https://raw.githubusercontent.com/JohnDoe/my-lists/main/modlists.json")]
    [InlineData("https://raw.githubusercontent.com/JohnDoe/my-lists/refs/modlists.json")]
    [InlineData("https://raw.githubusercontent.com/JohnDoe/refs/heads/modlists.json")]
    [InlineData("https://raw.githubusercontent.com/JohnDoe/my-lists/main/refs/heads/modlists.json")]
    [InlineData("https://raw.githubusercontent.com/JohnDoe/my-lists/refs/pull/12/modlists.json")]
    [InlineData("https://example.com/JohnDoe/my-lists/refs/heads/main/modlists.json")]
    [InlineData("https://johndoe.github.io/my-lists/modlists.json")]
    public void EveryOtherUrlIsLeftAlone(string url)
    {
        Assert.Equal(url, GitHubRawUrl.Normalize(new Uri(url)).OriginalString);
    }
}
