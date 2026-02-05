import { useState, useCallback, useEffect, useRef } from 'react';
import {
  validatePaths,
  checkNexusLogin,
  checkGameFiles,
  checkManualDownloads,
  checkDiskSpace,
  moveDownload,
  type PathValidationResult,
  type NexusLoginStatus,
  type GameFilesCheckResult,
  type ManualDownloadsCheckResult,
  type DiskSpaceCheckResult,
} from '../api/checklist';
import { subscribeToEvents, type ServerEvent } from '../api/client';

// ============================================================================
// Types
// ============================================================================

export type ChecklistStepStatus =
  | 'pending'
  | 'checking'
  | 'passed'
  | 'failed'
  | 'warning';

export interface ChecklistProgress {
  checkType: string;
  progress: number;
  message: string;
}

export interface HashProgress {
  fileName: string;
  bytesHashed: number;
  totalBytes: number;
  progress: number;
}

export interface UsePreInstallChecklistResult {
  // Folder state
  installFolder: string;
  setInstallFolder: (folder: string) => void;
  downloadFolder: string;
  setDownloadFolder: (folder: string) => void;

  // Check results
  pathValidation: PathValidationResult | null;
  nexusLogin: NexusLoginStatus | null;
  gameFilesCheck: GameFilesCheckResult | null;
  manualDownloadsCheck: ManualDownloadsCheckResult | null;
  diskSpaceCheck: DiskSpaceCheckResult | null;

  // Step statuses
  pathsStatus: ChecklistStepStatus;
  pathsError: string | null;
  nexusStatus: ChecklistStepStatus;
  gameFilesStatus: ChecklistStepStatus;
  manualDownloadsStatus: ChecklistStepStatus;
  diskSpaceStatus: ChecklistStepStatus;

  // Progress (from SSE)
  scanProgress: ChecklistProgress | null;
  hashProgress: HashProgress | null;

  // Actions
  runPathValidation: () => Promise<void>;
  runNexusCheck: () => Promise<void>;
  runGameFilesCheck: () => Promise<void>;
  runManualDownloadsCheck: () => Promise<void>;
  runDiskSpaceCheck: () => Promise<void>;
  moveFileToDownloads: (sourcePath: string) => Promise<boolean>;
  runAllChecks: () => Promise<void>;

  // Overall state
  canProceed: boolean;
  blockingIssues: string[];
  isChecking: boolean;
}

// ============================================================================
// Hook Implementation
// ============================================================================

