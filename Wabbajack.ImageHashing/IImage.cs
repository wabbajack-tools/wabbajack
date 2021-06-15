using System;

namespace Wabbajack.ImageHashing
{
    public interface IImage : IDisposable
    {
        public int Width { get; }
        public int Height { get; }
        public GPUCompressionLevel CompressionLevel { get; }
        public IImageState State { get; }
    }

    public interface IImageState
    {
        
    }

    public enum GPUCompressionLevel : int
    {
        Uncompressed = 0, // The Image is uncompressed on the GPU
        Old = 1, // The Image is compressed in a poor (old) format on the GPU
        New = 2 // The Image is compressed in a newer format (like BC7) on the GPU
    }
}
