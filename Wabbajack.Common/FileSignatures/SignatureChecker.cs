using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Wabbajack.Common.FileSignatures
{
    public class SignatureChecker
    {
        private HashSet<Definitions.FileType> _types;
        private (Definitions.FileType, byte[])[] _signatures;

        public SignatureChecker(params Definitions.FileType[] types)
        {
            _types = new HashSet<Definitions.FileType>(types);
            _signatures = Definitions.Signatures.Where(row => _types.Contains(row.Item1)).ToArray();
        }

        public async Task<Definitions.FileType?> MatchesAsync(AbsolutePath path)
        {
            await using var fs = await path.OpenShared();
            foreach (var signature in _signatures)
            {
                var buffer = new byte[signature.Item2.Length];
                fs.Position = 0;
                await fs.ReadAsync(buffer);
                if (AreEqual(buffer, signature.Item2))
                    return signature.Item1;
            }
            return null;
        }

        private static bool AreEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return true;
        }

    }
}
