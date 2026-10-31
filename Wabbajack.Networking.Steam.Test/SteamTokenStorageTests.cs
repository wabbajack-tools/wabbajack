using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

public class SteamTokenStorageTests
{
    private static ITokenProvider<SteamLoginState> MakeProvider(string key)
    {
        return new EncryptedJsonTokenProvider<SteamLoginState>(NullLogger.Instance,
            new DTOSerializer(Array.Empty<JsonConverter>()), key);
    }

    /// <summary>A stored login is readable by its owner alone off Windows, including one an older build wrote.</summary>
    [Fact]
    public async Task AStoredLoginIsReadableOnlyByItsOwner()
    {
        if (OperatingSystem.IsWindows()) return;

        var key = $"steam-login-test-{Guid.NewGuid():N}";
        var provider = MakeProvider(key);
        var file = KnownFolders.WabbajackAppLocal.Combine("encrypted", key).ToString();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, "");
            File.SetUnixFileMode(file, (UnixFileMode) 0b110_100_100);

            await provider.SetToken(new SteamLoginState {AccountName = "someaccount"});

            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        }
        finally
        {
            await provider.Delete();
        }
    }

    [Fact]
    public async Task RoundTripsAStoredLoginThroughTheEncryptedProvider()
    {
        // A distinct key per run so a test never stands on the user's real saved Steam login.
        var key = $"steam-login-test-{Guid.NewGuid():N}";
        var provider = MakeProvider(key);

        try
        {
            Assert.False(provider.HaveToken());

            var expiry = DateTimeOffset.FromUnixTimeSeconds(1_760_000_000);
            await provider.SetToken(new SteamLoginState
            {
                AccountName = "someaccount",
                RefreshToken = "header.payload.signature",
                GuardData = "guard-data",
                RefreshTokenExpiresAt = expiry
            });

            Assert.True(provider.HaveToken());

            var read = await provider.Get();
            Assert.NotNull(read);
            Assert.Equal("someaccount", read!.AccountName);
            Assert.Equal("header.payload.signature", read.RefreshToken);
            Assert.Equal("guard-data", read.GuardData);
            Assert.Equal(expiry, read.RefreshTokenExpiresAt);

            Assert.True(await provider.Delete());
            Assert.False(provider.HaveToken());
            Assert.False(await provider.Delete());
        }
        finally
        {
            await provider.Delete();
        }
    }

    [Fact]
    public async Task RoundTripsALoginWithNoGuardDataAndNoKnownExpiry()
    {
        var key = $"steam-login-test-{Guid.NewGuid():N}";
        var provider = MakeProvider(key);

        try
        {
            await provider.SetToken(new SteamLoginState
            {
                AccountName = "someaccount",
                RefreshToken = "header.payload.signature"
            });

            var read = await provider.Get();
            Assert.NotNull(read);
            Assert.Null(read!.GuardData);
            Assert.Null(read.RefreshTokenExpiresAt);
        }
        finally
        {
            await provider.Delete();
        }
    }

    [Fact]
    public void TheStoredStateSerialisesWithStableNames()
    {
        // The on-disk names are part of the format: changing one silently logs every user out.
        var names = new List<string>();
        foreach (var property in typeof(SteamLoginState).GetProperties())
        {
            var attribute = (JsonPropertyNameAttribute?) Attribute.GetCustomAttribute(property,
                typeof(JsonPropertyNameAttribute));
            Assert.NotNull(attribute);
            names.Add(attribute!.Name);
        }

        Assert.Equal(new[] {"account_name", "refresh_token", "guard_data", "refresh_token_expires_at"}, names);
    }
}
