# Wabbajack.Translation

Finds translation patches on Nexus Mods for the plugins in an installed modlist, merges them into one plugin
that keeps the modlists own overrides, switches the game language and optionally fetches the languages voice files from Steam.

## Mutagen

Plugin readingmerging and writing is done with [Mutagen](https://github.com/Mutagen-Modding/Mutagen),
a C# library for analyzing, modifying and creating Bethesda mods, by Noggog and contributors. Mutagen is
licensed under the GNU General Public License v3.0, as is Wabbajack, and is used unmodified from its
published NuGet packages. Its source is at https://github.com/Mutagen-Modding/Mutagen.
