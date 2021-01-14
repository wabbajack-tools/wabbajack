using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Common
{
  [JsonConverter(typeof(Utils.RelativePathConverter))]
    public struct RelativePath : IPath, IEquatable<RelativePath>, IComparable<RelativePath>
    {
        private readonly string? _nullable_path;
        private string _path => _nullable_path ?? string.Empty;

        public RelativePath(string path, bool skipValidation = false)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _nullable_path = null;
                return;
            }
            var trimmed = path.Replace("/", "\\").Trim('\\');
            if (string.IsNullOrEmpty(trimmed))
            {
                _nullable_path = null;
                return;
            }

            _nullable_path = trimmed;
            if (!skipValidation)
            {
                Validate();
            }
        }

        public override string ToString()
        {
            return _path;
        }

        public Extension Extension => Extension.FromPath(_path);

        public override int GetHashCode()
        {
            return _path.GetHashCode(StringComparison.InvariantCultureIgnoreCase);
        }

        public static RelativePath RandomFileName()
        {
            return (RelativePath)Guid.NewGuid().ToString();
        }
        
        
        public RelativePath Munge()
        {
            return (RelativePath)_path.Replace('\\', '_').Replace('/', '_').Replace(':', '_');
        }

        private void Validate()
        {
            if (Path.IsPathRooted(_path))
            {
                throw new InvalidDataException($"Cannot create relative path from absolute path string, got {_path}");
            }
        }

        public AbsolutePath RelativeTo(AbsolutePath abs)
        {
            return new AbsolutePath(Path.Combine((string)abs, _path));
        }

        public AbsolutePath RelativeToEntryPoint()
        {
            return RelativeTo(AbsolutePath.EntryPoint);
        }

        public AbsolutePath RelativeToWorkingDirectory()
        {
            return RelativeTo((AbsolutePath)Directory.GetCurrentDirectory());
        }

        public static explicit operator string(RelativePath path)
        {
            return path._path;
        }

        public static explicit operator RelativePath(string path)
        {
            return new RelativePath(path);
        }

        public AbsolutePath RelativeToSystemDirectory()
        {
            return RelativeTo((AbsolutePath)Environment.SystemDirectory);
        }

        public RelativePath Parent => (RelativePath)Path.GetDirectoryName(_path);

        public RelativePath FileName => new RelativePath(Path.GetFileName(_path));

        public RelativePath FileNameWithoutExtension => (RelativePath)Path.GetFileNameWithoutExtension(_path);
        
        public RelativePath TopParent
        {
            get
            {
                var curr = this;
                
                while (curr.Parent != default) 
                    curr = curr.Parent;

                return curr;
            }
        }

        public bool Equals(RelativePath other)
        {
            return string.Equals(_path, other._path, StringComparison.InvariantCultureIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return obj is RelativePath other && Equals(other);
        }

        public static bool operator ==(RelativePath a, RelativePath b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(RelativePath a, RelativePath b)
        {
            return !a.Equals(b);
        }

        public bool StartsWith(string s)
        {
            return _path.StartsWith(s, StringComparison.OrdinalIgnoreCase);
        }
        
        public bool StartsWith(RelativePath s)
        {
            return _path.StartsWith(s._path, StringComparison.OrdinalIgnoreCase);
        }
        public RelativePath Combine(params RelativePath[] paths )
        {
            return (RelativePath)Path.Combine(paths.Select(p => (string)p).Cons(_path).ToArray());
        }
        
        public RelativePath Combine(params string[] paths)
        {
            return (RelativePath)Path.Combine(paths.Cons(_path).ToArray());
        }

        public int CompareTo(RelativePath other)
        {
            return string.Compare(_path, other._path, StringComparison.OrdinalIgnoreCase);
        }
    }
}
