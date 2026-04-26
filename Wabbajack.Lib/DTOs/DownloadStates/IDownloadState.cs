using System.Text.Json.Serialization;

namespace Wabbajack.DTOs.DownloadStates;

public interface IDownloadState
{
    [JsonIgnore] object[] PrimaryKey { get; }

    [JsonIgnore] string TypeName { get; }

    string PrimaryKeyString { get; }
}