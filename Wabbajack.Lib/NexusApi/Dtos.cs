using System;

namespace Wabbajack.Lib.NexusApi
{
    public class UserStatus
    {
        public string email = string.Empty;
        public bool is_premium;
        public bool is_supporter;
        public string key = string.Empty;
        public string name = string.Empty;
        public string profile_url = string.Empty;
        public string user_id = string.Empty;
    }

    public class NexusFileInfo
    {
        public long category_id { get; set; }
        public string category_name { get; set; } = string.Empty;
        public string changelog_html { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public string external_virus_scan_url { get; set; } = string.Empty;
        public long file_id { get; set; }
        public string file_name { get; set; } = string.Empty;
        public bool is_primary { get; set; }
        public string mod_version { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public long size { get; set; }
        public long size_kb { get; set; }
        public DateTime uploaded_time { get; set; }
        public long uploaded_timestamp { get; set; }
        public string version { get; set; } = string.Empty;
    }

    public class ModInfo
    {
        public string name { get; set; } = string.Empty;
        public string summary { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public Uri? picture_url { get; set; }
        public string mod_id { get; set; } = string.Empty;
        public long game_id { get; set; }
        public bool allow_rating { get; set; }
        public string domain_name { get; set; } = string.Empty;
        public long category_id { get; set; }
        public string version { get; set; } = string.Empty;
        public long endorsement_count { get; set; }
        public long created_timestamp { get; set; }
        public DateTime created_time { get; set; }
        public long updated_timestamp { get; set; }
        public DateTime updated_time { get; set; }
        public string author { get; set; } = string.Empty;
        public string uploaded_by { get; set; } = string.Empty;
        public Uri? uploaded_users_profile_url { get; set; }
        public bool contains_adult_content { get; set; }
        public string status { get; set; } = string.Empty;
        public bool available { get; set; } = true;
    }

    public class MD5Response
    {
        public ModInfo? mod;
        public NexusFileInfo? file_details;
    }

    public class EndorsementResponse
    {
        public string message = string.Empty;
        public string status = string.Empty;
    }
}
