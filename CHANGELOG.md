### Changelog

#### Version - 2.0.3.0 - 4/29/2020
* Updated the MEGA Credentials Login form with more UI elements
* Switch LZ4 compression to L8 (vs L12) for much faster SSE BSA creation
* Several other internal code tweaks to improve performance and code quality
* Fixed Mediafire pop-up ads, they are no longer shown
* Updated 3rd party libraries to latest versions

#### Version - 2.0.2.0 - 4/27/2020
* Fixed mediafire links not getting resolved
* Fixed new mega links not being accepted
* Fixed cannot delete readonly file issue
* Fixed WABBAJACK_NOMATCH_INCLUDE with files inside BSAs
* Removed software rendering mode in the GUI...that should never had made it into master

#### Version - 2.0.1.0 - 4/27/2020
* Fixed "FileNotFound" and "File is open by another process" bugs during installation
* Raised the BSA limit from 2,000,000,000 bytes to 2 ^ 31 bytes
* Added NSFW flags for modlists/gallery
* Fixed zEdit settings integration

#### Version - 2.0.0.0 - 4/25/2020
* Reworked all internal routines to use Relative/Absolute path values instead of strings
* Reworked all internal routines to use Hash values instead of strings
* Reworked all internal routines to use Game values instead of strings
* Vortex support has been removed, it wasn't well tested, and wasn't used by enough people to justify further support
* Modlists are no longer saved in a binary format, everything uses Json
* Json type names are now a bit more human friendly
* All server-side code that used MongoDB now uses SQL (unifying the database)
* All Nexus validation code has been reworked to leverage RSS feeds for faster response times to updates
* All non-Nexus validation code has been reworked for better performance
* Feeds are now validated on demand, this is possible due to having a SQL backend and improved Nexus support
* Jobs in the job queue no long clobber each other so much
* BSA routines are now mostly async
* During installation, only the bare minimum number of files are extracted from a 7zip
* During indexing/extraction BSA files are not extracted, instead they are opened and files are read on-demand
* File extraction is now mostly async
* Modlists now only support website readmes (file readmes weren't used much and were a pain to read)
* Modlists now require a machine-readable version field 
* Added support for games installed via the Bethesda Launcher
* Cache disk benchmarking results to save startup time of compilation/install
* Added VectorPlexus mods to the slideshow

#### Version - 1.1.5.0 - 4/6/2020
* Included LOOT configs are no longer Base64 encoded
* Reworked Wabbajack-cli
* Can use a MEGA login (if you have it, not required)
* Don't use the buggy Nexus SSO server, instead use the in-browser API key generator
* Several fixes for zEdit merge integration, handles several side-cases of improper configuration

#### Version - 1.1.4.0 - 3/30/2020
* Added support for Morrowind on GOG
* Fix a bug in the Author file uploader (Sync Error)
* Include symbols in the launcher
* Fix a small race condition/deadlock in the worker queue code

#### Version - 1.1.3.0 - 3/23/2020
* Fix for a lack of VC++ Redist dlls on newly installed Windows machines.

#### Version - 1.1.2.0 - 3/20/2020
* We now set VRAM settings for Skyrim LE ENBs
* Fixes for Morrowind Game metadata
* We now provide suggestions for users who try to install modlists for games they don't have installed
* We now warn users if they aren't running a modern version of Windows

#### Version - 1.1.1.0 - 3/9/2020
* Hotfix for Virtual Memory errors while creating BSAs

#### Version - 1.1.0.0 - 3/5/2020
* Binary Patching stores temporary and patch data on disk instead of memory (reducing memory usage)
* Fix a memory leak with diffing progress reporting
* Fix a bug with bad data in inferred game INI files. 
* Added download support for YouTube
* Slideshow can now display mods from non-Nexus sites
* Building BSAs now leverage Virtual Memory resulting in a 32x reduction in memory usage during installation (#609)

#### Verison - 1.0.0.0 - 2/29/2020
* 1.0, first non-beta release

#### Version - 0.9.23.0 - 2/27/2020
* Several bugfixes and tweaks
* This is most likely the last version before the  1.0 release

#### Version - 0.9.22.1 - 2/25/2020
* Fix NaN error during installation 

#### Version - 0.9.22.0 - 2/24/2020
* Server side fixes for CORS support and FTP uploads
* Print the assembly version in the log (#565)
* Don't thrash the VFS cache name quite so much
* Use OctoDiff instead of BSDiff for better performance during diff generation 

#### Version - 0.9.21.0 - 2/23/2020
* Fix never ending hash issue

#### Version - 0.9.20.0 - 2/21/2020
* Don't reuse HTTP request objects (#532)
* Block popups in the in-app browser (#535)
* Don't print API keys in logs (#533)
* Store xxHash caches in binary format (#530)
* Added support for Morrowind BSA creation/unpacking
* Calculate screen size using DPI aware routines (#545)
* Only retain the most recent 50 log files


#### Version - 0.9.19.0 - 2/14/2020
* Disable server-side indexing of all mods from the Nexus
* Accept download states from clients and index the mods we haven't seen
* Fixes for Skyrin VR USSEP patch
* Remember the download states that we index on the server
* Only print remaining nexus quotas when they change
* Reworked the HTTP backend for Nexus/Http downloads performance and stability is much improved
* Fixed key errors with compilation and installation
* Improvements to the new manifest report

#### Version - 0.9.18.0 - 2/11/2020
* Auto update functionality added client-side.
* Slideshow now moves to next slide when users clicks, even if paused
* Installer now prints to log what modlist it is installing
* Adding `matchAll=<archive-name>` to a *mods's* `meta.ini` file will result in unconditional patching for all unmatching files or BSAs in
that mod (issue #465)
* Added support for non-premium Nexus downloads via manual downloading through the in-app browser.
* Downloads from Bethesda.NET are now supported. Can login via SkyrimSE or Fallout 4.
* Manual URL downloads are streamlined
* AFKMods.com download support is improved

#### Version - 1.0 beta 17 - 1/22/2020
* Build server now indexes CDN files after they are uploaded
* Build server actively looks for DynDOLOD updates
* Fix for the null key exception during compilation
* Added support for tesalliance, and afkmods
* Fix for queue size recommendation of 0GB RAM on low-end machines
* Fix for website readme compilation
* Fix for compiler downloads folder specification (was always standard path)

#### Version - 1.0 beta 16 - 1/19/2020
* Progress ring displays when downloading modlist images
* GUI releases memory of download modlists better when navigating around
* Fixed phrasing after failed installations to say "failed".
* Fixed download bug that was marking some modlists as corrupted if they were replacing older versions.
* While compiling Wabbajack will attempt to download VFS and .meta data from the build server

#### Version - 1.0 beta 15 - 1/6/2020
* Don't delete the download folder when deleting empty folders during an update
* If `Game Folder Files` exists in the MO2 folder during compilation the Game folder will be ignored as a file source

#### Version - 1.0 beta 14 - 1/6/2020
* Updating a list twice without starting WJ no longer deletes your modlist
* .mohidden files will now be correctly detected during binary patching
* Added support for MO2's new path format
* Added support for MO2 2.2.2's `portable.txt` feature
* Added support for VectorPlexus downloads
* Added a new CLI interface for providing Nexus API key overrides
* Several UI backend improvements

#### Version - 1.0 beta 13 - 1/4/22020
* Several fixes for steam game handling
* Fixes for metrics reporting

#### Version - 1.0 beta 12 - 1/3/22020
* Breaking change: the internal serialization format has changed, this will make existing lists inoperable on the latest version of WJ
* Added a change to serialization to make it backwards-compatible in the future
* Added an anonymous key to the metrics
* Fixed INI errors (again)

#### Version - 1.0 beta 11 - 1/3/2020
* Rewrote the ModDB downloader to retry with other mirrors after failure
* INI parse errors are now soft errors
* Fixed several backend stability bugs
* Changed application version scheme to better match the actual app version

#### Version - 1.0 beta 10 - 12/23/2019
* Many internal bug fixes releated to deadlocking
* Take the system RAM into account when configuring queue sizes
* Fixed the "This shouldn't happen" bug during patching. Thanks Noggog for spending countless hours on tracking down the problems.

#### Version - 1.0 beta 9 - 12/18/2019
* Create output folders before trying to download a file

#### Version - 1.0 beta 8 - 12/17/2019
* Fixed parsing of buggy ini files (Bethesda supports them so we must as well)
* Disable invalid modlists instead of hiding them
* Several Vortex improvements
* Implemented HTTP resuming for file downloads

#### Version - 1.0 beta 7 - 12/15/2019
* Fixed a regression with HTTP downloading introduced in beta 5
* No longer show broken modlists in the gallery
* Add Stardew Valley support
* Add support for .dat extraction 
* Several UI fixes

#### Version - 1.0 beta 6 - 12/14/2019
* Fixes for some strange steam library setups
* Implemented download/install counts

#### Version - 1.0 beta 5 - 12/14/2019
* Added LoversLab download support
* Nexus and LL logins now happen via a in-ap browser
* Several UI enhancements

#### Version - 1.0 beta 4 - 12/3/2019
* Several crash and bug fixes

#### Version - 1.0 beta 3 - 11/30/2019
* Reworked much of the UI into a single window
* Can download modlists directly through the single-window UI
* Removed hard error on lack of disk space. We need to think about how we calculate required space


#### Version - 1.0 beta 2 - 11/23/2019
* Optimized install process, if you install on a directory that already contains an install
  the minimal amount of work will be done to update the install, instead of doing a complete
  from-scratch install
* Vortex Support for some non-Bethesda games.
* Reworked several internal systems (VFS and workqueues) for better reliability and stability
* Patches are cached during compilation, and source files are no longer extracted every compile 

#### Version 1.0 beta 1 - 11/6/2019
* New Installation GUI
* Files are now moved during installation instead of copied
* Many other internal/non-user-facing improvements and optimizations

#### Version 1.0 alpha 5 - 11/2/2019
* Fix a NPE exception with game ESM verification

#### Version 1.0 alpha 4 - 11/2/2019
* Reorganize steps so that we run zEdit merges before NOMATCH_INCLUDE
* Look for hidden/optional ESMs when building zEdit plugins
* Check for modified ESMs before starting the long install process

#### Version 1.0 alpha 3 - 11/2/2019
* Slideshow more responsive on pressing next
* Slideshow timer resets when next is pressed
* Changed modlist extension to `.wabbajack`
* You can now open modlists directly (after initial launch)
* Wabbajack will exit if MO2 is running
* Added support for zEdit merges. We detect the zEdit install location by scanning the tool list in 
Mod Organizer's .ini files, then we use the merges.json file to figure out the contents of each merge. 

#### Version 1.0 alpha 2 - 10/15/2019
* Fix installer running in wrong mode

#### Version 1.0 alpha 1 - 10/14/2019
* Several internal bug fixes

#### Version 0.9.5 - 10/12/2019
* New Property system for chaning Modlist Name, Author, Description, Website, custom Banner and custom Readme
* Slideshow can now be disabled
* NSFW mods can be toggled to not appear in the Slideshow
* Set Oblivion's MO2 names to `Oblivion` not `oblivion`
* Fix validation tests to run in CI
* Add `check for broken archives` batch functionality
* Remove nexus timeout for login, it's pointless.
* Force slides to load before displaying
* Supress slide load failures
* UI is now resizeable
* Setup Crash handling at the very start of the app
* Add BA2 support
* Fix Downloads folder being incorrectly detected in some cases
* Fix validation error on selecting an installation directory in Install mode
* Reworked download code to be more extensible and stable

#### Version 0.9.4 - 10/2/2019
* Point github icon to https://github.com/wabbajack-tools/wabbajack
* Add game registry entry for Skyrim VR
* Modlists are now .zip files. 
* Modlists now end with `.modlist_v1` to enable better version control
* If `readme.md` is found in the profile directory, inline it into the install report.
* Fix bug with null uri in slideshow images
* Fix bug in CleanedESM generation 

#### Version 0.9.3 - 9/30/2019
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
* Added permissions system 
* Auto detect game folders

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
