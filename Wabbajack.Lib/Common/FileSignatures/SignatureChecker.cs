using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Common.FileSignatures;

public class SignatureChecker
{
    private readonly int _maxLength;
    private readonly (FileType, byte[])[] _signatures;

    public SignatureChecker(params FileType[] types)
    {
        HashSet<FileType> types1 = new(types);
        _signatures = Definitions.Signatures.Where(row => types1.Contains(row.Item1))
            .OrderByDescending(x => x.Item2.Length).ToArray();
        _maxLength = _signatures.First().Item2.Length;
    }

    public async ValueTask<FileType?> MatchesAsync(AbsolutePath path)
    {
        await using var fs = path.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        return await MatchesAsync(fs);
    }

    public async ValueTask<FileType?> MatchesAsync(Stream stream)
    {
        var buffer = new byte[_maxLength];
        stream.Position = 0;
        await stream.ReadAsync(buffer);
        stream.Position = 0;

        foreach (var (fileType, signature) in _signatures)
            if (AreEqual(buffer, signature))
                return fileType;

        return null;
    }

    private static bool AreEqual(IReadOnlyList<byte> a, IEnumerable<byte> b)
    {
        return !b.Where((t, i) => a[i] != t).Any();
    }
}