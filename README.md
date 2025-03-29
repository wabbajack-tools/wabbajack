# Wabbajack

[![Discord](https://img.shields.io/discord/605449136870916175)](https://www.wabbajack.org/discord)
[![CI Tests](https://github.com/wabbajack-tools/wabbajack/actions/workflows/tests.yaml/badge.svg)](https://github.com/wabbajack-tools/wabbajack/actions/workflows/tests.yaml)
[![GitHub all releases](https://img.shields.io/github/downloads/wabbajack-tools/wabbajack/total)](https://github.com/wabbajack-tools/wabbajack/releases)

Wabbajack is an automated Modlist Installer that can reproduce an entire modding setup on another machine without bundling any assets or re-distributing any mods.

## Social Links

- [wabbajack.org](https://www.wabbajack.org) The official Wabbajack website with a [Gallery](https://www.wabbajack.org/#/modlists/gallery), [Status Dashboard](https://www.wabbajack.org/#/modlists/status) and [Archive Search](https://www.wabbajack.org/#/modlists/search/all) for official Modlists.
- [wiki.wabbajack.org](https://wiki.wabbajack.org/) The official Wabbajack documentation, wiki & FAQ
- [Discord](https://www.wabbajack.org/discord) The official Wabbajack discord for instructions, support or friendly chatting with fellow modders.
- [Patreon](https://www.patreon.com/user?u=11907933) contains update posts and keeps the [Code Signing Certificate](https://www.digicert.com/code-signing/) as well as our supplementary build server alive.

## Supported Games and Mod Manager

[Described in this Wiki page.](https://wiki.wabbajack.org/user_documentation/Supported%20Games%20and%20Mod%20Managers.html)

## Installing a Modlist

[Described in this Wiki page.](https://wiki.wabbajack.org/user_documentation/Installing%20a%20Modlist.html)

## Creating your own Modlist

[Described in this Wiki section](https://wiki.wabbajack.org/modlist_author_documentation/Compilation.html)

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
