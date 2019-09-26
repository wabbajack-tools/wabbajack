## Wabbajack - An automated modlist installer for TES/Fallout games

[![Build Status](https://dev.azure.com/tbaldridge/tbaldridge/_apis/build/status/halgari.wabbajack?branchName=master)](https://dev.azure.com/tbaldridge/tbaldridge/_build/latest?definitionId=1&branchName=master)

The general idea behind this program is fairly simple. Given a Mod Organizer 2 folder and profile, generate list of instructions that will allow
a program to automatically recreate the contents of the folder on another machine. Think of it as replication, but without ever distributing copyrighted
files or syncing data between the source and destination machine. The end result is a program that recreate a modlist on a computer while respecting the
rights of the game publisher and the mod authors.

### Social Links

- [Discord](https://discord.gg/zgbrkmA)
- [Patreon](https://www.patreon.com/user?u=11907933) Check this page for updates and to vote on features

### What Wabbajack can do

At this point you may be wondering how much of a complex modlist Wabbajack can handle. At this point it's more about what Wabbajack *can't* handle, but
let's do a rundown of all the supported features:

- Support for the following games is tested on a regular basis
  - Fallout 4
  - Fallout New Vegas
  - Skyrim SE
  - Skyrim LE
- Support for automatic downloads from the following sources
  - Nexus Mods (Premium accounts only)
  - Dropbox
  - Google Drive
  - Mega
  - ModDB
  - Direct URLs (with custom header support)
- Support the following archive types
  - `.zip`
  - `.7z`
  - `.rar`
  - `.fomod` (FNV archives)
- The following mod installation types are supported
  - Files installed with our without fomod installers
  - Files from `.omod` mods like `DarNified UI` or `DarkUId Darn`
  - Manually installed mods
  - Renamed/deleted/moved files are detected and handled
  - Multiple mods installed into the same mod folder
  - A mod split across multiple mod folders
  - Any tools installed in the MO2 folder. Want your users to have BethIni or xEdit? Just put them in a folder inside the MO2 install folder
  - ENBseries files that exist in the game folder
  - SKSE install
- The following situations are automatically detected and handled by the automated binary patcher (not an exhaustive list)
  - ESP cleaning
  - form 44 conversion
  - ESP to ESL conversion
  - Adding masters
  - Dummy ESPs created by CAO
  - (really any ESP modifications are handled)
  - Mesh fixing
  - Texture compression / fixing
  The following BSA operations are detected by extracting or creating BSAs via Wabbajack's custom BSA routines
  - BSA Unpacking
  - BSA Creation (packing loose files)
  - BSA repacking (unpacking, fixing files and repacking)

That being said, there are some cases where we would need to do a bit more work to develop:

- Manually downloaded files
- LL Files (currently no plans to implement)
- esp to esm conversion (there are hacks for this)
- binary patching of non-bsa huge files. 256MB is the largest size Wabbajack can currently handle with the binary patcher

### Creating a ModList Installer

Overview video [`https://www.youtube.com/watch?v=5Fwr0Chtcuc`](https://www.youtube.com/watch?v=5Fwr0Chtcuc)

1) Download Wabbajack and install it somewhere outside of your normal Mod Organizer 2 folder
(otherwise Wabbajack will try to figure out how to install itself and that might cause a collapse in the time-space
continuum).
2) Make sure every archive you used in your MO2 profile has some sort of download information attached.
   - If the file was downloaded via MO2 you're good, no extra work is needed
   - If the file was downloaded manually from the Nexus, copy it into the MO2 downloads folder, go back to MO2
   - and go to the `downloads` tab. Find the file and click `Query Info` from the right-click menu. MO2 should find
   the download info for you
   - For other files (ENBSeries, SKSE, SRO, etc.) Look at the [`RECIPES.md`] file
   - for instructions specific to your file source.
3) Now load Wabbajack, and point it to the `\<MO2 Folder>\mods\<your profile>\modlist.txt` file.
4) Click `Begin`.
5) Wabbajack will start by indexing all your downloaded archives. This will take some time on most machines as the application
has to performa `SHA-256` hash on every file in every archive. However the results of this operation are cached, so you'll only need
to do this once for every downloaded file.
6) Once completed, Wabbajack will collect the files required for the modlist install and begin running them through the compilation stack.
7) If all goes well, you should see a new `<your profile name>.exe` file next to `Wabbajack.exe` that you just ran. This new `.exe` is the one
you want to hand out as a auto modlist installer.

