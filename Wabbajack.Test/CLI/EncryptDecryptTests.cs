using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.CLI.Verbs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.CLI.Test;

public class EncryptDecryptTests : IDisposable
{
    private readonly AbsolutePath _inputFile;
    private readonly AbsolutePath _outputFile;
    private readonly string _keyName;
    private readonly AbsolutePath _encryptedPath;

    public EncryptDecryptTests()
    {
        _inputFile = Path.GetTempFileName().ToAbsolutePath();
        _outputFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".bin").ToAbsolutePath();
        _keyName = "test-key-" + Guid.NewGuid().ToString("N")[..8];
        _encryptedPath = KnownFolders.WabbajackAppLocal
            .Combine("encrypted")
            .Combine(_keyName.ToRelativePath());
    }

    public void Dispose()
    {
        if (_inputFile.FileExists()) _inputFile.Delete();
        if (_outputFile.FileExists()) _outputFile.Delete();
        if (_encryptedPath.FileExists()) _encryptedPath.Delete();
    }

    [Fact]
    public async Task EncryptDecrypt_Roundtrip_PreservesContent()
    {
        var originalData = new byte[] { 72, 101, 108, 108, 111, 32, 87, 111, 114, 108, 100 };
        await _inputFile.WriteAllBytesAsync(originalData);

        var encryptVerb = new Encrypt(NullLogger<Encrypt>.Instance);
        var encryptResult = await encryptVerb.Run(_inputFile, _keyName);
        Assert.Equal(0, encryptResult);

        var decryptVerb = new Decrypt(NullLogger<Decrypt>.Instance);
        var decryptResult = await decryptVerb.Run(_outputFile, _keyName);
        Assert.Equal(0, decryptResult);

        var decryptedData = await _outputFile.ReadAllBytesAsync();
        Assert.Equal(originalData, decryptedData);
    }

    [Fact]
    public async Task Encrypt_WritesToExpectedLocation()
    {
        await _inputFile.WriteAllBytesAsync(new byte[] { 1, 2, 3 });

        var verb = new Encrypt(NullLogger<Encrypt>.Instance);
        await verb.Run(_inputFile, _keyName);

        Assert.True(_encryptedPath.FileExists());
    }

    [Fact]
    public async Task EncryptDecrypt_EmptyFile_Roundtrips()
    {
        await _inputFile.WriteAllBytesAsync(Array.Empty<byte>());

        var encryptVerb = new Encrypt(NullLogger<Encrypt>.Instance);
        await encryptVerb.Run(_inputFile, _keyName);

        var decryptVerb = new Decrypt(NullLogger<Decrypt>.Instance);
        await decryptVerb.Run(_outputFile, _keyName);

        var decryptedData = await _outputFile.ReadAllBytesAsync();
        Assert.Empty(decryptedData);
    }

    [Fact]
    public async Task EncryptDecrypt_LargeFile_Roundtrips()
    {
        var data = new byte[64 * 1024];
        new Random(42).NextBytes(data);
        await _inputFile.WriteAllBytesAsync(data);

        var encryptVerb = new Encrypt(NullLogger<Encrypt>.Instance);
        await encryptVerb.Run(_inputFile, _keyName);

        var decryptVerb = new Decrypt(NullLogger<Decrypt>.Instance);
        await decryptVerb.Run(_outputFile, _keyName);

        var decryptedData = await _outputFile.ReadAllBytesAsync();
        Assert.Equal(data, decryptedData);
    }
}
