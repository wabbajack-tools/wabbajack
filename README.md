# Wabbajack

[![Build Status](https://dev.azure.com/tbaldridge/tbaldridge/_apis/build/status/wabbajack-tools.wabbajack?branchName=master)](https://dev.azure.com/tbaldridge/tbaldridge/_build/latest?definitionId=3&branchName=master)

Wabbajack is an automated ModList installer that can recreate contents of a folder on another machine without ever distributing copyrighted materials or syncing data between the source and destination machine. Wabbajack will create _instructions_ for a ModList when compiling, those can be as simple as _Download Mod abc from the Nexus_ or complex as _Clean the Game ESM files using zEdit_.

## Social Links

- [wabbajack.org](https://www.wabbajack.org) The official Wabbajack website where you can find instructions and a [Gallery](https://www.wabbajack.org/gallery) for some ModLists.
- [Discord](https://discord.gg/zgbrkmA) The official Wabbajack discord for instructions, ModLists, support or friendly chatting with fellow modders.
- [Patreon](https://www.patreon.com/user?u=11907933) contains update posts and keeps the [Code Signing Certificate](https://www.digicert.com/code-signing/) alive.

## Supported Games and Mod Manager

| Game                      | Platform  | Mod Manager   | Notes |
|---------------------------|-----------|---------------|-------|
| Oblivion                  | Steam     | MO2           |       |
| Fallout 3                 | Steam     | MO2           |       |
| Fallout New Vegas         | Steam     | MO2           |       |
| Skyrim                    | Steam     | MO2           |       |
| Skyrim Special Edition    | Steam     | MO2           |       |
| Fallout 4                 | Steam     | MO2           |       |
| Skyrim VR                 | Steam     | MO2           |       |
| Darkest Dungeon           | Steam, GOG| Vortex        |       |
| Divinity Original Sin 2   | Steam, GOG| Vortex        | Normal and Definitive Edition      |
| Starbound                 | Steam, GOG| Vortex        |       |
| SW: KotOR                 | Steam, GOG| Vortex        |       |
| SW: KotOR 2               | Steam, GOG| Vortex        |       |
| The Witcher               | Steam, GOG| Vortex        | Enhanced Edition      |
| The Witcher 2             | Steam, GOG| Vortex        |       |
| The Witcher 3             | Steam, GOG| Vortex        | Normal and GotY Edition      |

### Note about Vortex Support

As you can probably guess from the table above: You will not be able to create ModLists from a Vortex installation for TES/Fallout games. The Vortex Support we're offering is designed for smaller games like Darkest Dungeon where you simply install mods, rearrange load order and be done. You do not need external tools like xEdit, DynDOLOD, CAO, FNIS and what not to mod these smaller games.

It is also important to note that Vortex Support is still in **early beta** meaning that stuff can go wrong, very fast. If you encounter such problems be sure to join our [Discord](https://discord.gg/zgbrkmA) and message `erri120#2285` about the error.

## Installing a ModList

Every ModList comes with its own set of instructions which you **should** read before doing anything. The following steps are the general steps you would take, but every ModList is different from one another so don't rely on those.

A ModList comes as a `.wabbajack` file. You might have a `.zip`, `.7z` or `.rar` file which contains the ModList so extract the archive using [7zip](https://www.7-zip.org/) before starting Wabbajack. Once extracted, start Wabbajack and click on the _Install a ModList from Disk_ button and select the extracted `.wabbajack` file.

Wabbajack will know if this ModList was compiled from an MO2 profile or a Vortex installation. For the former it downloads MO2 for you but for the latter you need an existing Vortex installation.

On the installation screen, configure the installation/staging and downloads folder before clicking the begin button. Once everything is correctly setup, the button will be clickable and you can proceed with the installation.

The installation can take everything from a few minutes to hours depending on the size of the ModList, your Internet speed and your hardware so just be patient and wait for _Installation complete! You may exit the program._ to appear in the log.

## Creating your own ModList

### Caching before compilation

Wabbajack will index all files in the game folder during compilation. It is highly recommended that you run a compilation with an empty ModList and copy the `vfs_cache.bin` file to a safe location. Depending on the game, indexing can take a long time for the game files alone so having done it already will save you time.

### Notes before compiling

Overview video on [YouTube](https://www.youtube.com/watch?v=5Fwr0Chtcuc).

Before doing anything make sure that:

1. Wabbajack is not inside commonly used folders like `Documents`, `Downloads`, `Desktop` but rather on a very high level directory like `C:\Wabbajack\`.
2. The game you modded is not inside the default Steam folder `C:\Program Files (x86)\Steam\steamapps\common`, see [this](https://support.steampowered.com/kb_article.php?ref=7418-YUBN-8129) post on how to move your library.
3. You have a stable connection to the Internet

### Creating a ModList from an MO2 Profile

Wabbajack **must not** be located inside the MO2 folder or else the entire universe and the time-space continuum we know and love will be destroyed... this includes all kitten and puppies you have ever seen so be careful!

MO2 **must** be in _Portable_ and every archive you used in your MO2 profile has some sort of download information attached:

- You need all archives and all `.meta` files for those archives inside your `MO2/downloads/` folder.
- If you downloaded a file manually from the Nexus, make sure its in the `MO2/downloads/` folder and click `Query Info` from the right-click menu in MO2.
- For other files like `ENBSeries`, `SKSE`, `SRO`, etc. look at the [RECIPES.md](https://github.com/halgari/wabbajack/blob/master/RECIPES.md) file.

#### Wabbajack Flags

There are special flags that can be placed in a mod's notes or comments to trigger special behavior in Wabbajack:

- `WABBAJACK_INCLUDE` - All mod files will be inlined into the `.wabbajack` file
- `WABBAJACK_ALWAYS_ENABLE` - The mod's files will be considered by the compiler even if the mod is disabled in the profile
- `WABBAJACK_NOMATCH_INCLUDE` - The mod's files will be included as inline files inside the `.wabbajack` file even if Wabbajack did not found any match for them

#### Patches

Wabbajack can create binary patches for files that have been modified after installation. This could be an `.esp` that has been cleaned or patched. It could also be a mesh or texture that has been optimized to work better in a given game.

In any case, a BSDiff file is generated. The output of this process is copied directly into the ModList instructions. However! It is important to note that the patch file 100% useless without the source file. So `original + patch = final_file`. Without the original file, the final file cannot be recreated. This allows us to distribute arbitrary changes without violating copyrights as we do not copy copyrighted material. Instead, we copy instructions on how to modify the copyrighted material.

#### Starting Wabbajack

Once you have everything setup, launch Wabbajack and click the _Create a ModList_ button. Select MO2 as your Mod Manager and point to your _modlist.txt_ file located at `MO2\profile\<profile_name>\modlist.txt`. Make sure that the downloads folder is set correctly.

You now also have the option to change some properties for your ModList. Users will see those information when they select your ModList before installation. **You can not change these properties during/after compilation!**

When everything is correctly set up, click the begin button and wait for Wabbajack to finish. Depeding on your hardware, size of your MO2 folder and game of choice, this can take up anything from a few minutes to hours. Do note that whenever Wabbajack has an error you can restart the compilation and Wabbajack will resume at the exact position.

Wabbajack is finished when you see _Done Building ModList_ in the log. At that point you can close Wabbajack and the `.wabbajack` file will be located in the same folder as `Wabbajack.exe`.

### Creating a ModList from an Vortex installation

**THIS IS STILL IN BETA** for more info see [this](#note-about-vortex-support) section.

Before you start compiling a ModList, make sure that the Vortex download folder for your game of choice only contains mods downloaded from the Nexus. The `VortexCompiler` will create an MD5 hash for the archive and search the Nexus using said hash.

Open Wabbajack and click on _Create a ModList_. Select Vortex and your game of choice. The _Staging_ and _Downloads_ folder will be filled automatically if they are at the default location. If you have changed these folders, be sure to change the paths in Wabbajack.

When everything is correctly set up, click the begin button and wait for Wabbajack to finish. Depeding on your hardware, size of your Vortex staging folder and game of choice, this can take up anything from a few minutes to hours. Do note that whenever Wabbajack has an error you can restart the compilation and Wabbajack will resume at the exact position.

Wabbajack is finished when you see _Done Building ModList_ in the log. At that point you can close Wabbajack and the `.wabbajack` file will be located in the same folder as `Wabbajack.exe`.

## FAQ

**How can I get Wabbajack to handle mods from `X`?**

Look at the [RECIPES.md](https://github.com/halgari/wabbajack/blob/master/RECIPES.md) file, we keep a knowledge base of how to deal with given types of mods in that file.

**How do I contribute to Wabbajack?**

Look at the [`CONTRIBUTING.md`](https://github.com/halgari/wabbajack/blob/master/CONTRIBUTING.md) file for detailed guidelines.

**How does Wabbajack differ from Automaton?**

I (_halgari_) used to be a developer working on Automaton. Sadly development was moving a bit too slowly for my liking, and I realized that a complete rewrite would allow the implementation of some really nice features (like BSA packing). As such I made the decision to strike out on my own and make an app that worked first, and then make it pretty. The end result is an app with a ton of features, and a less than professional UI. But that's my motto when coding "_make it work, then make it pretty_".

## Thanks to

Our testers and Discord members who encourage development and help test the builds.

### Patreon Supporters

#### Daedra level Patreon Supporters

- Ancalgon
- Theo
- Dascede
- Kristina Poňuchálková
- metherul
- Decopauge123

#### Chicken and Mudcrab level Supporters

- Druwski
- Soothsayre
- krageon
- Scumbag
- Burt Wheeler
- Jesse Earl Rockwell
- Mike Gray
- Theryl
- Daniel Gardner
- Dapper
- Corapol
- HQM
- Argos
- sorrydaijin
- William Chudziak
- N Kalim

### License & Copyright

All original code in Wabbajack is given freely via the GPL3 license. Parts of Wabbajack use libraries that carry their own Open Sources licenses, those parts retain their original copyrights. Note: Wabbajack installers contain code from Wabbajack. Therefore, selling of modlist files is strictly forbidden. As is hosting the files behind any sort of paywall. You recieved this tool free of charge, respect this by giving freely as you were given.
