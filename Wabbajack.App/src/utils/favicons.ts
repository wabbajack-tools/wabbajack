/**
 * Known favicon URLs for popular download sites.
 */
const KNOWN_FAVICONS: Record<string, string> = {
  'nexusmods.com': 'https://www.nexusmods.com/favicon.ico',
  'www.nexusmods.com': 'https://www.nexusmods.com/favicon.ico',
  'loverslab.com': 'https://www.loverslab.com/favicon.ico',
  'www.loverslab.com': 'https://www.loverslab.com/favicon.ico',
  'patreon.com': 'https://c5.patreon.com/external/favicon/favicon.ico',
  'www.patreon.com': 'https://c5.patreon.com/external/favicon/favicon.ico',
  'drive.google.com': 'https://ssl.gstatic.com/docs/doclist/images/drive_2022q3_32dp.png',
  'mega.nz': 'https://mega.nz/favicon.ico',
  'mega.co.nz': 'https://mega.nz/favicon.ico',
  'mediafire.com': 'https://www.mediafire.com/favicon.ico',
  'www.mediafire.com': 'https://www.mediafire.com/favicon.ico',
  'moddb.com': 'https://www.moddb.com/favicon.ico',
  'www.moddb.com': 'https://www.moddb.com/favicon.ico',
  'github.com': 'https://github.githubassets.com/favicons/favicon.svg',
  'dropbox.com': 'https://cfl.dropboxstatic.com/static/images/favicon.ico',
  'www.dropbox.com': 'https://cfl.dropboxstatic.com/static/images/favicon.ico',
};

/**
 * Gets the favicon URL for a given download URL.
 * Returns null if the host is unknown and should use a fallback.
 */
export function getFaviconForUrl(url: string): string | null {
  if (!url) return null;

  try {
    const parsedUrl = new URL(url);
    const host = parsedUrl.host.toLowerCase();

    // Check known favicons
    if (KNOWN_FAVICONS[host]) {
      return KNOWN_FAVICONS[host];
    }

    // Fallback to generic favicon
    return `https://${host}/favicon.ico`;
  } catch {
    return null;
  }
}

/**
 * Gets a display-friendly name for a download site.
 */
export function getSiteNameForUrl(url: string): string {
  if (!url) return 'Unknown';

  try {
    const parsedUrl = new URL(url);
    const host = parsedUrl.host.toLowerCase().replace('www.', '');

    const siteNames: Record<string, string> = {
      'nexusmods.com': 'Nexus Mods',
      'loverslab.com': 'LoversLab',
      'patreon.com': 'Patreon',
      'drive.google.com': 'Google Drive',
      'mega.nz': 'MEGA',
      'mega.co.nz': 'MEGA',
      'mediafire.com': 'MediaFire',
      'moddb.com': 'ModDB',
      'github.com': 'GitHub',
      'dropbox.com': 'Dropbox',
    };

    return siteNames[host] || host;
  } catch {
    return 'Unknown';
  }
}
