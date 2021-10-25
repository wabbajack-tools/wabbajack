using System.Collections.Generic;
using System.Linq;

namespace Wabbajack.DTOs.DownloadStates;

public abstract class ADownloadState : IDownloadState
{
    public abstract string TypeName { get; }
    public abstract object[] PrimaryKey { get; }

    public string PrimaryKeyString
    {
        get
        {
            var pk = new List<object>();
            pk.Add(TypeName);
            pk.AddRange(PrimaryKey);
            var pk_str = string.Join("|", pk.Select(p => p.ToString()));
            return pk_str;
        }
    }
}