using System;
using DirectXTexNet;
using Wabbajack.Common;

namespace Wabbajack.ImageHashing
{
    public class DDSImage : IImage
    {
        private DDSImage()
        {
            
        }

        private static Extension DDSExtension = new(".dds");
        private ScratchImage _image;
        private TexMetadata _metaData;

        public static DDSImage FromFile(AbsolutePath file)
        {
            if (file.Extension != DDSExtension)
                throw new Exception("File does not end in DDS");

            var img = TexHelper.Instance.LoadFromDDSFile(file.ToString(), DDS_FLAGS.NONE);
            
            return new DDSImage() {_image = img, _metaData = img.GetMetadata()};
        }

        public void Dispose()
        {
            if (!_image.IsDisposed) 
                _image.Dispose();
        }

        public int Width => _metaData.Width;
        public int Height => _metaData.Height;
        public GPUCompressionLevel CompressionLevel => GPUCompressionLevel.Uncompressed;
        public IImageState State { get; }
    }
}
