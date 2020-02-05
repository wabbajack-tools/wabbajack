using System;
using System.Collections.Generic;
using System.IO;
using APath = Alphaleonis.Win32.Filesystem.Path;
using AFile = Alphaleonis.Win32.Filesystem.File;
using Directory = System.IO.Directory;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;

namespace Wabbajack.Common.Paths
{
    public struct File
    {
        internal readonly string _path;

        public Directory Parent
        {
            get
            {
                var parent = APath.GetDirectoryName(_path);
                if (parent == "")
                {
                    throw new NoParentDirectoryException(_path);
                }
                return new Directory(parent);
            }
        }

        public bool Exists => AFile.Exists(_path);
        public bool IsTopLevelFile => APath.GetDirectoryName(_path) == null;
        public string Extension => APath.GetExtension(_path);
        public long Length => new FileInfo(_path).Length;
        
        private File(string path)
        {
            _path = path;
        }
        public FileStream Create()
        {
            return AFile.Create(_path);
        }
    }

    public struct Directory
    {
        private string _path;

        internal Directory(string path)
        {
            _path = path;
        }
    }

    public class NoParentDirectoryException : Exception
    {
        private string _path;

        public NoParentDirectoryException(string path) : base($"Cannot get the parent directory of a top level directory: {path}")
        {
            _path = path;
        }
    }
}
