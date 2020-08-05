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
using DriveInfo = Alphaleonis.Win32.Filesystem.DriveInfo;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Common
{

  

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



}
