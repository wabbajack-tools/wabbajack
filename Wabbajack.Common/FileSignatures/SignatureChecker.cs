using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Wabbajack.Common.FileSignatures
{
    public class SignatureChecker
    {
        private readonly HashSet<Definitions.FileType> _types;
        private readonly (Definitions.FileType, byte[])[] _signatures;

        private readonly int _maxLength;

        public SignatureChecker(params Definitions.FileType[] types)
        {
            _types = new HashSet<Definitions.FileType>(types);
            _signatures = Definitions.Signatures.Where(row => _types.Contains(row.Item1)).OrderByDescending(x => x.Item2.Length).ToArray();
            _maxLength = _signatures.First().Item2.Length;
        }

        public async Task<Definitions.FileType?> MatchesAsync(AbsolutePath path)
        {
            await using var fs = await path.OpenShared();
            var buffer = new byte[_maxLength];
            fs.Position = 0;
            await fs.ReadAsync(buffer);

            foreach (var (fileType, signature) in _signatures)
            {
                if (AreEqual(buffer, signature))
                    return fileType;
            }

            return null;
        }

        private static bool AreEqual(IReadOnlyList<byte> a, IEnumerable<byte> b)
        {
            return !b.Where((t, i) => a[i] != t).Any();
        }
    }
}
