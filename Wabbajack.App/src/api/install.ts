import { ApiResponse } from './client';

const API_BASE = 'http://localhost:13373';

// Pre-install check types
export interface ModlistPrepareRequest {
  downloadUrl: string;
  machineUrl: string;
}

export interface ModlistPrepareStatus {
  sessionId: string;
  status: 'downloading' | 'extracting' | 'ready' | 'error';
  progress: number;
  error?: string;
}

export interface ModlistBasicInfo {
  name: string;
  author: string;
  description: string;
  version: string;
  gameType: string;
  gameDisplayName: string;
  isNsfw: boolean;
  website?: string;
  readme?: string;
}

export interface InstallationRequirements {
  archiveCount: number;
  totalArchiveSize: number;
  directiveCount: number;
  totalInstalledSize: number;
  estimatedTempSpace: number;
  gameInstalled: boolean;
  gamePath?: string;
  manualDownloadCount: number;
  nonAutomaticDownloadCount: number;
}

export interface PreInstallWarning {
  type: string;
  message: string;
}

export interface ModlistPreInstallInfo {
  sessionId: string;
  modlist: ModlistBasicInfo;
  requirements: InstallationRequirements;
  warnings: PreInstallWarning[];
}

/**
 * Start preparing a modlist for installation.
 * Downloads the .wabbajack file and extracts metadata.
 */
export async function prepareModlist(
  request: ModlistPrepareRequest
): Promise<ApiResponse<ModlistPrepareStatus>> {
  const response = await fetch(`${API_BASE}/api/modlist/prepare`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  });
  return response.json();
}

/**
 * Get the current status of a modlist preparation.
 */
export async function getModlistStatus(
  sessionId: string
): Promise<ApiResponse<ModlistPrepareStatus>> {
  const response = await fetch(`${API_BASE}/api/modlist/${sessionId}/status`);
  return response.json();
}

/**
 * Get the pre-install information for a prepared modlist.
 */
export async function getModlistInfo(
  sessionId: string
): Promise<ApiResponse<ModlistPreInstallInfo>> {
  const response = await fetch(`${API_BASE}/api/modlist/${sessionId}/info`);
  return response.json();
}
