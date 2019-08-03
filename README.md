## Wabbajack - An automated modlist installer for TES/Fallout games

The general idea behind this program is fairly simple. Given a Mod Organizer 2 folder and profile, generate list of instructions that will allow
a program to automatically recreate the contents of the folder on another machine. Think of it as replication, but without ever distributing copyrighted
files or syncing data between the source and destination machine. The end result is a program that recreate a modlist on a computer while respecting the
rights of the game publisher and the mod authors. 

### Social Links
- [Discord](https://discord.gg/zgbrkmA)
- [Patreon] (https://www.patreon.com/user?u=11907933) Check this page for updates and to vote on features

### Creating a ModList Installer

1) Download Wabbajack and install it somewhere outside of your normal Mod Organizer 2 folder
(otherwise Wabbajack will try to figure out how to install itself and that might cuase a collapse in the time-space
continuum).
2) Make sure every archive you used in your MO2 profile has some sort of download information attached. 
   * If the file was downloaded via MO2 you're good, no extra work is needed
   * If the file was downloaded manually from the Nexus, copy it into the MO2 downloads folder, go back to MO2
   * and go to the `downloads` tab. Find the file and click `Query Info` from the right-click menu. MO2 should find
   the download info for you
   * For other files (ENBSeries, SKSE, SRO, etc.) Look at the [`RECIPES.md`](https://github.com/halgari/wabbajack/blob/master/RECIPES.md) file
   * for instructions specific to your file source.
3) Now load Wabbajack, and point it to the `\<MO2 Folder>\mods\<your profile>\modlist.txt` file. 
4) Click `Begin`.
5) Wabbajack will start by indexing all your downloaded archives. This will take some time on most machines as the application
has to performa `SHA-256` hash on every file in every archive. However the results of this operation are cached, so you'll only need 
to do this once for every downloaded file.
6) Once completed, Wabbajack will collect the files required for the modpack install and begin running them through the compilation stack.
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
Every file analyzed by Wabbajack is passed through a stack of rules. The first rule to match the file creates a `Install Directive` or a instruction on how to 
install that specific file. 

Currently the Resolution stack looks like this:

1) Ignore the contents of `logs\`
2) Ignore the contents of `downloads\`
3) Ignore the contents of `webcache\`
4) Ignore the contents of `overwrite\`
5) Ignore `.pyc` files
6) Ignore any files in `profiles` that are not for the selected MO2 profile
7) Ignore any disabled mods
8) Include any profile settings for the selected profile by including them directly in the modlist
9) Ignore "ModOrganizer.ini", it will be re-created when MO2 starts on the new machine
10) Ignore "Data" in the Game directly (in your Steam folder)
11) Ignore "Papyrus Compiler" in the game folder
12) Ignore the "Skyrim" folder in the game folder
13) Ignore any BSAs in the game folder
14) Include all meta.ini files from all (selected) mods 
15) Include archive and file meta information for any file that matches a file in an archive directly via a SHA256 comparison
16) Generate patches for files that may have been modified after being installed from an archive (see section on Patching for more info)
17) Deconstruct BSAs to see if they can be created by generating a BSA from assembing files and patches from archives
18) Ignore files in the game directory
19) Ignore .ini files
20) Ignore .html files (normally these are logs)
21) Ignore .txt files
22) Ignore `HavockBehaviourPostProcess.exe` this seems to get copied around by tools for some reason
23) Error for any file that survives to this point. 

So as you can see we handle a lot of possible install situations. See the section on `Creating a Modpack` for information on working with the installer

### Patches
Wabbajack can create binary patches for files that have been modified after installation. This could be `.esp` files that have been cleaned or patched. Or
it could be meshes and textures that have been optimized to work better in a given game. In any case a BSDiff file is generated. The output of this process 
is copied directly into the modlist instructions. However! It is important to note that the patch file is 100% useless without the source file. So original + patch = final_file
without the original file, the final file cannot be recrated. This allows us to distribute arbitrary changes without violating copyrights as we do not copy 
copyrighted material. Instead we copy instructions on how to modify the copyrighted material. 

### Archive Sources
Wabbajack can currently install from many different file hosting sources. Currently these consist of:

* Nexus Mods - Only Premium accounts, this restriction will not change. They host a lot of files and support the community, respect the service they provide.
* HTTP Servers - Bare HTTP downloads are supported, additional headers can also be passed to the server
* Dropbox
* Google Drive
* MEGA
* ModDB

### FAQ

**How do I get Wabbjack to handle mods from `X`**

Look at the `RECIPES.md` file, we keep a knowledgebase of how to deal with given types of mods in that file.

**Why does each modpack install another copy of Mod Organizer 2?**

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

### License & Copyright

All original code in Wabbajack is given freely via the GPL3 license. Parts of Wabbajack use libraries that carry their own Open Sources licenses, those parts 
retain their original copyrights. Note: Wabbajack installers contain code from Wabbajack. Therefore, selling of modlist files is strictly forbidden. As is hosting 
the files behind any sort of paywall. You recieved this tool free of charge, respect this by giving freely as you were given. 

