## Wabbajack - An automated modlist installer for TES/Fallout games

The general idea behind this program is fairly simple. Given a Mod Organizer 2 folder and profile, generate list of instructions that will allow
a program to automatically recreate the contents of the folder on another machine. Think of it as replication, but without ever distributing copyrighted
files or syncing data between the source and destination machine. The end result is a program that recrate a modlist on a computer while respecting the
rights of the game publisher and the mod authors. 

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


### License & Copyright

All original code in Wabbajack is given freely via the GPL3 license. Parts of Wabbajack use libraries that carry their own Open Sources licenses, those parts 
retain their original copyrights. Note: Wabbajack installers contain code from Wabbajack. Therefore, selling of modlist files is strictly forbidden. As is hosting 
the files behind any sort of paywall. You recieved this tool free of charge, respect this by giving freely as you were given. 

