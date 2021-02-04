using System;
using System.Linq;
using System.Management;

namespace Wabbajack.Common
{
    public class PhysicalDisk
    {
        public PhysicalDisk(string driveLetter)
        {
            if (driveLetter.Length == 2 && driveLetter[1] == ':') driveLetter = driveLetter.Remove(driveLetter.Length - 1);
            if (driveLetter.Length == 3 && (driveLetter[1] == ':' && driveLetter[2] == '\\')) driveLetter = driveLetter.Remove(driveLetter.Length - 2);
            if (driveLetter.Length > 3) Utils.Error("Incorrect drive name! Must be X, X: or X:\\");

            Utils.Log($"Phsyical Disk: {driveLetter}");

            // Connect to storage scope
            var scope = new ManagementScope(@"\\.\root\microsoft\windows\storage");
            scope.Connect();

            // Search partitions that use requested the drive letter
            var partitionSearcher = new ManagementObjectSearcher($"SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter='{driveLetter}'");
            partitionSearcher.Scope = scope;
            // Get first partition that matches
            var partition = partitionSearcher.Get().OfType<ManagementObject>().First();

            // Search for a disk where the device ID matches the one we got from the partition
            var physicalSearcher = new ManagementObjectSearcher($"SELECT * FROM MSFT_PhysicalDisk WHERE DeviceId='{partition["DiskNumber"]}'");
            physicalSearcher.Scope = scope;
            // Get disk information
            var physical = physicalSearcher.Get().Cast<ManagementBaseObject>().Single();

            DriveLetter = driveLetter;
            DeviceId = (string)physical["DeviceId"];
            MediaType = (MediaTypes)Convert.ToInt32(physical["MediaType"]);
            BusType = (BusTypes)Convert.ToInt32(physical["BusType"]);
        }

        public string DriveLetter { get; }

        public string DeviceId { get;  }

        public MediaTypes MediaType { get; }

        public BusTypes BusType { get; }

        // https://docs.microsoft.com/en-us/previous-versions/windows/desktop/stormgmt/msft-physicaldisk#properties
        public enum MediaTypes
        {
            Unspecified = 0,
            HDD = 3,
            SSD = 4,
            SCM = 5
        }

        public enum BusTypes
        {
            Unknown = 0,
            USB = 7,
            SATA = 11,
            NVMe = 17
        }
    }
}
