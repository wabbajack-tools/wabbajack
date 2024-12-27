using System;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.DTOs;

public class Archive : IComparable
{
    public Hash Hash { get; set; }
    public string Meta { get; set; } = "";
    public string Name { get; set; }
    public long Size { get; set; }
    public IDownloadState State { get; set; }

    public int CompareTo(object obj)
    {
        if (obj == null) return 1;
        Archive otherArchive = obj as Archive;
        if (otherArchive != null)
            return this.Size.CompareTo(otherArchive.Size);
        else
            throw new ArgumentException("Object is not an Archive");
    }
}