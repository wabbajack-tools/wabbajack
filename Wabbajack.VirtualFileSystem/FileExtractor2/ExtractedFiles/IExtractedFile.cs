using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.VirtualFileSystem.ExtractedFiles
{
    public interface IExtractedFile : IStreamFactory
    {
        /// <summary>
        /// Possibly destructive move operation. Should greatly optimize file copies when the file
        /// exists on the same disk as the newPath. Performs a copy if a move is not possible.
        /// </summary>
        /// <param name="newPath">destination to move the entry to</param>
        /// <returns></returns>
        public ValueTask Move(AbsolutePath newPath);
        
        public bool CanMove { get; set; }
    }
}
