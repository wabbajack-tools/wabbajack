using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wabbajack.BuildServer.Models
{
    public class ApiKey
    {
        public string Id { get; set; }
        
        public string Key { get; set; }
        public string Owner { get; set; }
        
        public List<string> CanUploadLists { get; set; }
        public List<string> Roles { get; set; }
    }
}
