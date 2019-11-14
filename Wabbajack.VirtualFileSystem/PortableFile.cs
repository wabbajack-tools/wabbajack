namespace Wabbajack.VirtualFileSystem
{
    public class PortableFile
    {
        public string Name { get; set; }
        public string Hash { get; set; }
        public string ParentHash { get; set; }
        public long Size { get; set; }
    }
}