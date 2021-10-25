using Wabbajack.Paths;

namespace Wabbajack.DTOs.BSA.FileStates;

public abstract class AFile
{
    public int Index { get; set; }
    public RelativePath Path { get; set; }
}