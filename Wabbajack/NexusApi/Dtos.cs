using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.NexusApi
{
    public class UserStatus
    {
        public string email;
        public bool is_premium;
        public bool is_supporter;
        public string key;
        public string name;
        public string profile_url;
        public string user_id;
    }

    public class NexusFileInfo
    {
        public ulong category_id;
        public string category_name;
        public string changelog_html;
        public string description;
        public string external_virus_scan_url;
        public ulong file_id;
        public string file_name;
        public bool is_primary;
        public string mod_version;
        public string name;
        public ulong size;
        public ulong size_kb;
        public DateTime uploaded_time;
        public ulong uploaded_timestamp;
        public string version;
    }

    public class ModInfo
    {
        public uint _internal_version;
        public string game_name;
        public string mod_id;
        public string name;
        public string summary;
        public string author;
        public string uploaded_by;
        public string uploaded_users_profile_url;
        public string picture_url;
    }

    public class SlideShowItem
    {
        public string ImageURL;
        public string ModName;
        public string ModSummary;
        public string AuthorName;
        public string ModURL;
    }

    public class EndorsementResponse
    {
        public string message;
        public string status;
    }
}
