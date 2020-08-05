using System;
using System.Linq;


namespace Wabbajack.Common
{
    public struct FullPath : IEquatable<FullPath>, IPath
    {
        public AbsolutePath Base { get; }
        
        public RelativePath[] Paths { get; }

        private readonly int _hash;

        public FullPath(AbsolutePath basePath, params RelativePath[] paths)
        {
            Base = basePath;
            Paths = paths == null ? Array.Empty<RelativePath>() : paths;
            _hash = Base.GetHashCode();
            foreach (var itm in Paths)
            {
                _hash ^= itm.GetHashCode();
            }
        }

        public override string ToString()
        {
            var paths = Paths == null ? EmptyPath : Paths;
            return string.Join("|", paths.Select(t => (string)t).Cons((string)Base));
        }

        public override int GetHashCode()
        {
            return _hash;
        }

        private static RelativePath[] EmptyPath = Array.Empty<RelativePath>();

        public static bool operator ==(FullPath a, FullPath b)
        {
            if (a.Paths == null || b.Paths == null) return false;
            
            if (a.Base != b.Base || a.Paths.Length != b.Paths.Length)
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

        public static bool operator !=(FullPath a, FullPath b)
        {
            return !(a == b);
        }

        public bool Equals(FullPath other)
        {
            return this == other;
        }

        public override bool Equals(object? obj)
        {
            return obj is FullPath other && Equals(other);
        }

        public RelativePath FileName => Paths.Length == 0 ? Base.FileName : Paths.Last().FileName;
    }
}
