using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public interface IPath
    {
        /// <summary>
        ///     Get the final file name, for c:\bar\baz this is `baz` for c:\bar.zip this is `bar.zip`
        ///     for `bar.zip` this is `bar.zip`
        /// </summary>
        public RelativePath FileName { get; }
    }

    public struct AbsolutePath : IPath, IComparable<AbsolutePath>
    {
        #region ObjectEquality

        private bool Equals(AbsolutePath other)
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

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((AbsolutePath)obj);
        }

        #endregion

        public override int GetHashCode()
        {
            return _path != null ? _path.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return _path;
        }

        private readonly string _path;

        public AbsolutePath(string path)
        {
            _path = path.ToLowerInvariant().Replace("/", "\\").TrimEnd('\\');
            Extension = new Extension(Path.GetExtension(_path));
            ValidateAbsolutePath();
        }

        public AbsolutePath(string path, bool skipValidation)
        {
            _path = path.ToLowerInvariant().Replace("/", "\\").TrimEnd('\\');
            Extension = Extension.FromPath(path);
            if (!skipValidation)
            {
                ValidateAbsolutePath();
            }
        }

        public AbsolutePath(AbsolutePath path)
        {
            _path = path._path;
            Extension = path.Extension;
        }

        private void ValidateAbsolutePath()
        {
            if (Path.IsPathRooted(_path))
            {
                return;
            }

            throw new InvalidDataException("Absolute path must be absolute");
        }

        public Extension Extension { get; }

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
        
        public void WriteAllText(string text)
        {
            using var fs = File.Create(_path);
            fs.Write(Encoding.UTF8.GetBytes(text));
        }

        public bool Exists => File.Exists(_path) || Directory.Exists(_path);
        public bool IsFile => File.Exists(_path);
        public bool IsDirectory => Directory.Exists(_path);

        public async Task DeleteDirectory()
        {
            if (IsDirectory)
            {
                await Utils.DeleteDirectory(this);
            }
        }

        public long Size => new FileInfo(_path).Length;

        public DateTime LastModified => File.GetLastWriteTime(_path);
        public DateTime LastModifiedUtc => File.GetLastWriteTimeUtc(_path);
        public AbsolutePath Parent => (AbsolutePath)Path.GetDirectoryName(_path);
        public RelativePath FileName => (RelativePath)Path.GetFileName(_path);
        public RelativePath FileNameWithoutExtension => (RelativePath)Path.GetFileNameWithoutExtension(_path);
        public bool IsEmptyDirectory => IsDirectory && !EnumerateFiles().Any();

        /// <summary>
        ///     Moves this file to the specified location
        /// </summary>
        /// <param name="otherPath"></param>
        /// <param name="overwrite">Replace the destination file if it exists</param>
        public void MoveTo(AbsolutePath otherPath, bool overwrite = false)
        {
            File.Move(_path, otherPath._path, overwrite ? MoveOptions.ReplaceExisting : MoveOptions.None);
        }

        public RelativePath RelativeTo(AbsolutePath p)
        {
            if (_path.Substring(0, p._path.Length + 1) != p._path + "\\")
            {
                throw new InvalidDataException("Not a parent path");
            }

            return new RelativePath(_path.Substring(p._path.Length + 1));
        }

        public async Task<string> ReadAllTextAsync()
        {
            await using var fs = File.OpenRead(_path);
            return Encoding.UTF8.GetString(await fs.ReadAllAsync());
        }

        /// <summary>
        ///     Assuming the path is a folder, enumerate all the files in the folder
        /// </summary>
        /// <param name="recursive">if true, also returns files in sub-folders</param>
        /// <returns></returns>
        public IEnumerable<AbsolutePath> EnumerateFiles(bool recursive = true)
        {
            return Directory
                .EnumerateFiles(_path, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(path => new AbsolutePath(path, true));
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

        public void Delete()
        {
            if (IsFile)
            {
                File.Delete(_path);
            }
        }

        public bool InFolder(AbsolutePath folder)
        {
            return _path.StartsWith(folder._path + Path.DirectorySeparator);
        }

        public async Task<byte[]> ReadAllBytesAsync()
        {
            await using var f = OpenRead();
            return await f.ReadAllAsync();
        }

        public AbsolutePath WithExtension(Extension hashFileExtension)
        {
            return new AbsolutePath(_path + (string)hashFileExtension, true);
        }

        public AbsolutePath ReplaceExtension(Extension extension)
        {
            return new AbsolutePath(
                Path.Combine(Path.GetDirectoryName(_path), Path.GetFileNameWithoutExtension(_path) + (string)extension),
                true);
        }

        public AbsolutePath AppendToName(AbsolutePath bsa, string toAppend)
        {
            return new AbsolutePath(
                Path.Combine(Path.GetDirectoryName(_path),
                    Path.GetFileNameWithoutExtension(_path) + toAppend + (string)Extension));
        }

        public AbsolutePath Combine(params RelativePath[] paths)
        {
            return new AbsolutePath(Path.Combine(paths.Select(s => (string)s).Cons(_path).ToArray()));
        }

        public AbsolutePath Combine(params string[] paths)
        {
            return new AbsolutePath(Path.Combine(paths.Cons(_path).ToArray()));
        }

        public IEnumerable<string> ReadAllLines()
        {
            return File.ReadAllLines(_path);
        }

        public void WriteAllBytes(byte[] data)
        {
            using var fs = Create();
            fs.Write(data);
        }

        public async Task WriteAllBytesAsync(byte[] data)
        {
            await using var fs = Create();
            await fs.WriteAsync(data);
        }

        public void AppendAllText(string text)
        {
            File.AppendAllText(_path, text);
        }

        public void CopyTo(AbsolutePath dest, bool useMove = false)
        {
            if (useMove)
            {
                File.Move(_path, dest._path, MoveOptions.ReplaceExisting);
            }
            else
            {
                File.Copy(_path, dest._path);
            }
        }

        public async Task<IEnumerable<string>> ReadAllLinesAsync()
        {
            return (await ReadAllTextAsync()).Split(new[] {'\n', '\r'}, StringSplitOptions.RemoveEmptyEntries);
        }

        public byte[] ReadAllBytes()
        {
            return File.ReadAllBytes(_path);
        }

        public static AbsolutePath GetCurrentDirectory()
        {
            return new AbsolutePath(Directory.GetCurrentDirectory());
        }

        public async Task CopyToAsync(AbsolutePath destFile)
        {
            await using var src = OpenRead();
            await using var dest = destFile.Create();
            await src.CopyToAsync(dest);
        }

        public IEnumerable<AbsolutePath> EnumerateDirectories(bool recursive = true)
        {
            return Directory.EnumerateDirectories(_path, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(p => (AbsolutePath)p);
        }

        public async Task WriteAllLinesAsync(params string[] strings)
        {
            await WriteAllTextAsync(string.Join("\r\n",strings));
        }

        public void	 WriteAllLines(params string[] strings)
        {
            WriteAllText(string.Join("\n",strings));
        }

        public int CompareTo(AbsolutePath other)
        {
            return string.Compare(_path, other._path, StringComparison.Ordinal);
        }
    }

    public struct RelativePath : IPath, IEquatable<RelativePath>, IComparable<RelativePath>
    {
        private readonly string _path;

        public RelativePath(string path)
        {
            _path = path.ToLowerInvariant().Replace("/", "\\").Trim('\\');
            Extension = new Extension(Path.GetExtension(path));
            Validate();
        }

        public override string ToString()
        {
            return _path;
        }

        public Extension Extension { get; }

        public override int GetHashCode()
        {
            return _path != null ? _path.GetHashCode() : 0;
        }

        public static RelativePath RandomFileName()
        {
            return (RelativePath)Guid.NewGuid().ToString();
        }

        private void Validate()
        {
            if (Path.IsPathRooted(_path))
            {
                throw new InvalidDataException("Cannot create relative path from absolute path string");
            }
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

        public RelativePath Parent => (RelativePath)Path.GetDirectoryName(_path);

        public RelativePath FileName => new RelativePath(Path.GetFileName(_path));

        public RelativePath FileNameWithoutExtension => (RelativePath)Path.GetFileNameWithoutExtension(_path);

        public bool Equals(RelativePath other)
        {
            return _path == other._path;
        }

        public override bool Equals(object obj)
        {
            return obj is RelativePath other && Equals(other);
        }

        public static bool operator ==(RelativePath a, RelativePath b)
        {
            return a._path == b._path;
        }

        public static bool operator !=(RelativePath a, RelativePath b)
        {
            return !(a == b);
        }

        public bool StartsWith(string s)
        {
            return _path.StartsWith(s);
        }
        
        public bool StartsWith(RelativePath s)
        {
            return _path.StartsWith(s._path);
        }

        public RelativePath Combine(params RelativePath[] paths )
        {
            return (RelativePath)Path.Combine(paths.Select(p => (string)p).Cons(_path).ToArray());
        }

        public int CompareTo(RelativePath other)
        {
            return string.Compare(_path, other._path, StringComparison.Ordinal);
        }
    }

    public static partial class Utils
    {
        public static RelativePath ToPath(this string str)
        {
            return (RelativePath)str;
        }

        public static AbsolutePath RelativeTo(this string str, AbsolutePath path)
        {
            return ((RelativePath)str).RelativeTo(path);
        }

        public static void Write(this BinaryWriter wtr, IPath path)
        {
            wtr.Write(path is AbsolutePath);
            if (path is AbsolutePath)
            {
                wtr.Write((AbsolutePath)path);
            }
            else
            {
                wtr.Write((RelativePath)path);
            }
        }

        public static void Write(this BinaryWriter wtr, AbsolutePath path)
        {
            wtr.Write((string)path);
        }

        public static void Write(this BinaryWriter wtr, RelativePath path)
        {
            wtr.Write((string)path);
        }

        public static IPath ReadIPath(this BinaryReader rdr)
        {
            if (rdr.ReadBoolean())
            {
                return rdr.ReadAbsolutePath();
            }

            return rdr.ReadRelativePath();
        }

        public static AbsolutePath ReadAbsolutePath(this BinaryReader rdr)
        {
            return new AbsolutePath(rdr.ReadString());
        }

        public static RelativePath ReadRelativePath(this BinaryReader rdr)
        {
            return new RelativePath(rdr.ReadString());
        }

        public static T[] Add<T>(this T[] arr, T itm)
        {
            var newArr = new T[arr.Length + 1];
            Array.Copy(arr, 0, newArr, 0, arr.Length);
            newArr[arr.Length] = itm;
            return newArr;
        }
    }

    public struct Extension
    {
        public static Extension None = new Extension("", false);

        #region ObjectEquality

        private bool Equals(Extension other)
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

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((Extension)obj);
        }

        public override string ToString()
        {
            return _extension;
        }

        public override int GetHashCode()
        {
            return _extension != null ? _extension.GetHashCode() : 0;
        }

        #endregion

        private readonly string _extension;

        public Extension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                _extension = None._extension;
                return;
            }

            _extension = string.Intern(extension);
            Validate();
        }

        private Extension(string extension, bool validate)
        {
            _extension = string.Intern(extension);
            if (validate)
            {
                Validate();
            }
        }

        public Extension(Extension other)
        {
            _extension = other._extension;
        }

        private void Validate()
        {
            if (!_extension.StartsWith("."))
            {
                throw new InvalidDataException("Extensions must start with '.'");
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
            // Super fast comparison because extensions are interned
            if ((object)a == null && (object)b == null)
            {
                return true;
            }

            if ((object)a == null || (object)b == null)
            {
                return false;
            }

            return ReferenceEquals(a._extension, b._extension);
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
            return string.Join("|", Paths.Select(t => t.ToString()).Cons(BaseHash.ToString()));
        }
        public static bool operator ==(HashRelativePath a, HashRelativePath b)
        {
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

        public override bool Equals(object obj)
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

    public struct FullPath : IEquatable<FullPath>, IPath
    {
        public AbsolutePath Base { get; }
        public RelativePath[] Paths { get; }

        private readonly int _hash;

        public FullPath(AbsolutePath basePath, params RelativePath[] paths)
        {
            Base = basePath;
            Paths = paths;
            _hash = Base.GetHashCode();
            foreach (var itm in Paths)
            {
                _hash ^= itm.GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Join("|", Paths.Select(t => (string)t).Cons((string)Base));
        }

        public override int GetHashCode()
        {
            return _hash;
        }

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

        public override bool Equals(object obj)
        {
            return obj is FullPath other && Equals(other);
        }

        public RelativePath FileName => Paths.Length == 0 ? Base.FileName : Paths.Last().FileName;
    }
}
