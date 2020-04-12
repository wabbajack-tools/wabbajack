Deployment Plan for 2.0 go-live

1. Release 2.0 to authors and let them rebuild their lists
1. Save old configs so the don't get overwritten
1. Backup SQL server data
1. Update SQL Tables
   1. Nexus Mod Files
   1. Nexus Mod Infos
   1. Job Queue
   1. Api Keys
   1. Mod Lists
   1. Download States
   1. Uploaded Files
1. Export Download Inis from server
1. Export all cache files from server
1. Hand insert all API keys
1. Copy over new server binaries
1. Disable background jobs on server
1. Start new server
1. Load data
   1. Import downloaded Inis
   1. Import all cache files
1. Stop server
1. Enable backend jobs
1. Start server
1. Verify that list validation triggers
1. ???
1. Profit?
