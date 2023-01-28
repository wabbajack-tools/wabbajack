using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shipwreck.Phash;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Wabbajack.Common;
using Wabbajack.Common.FileSignatures;
using Wabbajack.DTOs.Texture;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;


namespace Wabbajack.Hashing.PHash;

public class TexConvImageLoader : IImageLoader
{
    private readonly SignatureChecker _sigs;
    private readonly TemporaryFileManager _tempManager;

    public TexConvImageLoader(TemporaryFileManager manager)
    {
        _tempManager = manager;
        _sigs = new SignatureChecker(FileType.DDS, FileType.PNG, FileType.JPG, FileType.BMP);
    }
    
    public async ValueTask<ImageState> Load(AbsolutePath path)
    {
        return await GetState(path);
    }

    public async ValueTask<ImageState> Load(Stream stream)
    {

        var ext = await DetermineType(stream);
        var temp = _tempManager.CreateFile(ext);
        await using var fs = temp.Path.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fs);
        fs.Close();
        return await GetState(temp.Path);
    }

    private async Task<Extension> DetermineType(Stream stream)
    {
        var sig = await _sigs.MatchesAsync(stream);

        var ext = new Extension(".tga");
        if (sig != null)
            ext = new Extension("." + Enum.GetName(sig.Value));

        stream.Position = 0;
        return ext;
    }

    public async Task Recompress(AbsolutePath input, int width, int height, int mipMaps, DXGI_FORMAT format, AbsolutePath output,
        CancellationToken token)
    {
        var outFolder = _tempManager.CreateFolder();
        var outFile = input.FileName.RelativeTo(outFolder.Path);
        await ConvertImage(input, outFolder.Path, width, height, mipMaps, format, input.Extension);
        await outFile.MoveToAsync(output, token: token, overwrite:true);
    }

    public async Task Recompress(Stream input, int width, int height, int mipMaps, DXGI_FORMAT format, Stream output, CancellationToken token,
        bool leaveOpen = false)
    {
        var type = await DetermineType(input);
        await using var toFolder = _tempManager.CreateFolder();
        await using var fromFile = _tempManager.CreateFile(type);
        await input.CopyToAsync(fromFile.Path, token);
        var toFile = fromFile.Path.FileName.RelativeTo(toFolder);
        
        await ConvertImage(fromFile.Path, toFolder.Path, width, height, mipMaps, format, type);
        await using var fs = toFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        await fs.CopyToAsync(output, token);
    }
    
    
    public async Task ConvertImage(AbsolutePath from, AbsolutePath toFolder, int w, int h, int mipMaps, DXGI_FORMAT format, Extension fileFormat)
    {
        // User isn't renaming the file, so we don't have to create a temporary folder
        var ph = new ProcessHelper
        {
            Path = @"Tools\texconv.exe".ToRelativePath().RelativeTo(KnownFolders.EntryPoint),
            Arguments = new object[] {from, "-ft", fileFormat.ToString()[1..], "-f", format, "-o", toFolder, "-w", w, "-h", h, "-m", mipMaps, "-if", "CUBIC", "-singleproc"},
            ThrowOnNonZeroExitCode = true,
            LogError = true
        }; 
        await ph.Start();

    }

    public async Task ConvertImage(Stream from, ImageState state, Extension ext, AbsolutePath to)
    {
        await using var tmpFile = _tempManager.CreateFolder();
        var inFile = to.FileName.RelativeTo(tmpFile.Path);
        await inFile.WriteAllAsync(from, CancellationToken.None);
        await ConvertImage(inFile, to.Parent, state.Width, state.Height, state.MipLevels, state.Format, ext);
    }
    
    // Internals
    public async Task<ImageState> GetState(AbsolutePath path)
    {
        try
        {
            var ph = new ProcessHelper
            {
                Path = @"Tools\texdiag.exe".ToRelativePath().RelativeTo(KnownFolders.EntryPoint),
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
                PerceptualHash = await GetPHash(path),
                MipLevels = byte.Parse(data["mipLevels"])
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }
    

    public async Task<DTOs.Texture.PHash> GetPHash(AbsolutePath path)
    {
        if (!path.FileExists())
            throw new FileNotFoundException($"Can't hash non-existent file {path}");
            
        await using var tmp = _tempManager.CreateFolder();
        await ConvertImage(path, tmp.Path, 512, 512, 1, DXGI_FORMAT.R8G8B8A8_UNORM, Ext.Png);
            
        using var img = await Image.LoadAsync(path.FileName.RelativeTo(tmp.Path).ReplaceExtension(Ext.Png).ToString());
        img.Mutate(x => x.Resize(512, 512, KnownResamplers.Welch).Grayscale(GrayscaleMode.Bt601));

        return new DTOs.Texture.PHash(ImagePhash.ComputeDigest(new CrossPlatformImageLoader.ImageBitmap((Image<Rgba32>)img)).Coefficients);
    }

}