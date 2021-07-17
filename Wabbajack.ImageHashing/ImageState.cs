using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Shipwreck.Phash;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Shipwreck.Phash.Bitmaps;

namespace Wabbajack.ImageHashing
{
    [JsonName("ImageState")]
    public class ImageState
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public DXGI_FORMAT Format { get; set; }
        public PHash PerceptualHash { get; set; }

        public static ImageState Read(BinaryReader br)
        {
            return new()
            {
                Width = br.ReadUInt16(),
                Height = br.ReadUInt16(),
                Format = (DXGI_FORMAT)br.ReadByte(),
                PerceptualHash = PHash.Read(br)
            };
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write((ushort)Width);
            bw.Write((ushort)Height);
            bw.Write((byte)Format);
            PerceptualHash.Write(bw);
        }

        public static async Task<ImageState> FromImageStream(Stream stream, Extension ext, bool takeStreamOwnership = true)
        {
            await using var tf = new TempFile(ext);
            await tf.Path.WriteAllAsync(stream, takeStreamOwnership);
            return await GetState(tf.Path);
        }

        private static readonly Extension PNGExtension = new(".png");
        public static async Task<PHash> GetPHash(AbsolutePath path)
        {
            await using var tmp = await TempFolder.Create();
            await ConvertImage(path, tmp.Dir, 512, 512, DXGI_FORMAT.R8G8B8A8_UNORM, PNGExtension);
            
            using var img = (Bitmap)Image.FromFile(path.FileName.RelativeTo(tmp.Dir).ReplaceExtension(PNGExtension).ToString());
            return PHash.FromDigest(ImagePhash.ComputeDigest(img.ToLuminanceImage()));
        }

        public static async Task ConvertImage(AbsolutePath from, AbsolutePath toFolder, int w, int h, DXGI_FORMAT format, Extension fileFormat)
        {
            // User isn't renaming the file, so we don't have to create a temporary folder
            var ph = new ProcessHelper
            {
                Path = @"Tools\texconv.exe".RelativeTo(AbsolutePath.EntryPoint),
                Arguments = new object[] {from, "-ft", fileFormat.ToString()[1..], "-f", format, "-o", toFolder, "-w", w, "-h", h, "-if", "CUBIC", "-singleproc"},
                ThrowOnNonZeroExitCode = true,
                LogError = true
            }; 
            await ph.Start();

        }

        public static async Task ConvertImage(Stream from, ImageState state, Extension ext, AbsolutePath to)
        {
            await using var tmpFile = await TempFolder.Create();
            var inFile = to.FileName.RelativeTo(tmpFile.Dir).WithExtension(ext);
            await inFile.WriteAllAsync(from);
            await ConvertImage(inFile, to.Parent, state.Width, state.Height, state.Format, ext);
        }

        public static async Task<ImageState> GetState(AbsolutePath path)
        {
            var ph = new ProcessHelper
                {
                    Path = @"Tools\texdiag.exe".RelativeTo(AbsolutePath.EntryPoint),
                    Arguments = new object[] {"info", path, "-nologo"},
                    ThrowOnNonZeroExitCode = true,
                    LogError = true
                };
                var lines = new ConcurrentStack<string>();
                using var _ = ph.Output.Where(p => p.Type == ProcessHelper.StreamType.Output)
                    .Select(p => p.Line)
                    .Where(p => p.Contains(" = "))
                    .Subscribe(l => lines.Push(l));
                await ph.Start();

                var data = lines.Select(l =>
                {
                    var split = l.Split(" = ");
                    return (split[0].Trim(), split[1].Trim());
                }).ToDictionary(p => p.Item1, p => p.Item2);

                return new ImageState
                {
                    Width = int.Parse(data["width"]),
                    Height = int.Parse(data["height"]),
                    Format = Enum.Parse<DXGI_FORMAT>(data["format"]),
                    PerceptualHash = await GetPHash(path)
                };
        }
    }
}
