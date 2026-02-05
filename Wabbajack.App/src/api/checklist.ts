import { ApiResponse } from './client';

const API_BASE = 'http://localhost:13373';

// ============================================================================
// Types
// ============================================================================

// Path validation
export interface ValidatePathsRequest {
  installFolder: string;
  downloadFolder: string;
}

export interface PathValidationResult {
  installFolder: FolderValidation;
  downloadFolder: FolderValidation;
}

export interface FolderValidation {
  isValid: boolean;
  errors: string[];
  warnings: string[];
}

// Game files check
export interface GameFilesCheckResult {
  status: string;
  totalFiles: number;
  checkedFiles: number;
  files: GameFileStatus[];
}

export interface GameFileStatus {
  relativePath: string;
  status: string; // 'found' | 'missing' | 'hash_mismatch' | 'size_mismatch'
  expectedHash: string;
  actualHash?: string;
}

// Manual downloads check
export interface CheckManualDownloadsRequest {
  downloadFolder: string;
}

export interface ManualDownloadsCheckResult {
  status: string;
  totalFiles: number;
  foundFiles: number;
  files: ManualDownloadStatus[];
}

export interface ManualDownloadStatus {
  name: string;
  url: string;
  prompt: string;
  status: string; // 'ready' | 'missing' | 'found_in_os_downloads' | 'hash_mismatch'
  expectedSize: number;
  expectedHash: string;
  foundPath?: string;
  favicon?: string;
}

// Move download
export interface MoveDownloadRequest {
  sourcePath: string;
  downloadFolder: string;
}

// Disk space check
export interface CheckDiskSpaceRequest {
  installFolder: string;
  downloadFolder: string;
}

export interface DiskSpaceCheckResult {
  downloadDrive: DriveSpaceInfo;
  installDrive: DriveSpaceInfo;
  areSameDrive: boolean;
}

export interface DriveSpaceInfo {
  drivePath: string;
  availableSpace: number;
  requiredSpace: number;
  hasEnoughSpace: boolean;
}

// Nexus login check
export interface NexusLoginStatus {
  isLoggedIn: boolean;
  username?: string;
}

// Full checklist state
export interface PreInstallChecklistState {
  sessionId: string;
  pathValidation?: PathValidationResult;
  nexusLogin?: NexusLoginStatus;
  gameFilesCheck?: GameFilesCheckResult;
  manualDownloadsCheck?: ManualDownloadsCheckResult;
  diskSpaceCheck?: DiskSpaceCheckResult;
  canProceed: boolean;
  blockingIssues: string[];
}

// ============================================================================
// API Functions
// ============================================================================

/**
 * Check Nexus Mods login status.
 */
export async function checkNexusLogin(): Promise<ApiResponse<NexusLoginStatus>> {
  const response = await fetch(`${API_BASE}/api/auth/nexus/status`);
  return response.json();
}

/**
 * Validate install and download folder paths.
 */
export async function validatePaths(
  sessionId: string,
  request: ValidatePathsRequest
): Promise<ApiResponse<PathValidationResult>> {
  const response = await fetch(
    `${API_BASE}/api/modlist/${sessionId}/validate-paths`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }
  );
  return response.json();
}

/**
 * Check game files required by the modlist.
 */
export async function checkGameFiles(
  sessionId: string
): Promise<ApiResponse<GameFilesCheckResult>> {
  const response = await fetch(
    `${API_BASE}/api/modlist/${sessionId}/check-game-files`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: '{}',
    }
  );
  return response.json();
}

/**
 * Check manual downloads required by the modlist.
 */
export async function checkManualDownloads(
  sessionId: string,
  request: CheckManualDownloadsRequest
): Promise<ApiResponse<ManualDownloadsCheckResult>> {
  const response = await fetch(
    `${API_BASE}/api/modlist/${sessionId}/check-manual-downloads`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }
  );
  return response.json();
}

/**
 * Move a download file from source to downloads folder.
 */
export async function moveDownload(
  sessionId: string,
  request: MoveDownloadRequest
): Promise<ApiResponse<boolean>> {
  const response = await fetch(
    `${API_BASE}/api/modlist/${sessionId}/move-download`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }
  );
  return response.json();
}

/**
 * Check disk space for download and install folders.
 */
export async function checkDiskSpace(
  sessionId: string,
  request: CheckDiskSpaceRequest
): Promise<ApiResponse<DiskSpaceCheckResult>> {
  const response = await fetch(
    `${API_BASE}/api/modlist/${sessionId}/check-disk-space`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }
  );
  return response.json();
}

/**
 * Get the full checklist state for a session.
 */
export async function getChecklistState(
  sessionId: string
): Promise<ApiResponse<PreInstallChecklistState>> {
  const response = await fetch(
    `${API_BASE}/api/modlist/${sessionId}/checklist`
  );
  return response.json();
}
