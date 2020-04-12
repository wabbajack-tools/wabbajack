using System;

namespace Wabbajack.Lib.NexusApi
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
        public long category_id { get; set; }
        public string category_name { get; set; }
        public string changelog_html { get; set; }
        public string description { get; set; }
        public string external_virus_scan_url { get; set; }
        public long file_id { get; set; }
        public string file_name { get; set; }
        public bool is_primary { get; set; }
        public string mod_version { get; set; }
        public string name { get; set; }
        public long size { get; set; }
        public long size_kb { get; set; }
        public DateTime uploaded_time { get; set; }
        public long uploaded_timestamp { get; set; }
        public string version { get; set; }
    }

    public class ModInfo
    {
        public string name { get; set; }
        public string summary { get; set; }
        public string description { get; set; }
        public Uri picture_url { get; set; }
        public string mod_id { get; set; }
        public long game_id { get; set; }
        public bool allow_rating { get; set; }
        public string domain_name { get; set; }
        public long category_id { get; set; }
        public string version { get; set; }
        public long endorsement_count { get; set; }
        public long created_timestamp { get; set; }
        public DateTime created_time { get; set; } 
        public long updated_timestamp { get; set; }
        public DateTime updated_time { get; set; }
        public string author { get; set; }
        public string uploaded_by { get; set; }
        public Uri uploaded_users_profile_url { get; set; }
        public bool contains_adult_content { get; set; }
        public string status { get; set; }
        public bool available { get; set; } = true;
    }

    public class MD5Response
    {
        public ModInfo mod;
        public NexusFileInfo file_details;
    }

    public class EndorsementResponse
    {
        public string message;
        public string status;
    }
}
