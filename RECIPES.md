## Instructions on how to configure specific mods/utilities for use in Wabbajack

#### Manually downloaded Nexus files
MO2 can get archive info from random files in the download folder

1) Download the file manually
2) Copy the file into the MO2 downloads folder
3) In MO2 click `Query Info` on the file (may need to refresh the GUI)
4) The red icon should go away if MO2 found the query info.
5) Wabbajack will find the info queried by MO2

#### Mod Organizer 2
Comes from the Nexus as a Nexus mod. 

1) Download MO2 (archive version)
2) Extract it into a folder
3) Start it in portable version and select your game
4) Copy the MO2 archive into the `downloads` folder in the MO2 install folder
5) Open MO2 go to downloads, right click on the MO2 archive and click `Query Info` MO2 will
automatically link the file to the nexus modID and fileID. These values will be found and used
by wabbjack

#### SKSE
SKSE Servers support direct URL downloads

1) Download SKSE and copy the binary files into your game folder
2) Copy the SKSE archive into the `downloads` folder in MO2. 
3) Install the SKSE archive through MO2 from the downloads tab
4) In a file browser, go to the MO2 `downloads` folder and load the `skse*.7z.meta` file, and add
the following line to the `[General]` section, changing the URL to the exact URL you used to download skse:

```ini
directURL=https://skse.silverlock.org/beta/skse64_2_00_16.7z
```

Your entire `skse*.7z.meta` file should now look like this

```ini
[General]
installed=true
uninstalled=false
directURL=https://skse.silverlock.org/beta/skse64_2_00_16.7z
```

#### ENB Series
ENB Series Servers support direct downloads as long as a header is provided.

1) Download the ENBSeries archive and copy the binary files into your game folder
2) Copy the ENBSeries archive into your downloads folder in MO2
3) Create a `.meta` file with the same name prefix as the ENBSeries archive name. So if the ENBSeries
archive is named `enb-foo.bar.7z` make a text file named `enb-foo.bar.7z.meta`
4) Update the contents of the `.meta` file to look like this, updating the URL to the exact URL
used to download the ENB (not the ENB landing page, but the download URL)
5) The Referer value may need to be updated for games that are not Skyrim SE

```ini
[General]
installed=true
uninstalled=false
directURL=http://enbdev.com/enbseries_skyrimse_v0390.zip
directURLHeaders=Referer:http://enbdev.com/download_mod_tesskyrimse.html
```

#### SSE Engine Fixes
Both files come from the nexus. 

1) Download and install Part 1 via MO2
2) Download Part 2 manually, copy the binaries into the MO2 folder
3) Copy the Part 2 archive into the `downloads` folder in MO2
4) Use MO2 to `Query Info` on the Part 2 file

#### SSEdit BethINI

#### Skyrim Realistic Overhaul
SRO is only stored on the ModDB servers

1) Download the files manually
2) Copy them into the MO2 downloads folder
3) Install them via MO2
4) For each file, go back to ModDB, and right click on the download button clicking "copy link as"
5) Update the `.meta` file for each archive to look something like this

```ini
[General]
installed=true
uninstalled=false
directURL=https://www.moddb.com/downloads/start/116934?referer=https%3A%2F%2Fwww.moddb.com%2Fmods%2Fskyrim-realistic-overhaul%2Fdownloads
```

If the general format of the URL does not match what you see here, you probably have the wrong link, you want the link from 
the download button of the file's page, the link that will take you to the "your download is starting now" page. 

#### Dropbox Files
If you have a file that comes from Dropbox, the process is simple:

1) Download the file
2) Copy it into the MO2 downloads folder
3) Install it via MO2
4) Update the .meta file for the archive to point to the dropbox URL