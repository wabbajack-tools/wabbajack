// Modlist data types - matches the format from GitHub raw files

export interface ModlistLinks {
  image: string;
  readme: string;
  download: string;
  machineURL: string;
  discordURL?: string;
  websiteURL?: string;
}

export interface DownloadMetadata {
  Hash: string;
  Size: number;
  NumberOfArchives: number;
  SizeOfArchives: number;
  NumberOfInstalledFiles: number;
  SizeOfInstalledFiles: number;
  TotalSize: number;
}

export interface ModlistMetadata {
  title: string;
  description: string;
  author: string;
  maintainers?: string[];
  game: string;
  official: boolean;
  tags: string[];
  nsfw: boolean;
  utility_list: boolean;
  image_contains_title: boolean;
  force_down: boolean;
  links: ModlistLinks;
  download_metadata?: DownloadMetadata;
  version: string;
  dateCreated: string;
  dateUpdated: string;
  // Enriched fields (added during fetch)
  repositoryName: string;
  namespacedName?: string;
}

export interface Repositories {
  [key: string]: string;
}

// Game display names - derived from Wabbajack.DTOs/Game/Game.cs
export const gameDisplayNames: Record<string, string> = {
  sevendaystodie: '7 Days to Die',
  baldursgate3: "Baldur's Gate 3",
  cyberpunk2077: 'Cyberpunk 2077',
  darkestdungeon: 'Darkest Dungeon',
  dishonored: 'Dishonored',
  dragonage2: 'Dragon Age 2',
  dragonageinquisition: 'Dragon Age: Inquisition',
  dragonageorigins: 'Dragon Age: Origins',
  dragonsdogma2: "Dragon's Dogma 2",
  dragonsdogma: "Dragon's Dogma: Dark Arisen",
  enderal: 'Enderal',
  enderalspecialedition: 'Enderal Special Edition',
  fallout3: 'Fallout 3',
  fallout4: 'Fallout 4',
  fallout4vr: 'Fallout 4 VR',
  fallout76: 'Fallout 76',
  falloutnewvegas: 'Fallout New Vegas',
  fallout4london: 'Fallout: London',
  finalfantasy7remake: 'Final Fantasy VII Remake',
  karrynsprison: "Karryn's Prison",
  kerbalspaceprogram: 'Kerbal Space Program',
  kingdomcomedeliverance: 'Kingdom Come: Deliverance',
  kingdomcomedeliverance2: 'Kingdom Come: Deliverance II',
  mechwarrior5mercenaries: 'MechWarrior 5: Mercenaries',
  moddingtools: 'Modding Tools',
  morrowind: 'Morrowind',
  mountandblade2bannerlord: 'Mount & Blade II: Bannerlord',
  nomanssky: "No Man's Sky",
  oblivion: 'Oblivion',
  oblivionremastered: 'Oblivion Remastered',
  skyrim: 'Skyrim Legendary Edition',
  skyrimspecialedition: 'Skyrim Special Edition',
  skyrimvr: 'Skyrim VR',
  kotor2: 'STAR WARS Knights of the Old Republic II',
  starfield: 'Starfield',
  stardewvalley: 'Stardew Valley',
  sims4: 'The Sims 4',
  terraria: 'Terraria',
  valheim: 'Valheim',
  vtmb: 'Vampire: The Masquerade - Bloodlines',
  warhammer40kdarktide: 'Warhammer 40,000: Darktide',
  witcher: 'Witcher: Enhanced Edition',
  witcher3: 'Witcher 3',
};

export function getGameDisplayName(gameId: string): string {
  return gameDisplayNames[gameId.toLowerCase()] || gameId;
}

// Constants
export const API_BASE_URL = 'https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master';
export const FALLBACK_MODLIST_IMAGE = 'https://raw.githubusercontent.com/wabbajack-tools/wabbajack/refs/heads/main/Branding/PNGs/Wabba_Mouth_Small.png';
