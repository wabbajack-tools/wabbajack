using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
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

    public struct AbsolutePath : IPath, IComparable<AbsolutePath>, IEquatable<AbsolutePath>
    {
        #region ObjectEquality

        public bool Equals(AbsolutePath other)
        {
            return string.Equals(_path, other._path, StringComparison.InvariantCultureIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return obj is AbsolutePath other && Equals(other);
        }

        #endregion

        public override int GetHashCode()
        {
            return _path.GetHashCode(StringComparison.InvariantCultureIgnoreCase);
        }

        public override string ToString()
        {
            return _path;
        }

        private readonly string _nullable_path;
        private string _path => _nullable_path ?? string.Empty;

        public AbsolutePath(string path, bool skipValidation = false)
        {
            _nullable_path = path.Replace("/", "\\").TrimEnd('\\');
            if (!skipValidation)
            {
                ValidateAbsolutePath();
            }
        }

        public AbsolutePath(AbsolutePath path)
        {
            _nullable_path = path._path;
        }

        private void ValidateAbsolutePath()
        {
            if (Path.IsPathRooted(_path))
            {
                return;
            }

            throw new InvalidDataException($"Absolute path must be absolute, got {_path}");
        }

        public string Normalize()
        {
            return _path.Replace("/", "\\").TrimEnd('\\');
        }

        public Extension Extension => Extension.FromPath(_path);

        public ValueTask<FileStream> OpenRead()
        {
            return OpenShared();
        }

        public ValueTask<FileStream> Create()
        {
            var path = _path;
            return CircuitBreaker.WithAutoRetryAsync<FileStream, IOException>(async () => File.Open(path, FileMode.Create, FileAccess.ReadWrite));
        }

        public ValueTask<FileStream> OpenWrite()
        {
            var path = _path;
            return CircuitBreaker.WithAutoRetryAsync<FileStream, IOException>(async () => File.OpenWrite(path));
        }

        public async Task WriteAllTextAsync(string text)
        {
            await using var fs = File.Create(_path);
            await fs.WriteAsync(Encoding.UTF8.GetBytes(text));
        }

        public bool Exists => File.Exists(_path) || Directory.Exists(_path);
        public bool IsFile => File.Exists(_path);
        public bool IsDirectory => Directory.Exists(_path);

        public async Task DeleteDirectory(bool dontDeleteIfNotEmpty = false)
        {
            if (IsDirectory)
            {
                if (dontDeleteIfNotEmpty && (EnumerateFiles().Any() || EnumerateDirectories().Any())) return;
                await Utils.DeleteDirectory(this);
            }
        }

        public long Size => Exists ? new FileInfo(_path).Length : 0;

        public DateTime LastModified
        {
            get => File.GetLastWriteTime(_path);
            set => File.SetLastWriteTime(_path, value);
        }

        public DateTime LastModifiedUtc => File.GetLastWriteTimeUtc(_path);
        public AbsolutePath Parent => (AbsolutePath)Path.GetDirectoryName(_path);
        public RelativePath FileName => (RelativePath)Path.GetFileName(_path);
        public RelativePath FileNameWithoutExtension => (RelativePath)Path.GetFileNameWithoutExtension(_path);
        public bool IsEmptyDirectory => IsDirectory && !EnumerateFiles().Any();

        public bool IsReadOnly
        {
            get
            {
                return new FileInfo(_path).IsReadOnly;
            }
            set
            {
                new FileInfo(_path).IsReadOnly = value;
            }
        }

        public void SetReadOnly(bool val)
        {
            IsReadOnly = true;
        }

        /// <summary>
        /// Returns the full path the folder that contains Wabbajack.Common. This will almost always be
        /// where all the binaries for the project reside.
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public static AbsolutePath EntryPoint
        {
            get
            {
                var location = Assembly.GetExecutingAssembly().Location ?? null;
                if (location == null)
                    throw new ArgumentException("Could not find entry point.");
                return ((AbsolutePath)location).Parent;
            }
        }

        public AbsolutePath Root => (AbsolutePath)Path.GetPathRoot(_path);

        /// <summary>
        ///     Moves this file to the specified location, will use Copy if required
        /// </summary>
        /// <param name="otherPath"></param>
        /// <param name="overwrite">Replace the destination file if it exists</param>
        public async Task MoveToAsync(AbsolutePath otherPath, bool overwrite = false)
        {
            if (Root != otherPath.Root)
            {
                if (otherPath.Exists && overwrite)
                    await otherPath.DeleteAsync();
                
                await CopyToAsync(otherPath);
                await DeleteAsync();
                return;
            }
            File.Move(_path, otherPath._path, overwrite ? MoveOptions.ReplaceExisting : MoveOptions.None);
        }

        public RelativePath RelativeTo(AbsolutePath p)
        {
            var relPath = Path.GetRelativePath(p._path, _path);
            if (relPath == _path) 
                throw new ArgumentException($"{_path} is not a subpath of {p._path}");
            return new RelativePath(relPath);
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
        /// <param name="pattern">pattern to match against</param>
        /// <returns></returns>
        public IEnumerable<AbsolutePath> EnumerateFiles(bool recursive = true, string pattern = "*")
        {
            if (!IsDirectory) return new AbsolutePath[0];
            return Directory
                .EnumerateFiles(_path, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(path => new AbsolutePath(path, true));
        }

        #region Operators

        public static explicit operator string(AbsolutePath path)
        {
            return path._path;
        }

        public static explicit operator AbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return default;
            return !Path.IsPathRooted(path) ? ((RelativePath)path).RelativeToEntryPoint() : new AbsolutePath(path);
        }

        public static bool operator ==(AbsolutePath a, AbsolutePath b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(AbsolutePath a, AbsolutePath b)
        {
            return !a.Equals(b);
        }

        #endregion

        public void CreateDirectory()
        {
            Directory.CreateDirectory(_path);
        }

        public async Task DeleteAsync()
        {
            try
            {
                if (!IsFile) return;

                if (IsReadOnly) IsReadOnly = false;

                var path = _path;
                await CircuitBreaker.WithAutoRetryAsync<IOException>(async () => File.Delete(path));
            }
            catch (FileNotFoundException)
            {
                // ignore, it doesn't exist so why delete it?
            }
        }
        
        public void Delete()
        {
            if (!IsFile) return;

            if (IsReadOnly) IsReadOnly = false;

            var path = _path;
            CircuitBreaker.WithAutoRetry<IOException>(async () => File.Delete(path));
        }

        public bool InFolder(AbsolutePath folder)
        {
            return _path.StartsWith(folder._path + Path.DirectorySeparator);
        }

        public async Task<byte[]> ReadAllBytesAsync()
        {
            await using var f = await OpenShared();
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

        public AbsolutePath AppendToName(string toAppend)
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

        public async Task WriteAllBytesAsync(byte[] data)
        {
            await using var fs = await Create();
            await fs.WriteAsync(data);
        }

        public async Task WriteAllAsync(Stream data, bool disposeAfter = true)
        {
            await using var fs = await Create();
            await data.CopyToAsync(fs);
            if (disposeAfter) await data.DisposeAsync();
        }

        [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Auto)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        public bool HardLinkTo(AbsolutePath destination)
        {
            Utils.Log($"Hard Linking {_path} to {destination}");
            return CreateHardLink((string)destination, (string)this, IntPtr.Zero);
        }

        public async ValueTask HardLinkIfOversize(AbsolutePath destination)
        {
            if (!destination.Parent.Exists) 
                destination.Parent.CreateDirectory();
            
            if (Root == destination.Root && Consts.SupportedBSAs.Contains(Extension))
            {
                if (HardLinkTo(destination))
                    return;
            }

            await CopyToAsync(destination);
        }

        public async Task<IEnumerable<string>> ReadAllLinesAsync()
        {
            return (await ReadAllTextAsync()).Split(new[] {'\n', '\r'}, StringSplitOptions.RemoveEmptyEntries);
        }

        public static AbsolutePath GetCurrentDirectory()
        {
            return new AbsolutePath(Directory.GetCurrentDirectory());
        }

        public async Task CopyToAsync(AbsolutePath destFile)
        {
            await using var src = await OpenRead();
            await using var dest = await destFile.Create();
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

        public int CompareTo(AbsolutePath other)
        {
            return string.Compare(_path, other._path, StringComparison.Ordinal);
        }

        public string ReadAllText()
        {
            return File.ReadAllText(_path);
        }

        public ValueTask<FileStream> OpenShared()
        {
            var path = _path;
            return CircuitBreaker.WithAutoRetryAsync<FileStream, IOException>(async () =>
                File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        }

        public ValueTask<FileStream> WriteShared()
        {
            var path = _path;
            return CircuitBreaker.WithAutoRetryAsync<FileStream, IOException>(async () =>
                File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite));
        }

        public async Task CopyDirectoryToAsync(AbsolutePath destination)
        {
            destination.CreateDirectory();
            foreach (var file in EnumerateFiles())
            {
                var dest = file.RelativeTo(this).RelativeTo(destination);
                await file.CopyToAsync(dest);
            }
        }
    }

    [JsonConverter(typeof(Utils.RelativePathConverter))]
    public struct RelativePath : IPath, IEquatable<RelativePath>, IComparable<RelativePath>
    {
        private readonly string? _nullable_path;
        private string _path => _nullable_path ?? string.Empty;

        public RelativePath(string path)
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
            Validate();
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
        
        public RelativePath Combine(params string[] paths)
        {
            return (RelativePath)Path.Combine(paths.Cons(_path).ToArray());
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
            if (Path.IsPathRooted(str)) return (AbsolutePath)str;
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
            // Super fast comparison because extensions are interned
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
