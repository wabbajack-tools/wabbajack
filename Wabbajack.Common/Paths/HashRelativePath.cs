using System;
using System.Linq;

namespace Wabbajack.Common
{
    public struct HashRelativePath : IEquatable<HashRelativePath>
    {
        private static RelativePath[] EMPTY_PATH;
        public Hash BaseHash { get; }
        public RelativePath[] Paths { get; }

        static HashRelativePath()
        {
            EMPTY_PATH = new RelativePath[0];
        }

        public HashRelativePath(Hash baseHash, params RelativePath[] paths)
        {
            BaseHash = baseHash;
            Paths = paths;
        }

        public override string ToString()
        {
            var paths = Paths == null ? EmptyPath : Paths;
            return string.Join("|", paths.Select(t => t.ToString()).Cons(BaseHash.ToString()));
        }
        
        private static RelativePath[] EmptyPath = Array.Empty<RelativePath>();

        public static bool operator ==(HashRelativePath a, HashRelativePath b)
        {
            if (a.Paths == null || b.Paths == null) return false;
            
            if (a.BaseHash != b.BaseHash || a.Paths.Length != b.Paths.Length)
            {
                return false;
            }

            for (var idx = 0; idx < a.Paths.Length; idx += 1)
            {
                if (a.Paths[idx] != b.Paths[idx])
                {
                    return false;
                }
            }

            return true;
        }

        public static bool operator !=(HashRelativePath a, HashRelativePath b)
        {
            return !(a == b);
        }

        public bool Equals(HashRelativePath other)
        {
            return this == other;
        }

        public override bool Equals(object? obj)
        {
            return obj is HashRelativePath other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(BaseHash, Paths);
        }

        public static HashRelativePath FromStrings(string hash, params string[] paths)        
        {
            return new HashRelativePath(Hash.FromBase64(hash), paths.Select(p => (RelativePath)p).ToArray());
        }
    }

}