### Installing a ModList

1) Get a modlist installer, it's a `.exe` file that was created by Wabbajack
2) Run the `.exe`, the install folder defaults to the same folder as the executable, change it if you want.
3) Click `Begin` to start installation. At some point you will be prompted for SSO authorization on the Nexus, files
will be auto installed and downloaded
4) After installation has completed, run `Mod Organizer 2.exe`, select `Portable` and your game type.

### How it works

At a technical level the process is as follows.

1) Hash and cache the contents of every archive in the `\downloads` folder. This lets Wabbajack know of all the possible locations where you could have installed mods
2) Apply the `resolution stack` to every file in both the game's root folder and in the MO2 folder.
3) Take the install directives and required archives and write their metadata to a JSON file.
4) Attach the JSON file to Wabbajack itself, creating a new Auto-installer for the profile

### The Resolution Stack

Every file analyzed by Wabbajack is passed through a stack of rules. The first rule to match the file creates a `Install Directive` or a instruction on how to install that specific file.

Currently the Resolution stack looks like this:

1) Ignore the contents of `logs\`
2) Directly include .meta files int the `downloads\` folder
3) Ignore the contents of `downloads\`
4) Ignore the contents of `webcache\`
5) Ignore the contents of `overwrite\`
6) Ignore any files with `temporary_logs` as a folder in the path
7) Ignore `.pyc` files
8) Ignore `.log` files
9) Ignore any files in `profiles` that are not for the selected MO2 profile
10) Ignore any disabled mods
11) Include any profile settings for the selected profile by including them directly in the modlist
12) Ignore "ModOrganizer.ini", it will be re-created when MO2 starts on the new machine
13) Ignore "Data" in the Game directly (in your Steam folder)
14) Ignore "Papyrus Compiler" in the game folder
15) Ignore the "Skyrim" folder in the game folder
16) Ignore any BSAs in the game folder
17) Include all meta.ini files from all (selected) mods
18) Include archive and file meta information for any file that matches a file in an archive directly via a SHA256 comparison
19) Rip apart any `.bsa` files and run a mini resolution stack on the contents to figure out how to build the .bsa from the input files
20) Generate patches for files that may have been modified after being installed from an archive (see section on Patching for more info)
21) Include dummy ESPs directly into the modlist
22) Ignore files in the game directory
23) Ignore .ini files
24) Ignore .html files (normally these are logs)
25) Ignore .txt files
26) Ignore `HavockBehaviourPostProcess.exe` this seems to get copied around by tools for some reason
27) Ignore `splash.png` it's created for some games (like FO4) by MO2
28) Error for any file that survives to this point.

So as you can see we handle a lot of possible install situations. See the section on [`Creating a Modpack`](README.md#Creating_a_ModList_Installer) for information on working with the installer

### Wabbajack Flags

The if the following words are found in a mod's notes or comments they trigger special behavior in Wabbajack.

- `WABBAJACK_INCLUDE` - All the files int he mod will be inlined into the installer
- `WABBAJAC_ALWAYS_ENABLE` - The mod's files will be considered by the compiler even if the mod is disabled in the profile

### Patches

Wabbajack can create binary patches for files that have been modified after installation. This could be `.esp` files that have been cleaned or patched. Or
it could be meshes and textures that have been optimized to work better in a given game. In any case a BSDiff file is generated. The output of this process
is copied directly into the modlist instructions. However! It is important to note that the patch file is 100% useless without the source file. So `original + patch = final_file`. Without the original file, the final file cannot be recrated. This allows us to distribute arbitrary changes without violating copyrights as we do not copy
copyrighted material. Instead we copy instructions on how to modify the copyrighted material.

### FAQ

**How do I get Wabbjack to handle mods from `X`**

Look at the [`RECIPES.md`] file, we keep a knowledgebase of how to deal with given types of mods in that file.

**How do I contribute to Wabbajack?**

Look at the [`CONTRIBUTION.md`](https://github.com/halgari/wabbajack/blob/master/CONTRIBUTING.md) file for detailed guidelines.

**Why does each modlist install another copy of Mod Organizer 2?**

Self-contained folders are a cleaner abstraction than dumping tons of modlists into the same set of folders. It's easy to uninstall a modlist (simply delete the folder),
and MO2 really isn't designed to support lots of disparate modlists. For example if two modlists both wanted a given texture mod, but different options they would
somehow have to keep the names of their mods separate. MO2 isn't that big of an app, so there's really no reason not to install a new copy for each modlist.

**Why don't I see any mods when I open Mod Organizer 2 after install?**

Make sure you selected the "Portable" mode when starting MO2 for the first time. In addition, make sure you haven't installed MO2 in a non-portable way on the same box.
Really, always use "Portable Mode" it's cleaner and there really isn't a reason not too do so. Make the data self-contained. It's cleaner that way.

**Will Wabbajack ever support Vortex/other mod managers?**

I'll be honest, I don't use anything but MO2, so I probably won't write the code. If someone were to write a patch for the functionality
I wouldn't throw away the code, but it would have to be done in a way that was relatively seamless for users. Since Wabbajack treats all files in the same way
it doesn't know what mod manager a user is using. This means that if the modlist creator used Vortex all users of the modlist would have to use Vortex. This doesn't seem
optimal. It's possible perhaps, but it's at the bottom of the priority list.

**Where is the modlist? Why am I just given an .exe?**

When Wabbajack creates a modlist, as a final step it copies itself (the wabbajack.exe) and tacks onto the end of the file the modlist data, and a few bits
of magic text. When Wabbajack starts it looks at itself to see if it has this extra data tacked on to the end of the executable. If the data is found the app kicks
into installation mode. This means that Wabbajack acts a lot like a self-extracting installer.

**Do you know that some mod authors don't like their mods being automatically installed?**

Yes, I've heard this, but they chose to host their data on a public site. And no, they don't have the right to dictate what HTTP client is used to download a file.
We're using official Nexus APIs for nexus downloads, so any downloads Wabbajack performs are correctly tracked, and MO2 encourages users to endorse mods. It's 2019, we can
have better tools than manually clicking links.

**How does Wabbajack differ from Automaton?**

I (halgari) used to be a developer working on Automaton. Sadly development was moving a bit too slowly for my liking, and I realized that a complete rewrite would allow the
implementation of some really nice features (like BSA packing). As such I made the decision to strike out on my own and make an app that worked first, and then make it pretty.
The end result is an app with a ton of features, and a less than professional UI. But that's my motto when coding "make it work, then make it pretty".

## Thanks to

Our tester and Discord members who encourage development and help test the builds.

### Patreon Supporters

#### Daedra level Patreon Supporters

- Ancalgon

#### Patreon Supporters

- Druwski

### License & Copyright

All original code in Wabbajack is given freely via the GPL3 license. Parts of Wabbajack use libraries that carry their own Open Sources licenses, those parts
retain their original copyrights. Note: Wabbajack installers contain code from Wabbajack. Therefore, selling of modlist files is strictly forbidden. As is hosting
the files behind any sort of paywall. You recieved this tool free of charge, respect this by giving freely as you were given.

[`RECIPES.md`]: https://github.com/halgari/wabbajack/blob/master/RECIPES.md
