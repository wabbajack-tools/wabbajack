### Changelog

#### Version 0.9 - ????
* Added log information for when modlists start parsing during installation
* Check all links during mod list creation
* Generate a installation report during compilation
* Show the report after compiling
* Added a button to view the report before installing
* Added support for non-archive files in downloads and installation. You can now provide a link directly to a file
that is copied directly into a modfile (commonly used for `SSE Terrain Tamriel.esm`)
* Fix crash caused by multiple downloads with the same SHA256
* Putting `WABBAJACK_ALWAYS_INCLUDE` on a mod's notes/comments will cause it to always be included in the modlist, even if disabled
* All `.json`, `.ini`, and `.yaml` files that contain remappable paths are now inlined and remapped.
* If Wabbajack finds a file called `otherprofiles.txt` inside the compiled profile's folder. Then that file is assumed
to be a list of other profiles to be included in the install. This list should be the name of a profile, one name per line. 
* Can now set the download folder both during compilation and installation.
* Any config files pointing to the download folder are remapped.
* Refuse to run inside `downloads` folders (anti-virus watches these files too closely and it can cause VFS issues)
* Refuse to run if MO2 is on the system installed in non-portable mode (otherwise broken installs may result) 

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