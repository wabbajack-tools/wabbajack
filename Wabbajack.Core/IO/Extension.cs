using System;
using System.IO;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Core.IO
{
public struct Extension
    {
        public static Extension None = new Extension("", false);

        #region ObjectEquality

        private bool Equals(Extension other)
        {
            return string.Equals(_extension, other._extension, StringComparison.InvariantCultureIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return obj is Extension other && Equals(other);
        }

        public override string ToString()
        {
            return _extension;
        }

        public override int GetHashCode()
        {
            return _extension?.GetHashCode(StringComparison.InvariantCultureIgnoreCase) ?? 0;
        }

        #endregion

        private readonly string? _nullable_extension;
        private string _extension => _nullable_extension ?? string.Empty;

        public Extension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                _nullable_extension = None._extension;
                return;
            }

            _nullable_extension = string.Intern(extension);
            Validate();
        }

        private Extension(string extension, bool validate)
        {
            _nullable_extension = string.Intern(extension);
            if (validate)
            {
                Validate();
            }
        }

        public Extension(Extension other)
        {
            _nullable_extension = other._extension;
        }

        private void Validate()
        {
            if (!_extension.StartsWith("."))
            {
                throw new InvalidDataException($"Extensions must start with '.' got {_extension}");
            }
        }

        public static explicit operator string(Extension path)
        {
            return path._extension;
        }

        public static explicit operator Extension(string path)
        {
            return new Extension(path);
        }

        public static bool operator ==(Extension a, Extension b)
        {
            return string.Equals(a._extension, b._extension, StringComparison.CurrentCultureIgnoreCase);
        }

        public static bool operator !=(Extension a, Extension b)
        {
            return !(a == b);
        }

        public static Extension FromPath(string path)
        {
            var ext = Path.GetExtension(path);
            return !string.IsNullOrWhiteSpace(ext) ? new Extension(ext) : None;
        }
    }
}
