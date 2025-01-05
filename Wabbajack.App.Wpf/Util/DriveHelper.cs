using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;

namespace Wabbajack;
public static class DriveHelper
{
    private static Dictionary<string, PhysicalDisk> _cachedDisks = new Dictionary<string, PhysicalDisk>();
    private static Dictionary<char, PhysicalDisk> _cachedPartitions = new Dictionary<char, PhysicalDisk>();
    private static DriveInfo[]? _cachedDrives = null;

    /// <summary>
    /// All the physical disks by disk number
    /// </summary>
    public static Dictionary<string, PhysicalDisk> PhysicalDisks
    {
        get
        {
            if (_cachedDisks.Count == 0)
                _cachedDisks = GetPhysicalDisks();
            return _cachedDisks;
        }
    }

    /// <summary>
    /// All the physical disks by partition (drive letter)
    /// </summary>
    public static Dictionary<char, PhysicalDisk> Partitions
    {
        get
        {
            if (_cachedPartitions.Count == 0)
                _cachedPartitions = GetPartitions();
            return _cachedPartitions;
        }
    }

    public static DriveInfo[] Drives
    {
        get
        {
            if (_cachedDrives == null)
                _cachedDrives = DriveInfo.GetDrives();
            return _cachedDrives;
        }
    }

    public static void ReloadPhysicalDisks()
    {
        if (_cachedDisks.Count > 0)
            _cachedDisks.Clear();
        _cachedDisks = GetPhysicalDisks();
    }

