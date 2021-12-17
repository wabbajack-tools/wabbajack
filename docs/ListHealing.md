## Overview of 3.0 "Auto-healing" or "Force Healing"

In the past with the Nexus deleting files every day, we saw a need
for rapid fully automatic healing for Wabbajack lists. This code was 
brittle, quite complex, and hard to debug. However, these days with
the Nexus no longer deleting files, we have an opportunity to simplify
the process. 

### Parts in play
* List Validation service -  a GitHub action with some static storage, and rights to log into all our download soruces
* Storage Server - the backing store behind the Wabbajack CDN consists of 3 storage spaces:
  * Patches - a directory of files stored as `{from_hash_hex}_{to_hash_hex}`
  * Mirrors - a directory of files in the CDN multi-parts format stored as `{file_hash_hex}`
  * Authored files - a directory of files in the CDN multi-parts format

### Multi-Parts Format
The structure for CDN files in this format is:
* `./definition.json.gz` - JSON data storing the hash of the files, and the hash of each part
* `./parts/{idx}` - each part stored as `0`, `1`, etc. Each file is uncompressed and roughly 2MB


### File Validation Process
The workflow for list validation in 3.0 is as follows:
1) Load the `configs/forced_healing.json` file that contains mirrored and patch files specified by list authors
2) Download every modlist and archive it for future use, if already downloaded, don't redownload
3) For each modlist, load it, and start validating the files
4) For each file that passes, return `Valid`
5) If the file fails, check the mirrors list for a match, if it matches return `Mirrored`
6) If the file fails, check the patches list for a match,
   * If one is found, validate the new file in the patch, if it fails try the next patch
7) If all patches fail to match, return `Invalid`
8) Write out reports for all modlists

### List Author Interaction
List authors now have two controls they can use:
* `wabbajack-cli.exe force-heal -o <old_file> -i <new-file>` 
  * Creates a patch for back porting `<new-file>` to `<old-file>`
  * Uploads the patch
  * Adds the patch go the `config/forced_healing.json` file
* `wabbajack-cli.exe mirror-file -f <file>`
  * Uploads a file as a mirror
  * Adds the file to the `config/forced_healing.json` file
  * Note: using this to violate author copyrights is strictly forbidden do not mirror files without seeking prior approval from WJ staff.