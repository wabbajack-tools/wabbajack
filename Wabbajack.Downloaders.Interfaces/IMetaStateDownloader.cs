using System.Threading.Tasks;
using Wabbajack.DTOs;

namespace Wabbajack.Downloaders.Interfaces;

public interface IMetaStateDownloader
{
    public Task<Archive> FillInMetadata(Archive archive);
}