using System.Text;

namespace Wabbajack.Compression.CKM
{
    /// <summary>
    /// Credits to Nukem9 for the original implementation
    /// https://github.com/Nukem9/bethnet-cli
    /// </summary>
    public class CKMUtil
    {
        public static void Extract(string archivePath, string outputDirectory)
        {
            using BinaryReader reader = new BinaryReader(File.OpenRead(archivePath));
            uint magic = reader.ReadUInt32();
            ushort majorVersion = reader.ReadUInt16();
            ushort minorVersion = reader.ReadUInt16();

            if (magic != 0x52415442)
                throw new Exception("Header magic is unknown. Should be 'BTAR'.");

            if (majorVersion != 1)
                throw new Exception("Archive major version is unknown. Should be 1.");

            if (minorVersion < 2 || minorVersion > 4)
                throw new Exception("Archive minor version is unknown. Should be 2, 3, or 4.");

            // Files are stored sequentially after the header. Names have a length prefix and raw data is next.
            while (reader.PeekChar() != -1)
            {
                ushort nameLength = reader.ReadUInt16();
                string name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                ulong dataLength = reader.ReadUInt64();

                if (dataLength > int.MaxValue)
                    throw new Exception("Data length exceeded max integer value");

                string outputPath = Path.Combine(outputDirectory, name);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

                using Stream output = File.Open(outputPath, FileMode.Create);
                while (dataLength > 0)
                {
                    int chunkSize = Math.Min(16384, (int)dataLength);
                    dataLength -= (uint)chunkSize;

                    byte[] data = reader.ReadBytes(chunkSize);
                    output.Write(data, 0, chunkSize);
                }
            }
        }

        public static void Pack(string archivePath, List<string> files)
        {
            using BinaryWriter writer = new BinaryWriter(File.Open(archivePath, FileMode.Create));
            writer.Write((uint)0x52415442); // INT32: Header magic
            writer.Write((ushort)1);        // INT16: Major version
            writer.Write((ushort)4);        // INT16: Minor version

            foreach (string file in files)
            {
                writer.Write((ushort)file.Length);          // INT16: File name length
                writer.Write(Encoding.UTF8.GetBytes(file)); // X: File name

                using BinaryReader reader = new BinaryReader(File.Open(file, FileMode.Open));
                ulong dataLength = (ulong)reader.BaseStream.Length;

                if (dataLength > int.MaxValue)
                    throw new Exception();

                writer.Write(dataLength); // INT64: File length

                while (dataLength > 0)
                {
                    int chunkSize = Math.Min(16384, (int)dataLength);
                    dataLength -= (uint)chunkSize;

                    byte[] data = reader.ReadBytes(chunkSize);
                    writer.Write(data);
                }
            }
        }
    }
}
