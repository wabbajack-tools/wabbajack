using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using DriveInfo = Alphaleonis.Win32.Filesystem.DriveInfo;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Common
{

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

        public DriveInfo DriveInfo()
        {
            return new DriveInfo(Path.GetPathRoot(_path));
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
            return CircuitBreaker.WithAutoRetryAsync<FileStream, IOException>(async () => File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite));
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
        public async ValueTask MoveToAsync(AbsolutePath otherPath, bool overwrite = false)
        {
            if (Root != otherPath.Root)
            {
                if (otherPath.Exists && overwrite)
                    await otherPath.DeleteAsync();
                
                await CopyToAsync(otherPath);
                await DeleteAsync();
                return;
            }

            var path = _path;
            await CircuitBreaker.WithAutoRetryAsync<IOException>(async () => File.Move(path, otherPath._path, overwrite ? MoveOptions.ReplaceExisting : MoveOptions.None));
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
            return _path.StartsWith(folder._path + Path.DirectorySeparator, StringComparison.OrdinalIgnoreCase);
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

        public async Task WriteAllAsync(Stream data, bool disposeDataAfter = true)
        {
            await using var fs = await Create();
            await data.CopyToAsync(fs);
            await data.FlushAsync();
            if (disposeDataAfter) await data.DisposeAsync();
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
            return string.Compare(_path, other._path, StringComparison.OrdinalIgnoreCase);
        }

        public string ReadAllText()
        {
            return File.ReadAllText(_path);
        }

        public ValueTask<FileStream> OpenShared()
        {
            var path = _path;
            return CircuitBreaker.WithAutoRetryAsync<FileStream, IOException>(async () =>
                File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 1048576, useAsync: false));
        }

        public ValueTask<FileStream> WriteShared()
        {
            var path = _path;
            return CircuitBreaker.WithAutoRetryAsync<FileStream, IOException>(async () =>
                File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, bufferSize: 1048576, useAsync: false));
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

}
