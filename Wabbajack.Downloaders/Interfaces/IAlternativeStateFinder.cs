using System.Collections.Generic;
using System.Threading.Tasks;
using Wabbajack.DTOs;

namespace Wabbajack.Downloaders.Interfaces;

/// <summary>
///     Placed on a IDownloader to specify that it can find other files in the same mod
///     used to find files for auto-healing lists
/// </summary>
public interface IAlternativeStateFinder
{
    public Task<IReadOnlyList<Archive>> FindAlternativeStates(Archive a);
}