### Changelog

#### Version 0.9.3 - ???
* Add WABBAJACK_NOMATCH_INCLUDE works like WABBAJACK_INCLUDE but only includes files that are found to be missing at the end of compilation
* Add a list of all inlined data blobs to the install report, useful for reducing installer sizes
* Increased dummy EPS detection size to 250 bytes and added .esm files to the filter logic
* Only sync the VFS cache when it changes.
* Fix a crash in GroupedByArchive()
* Detect and zEdit Merges and include binary patches for merges (no install support yet)
* Add unit/integration tests. 
* Don't assume *everyone* has LOOT
* Added support for `.exe` installers
* Rework UI to support a slideshow of used mods during installation and compilation
* Remove support for extracting `.exe` installers
* Added support for `.omod` files
* Stop emitting `.exe` modlist installers
* Reworked Nexus HTTP API - Thanks Cyclonit

#### Version 0.9.2 - 9/18/2013
* Fixed a bug with BSA string encoding
* Fixed another profile issue confirmed that they are properly included now
* Log when the executable is being generated
* Fixed a integer overflow resulting in a crash in very large BSA reading
* Fix a bug in BSA string encoding
* Add human friendly filesizes to the download header and file info sections in the Install Report
* Improve compilation times by caching BSDiff patches
* Detect when VFS root folders don't exist
* Only reauth against the Nexus every 3 days (instead of 12 hours)
* Optimize executable patching by switching to .NET serialization and LZ4 compression
* Ignore some files Wabbajack creates
* Improve compilation times by reworking file indexing algorithm
* Store patch files in byte format instead of base64 strings
* Verify SHA of patched files after install
* Treat .fomod files as archives
* Include WABBAJACK_INCLUDE files before including patches
* Ignore .bin and .refcache files (DynDOLOD temp files)
* Shell out to cmd.exe for VFS cleaning should fix "ReadOnlyFile" errors once and for all
* Switch out folder selection routines for Win32 APIs, should fix issue #27
* Disable the UI while working on things, so users don't accidentally mis-click during installation/loading
* Disabled "ignore missing files", it didn't work anyways
* Properly delete BSA temp folder after install
* Include size and hash for installed files
 
#### Version 0.9.1 - 9/5/2019
* Fixed a bug where having only one profile selected would result in no profiles being selected


#### Version 0.9 - 9/5/2019
* Added log information for when modlists start parsing during installation
* Check all links during mod list creation
* Generate a installation report during compilation
* Show the report after compiling
* Added a button to view the report before installing
* Added support for non-archive files in downloads and installation. You can now provide a link directly to a file
that is copied directly into a modfile (commonly used for `SSE Terrain Tamriel.esm`)
* Fix crash caused by multiple downloads with the same SHA256
* Putting `WABBAJACK_ALWAYS_ENABLE` on a mod's notes/comments will cause it to always be included in the modlist, even if disabled
* All `.json`, `.ini`, and `.yaml` files that contain remappable paths are now inlined and remapped.
* If Wabbajack finds a file called `otherprofiles.txt` inside the compiled profile's folder. Then that file is assumed
to be a list of other profiles to be included in the install. This list should be the name of a profile, one name per line. 
* Can now set the download folder both during compilation and installation.
* Any config files pointing to the download folder are remapped.
* Refuse to run inside `downloads` folders (anti-virus watches these files too closely and it can cause VFS issues)
* Refuse to run if MO2 is on the system installed in non-portable mode (otherwise broken installs may result) 
* Config files that don't otherwise match a rule are inlined into the modlist
* Warn users before installing into an existing MO2 install folder (prevents unintentional data loss from overwriting existing data #24)
* Fix for read only folder deletion bug (#23)
* Include version numbers and SHAs in the install report
* Removed option to endorse mods, Nexus devs mentioned it was of questionable worth, I (halgari) agree

#### Version 0.8.1 - 8/29/2019
* Fixed a bug that was causing VFS temp folders not to be cleaned
* 7zip Extraction code now shows a progress bar
* Told 7zip not to ask for permission before overwriting a file (should fix the hanging installer problem)
* Fixed several places where we were using long-path incompatible file routines
* Changed the work queue from FIFO to LIFO which results in depth-first work instead of breadth-first
TLDR: We now fully analyze a single archive before moving on to the next.

  

#### Version 0.8 - 8/26/2019
* Mod folders that contain ESMs with names matching the Skyrim core ESMs are assumed to be cleaned versions of the core
game mods, and will be patched from the ESMs found in the game folder. **Note:** if you have also cleaned the files in the Skyrim
folder, this will result in a broken install. The ESMs in the game folder should be the original ESMs, the cleaned
ESMs should go into their own mod. These files currently only include:
    * `Update.esm`
    * `Dragonborn.esm`
    * `HearthFires.esm`
    * `Dawnguard.esm`
* `ModOrganizer.ini` is now interpreted and included as part of the install. As part of the install users will be asked
to point to their game folder, and then all the references to the MO2 folder or Game folder in `ModOrganizer.ini` will
be remapped to the new locations.
* Progress bars were added to several install/hashing and compilation instructions
* 7zip routines were rewritten to use subprocesses instead of a C# library. This will result in slower indexing and installation
but should have full compatability with all usable archive formats. It should also reduce total memory usage during extraction.
* Added the ability to endorse all used mods at the completion of an install
* Custom LOOT rules are now included in a special folder in MO2 after install. Users can use this to quickly import new LOOT rules.
* Moved the VFS cache from using BSON to a custom binary encoding. Much faster read/write times and greatly reduced memory usage.
**Note:** This means that modlist authors will have to re-index their archives with this version. This is automatic but the first 
compilation will take quite some time while the cache reindexes.
