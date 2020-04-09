using System;
using System.Threading.Tasks;

namespace Wabbajack.BuildServer.Models
{
    public class NexusCacheData<T>
    {
        public string Path { get; set; }
        public T Data { get; set; }
        public string Game { get; set; }
        
        public long ModId { get; set; }

        public DateTime LastCheckedUTC { get; set; } = DateTime.UtcNow;

        public string FileId { get; set; }

    }
}