    public static MediaType GetMediaTypeForPath(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root)) return MediaType.Unspecified;
        return Partitions[root[0]].MediaType;
    }

    public static DriveInfo? GetPreferredInstallationDrive(long modlistSize)
    {
        return DriveInfo.GetDrives()
                        .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                        .OrderByDescending(d => d.AvailableFreeSpace > modlistSize)
                        .ThenByDescending(d => Partitions[d.RootDirectory.Name[0]].MediaType == MediaType.SSD)
                        .ThenByDescending(d => d.AvailableFreeSpace)
                        .FirstOrDefault();
    }

    [DebuggerHidden]
    private static Dictionary<string, PhysicalDisk> GetPhysicalDisks()
    {
        try
        {
            var disks = new Dictionary<string, PhysicalDisk>();
            var scope = new ManagementScope(@"\\localhost\ROOT\Microsoft\Windows\Storage");
            var query = new ObjectQuery("SELECT * FROM MSFT_PhysicalDisk");
            using var searcher = new ManagementObjectSearcher(scope, query);
            var dObj = searcher.Get();
            foreach (ManagementObject diskobj in dObj)
            {
                var dis = new PhysicalDisk();
                try
                {
                    dis.SupportedUsages = (ushort[])diskobj["SupportedUsages"];
                }
                catch (Exception)
                {
                    dis.SupportedUsages = null;
                }
                try
                {
                    dis.CannotPoolReason = (ushort[])diskobj["CannotPoolReason"];
                }
                catch (Exception)
                {
                    dis.CannotPoolReason = null;
                }
                try
                {
                    dis.OperationalStatus = (ushort[])diskobj["OperationalStatus"];
                }
                catch (Exception)
                {
                    dis.OperationalStatus = null;
                }
                try
                {
                    dis.OperationalDetails = (string[])diskobj["OperationalDetails"];
                }
                catch (Exception)
                {
                    dis.OperationalDetails = null;
                }
                try
                {
                    dis.UniqueIdFormat = (ushort)diskobj["UniqueIdFormat"];
                }
                catch (Exception)
                {
                    dis.UniqueIdFormat = 0;
                }
                try
                {
                    dis.DeviceId = diskobj["DeviceId"].ToString();
                }
                catch (Exception)
                {
                    dis.DeviceId = "NA";
                }
                try
                {
                    dis.FriendlyName = (string)diskobj["FriendlyName"];
                }
                catch (Exception)
                {
                    dis.FriendlyName = "?";
                }
                try
                {
                    dis.HealthStatus = (ushort)diskobj["HealthStatus"];
                }
                catch (Exception)
                {
                    dis.HealthStatus = 0;
                }
                try
                {
                    dis.PhysicalLocation = (string)diskobj["PhysicalLocation"];
                }
                catch (Exception)
                {
                    dis.PhysicalLocation = "?";
                }
                try
                {
                    dis.VirtualDiskFootprint = (ushort)diskobj["VirtualDiskFootprint"];
                }
                catch (Exception)
                {
                    dis.VirtualDiskFootprint = 0;
                }
                try
                {
                    dis.Usage = (ushort)diskobj["Usage"];
                }
                catch (Exception)
                {
                    dis.Usage = 0;
                }
                try
                {
                    dis.Description = (string)diskobj["Description"];
                }
                catch (Exception)
                {
                    dis.Description = "?";
                }
                try
                {
                    dis.PartNumber = (string)diskobj["PartNumber"];
                }
                catch (Exception)
                {
                    dis.PartNumber = "?";
                }
                try
                {
                    dis.FirmwareVersion = (string)diskobj["FirmwareVersion"];
                }
                catch (Exception)
                {
                    dis.FirmwareVersion = "?";
                }
                try
                {
                    dis.SoftwareVersion = (string)diskobj["SoftwareVersion"];
                }
                catch (Exception)
                {
                    dis.SoftwareVersion = "?";
                }
                try
                {
                    dis.Size = (ulong)diskobj["SoftwareVersion"];
                }
                catch (Exception)
                {
                    dis.Size = 0;
                }
                try
                {
                    dis.AllocatedSize = (ulong)diskobj["AllocatedSize"];
                }
                catch (Exception)
                {
                    dis.AllocatedSize = 0;
                }
                try
                {
                    dis.BusType = (ushort)diskobj["BusType"];
                }
                catch (Exception)
                {
                    dis.BusType = 0;
                }
                try
                {
                    dis.IsWriteCacheEnabled = (bool)diskobj["IsWriteCacheEnabled"];
                }
                catch (Exception)
                {
                    dis.IsWriteCacheEnabled = false;
                }
                try
                {
                    dis.IsPowerProtected = (bool)diskobj["IsPowerProtected"];
                }
                catch (Exception)
                {
                    dis.IsPowerProtected = false;
                }
                try
                {
                    dis.PhysicalSectorSize = (ulong)diskobj["PhysicalSectorSize"];
                }
                catch (Exception)
                {
                    dis.PhysicalSectorSize = 0;
                }
                try
                {
                    dis.LogicalSectorSize = (ulong)diskobj["LogicalSectorSize"];
                }
                catch (Exception)
                {
                    dis.LogicalSectorSize = 0;
                }
                try
                {
                    dis.SpindleSpeed = (uint)diskobj["SpindleSpeed"];
                }
                catch (Exception)
                {
                    dis.SpindleSpeed = 0;
                }
                try
                {
                    dis.IsIndicationEnabled = (bool)diskobj["IsIndicationEnabled"];
                }
                catch (Exception)
                {
                    dis.IsIndicationEnabled = false;
                }
                try
                {
                    dis.EnclosureNumber = (ushort)diskobj["EnclosureNumber"];
                }
                catch (Exception)
                {
                    dis.EnclosureNumber = 0;
                }
                try
                {
                    dis.SlotNumber = (ushort)diskobj["SlotNumber"];
                }
                catch (Exception)
                {
                    dis.SlotNumber = 0;
                }
                try
                {
                    dis.CanPool = (bool)diskobj["CanPool"];
                }
                catch (Exception)
                {
                    dis.CanPool = false;
                }
                try
                {
                    dis.OtherCannotPoolReasonDescription = (string)diskobj["OtherCannotPoolReasonDescription"];
                }
                catch (Exception)
                {
                    dis.OtherCannotPoolReasonDescription = "?";
                }
                try
                {
                    dis.IsPartial = (bool)diskobj["IsPartial"];
                }
                catch (Exception)
                {
                    dis.IsPartial = false;
                }
                try
                {
                    dis.MediaType = (MediaType)diskobj["MediaType"];
                }
                catch (Exception)
                {
                    dis.MediaType = 0;
                }
                disks.Add(dis.DeviceId, dis);
            }
            return disks;
        }
        catch(Exception ex)
        {
            return new Dictionary<string, PhysicalDisk>();
        }
    }

    [DebuggerHidden]
    private static Dictionary<char, PhysicalDisk> GetPartitions()
    {
        var partitions = new Dictionary<char, PhysicalDisk>();
        try
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();

            using var partitionSearcher = new ManagementObjectSearcher($"SELECT DiskNumber, DriveLetter FROM MSFT_Partition");
            partitionSearcher.Scope = scope;

            var queryResult = partitionSearcher.Get();
            if (queryResult.Count <= 0) return new Dictionary<char, PhysicalDisk>();

            foreach (var partition in queryResult)
            {
                var diskNumber = partition["DiskNumber"].ToString();
                var driveLetter = partition["DriveLetter"].ToString()[0];

                partitions[driveLetter] = PhysicalDisks[diskNumber];
            }

            return partitions;
        }
        catch(Exception)
        {
            return partitions;
        }
    }
}

/// <summary>
/// Documentation: https://learn.microsoft.com/en-us/windows-hardware/drivers/storage/msft-physicaldisk
/// </summary>
public class PhysicalDisk
{
    public ulong AllocatedSize;
    public ushort BusType;
    public ushort[] CannotPoolReason;
    public bool CanPool;
    public string Description;
    public string DeviceId;
    public ushort EnclosureNumber;
    public string FirmwareVersion;
    public string FriendlyName;
    public ushort HealthStatus;
    public bool IsIndicationEnabled;
    public bool IsPartial;
    public bool IsPowerProtected;
    public bool IsWriteCacheEnabled;
    public ulong LogicalSectorSize;
    public MediaType MediaType;
    public string[] OperationalDetails;
    public ushort[] OperationalStatus;
    public string OtherCannotPoolReasonDescription;
    public string PartNumber;
    public string PhysicalLocation;
    public ulong PhysicalSectorSize;
    public ulong Size;
    public ushort SlotNumber;
    public string SoftwareVersion;
    public uint SpindleSpeed;
    public ushort[] SupportedUsages;
    public ushort UniqueIdFormat;
    public ushort Usage;
    public ushort VirtualDiskFootprint;
}

public enum MediaType : ushort
{
    Unspecified = 0,
    HDD = 3,
    SSD = 4,
    SCM = 5
}
