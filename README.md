# Wabbajack

[![Discord](https://img.shields.io/discord/605449136870916175)](https://discord.gg/wabbajack)
[![CI Tests](https://github.com/wabbajack-tools/wabbajack/actions/workflows/tests.yaml/badge.svg)](https://github.com/wabbajack-tools/wabbajack/actions/workflows/tests.yaml)
[![GitHub all releases](https://img.shields.io/github/downloads/wabbajack-tools/wabbajack/total)](https://github.com/wabbajack-tools/wabbajack/releases)

Wabbajack is an automated Modlist Installer that can reproduce an entire modding setup on another machine without bundling any assets or re-distributing any mods.

## Social Links

- [wabbajack.org](https://www.wabbajack.org) The official Wabbajack website with a [Gallery](https://www.wabbajack.org/#/modlists/gallery), [Status Dashboard](https://www.wabbajack.org/#/modlists/status) and [Archive Search](https://www.wabbajack.org/#/modlists/search/all) for official Modlists.
- [Discord](https://discord.gg/wabbajack) The official Wabbajack discord for instructions, Modlists, support or friendly chatting with fellow modders.
- [Patreon](https://www.patreon.com/user?u=11907933) contains update posts and keeps the [Code Signing Certificate](https://www.digicert.com/code-signing/) as well as our supplementary build server alive.

## Supported Games and Mod Manager

If you own a game on this list on the Epic Games Store, and the store isn't listed as suppoted, please get in touch on [Discord](https://discord.gg/wabbajack), so you can help us make Wabbajack support those versions as well.
This is needed, since the EGS has no public database of its game IDs.

| Game                       | Platform                     | Versions                            | Notes                                      |
|----------------------------|------------------------------|-------------------------------------|--------------------------------------------|
| Morrowind                  | Steam, GOG, BethNet          |                                     |                                            |
| Oblivion                   | Steam, GOG                   | Normal and GotY                     |                                            |
| Fallout 3                  | Steam, GOG                   | Normal and GotY                     |                                            |
| Fallout New Vegas          | Steam, GOG                   | Normal and region locked RU version |                                            |
| Skyrim                     | Steam                        |                                     |                                            |
| Skyrim Special Edition     | Steam, GOG                   |                                     | Platform support varies between mod lists! |
| Enderal                    | Steam                        |                                     |                                            |
| Enderal Special Edition    | Steam                        |                                     |                                            |
| Fallout 4                  | Steam                        |                                     |                                            |
| Skyrim VR                  | Steam                        |                                     |                                            |
| Fallout 4 VR               | Steam                        |                                     |                                            |
| Darkest Dungeon            | Steam, GOG, Epic Games Store |                                     | Experimental                               |
| The Witcher 3              | Steam, GOG                   | Normal and GotY                     | Experimental                               |
| Stardew Valley             | Steam, GOG                   |                                     | Experimental                               |
| Kingdom Come: Deliverance  | Steam, GOG                   |                                     | Experimental                               |
| Mechwarrior 5: Mercenaries | Epic Games Store             |                                     | Experimental                               |
| No Man's Sky               | Steam, GOG                   |                                     | Experimental                               |
| Dragon Age Origins         | Stean, GOG, Origin           |                                     | Experimental                               |
| Dragon Age 2               | Steam, Origin                |                                     | Experimental                               |
| Dragon Age Inquisition     | Steam, Origin                |                                     | Experimental                               |
| Kerbal Space Program       | Steam, GOG                   |                                     | Experimental                               |
   

**Note about games marked with experimental support**:

A new MO2 plugin called [Basic Games Plugin](https://github.com/ModOrganizer2/modorganizer-basic_games) enables the easy creation of new game plugins for non BGS games. This is still very experimental in both MO2 and Wabbajack.

Some like Mechwarrior 5 use a complete new method of creating a list found [here](https://github.com/wabbajack-tools/wabbajack/wiki/Native-Game-Installer---(Installers-not-using-MO2)), for more info on that best join the [Discord](https://discord.gg/wabbajack) for guidance.

## Installing a Modlist

Every Modlist comes with its own README containing information and instructions on how to install it correctly. You should definitely read the Modlist specific README if you want a successful installation.

The general procedure is download the Modlist which comes as a `.wabbajack` file, opening Wabbajack, clicking on the _Install From Disk_ button, configuring Install and Download Location and hitting start.

Do note that installation can take anything from a few minutes to hours depending on the size of the Modlist, your Internet connection as well as your hardware. In the meantime you can take a look at some of the included mods of the Modlist in the Slideshow that is playing during installation.

## Creating your own Modlist

Modlist Creation or Compilation as we call it, is a trial and error process. You will likely get a decent amount of compilation errors in the beginning and have to spent some time fixing them before your first compilation succeeds. After that you can do incremental builds which take significantly less time and have close to zero errors compared to your first build.

### Requirements

Wabbajack requires that you created your Modlist using a **portable** version of Mod Organizer 2. This is a **hard requirement** and Wabbajack won't work with Vortex, Kortex, Wyre Bash, Nexus Mod Manager, Oblivion Mod Manager or any other Mod Manager, not even Mod Organizer 1.

Aside from needed a portable MO2 installation you need to have a specific setup and workflow when modding, see [Pre-Compilation](#pre-compilation) for more information about that.

### Pre-Compilation

Compilation itself is similar to Installation as you just configure some paths and start the process, sit back and wait for Wabbajack to finish. Wabbajack requires a rather specific MO2 setup and you might have to change your workflow when modding for a WJ Modlist.

#### Everything comes from somewhere

> Wabbajack is an automated Modlist Installer that can reproduce an entire modding setup on another machine without bundling any assets or re-distributing any mods.

The focus is on _without bundling any assets or re-distributing any mods_. This means that Wabbajack needs to know where **every single file** came from. The most common origin is your downloads folder.

If you take a quick look inside your MO2 downloads folder you will find 2 types of files:

1) The actual download (some archive)
2) A `.meta` file that has the same name the archive its for

You might have `SkyUI_5_1-3863-5-1.7z` and `SkyUI_5_1-3863-5-1.7z.meta`.

These `.meta` files are created by MO2 and typically get created when you click the _MOD MANAGER DOWNLOAD_ button on Nexus Mods. If you open the `.meta` file you will find some metadata for the accompanied archive, the most important entries being `fileID`, `modID` and `gameName`.

Wabbajack will use the MO2 `.meta` files to find out where the archive came from using the [Nexus Mods API](https://app.swaggerhub.com/apis-docs/NexusMods/nexus-mods_public_api_params_in_form_data/1.0#/). This means that you **should** use the _MOD MANAGER DOWNLOAD_ button on Nexus Mods or you have to create those `.meta` files yourself (you can also use the _Query Info_ context menu option in the MO2 Downloads tab. This will populate the metadata of the selected archive).

For more information about `.meta` files, support sites and how to actually create them, see the [Meta File](#meta-files) section.

#### Script Extender and Cleaned Masters

The first step for modding a Bethesda game: Download the game, install the Script Extender and clean base game master files (`.esms`). Wabbajack can, of course, automate these steps.

_(You should use MO2s virtual file system for SKSE Scripts, it is not recommended to install the SKSE Scripts into the game folder directly)_.

Wabbajack can match files from your game folder with your downloads folder (more in-depth info on that topic: [Game Folder Files and MO2](#game-folder-files-and-mo2)), this means you download the Script Extender to your MO2 downloads folder and create a `.meta` file for it. The meta file should look something like this:

```ini
[General]
directURL=http://skse.silverlock.org/beta/skse64_2_00_18.7z
```

In the example above we used [SKSE SE 2.0.18](http://skse.silverlock.org/) and you'd have to replace the URL with the direct download link of the Script Extender you are using.

Next up are Cleaned Master Files. Use xEdit through MO2 and clean the base game master files normally, copy the cleaned master files to a new mod in MO2 (called `Cleaned Master Files` or something) and replace the cleaned master files in your game folder with the backup created by xEdit to revert them to their default unmodified state.

#### Game Folder Files and MO2

You now know about `.meta` files and understand that every file has to come from somewhere, this also means that you also need to create `.meta` files for MO2 and files in your game folder. We recommend using the [MO2 GitHub Releases](https://github.com/ModOrganizer2/modorganizer/releases/) instead of the ones you find on the Nexus.

Mods that need to be installed to the game folder by the user are required to be installed to a `Game Folder Files` folder in your MO2 directory. Let's look at an example for better understanding:

You will likely make use of SKSE/F4SE/OBSE or other Script Extender if you are modding Bethesda titles. Eg for SKSE you'd need to install `skse64_1_5_97.dll`, `skse64_loader.exe` and `skse64_steam_loader.dll` to your game folder. To make things easier for the end user, you can should put those files inside the `Game Folder Files` folder at `MO2\Game Folder Files`. You also need the archive and it's `.meta` file (see [Script Extender and Cleaned ESMs](#script-extender-and-cleaned-masters) section if you haven't) in your downloads folder so Wabbajack can match those files.

The user then only has to copy the files from the `Game Folder Files` directory to their game folder after installation. This also works for other files that need to be installed directly to the game folder like ENB or ReShade.

#### Stock Game

As an alternative to Game Folder Files, you may instead utilize the [Stock Game](https://github.com/wabbajack-tools/wabbajack/wiki/Keeping-The-Game-Folder-Clean-(via-local-game-installs)) feature. Stock Game is a folder within the MO2 instance folder that contains the base game files necessary to run a list, including any files you need to add to it such as `skse64_loader.exe`. Stock Game eliminates the need for the user to copy `Game Folder Files` into their game's installation directory and keeps the final installed list separate from the game's installation directory. Additionally, this allows a list author fine control over what files are included by default in the game's installation, such as SKSE, ENB, and so on. 

Note that Stock Game has one downside: When using Stock Game, Wabbajack will expect the game to come from a single source and *only* a single source (Steam, GOG, etc.). As an example: Morrowind is available on GOG, Steam, and Beth.net. When utilizing Stock Game with a copy of Morrowind from Steam, Wabbajack will expect all users who install the list to have the game through Steam, effectively excluding users who have purchased the game from GOG or Beth.net. Because of this, it is recommended that Stock Game only be utilized with games that have a single source, such as Skyrim SE which is only available through Steam.

Detailed setup information for Stock Game on Skyrim SE can be found [here.](https://github.com/LivelyDismay/Learn-To-Mod/blob/main/lessons/Setting%20up%20Stock%20Game%20for%20Skyrim%20SE.md)

#### Special Flags

There are some special cases where you want to change the default Wabbajack behavior for a specific mod. You can place the following flags in the notes or comments section of a mod to change how Wabbajack handles that mod.

| Flag | Description | Notes |
|------|-------------|-------|
| `WABBAJACK_INCLUDE` | All mod files will be inlined into the `.wabbajack` file | **DO NOT USE ON DOWNLOADED MODS** As it would result in you distributing that mod. Can lead to large `.wabbajack` files if used unsparingly |
| `WABBAJACK_NOMATCH_INCLUDE` | Any unmatched files will be inlined into the `.wabbajack` file | Useful for custom patches or generated files |
| `WABBAJACK_ALWAYS_ENABLE` | The mod will not be ignored by Wabbajack even if it's disabled | Wabbajack will normally ignore all mods you disabled in MO2 but there are some cases where you might want to give some choice to the end user and want to have the mod included |
| `WABBAJACK_ALWAYS_DISABLE` | The mod will always be ignored by Wabbajack | Useful if you don't want some mods included in the Modlist but still want to keep it active in your own setup |

#### Tagfile Tags

You can create an empty `tagfile` with no extention in any folder you want to apply this tags to. This is meant to be used with folders that aren't mods.

| Flag/File | Description | Notes |
|------|-------------|-------|
| `WABBAJACK_INCLUDE` | All files in this folder will be inlined into the `.wabbajack` file | **DO NOT USE ON DOWNLOADED MODS** As it would result in you distributing that mod. Can lead to large `.wabbajack` files if used unsparingly |
| `WABBAJACK_NOMATCH_INCLUDE` | Any unmatched files will be inlined into the `.wabbajack` file | Useful for custom patches or generated files |
| `WABBAJACK_IGNORE` | All files in this folder will be ignored by Wabbajack and therefore not be put into into the `.wabbajack` file. | Useful for tools or other things outside a mod you don't want/need reproduced on a users machine. Handle with care since excluded stuff can potentially break a setup.\* |
| `WABBAJACK_INCLUDE_SAVES` | When this file exists Wabbajack will include your save files in the `.wabbajack` file.| This will remove previous savefiles when the list gets installed as an update. |
| `WABBAJACK_NOMATCH_INCLUDE_FILES.txt` | All files listed in this file will be included in the `.wabbajack` file. | Every file needs to be in the same folder as the tag file. Every file need to be written into a new line. Every file needs to be added with its file extension.|
| `WABBAJACK_IGNORE_FILES.txt` | All files listed in this file will be ignored by Wabbajack and not included in the `.wabbajack` file. | Every file needs to be in the same folder as the tag file. Every file need to be written into a new line. Every file needs to be added with its file extension.|

\*It will finish the installation of a modlist, but the installed list might not run if you excluded a crutial part of it.

#### Patches

Reading all the previous section you might wonder if Wabbajack is able to detect modified files and how it deals with them. Wabbajack can't include the modified file so instead we just include the difference between the original and modified version.

This basically means `original + patch = final` and we only include `patch` in the `.wabbajack` file which, by itself, is just gibberish and completely useless without the original file. This allows us to distribute arbitrary changes without violating copyrights as we do not copy copyrighted material. Instead, we copy instructions on how to modify the copyrighted material.

You don't even have to tell Wabbajack that a specific file was modified, that would be way too much work. Instead Wabbajack will figure out which file got modified and create a binary patch. The modified file can be anything from some modified settings file to a patched plugin or optimized mesh/texture.

#### BSA Decomposition

Wabbajack is able to analyse `.bsa` and `.ba2` files and can also create those. This means that any changes made to a BSA/BA2 are copied on the end-user's installation, including the creation of new BSAs or the modification of existing ones. Unpacking, modifying, and then repacking a BSA is possible and those modifications will carry over to the final install. Similarly, creation of new BSAs will be replicated. Wabbajack does not analyze the BSA as a whole - instead it will look inside the BSA, hash the files within, and compare those to their original sources. It will then construct the BSA if new or reconstruct it if modified as necessary. Note that with particularly large BSAs this building and rebuilding process can take a significant amount of time on the user's machine depending on the user's hardware specifications.

#### Merges

Similar to BSAs, Wabbajack is able to analyze and create merged mods. Wabbajack will source the necessary files to create the merge from aforementioned sources of mods and then rebuild the merge on the user's end. Generally it is recommended to tag merges with the `WABBAJACK_NOMATCH_INCLUDE` flag to ensure that any files generated by the merging process are also carried over to the user. Wabbajack does not by default include the files and information necessary to build or rebuild the merge in the program used to build the merge originally, only the merge itself will be installed. 

#### Multiple MO2 Profiles

Wabbajack will normally ignore every other profile but you might have more than one profile that should be included. In this case you can create a `otherprofiles.txt` file in your main profile folder and write the names of the other profiles (1 per line) in it.

**Example**:

Profiles: `SFW`, `NSFW` (`SFW` is the main profile)

contents of `MO2\profiles\SFW\otherprofiles.txt`:

```txt
NSFW
```

#### Minimal Downloads Folder

We already talked about how Wabbajack will _match_ files to determine where they came from. This can sometimes not work as intended and Wabbajack might end up matching files against the wrong archive. This can happen when you have multiple versions of the same mod in your downloads folder. It is highly recommended that you **delete unused downloads and previous versions of mods** so that Wabbajack doesn't end up mis-matching files and the end user ends up downloading more than they need.

### Compilation

Compilation itself is very straight forward and similar to Installation: you let Wabbajack run and hope it finishes successfully. The key for making it run successfully is having a "Wabbajack-compliant" MO2 setup which you can get by following the advice from the previous sections.

For a more detailed guide on executing the compilation process, refer to [this document.](https://github.com/LivelyDismay/Learn-To-Mod/blob/main/lessons/Compiling%20a%20Modlist%20for%20Wabbajack.md)

#### Wabbajack Compilation Configuration

In Wabbajack select _Create a Modlist_ to navigate to the configuration screen. Here you can configure some metadata for your Modlist which will later be viewable by the user.

| Field | Description | Notes |
|-------|-------------|-------|
| Modlist Name | **REQUIRED:** Name of your Modlist | |
| Version | **REQUIRED:** Current Version | Do note that this has to be a semantic version (1.0 or 0.1 (it can have multiple `.` separators)) and not some random text like "The best version on Earth" (this is not the Nexus)! |
| Author | Modlist Author | Should be your name in original Modlists and/or the name of the original Modlist author if you adapted a normal Modlist to a Wabbajack Modlist |
| Description | 700 characters Descriptions | |
| Image | Modlist Image | Aspect ratio should be 16:9 for the best result |
| Website | Website URL | |
| Readme | Readme URL | |
| NSFW | NSFW Checkbox | Only really needed for official Modlists as our Galleries have the option to hide NSFW Modlists |

#### Compilation Errors

You will likely get a ton of errors during your first compilation which is to be expected. We highly recommend you join our [Discord](https://discord.gg/wabbajack) and check the `modlist-development-help` channel for help.

### Post-Compilation

After you have finished the Compilation you are basically done with Wabbajack and can upload the resulting `.wabbajack` file to somewhere, share it with people or just keep it for yourself as a Modlist backup. This section is for people who want to share their Modlist with the community.

In the Wabbajack community we differentiate between _official_ and _unofficial_ Modlists. The former being curated Modlists which have been tested and are available in the Wabbajack Gallery while the latter are Modlists submitted on our [Discord](https://discord.gg/wabbajack) in the `unofficial-modlist-submissions` channel and are not tested.

Official Modlists also get their own support channel on our Discord and often have 500-2500 users depending on the game. Do note that these Modlists receive regular updates and the authors are putting in a lot of hours to deliver the best possible version. If you think your Modlist and more importantly: yourself are up for the task you can submit your Modlist for official status on the Discord in the `modlist-development-info` channel.

Managing a Modlist can be tricky if you have never done something like this. In the early days of Wabbajack we mostly relied on GDocs READMEs and hosted all accompanying files on GDrive while managing issues through the support channel on Discord. This system was later almost completely replaced through valiant effort by one of our developers: almost all new Modlists use GitHub for managing their Modlist.

GitHub was made for developers and is the site you are currently on. It mostly hosts source code for open source projects, such as Wabbajack, but can also be used for project management. Another strong point is Markdown support. This README you are currently reading is, like every other GitHub README, written in Markdown and rendered on GitHub.

On the topic of READMEs: you should create a good one.

It is not sufficient to just slap some install and MCM instructions in there. Your Modlist might contain a thousand mods and offer hundreds of playtime but the user might not know what in oblivion is even going on and what's included.

Example structure for a good README:

- **Preamble**: Give a quick overview about what your Modlist is about, the core idea and also mention that it's a Wabbajack Modlist (some might not know what Wabbajack is so link to this README)
- **Requirements**: Simple list of requirements such as min CPU, GPU, RAM and most importantly: Drive Space. Also include a list of accounts the user needs such as a Nexus Mods, LoversLab, VectorPlexus Account or any other sites your are downloading from
- **Installation**: Installation instructions specific to your Modlist. You can take a look at some of the Modlists linked below as almost all of them use the same instructions with minor changes
- **Important Mods you should know about**: This is an important section you should not forget. Go in-depth on core mods and talk about _important mods the user should know about_. It can be overwhelming for the user to just be thrusted into a completely modded world without knowing what's even included and possible.
- **MCM**: If your Modlist has some sort of MCM like for Skyrim or Fallout 4 then you should give instructions on what settings to use
- **Changelog**: Should not be in the README and can be in a separate file called `CHANGELOG.md` but make sure to create one

Some Modlists that host all their stuff on GitHub:

- the OG GitHub Modlist: [Lotus](https://github.com/erri120/lotus)
- [Keizaal](https://github.com/PierreDespereaux/Keizaal)
- [Elder Souls](https://github.com/jdsmith2816/eldersouls)
- [Total Visual Overhaul](https://github.com/NotTotal/Total-Visual-Overhaul)
- [Serenity](https://github.com/ixanza/serenity)
- [RGE](https://github.com/jdsmith2816/rge)
- [Elysium](https://github.com/TitansBane/Elysium)

Other modlists opt to host all of their information on a dedicated website:
- [The Phoenix Flavour](https://thephoenixflavour.com)
- [Living Skyrim](https://www.fgsmodlists.com/living-skyrim)
- [MOISE](https://www.fgsmodlists.com/moise)
- [Dungeons & Deviousness](https://www.fgsmodlists.com/dd)
- [Tales From the Northern Lands](https://eziothedeadpoet.github.io/Tales-from-the-Northern-Lands/)

### Meta Files

_Read [Everything comes from somewhere](#everything-comes-from-somewhere) first, as that section explains the basic concept of meta files. This section focuses more on advanced uses._

You already know how `.meta` files should look for mods that come from Nexus Mods:

```ini
[General]
gameName=Skyrim
modID=3863
fileID=1000172397
```

This is the basic configuration for an archive that came from Nexus Mods. You can get these types of `.meta` files through MO2 by using the _Query Info_ option in the Downloads tab.

Mods can also be hosted somewhere else, eg on other modding sites like LoversLab, ModDB or normal file hosting services like GDrive and MEGA. Below is a table for all supported sites that Wabbajack can download from.

**Additional notes**:

- Every `.meta` file, no matter the site, has to start with `[General]`
- `.meta` files are **case sensitive** (see [Issue `#480`](https://github.com/wabbajack-tools/wabbajack/issues/480) for more info, this will be fixed somewhere in the future)
- The "Whitelist" Wabbajack uses is available on [GitHub](https://github.com/wabbajack-tools/opt-out-lists/blob/master/ServerWhitelist.yml) and you might have to create a Pull Request if you want to add a link
- For IPS4 Sites: IPS4 sites have a common layout and all behave very similar. There are some minor differences because they use different template versions but in general the same layout applies to all of them. Here are the different download links you can have for IPS4 sites:
  - Multi-file download: `{SITE-PREFIX}/files/file/{FILE}/?do=download&r={ID}&confirm=1&t=1`
  - Single-file download: `{SITE-PREFIX}/files/file/{FILE}/`
  - Attachments: `{SITE-PREFIX}/applications/core/interface/file/attachment.php?id={ID}`

| Site | Requires Login | Requires Whitelist Entry | Meta File Layout | Example | Notes |
|------|----------------|--------------------------|------------------|---------|-------|
| [LoversLab](https://www.loverslab.com/) | Yes | No | IPS4 Site Prefix: `https://www.loverslab.com` | `directURL=https://www.loverslab.com/files/file/11116-test-file-for-wabbajack-integration/?do=download&r=737123&confirm=1&t=1` | IPS4 Site, mostly multi-file downloads |
| [VectorPlexus](https://vectorplexus.com/) | Yes | No | IPS4 Site Prefix: `https://vectorplexus.com` | `directURL=https://vectorplexus.com/files/file/290-wabbajack-test-file` | IPS4 Site, mostly multi-file downloads |
| [TES Alliance](http://tesalliance.org/) | Yes | No | IPS4 Site Prefix: `http://tesalliance.org/forums/index.php?` | `directURL=http://tesalliance.org/forums/index.php?/files/file/2035-wabbajack-test-file/` | IPS4 Site, mostly single-file downloads |
| [TESAll](https://tesall.ru) | Yes | No | IPS4 Site Prefix: `https://tesall.ru` | `directURL=https://tesall.ru/files/getdownload/594545-wabbajack-test-file/` | IPS4 Site, mostly single-file downloads |
| [ModDB](https://www.moddb.com/) | No | No | `directURL=https://www.moddb.com/downloads/start/{ID}` | `https://www.moddb.com/downloads/start/124908` | Downloads can be very slow |
| [Patreon](https://www.patreon.com/) | No | Yes | `directURL=https://www.patreon.com/file?h={ID1}i={ID2}` | `directURL=https://www.patreon.com/file?h=34874668&i=5247431` | Only public downloads, paywalled downloads can not be downloaded |
| [GitHub](https://github.com/) | No | No | `directURL=https://github.com/{USER}/{REPO}/releases/download/{TAG}/{FILE}` | `directURL=https://github.com/ModOrganizer2/modorganizer/releases/download/v2.3.1/Mod.Organizer-2.3.1.7z` | |
| [Google Drive](https://drive.google.com/) | No | Yes | `directURL=https://drive.google.com/file/d/{ID}` | `directURL=https://drive.google.com/file/d/1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_/` | Google Drive is known to not be a reliable file hosting service as a file can get temporarily inaccessible if too many users try to download it |
| [MEGA](https://mega.nz/) | No (Optional) | Yes | `directURL=https://mega.nz/#!{ID}` | `directURL=https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k` | MEGA has a 5GB transfer quota on non-premium users, even less for non-registered ones. It is recommended to login to MEGA before downloading any files |
| [Mediafire](https://www.mediafire.com/) | No | Yes | `directURL=https://www.mediafire.com/file/{FILE}` | `directURL=http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.txt` | Medafire downloads are known to fail from time to time |
| [Dropbox](https://www.dropbox.com/) | No | Yes | `directURL=https://www.dropbox.com/s/{FILE}?dl=0` | `directURL=https://www.dropbox.com/s/5hov3m2pboppoc2/WABBAJACK_TEST_FILE.txt?dl=0` | |
| [Yandex Disk](https://disk.yandex.com/) | No | Yes | `directURL=https://yadi.sk/d/{ID}` | `directURL=https://yadi.sk/d/jqwQT4ByYtC9Tw` | |

## FAQ

**How does Wabbajack differ from Automaton?**

I, halgari, used to be a developer working on Automaton. Sadly development was moving a bit too slowly for my liking, and I realized that a complete rewrite would allow the implementation of some really nice features (like BSA packing). As such I made the decision to strike out on my own and make an app that worked first, and then make it pretty. The end result is an app with a ton of features, and a less than professional UI. But that's my motto when coding "_make it work, then make it pretty_".

**Can I charge for a Wabbajack Modlist I created?**

No, as specified in the [License](#license--copyright), Wabbajack Modlists must be available for free. Any payment in exchange for access to a Wabbajack installer is strictly prohibited. This includes paywalling, "pay for beta access", "pay for current version, previous version is free", or any sort of other quid-pro-quo monetization structure. The Wabbajack team reserves the right to implement software that will prohibit the installation of any lists that are paywalled.

**Can I accept donations for my installer?**

Absolutely! As long as the act of donating does not entitle the donator to access to the installer. The installer must be free, donations must be a "thank you" - not a purchase of services or content.

### For Mod Authors

**How does Wabbajack download mods from the Nexus?**

Wabbajack uses the official [Nexus API](https://app.swaggerhub.com/apis-docs/NexusMods/nexus-mods_public_api_params_in_form_data/1.0#/) to retrieve download links from the Nexus. Mod Managers such as MO2 or Vortex also use this API to download files. Downloading using the API is the same as downloading directly from the website, both will increase your download count and give you donation points.

**How can I opt out of having my mod be included in a Modlist?**

As explained before:

> We use the official [Nexus API](https://app.swaggerhub.com/apis-docs/NexusMods/nexus-mods_public_api_params_in_form_data/1.0#/) to retrieve download links from the Nexus.

Everyone who has access to the Nexus can download your mod. The Nexus does not and can not lock out Wabbajack from using the API to download a specific mod based on _author preferences_.

**Will the end user even know they use my mod?**

Your mod is exposed in several layers of the user experience when installing a Modlist. Before the installation even starts, the user has access to the manifest of the Modlist. This contains a list of all mods to be installed as well as the authors, version, size, links and more meta data depending on origin.

Wabbajack will start a Slideshow during installation which features all mods to be installed in random order. The Slideshow displays the title, author, main image, description, version and a link to the Nexus page.

After installation the user most likely needs to check the instructions of the Modlist for recommended MCM options. If your mod has an MCM and needs a lot of configuring than your mod will likely be featured in the instructions.

Some Modlists also have an extensive README and we highly encourage new Modlist Authors to add a section about important mods to their README (see [Post-Compilation](#post-compilation)).

**What if my mod is not on the Nexus?**

You can check all sites we can download from [here](#meta-files) and we can easily add support for other sites. As long as your mod is publicly accessible and available on the Internet, Wabbajack can probably download it. Even if the site requires a login and does not have an API, we can always just resort to our internal browser and download the mod as if a user would go to the website using Firefox/Chrome and click the download button.

## License & Copyright

All original code in Wabbajack is given freely via the [GPL3 license](LICENSE.txt). Parts of Wabbajack use libraries that carry their own Open Sources licenses, those parts retain their original copyrights. Selling of Modlist files is strictly forbidden. As is hosting the files behind any sort of paywall. You received this tool free of charge, respect this by giving freely as you were given.

## Contributing

Look at the [`CONTRIBUTING.md`](https://github.com/halgari/wabbajack/blob/master/CONTRIBUTING.md) file for detailed guidelines.

## Thanks to

Our testers and Discord members who encourage development and help test the builds.
