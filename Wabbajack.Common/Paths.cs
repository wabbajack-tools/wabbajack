using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Directory = System.IO.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Common
{
    public class AbsolutePath
    {

        #region ObjectEquality
        protected bool Equals(AbsolutePath other)
        {
            return _path == other._path;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((AbsolutePath) obj);
        }
        #endregion

        public override int GetHashCode()
        {
            return (_path != null ? _path.GetHashCode() : 0);
        }

        private readonly string _path;
        private Extension _extension;

        public AbsolutePath(string path)
        {
            _path = path.ToLowerInvariant().Replace("/", "\\").TrimEnd('\\');
            ValidateAbsolutePath();
        }

        public AbsolutePath(AbsolutePath path)
        {
            _path = path._path;
        }

        private void ValidateAbsolutePath()
        {
            if (Path.IsPathRooted(_path)) return;
            throw new InvalidDataException($"Absolute path must be absolute");
        }
        
        public Extension Extension 
        {
            get
            {
                if (_extension != null) return _extension;
                var extension = Path.GetExtension(_path);
                if (string.IsNullOrEmpty(extension))
                    return null;
                _extension = (Extension)extension;
                return _extension;
            }
        }

        public FileStream OpenRead()
        {
            return File.OpenRead(_path);
        }

        public FileStream Create()
        {
            return File.Create(_path);
        }

        public FileStream OpenWrite()
        {
            return File.OpenWrite(_path);
        }

        public async Task WriteAllTextAsync(string text)
        {
            await using var fs = File.Create(_path);
            await fs.WriteAsync(Encoding.UTF8.GetBytes(text));
        }

        public bool Exists => File.Exists(_path) || Directory.Exists(_path);
        public bool IsFile => File.Exists(_path);
        public bool IsDirectory => Directory.Exists(_path);
        
        public long Size => (new FileInfo(_path)).Length;

        public DateTime LastModified => File.GetLastWriteTime(_path);
        public DateTime LastModifiedUtc => File.GetLastWriteTimeUtc(_path);
        public AbsolutePath Parent => (AbsolutePath)Path.GetDirectoryName(_path);
        public RelativePath FileName => (RelativePath)Path.GetFileName(_path); 

        public void Copy(AbsolutePath otherPath)
        {
            File.Copy(_path, otherPath._path);
        }

        public void Move(AbsolutePath otherPath, bool overwrite = false)
        {
            File.Move(_path, otherPath._path, overwrite ? MoveOptions.ReplaceExisting : MoveOptions.None);
        }

        public RelativePath RelativeTo(AbsolutePath p)
        {
            if (_path.Substring(0, p._path.Length + 1) != p._path + "\\") 
                throw new InvalidDataException("Not a parent path");
            return new RelativePath(_path.Substring(p._path.Length + 1));
        }
        
        public async Task<string> ReadAllTextAsync()
        {
            await using var fs = File.OpenRead(_path);
            return Encoding.UTF8.GetString(await fs.ReadAllAsync());
        }


        #region Operators
        public static explicit operator string(AbsolutePath path)
        {
            return path._path;
        }
        
        public static explicit operator AbsolutePath(string path)
        {
            return !Path.IsPathRooted(path) ? ((RelativePath)path).RelativeToEntryPoint() : new AbsolutePath(path);
        }
        
        public static bool operator ==(AbsolutePath a, AbsolutePath b)
        {
            return a._path == b._path;
        }
        
        public static bool operator !=(AbsolutePath a, AbsolutePath b)
        {
            return a._path != b._path;
        }
        #endregion

        public void CreateDirectory()
        {
            Directory.CreateDirectory(_path);
        }
    }

    public class RelativePath
    {
        private readonly string _path;
        private Extension _extension;

        public RelativePath(string path)
        {
            _path = path.ToLowerInvariant().Replace("/", "\\").Trim('\\');
            Validate();
        }

        public static RelativePath RandomFileName()
        {
            return (RelativePath)Guid.NewGuid().ToString();
        }

        private void Validate()
        {
            if (Path.IsPathRooted(_path))
                throw new InvalidDataException("Cannot create relative path from absolute path string");
        }

        public AbsolutePath RelativeTo(AbsolutePath abs)
        {
            return new AbsolutePath(Path.Combine((string)abs, _path));
        }

        public AbsolutePath RelativeToEntryPoint()
        {
            return RelativeTo(((AbsolutePath)Assembly.GetEntryAssembly().Location).Parent);
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
    }

    public class Extension
    {
        #region ObjectEquality
        protected bool Equals(Extension other)
        {
            return _extension == other._extension;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((Extension) obj);
        }

        public override int GetHashCode()
        {
            return (_extension != null ? _extension.GetHashCode() : 0);
        }
        #endregion

        private readonly string _extension;

        public Extension(string extension)
        {
            _extension = string.Intern(extension);
            Validate();
        }
        private void Validate()
        {
            if (!_extension.StartsWith("."))
                throw new InvalidDataException($"Extensions must start with '.'");
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
            // Super fast comparison because extensions are interned
            return ReferenceEquals(a._extension, b._extension);
        }
        
        public static bool operator !=(Extension a, Extension b)
        {
            return !ReferenceEquals(a._extension, b._extension);
        }
    }
}
