using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Paths.IO;

public static class AbsolutePathExtensions
{
    public const int BufferSize = 1024 * 128;

    public static Stream Open(this AbsolutePath file, FileMode mode, FileAccess access = FileAccess.Read,
        FileShare share = FileShare.ReadWrite)
    {
        return File.Open(file.ToNativePath(), mode, access, share);
    }

    public static void Delete(this AbsolutePath file)
    {
        var path = file.ToNativePath();
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch (UnauthorizedAccessException ex)
            {
                var fi = new FileInfo(path);
                if (fi.IsReadOnly)
                {
                    fi.IsReadOnly = false;
                    File.Delete(path);
                }
                else
                {
                    throw;
                }
            }
        }
        if (Directory.Exists(path))
            file.DeleteDirectory();
    }

    public static long Size(this AbsolutePath file)
    {
        return new FileInfo(file.ToNativePath()).Length;
    }

    public static DateTime LastModifiedUtc(this AbsolutePath file)
    {
        return new FileInfo(file.ToNativePath()).LastWriteTimeUtc;
    }

    public static DateTime LastModified(this AbsolutePath file)
    {
        return new FileInfo(file.ToNativePath()).LastWriteTime;
    }
    
    public static void Touch(this AbsolutePath file)
    {
        new FileInfo(file.ToNativePath()).LastWriteTime = DateTime.Now;
    }

    public static byte[] ReadAllBytes(this AbsolutePath file)
    {
        using var s = File.Open(file.ToNativePath(), FileMode.Open, FileAccess.Read, FileShare.Read);
        var remain = s.Length;
        var length = remain;
        var bytes = new byte[length];

        while (remain > 0) remain -= s.Read(bytes, (int) Math.Min(length - remain, 1024 * 1024), bytes.Length);

        return bytes;
    }

    public static string ReadAllText(this AbsolutePath file)
    {
        return Encoding.UTF8.GetString(file.ReadAllBytes());
    }

    public static async IAsyncEnumerable<string> ReadAllLinesAsync(this AbsolutePath file)
    {
        await using var fs = file.Open(FileMode.Open);
        var sr = new StreamReader(fs);
        while (true)
        {
            var line = await sr.ReadLineAsync();
            if (line == null) break;
            yield return line;
        }
    }

    public static IEnumerable<string> ReadAllLines(this AbsolutePath file)
    {
        using var fs = file.Open(FileMode.Open);
        var sr = new StreamReader(fs);
        while (true)
        {
            var line = sr.ReadLine();
            if (line == null) break;
            yield return line;
        }
    }

    public static async Task<string> ReadAllTextAsync(this AbsolutePath file)
    {
        return Encoding.UTF8.GetString(await file.ReadAllBytesAsync());
    }

    public static async ValueTask<byte[]> ReadAllBytesAsync(this AbsolutePath file,
        CancellationToken token = default)
    {
        await using var s = File.Open(file.ToNativePath(), FileMode.Open, FileAccess.Read, FileShare.Read);
        var remain = s.Length;
        var length = remain;
        var bytes = new byte[length];

        while (remain > 0)
            remain -= await s.ReadAsync(bytes.AsMemory((int) Math.Min(length - remain, 1024 * 1024), bytes.Length),
                token);

        return bytes;
    }

    public static void WriteAllBytes(this AbsolutePath file, ReadOnlySpan<byte> data)
    {
        using var s = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        s.Write(data);
    }

    public static async Task WriteAllAsync(this AbsolutePath file, Stream srcStream, CancellationToken token,
        bool closeWhenDone = true)
    {
        var buff = new byte[BufferSize];
        await using var dest = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        while (true)
        {
            var read = await srcStream.ReadAsync(buff.AsMemory(0, BufferSize), token);
            if (read == 0)
                break;
            await dest.WriteAsync(buff.AsMemory(0, read), token);
        }

        if (closeWhenDone)
            await srcStream.DisposeAsync();
    }

    public static async Task WriteAllLinesAsync(this AbsolutePath file, IEnumerable<string> src,
        CancellationToken token, bool closeWhenDone = true)
    {
        await using var dest = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await using var sw = new StreamWriter(dest, Encoding.UTF8);

        foreach (var line in src) await sw.WriteLineAsync(line);

        await sw.DisposeAsync();
    }

    public static async ValueTask WriteAllBytesAsync(this AbsolutePath file, Memory<byte> data,
        CancellationToken token = default)
    {
        await using var s = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await s.WriteAsync(data, token);
    }

    public static async ValueTask MoveToAsync(this AbsolutePath src, AbsolutePath dest, bool overwrite,
        CancellationToken token)
    {
        // TODO: Make this async
        var srcStr = src.ToString();
        var destStr = dest.ToString();
        var fi = new FileInfo(srcStr);
        if (fi.IsReadOnly)
            fi.IsReadOnly = false;

        var fid = new FileInfo(destStr);
        if (dest.FileExists() && fid.IsReadOnly)
        {
            fid.IsReadOnly = false;
        }
        
        
        
        try
        {
            File.Move(srcStr, destStr, overwrite);
        }
        catch (Exception)
        {
            
        }
    }

    public static async ValueTask CopyToAsync(this AbsolutePath src, AbsolutePath dest, bool overwrite,
        CancellationToken token)
    {
        // TODO: Make this async
        File.Copy(src.ToString(), dest.ToString(), overwrite);
    }

    public static void WriteAllText(this AbsolutePath file, string str)
    {
        file.WriteAllBytes(Encoding.UTF8.GetBytes(str));
    }

    public static async Task WriteAllTextAsync(this AbsolutePath file, string str,
        CancellationToken token = default)
    {
        await file.WriteAllBytesAsync(Encoding.UTF8.GetBytes(str), token);
    }

    private static string ToNativePath(this AbsolutePath file)
    {
        return file.ToString();
    }

    #region Directories

    public static void CreateDirectory(this AbsolutePath path)
    {
        if (path.Depth > 1 && !path.Parent.DirectoryExists())
            path.Parent.CreateDirectory();
        Directory.CreateDirectory(ToNativePath(path));
    }

    public static void DeleteDirectory(this AbsolutePath path, bool dontDeleteIfNotEmpty = false)
    {
        if (!path.DirectoryExists()) return;
        if (dontDeleteIfNotEmpty && (path.EnumerateFiles().Any() || path.EnumerateDirectories().Any())) return;
      
        foreach (var directory in Directory.GetDirectories(path.ToString()))
        {
            DeleteDirectory(directory.ToAbsolutePath(), dontDeleteIfNotEmpty);
        }
        try
        {
            var di = new DirectoryInfo(path.ToString());
            if (di.Attributes.HasFlag(FileAttributes.ReadOnly))
                di.Attributes &= ~FileAttributes.ReadOnly;
            Directory.Delete(path.ToString(), true);
        }
        catch (UnauthorizedAccessException)
        {
            Directory.Delete(path.ToString(), true);
        }
    }

    public static bool DirectoryExists(this AbsolutePath path)
    {
        return path != default && Directory.Exists(path.ToNativePath());
    }

    public static bool FileExists(this AbsolutePath path)
    {
        if (path == default) return false;
        return File.Exists(path.ToNativePath());
    }

    public static IEnumerable<AbsolutePath> EnumerateFiles(this AbsolutePath path, string pattern = "*",
        bool recursive = true)
    {
        return Directory.EnumerateFiles(path.ToString(), pattern,
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Select(file => file.ToAbsolutePath());
    }


    public static IEnumerable<AbsolutePath> EnumerateFiles(this AbsolutePath path, Extension pattern,
        bool recursive = true)
    {
        return Directory.EnumerateFiles(path.ToString(), "*" + pattern,
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Select(file => file.ToAbsolutePath());
    }


    public static IEnumerable<AbsolutePath> EnumerateDirectories(this AbsolutePath path, bool recursive = true)
    {
        return Directory.EnumerateDirectories(path.ToString(), "*",
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Select(p => (AbsolutePath) p);
    }

    #endregion
}