export function usePreInstallChecklist(
  sessionId: string
): UsePreInstallChecklistResult {
  // Folder state
  const [installFolder, setInstallFolder] = useState('');
  const [downloadFolder, setDownloadFolder] = useState('');

  // Check results
  const [pathValidation, setPathValidation] =
    useState<PathValidationResult | null>(null);
  const [nexusLogin, setNexusLogin] = useState<NexusLoginStatus | null>(null);
  const [gameFilesCheck, setGameFilesCheck] =
    useState<GameFilesCheckResult | null>(null);
  const [manualDownloadsCheck, setManualDownloadsCheck] =
    useState<ManualDownloadsCheckResult | null>(null);
  const [diskSpaceCheck, setDiskSpaceCheck] =
    useState<DiskSpaceCheckResult | null>(null);

  // Step statuses
  const [pathsStatus, setPathsStatus] = useState<ChecklistStepStatus>('pending');
  const [pathsError, setPathsError] = useState<string | null>(null);
  const [nexusStatus, setNexusStatus] = useState<ChecklistStepStatus>('pending');
  const [gameFilesStatus, setGameFilesStatus] =
    useState<ChecklistStepStatus>('pending');
  const [manualDownloadsStatus, setManualDownloadsStatus] =
    useState<ChecklistStepStatus>('pending');
  const [diskSpaceStatus, setDiskSpaceStatus] =
    useState<ChecklistStepStatus>('pending');

  // Progress state
  const [scanProgress, setScanProgress] = useState<ChecklistProgress | null>(
    null
  );
  const [hashProgress, setHashProgress] = useState<HashProgress | null>(null);

  // Checking state
  const [isChecking, setIsChecking] = useState(false);

  // SSE subscription ref
  const unsubscribeRef = useRef<(() => void) | null>(null);

  // Subscribe to SSE events
  useEffect(() => {
    const handleEvent = (event: ServerEvent) => {
      if (event.type === 'ChecklistProgress') {
        const data = event.data as {
          sessionId: string;
          checkType: string;
          progress: number;
          message: string;
        };
        if (data.sessionId === sessionId) {
          setScanProgress({
            checkType: data.checkType,
            progress: data.progress,
            message: data.message,
          });
        }
      } else if (event.type === 'HashProgress') {
        const data = event.data as {
          sessionId: string;
          fileName: string;
          bytesHashed: number;
          totalBytes: number;
          progress: number;
        };
        if (data.sessionId === sessionId) {
          setHashProgress({
            fileName: data.fileName,
            bytesHashed: data.bytesHashed,
            totalBytes: data.totalBytes,
            progress: data.progress,
          });
        }
      }
    };

    unsubscribeRef.current = subscribeToEvents(handleEvent);

    return () => {
      if (unsubscribeRef.current) {
        unsubscribeRef.current();
      }
    };
  }, [sessionId]);

  // Actions
  const runPathValidation = useCallback(async () => {
    if (!installFolder || !downloadFolder) {
      setPathsStatus('pending');
      setPathsError(null);
      return;
    }

    setPathsStatus('checking');
    setPathsError(null);
    try {
      const response = await validatePaths(sessionId, {
        installFolder,
        downloadFolder,
      });

      if (response.success && response.data) {
        setPathValidation(response.data);
        const hasErrors =
          !response.data.installFolder.isValid ||
          !response.data.downloadFolder.isValid;
        const hasWarnings =
          response.data.installFolder.warnings.length > 0 ||
          response.data.downloadFolder.warnings.length > 0;
        setPathsStatus(hasErrors ? 'failed' : hasWarnings ? 'warning' : 'passed');
      } else {
        setPathsError(response.error || 'Validation request failed');
        setPathsStatus('failed');
      }
    } catch (error) {
      console.error('Path validation failed:', error);
      setPathsError(error instanceof Error ? error.message : 'Failed to connect to server');
      setPathsStatus('failed');
    }
  }, [sessionId, installFolder, downloadFolder]);

  const runNexusCheck = useCallback(async () => {
    setNexusStatus('checking');
    try {
      const response = await checkNexusLogin();
      if (response.success && response.data) {
        setNexusLogin(response.data);
        setNexusStatus(response.data.isLoggedIn ? 'passed' : 'failed');
      } else {
        setNexusStatus('failed');
      }
    } catch (error) {
      console.error('Nexus login check failed:', error);
      setNexusStatus('failed');
    }
  }, []);

  const runGameFilesCheck = useCallback(async () => {
    setGameFilesStatus('checking');
    setScanProgress(null);
    setHashProgress(null);

    try {
      const response = await checkGameFiles(sessionId);
      if (response.success && response.data) {
        setGameFilesCheck(response.data);
        const allFound = response.data.files.every((f) => f.status === 'found');
        setGameFilesStatus(allFound ? 'passed' : 'failed');
      } else {
        setGameFilesStatus('failed');
      }
    } catch (error) {
      console.error('Game files check failed:', error);
      setGameFilesStatus('failed');
    } finally {
      setScanProgress(null);
      setHashProgress(null);
    }
  }, [sessionId]);

  const runManualDownloadsCheck = useCallback(async () => {
    setManualDownloadsStatus('checking');
    setScanProgress(null);

    try {
      const response = await checkManualDownloads(sessionId, {
        downloadFolder,
      });
      if (response.success && response.data) {
        setManualDownloadsCheck(response.data);
        const allReady = response.data.files.every((f) => f.status === 'ready');
        const hasMissing = response.data.files.some((f) => f.status === 'missing');
        setManualDownloadsStatus(
          allReady ? 'passed' : hasMissing ? 'failed' : 'warning'
        );
      } else {
        setManualDownloadsStatus('failed');
      }
    } catch (error) {
      console.error('Manual downloads check failed:', error);
      setManualDownloadsStatus('failed');
    } finally {
      setScanProgress(null);
    }
  }, [sessionId, downloadFolder]);

  const runDiskSpaceCheck = useCallback(async () => {
    if (!installFolder || !downloadFolder) {
      setDiskSpaceStatus('pending');
      return;
    }

    setDiskSpaceStatus('checking');
    try {
      const response = await checkDiskSpace(sessionId, {
        installFolder,
        downloadFolder,
      });
      if (response.success && response.data) {
        setDiskSpaceCheck(response.data);
        const hasEnoughSpace =
          response.data.downloadDrive.hasEnoughSpace &&
          response.data.installDrive.hasEnoughSpace;
        setDiskSpaceStatus(hasEnoughSpace ? 'passed' : 'failed');
      } else {
        setDiskSpaceStatus('failed');
      }
    } catch (error) {
      console.error('Disk space check failed:', error);
      setDiskSpaceStatus('failed');
    }
  }, [sessionId, installFolder, downloadFolder]);

  const moveFileToDownloads = useCallback(
    async (sourcePath: string): Promise<boolean> => {
      try {
        const response = await moveDownload(sessionId, {
          sourcePath,
          downloadFolder,
        });
        if (response.success && response.data) {
          // Re-run manual downloads check after moving
          await runManualDownloadsCheck();
          return true;
        }
        return false;
      } catch (error) {
        console.error('Move file failed:', error);
        return false;
      }
    },
    [sessionId, downloadFolder, runManualDownloadsCheck]
  );

  const runAllChecks = useCallback(async () => {
    setIsChecking(true);
    try {
      // Run checks in sequence with visual delay for better UX
      await runPathValidation();
      await runNexusCheck();
      await runGameFilesCheck();
      await runManualDownloadsCheck();
      await runDiskSpaceCheck();
    } finally {
      setIsChecking(false);
    }
  }, [
    runPathValidation,
    runNexusCheck,
    runGameFilesCheck,
    runManualDownloadsCheck,
    runDiskSpaceCheck,
  ]);

  // Compute overall state
  const blockingIssues: string[] = [];

  if (pathsStatus === 'failed') {
    // Add specific path errors instead of generic message
    if (pathValidation) {
      pathValidation.installFolder.errors.forEach((err) =>
        blockingIssues.push(`Install folder: ${err}`)
      );
      pathValidation.downloadFolder.errors.forEach((err) =>
        blockingIssues.push(`Downloads folder: ${err}`)
      );
    } else {
      blockingIssues.push('Install or download folder has errors');
    }
  }
  if (nexusStatus === 'failed') {
    blockingIssues.push('Not logged into Nexus Mods');
  }
  if (gameFilesStatus === 'failed') {
    blockingIssues.push('Game files are missing or mismatched');
  }
  if (manualDownloadsStatus === 'failed') {
    blockingIssues.push('Manual downloads are missing');
  }
  if (diskSpaceStatus === 'failed') {
    blockingIssues.push('Not enough disk space');
  }

  const canProceed =
    pathsStatus === 'passed' &&
    nexusStatus === 'passed' &&
    (gameFilesStatus === 'passed' || gameFilesCheck?.files.length === 0) &&
    (manualDownloadsStatus === 'passed' ||
      manualDownloadsCheck?.files.length === 0) &&
    diskSpaceStatus === 'passed';

  return {
    // Folder state
    installFolder,
    setInstallFolder,
    downloadFolder,
    setDownloadFolder,

    // Check results
    pathValidation,
    nexusLogin,
    gameFilesCheck,
    manualDownloadsCheck,
    diskSpaceCheck,

    // Step statuses
    pathsStatus,
    pathsError,
    nexusStatus,
    gameFilesStatus,
    manualDownloadsStatus,
    diskSpaceStatus,

    // Progress
    scanProgress,
    hashProgress,

    // Actions
    runPathValidation,
    runNexusCheck,
    runGameFilesCheck,
    runManualDownloadsCheck,
    runDiskSpaceCheck,
    moveFileToDownloads,
    runAllChecks,

    // Overall state
    canProceed,
    blockingIssues,
    isChecking,
  };
}
