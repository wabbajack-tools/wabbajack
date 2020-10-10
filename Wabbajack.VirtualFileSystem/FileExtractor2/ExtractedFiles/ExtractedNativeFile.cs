using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem.ExtractedFiles
{
    public class ExtractedNativeFile : NativeFileStreamFactory, IExtractedFile
    {
        public bool CanMove { get; set; } = true;

        public ExtractedNativeFile(AbsolutePath file, IPath path) : base(file, path)
        {
        }

        public ExtractedNativeFile(AbsolutePath file) : base(file)
        {
        }

        public async ValueTask Move(AbsolutePath newPath)
        {
            if (CanMove) 
                await _file.MoveToAsync(newPath, overwrite: true);
            else 
                await _file.CopyToAsync(newPath);
        }
    }
}
