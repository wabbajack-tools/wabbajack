import { useEffect, useState } from 'react';
import { ModlistPreInstallInfo } from '../../api/install';
import { usePreInstallChecklist } from '../../hooks/usePreInstallChecklist';
import {
  ChecklistStep,
  GameFilesStep,
  ManualDownloadsStep,
  DiskSpaceStep,
} from './checklist';

interface PreInstallChecksPageProps {
  info: ModlistPreInstallInfo;
  onProceed: (downloadFolder: string, installFolder: string) => void;
  onCancel: () => void;
}

function formatSize(bytes: number): string {
  if (bytes === 0) return '0 B';
  const gb = bytes / (1024 * 1024 * 1024);
  if (gb >= 1) return `${gb.toFixed(2)} GB`;
  const mb = bytes / (1024 * 1024);
  if (mb >= 1) return `${mb.toFixed(0)} MB`;
  const kb = bytes / 1024;
  return `${kb.toFixed(0)} KB`;
}

export function PreInstallChecksPage({
  info,
  onProceed,
  onCancel,
}: PreInstallChecksPageProps) {
  const { modlist, requirements, sessionId } = info;
  const [hasRunInitialChecks, setHasRunInitialChecks] = useState(false);

  const checklist = usePreInstallChecklist(sessionId);

  // Auto-set download folder when install folder changes
  useEffect(() => {
    if (checklist.installFolder && !checklist.downloadFolder) {
      checklist.setDownloadFolder(`${checklist.installFolder}/downloads`);
    }
  }, [checklist.installFolder, checklist.downloadFolder, checklist.setDownloadFolder]);

  // Run initial checks when folders are set
  useEffect(() => {
    if (
      checklist.installFolder &&
      checklist.downloadFolder &&
      !hasRunInitialChecks
    ) {
      setHasRunInitialChecks(true);
      checklist.runAllChecks();
    }
  }, [checklist.installFolder, checklist.downloadFolder, hasRunInitialChecks, checklist.runAllChecks]);

  // Calculate overall progress
  const stepStatuses = [
    checklist.pathsStatus,
    checklist.nexusStatus,
    checklist.gameFilesStatus,
    checklist.manualDownloadsStatus,
    checklist.diskSpaceStatus,
  ];
  const passedCount = stepStatuses.filter((s) => s === 'passed').length;
  const overallProgress = passedCount / stepStatuses.length;

  // Folder input handler with Tauri dialog
  const handleBrowse = async (setter: (value: string) => void, title: string) => {
    if (window.__TAURI__) {
      try {
        const { open } = await import('@tauri-apps/plugin-dialog');
        const selected = await open({
          directory: true,
          multiple: false,
          title,
        });
        if (selected && typeof selected === 'string') {
          setter(selected);
        }
      } catch (err) {
        console.error('Failed to open folder dialog:', err);
      }
    }
  };

  return (
    <div className="space-y-6">
      {/* Back button */}
      <button
        onClick={onCancel}
        className="inline-flex items-center gap-2 text-text-muted hover:text-neon-purple transition-colors"
      >
        <svg
          className="h-5 w-5"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M10 19l-7-7m0 0l7-7m-7 7h18"
          />
        </svg>
        Back to Details
      </button>

      {/* Preflight Header */}
      <div className="glass rounded-2xl p-6 border border-neon-purple/30 relative overflow-hidden">
        {/* Background glow effect */}
        <div className="absolute inset-0 bg-gradient-to-br from-neon-purple/5 via-transparent to-neon-cyan/5" />

        <div className="relative">
          <div className="flex items-center gap-4">
            <div className="w-14 h-14 bg-gradient-to-br from-neon-purple to-neon-cyan rounded-xl flex items-center justify-center shadow-lg shadow-neon-purple/20">
              <svg
                className="w-7 h-7 text-white"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M13 10V3L4 14h7v7l9-11h-7z"
                />
              </svg>
            </div>
            <div>
              <h1 className="text-2xl font-bold font-display text-text-primary tracking-wide">
                PRE-FLIGHT CHECKLIST
              </h1>
              <p className="text-text-muted text-sm mt-0.5">
                {modlist.name} • {modlist.gameDisplayName}
              </p>
            </div>
          </div>

          {/* Overall Progress */}
          <div className="mt-6">
            <div className="flex items-center justify-between text-xs mb-2">
              <span className="text-text-muted">
                Step {passedCount + 1} of 6
              </span>
              <span className="text-neon-purple font-mono">
                {Math.round(overallProgress * 100)}%
              </span>
            </div>
            <div className="h-2 bg-surface-light/50 rounded-full overflow-hidden">
              <div
                className="h-full bg-gradient-to-r from-neon-purple via-neon-pink to-neon-cyan transition-all duration-500 ease-out"
                style={{ width: `${overallProgress * 100}%` }}
              />
            </div>
          </div>
        </div>
      </div>

      {/* Folder Configuration */}
      <div className="glass rounded-2xl p-6 space-y-4 border border-neon-purple/10">
        <div className="grid gap-4 md:grid-cols-2">
          {/* Install Folder */}
          <div className="space-y-2">
            <label className="flex items-center gap-2 text-sm font-medium text-text-primary">
              <svg
                className="w-4 h-4 text-neon-pink"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M5 8h14M5 8a2 2 0 110-4h14a2 2 0 110 4M5 8v10a2 2 0 002 2h10a2 2 0 002-2V8m-9 4h4"
                />
              </svg>
              Installation Folder
            </label>
            <div className="flex gap-2">
              <input
                type="text"
                value={checklist.installFolder}
                onChange={(e) => checklist.setInstallFolder(e.target.value)}
                placeholder="Select installation folder..."
                className={`flex-1 px-4 py-2 bg-surface-light/50 border rounded-lg text-text-primary placeholder-text-muted focus:outline-none focus:border-neon-purple/50 transition-colors ${
                  checklist.pathValidation?.installFolder?.errors?.length
                    ? 'border-error/50'
                    : 'border-neon-purple/20'
                }`}
              />
              <button
                onClick={() =>
                  handleBrowse(
                    checklist.setInstallFolder,
                    'Select Installation Folder'
                  )
                }
                className="px-4 py-2 bg-surface-light/50 hover:bg-surface-light border border-neon-purple/20 hover:border-neon-purple/40 rounded-lg text-text-secondary hover:text-text-primary transition-all"
              >
                Browse
              </button>
            </div>
            {checklist.pathValidation?.installFolder?.errors?.map((err, i) => (
              <p key={i} className="text-xs text-error">
                {err}
              </p>
            ))}
            {checklist.pathValidation?.installFolder?.warnings?.map((warn, i) => (
              <p key={i} className="text-xs text-warning">
                {warn}
              </p>
            ))}
          </div>

          {/* Download Folder */}
          <div className="space-y-2">
            <label className="flex items-center gap-2 text-sm font-medium text-text-primary">
              <svg
                className="w-4 h-4 text-neon-cyan"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"
                />
              </svg>
              Downloads Folder
            </label>
            <div className="flex gap-2">
              <input
                type="text"
                value={checklist.downloadFolder}
                onChange={(e) => checklist.setDownloadFolder(e.target.value)}
                placeholder="Select downloads folder..."
                className={`flex-1 px-4 py-2 bg-surface-light/50 border rounded-lg text-text-primary placeholder-text-muted focus:outline-none focus:border-neon-purple/50 transition-colors ${
                  checklist.pathValidation?.downloadFolder?.errors?.length
                    ? 'border-error/50'
                    : 'border-neon-purple/20'
                }`}
              />
              <button
                onClick={() =>
                  handleBrowse(
                    checklist.setDownloadFolder,
                    'Select Downloads Folder'
                  )
                }
                className="px-4 py-2 bg-surface-light/50 hover:bg-surface-light border border-neon-purple/20 hover:border-neon-purple/40 rounded-lg text-text-secondary hover:text-text-primary transition-all"
              >
                Browse
              </button>
            </div>
            {checklist.pathValidation?.downloadFolder?.errors?.map((err, i) => (
              <p key={i} className="text-xs text-error">
                {err}
              </p>
            ))}
            {checklist.pathValidation?.downloadFolder?.warnings?.map((warn, i) => (
              <p key={i} className="text-xs text-warning">
                {warn}
              </p>
            ))}
          </div>
        </div>
      </div>

      {/* Checklist Steps */}
      <div className="space-y-3">
        {/* Step 1: Path Validation */}
        <ChecklistStep
          stepNumber={1}
          title="INSTALL FOLDERS"
          status={checklist.pathsStatus}
          subtitle={
            checklist.pathsStatus === 'passed'
              ? 'Folders validated'
              : checklist.pathsStatus === 'checking'
                ? 'Validating...'
                : checklist.pathsStatus === 'failed'
                  ? 'Invalid folder configuration'
                  : 'Set folders above'
          }
          isActive={checklist.pathsStatus === 'checking'}
        />

        {/* Step 2: Nexus Login */}
        <ChecklistStep
          stepNumber={2}
          title="NEXUS MODS LOGIN"
          status={checklist.nexusStatus}
          subtitle={
            checklist.nexusStatus === 'passed'
              ? `Connected${checklist.nexusLogin?.username ? ` as ${checklist.nexusLogin.username}` : ''}`
              : checklist.nexusStatus === 'checking'
                ? 'Checking...'
                : checklist.nexusStatus === 'failed'
                  ? 'Not logged in'
                  : 'Pending'
          }
          isActive={checklist.nexusStatus === 'checking'}
        />

        {/* Step 3: Game Files */}
        <ChecklistStep
          stepNumber={3}
          title="GAME FILES"
          status={checklist.gameFilesStatus}
          subtitle={
            checklist.gameFilesStatus === 'passed'
              ? `${checklist.gameFilesCheck?.checkedFiles || 0} files verified`
              : checklist.gameFilesStatus === 'checking'
                ? checklist.scanProgress?.message || 'Verifying...'
                : checklist.gameFilesStatus === 'failed'
                  ? 'Files missing or mismatched'
                  : 'Pending'
          }
          progress={
            checklist.gameFilesStatus === 'checking'
              ? checklist.scanProgress?.progress
              : undefined
          }
          progressText={
            checklist.hashProgress
              ? `Hashing ${checklist.hashProgress.fileName}: ${Math.round(checklist.hashProgress.progress * 100)}%`
              : undefined
          }
          isActive={checklist.gameFilesStatus === 'checking'}
        >
          {checklist.gameFilesCheck && (
            <GameFilesStep
              result={checklist.gameFilesCheck}
              onRescan={checklist.runGameFilesCheck}
              isScanning={checklist.gameFilesStatus === 'checking'}
            />
          )}
        </ChecklistStep>

        {/* Step 4: Manual Downloads */}
        <ChecklistStep
          stepNumber={4}
          title="MANUAL DOWNLOADS"
          status={checklist.manualDownloadsStatus}
          subtitle={
            checklist.manualDownloadsStatus === 'passed'
              ? `${checklist.manualDownloadsCheck?.foundFiles || 0} files ready`
              : checklist.manualDownloadsStatus === 'checking'
                ? checklist.scanProgress?.message || 'Scanning...'
                : checklist.manualDownloadsStatus === 'failed'
                  ? 'Downloads needed'
                  : checklist.manualDownloadsStatus === 'warning'
                    ? 'Found in OS Downloads'
                    : 'Pending'
          }
          progress={
            checklist.manualDownloadsStatus === 'checking'
              ? checklist.scanProgress?.progress
              : undefined
          }
          isActive={checklist.manualDownloadsStatus === 'checking'}
        >
          {checklist.manualDownloadsCheck && (
            <ManualDownloadsStep
              result={checklist.manualDownloadsCheck}
              onMoveFile={checklist.moveFileToDownloads}
              onRescan={checklist.runManualDownloadsCheck}
              isScanning={checklist.manualDownloadsStatus === 'checking'}
            />
          )}
        </ChecklistStep>

        {/* Step 5: Disk Space */}
        <ChecklistStep
          stepNumber={5}
          title="DISK SPACE"
          status={checklist.diskSpaceStatus}
          subtitle={
            checklist.diskSpaceStatus === 'passed'
              ? 'Sufficient space available'
              : checklist.diskSpaceStatus === 'checking'
                ? 'Checking...'
                : checklist.diskSpaceStatus === 'failed'
                  ? 'Insufficient space'
                  : 'Pending'
          }
          isActive={checklist.diskSpaceStatus === 'checking'}
        >
          {checklist.diskSpaceCheck && (
            <DiskSpaceStep result={checklist.diskSpaceCheck} />
          )}
        </ChecklistStep>
      </div>

      {/* Requirements Summary */}
      <div className="glass rounded-xl p-4 border border-neon-purple/10">
        <div className="grid gap-4 md:grid-cols-3 text-xs">
          <div>
            <span className="text-text-muted">Download Size</span>
            <p className="text-text-primary font-mono">
              {formatSize(requirements.totalArchiveSize)}
            </p>
          </div>
          <div>
            <span className="text-text-muted">Install Size</span>
            <p className="text-text-primary font-mono">
              {formatSize(requirements.totalInstalledSize)}
            </p>
          </div>
          <div>
            <span className="text-text-muted">Temp Space</span>
            <p className="text-text-primary font-mono">
              ~{formatSize(requirements.estimatedTempSpace)}
            </p>
          </div>
        </div>
      </div>

      {/* Blocking Issues */}
      {checklist.blockingIssues.length > 0 && (
        <div className="glass rounded-xl p-4 border border-error/30 bg-error/5">
          <div className="flex items-start gap-3">
            <svg
              className="w-5 h-5 text-error flex-shrink-0 mt-0.5"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
            <div>
              <p className="text-sm font-medium text-error">
                Cannot proceed with installation:
              </p>
              <ul className="mt-1 text-sm text-error/80 list-disc list-inside">
                {checklist.blockingIssues.map((issue, i) => (
                  <li key={i}>{issue}</li>
                ))}
              </ul>
            </div>
          </div>
        </div>
      )}

      {/* Action Buttons */}
      <div className="flex gap-4 justify-between items-center">
        <button
          onClick={checklist.runAllChecks}
          disabled={checklist.isChecking || !checklist.installFolder || !checklist.downloadFolder}
          className="px-4 py-2 text-sm bg-surface-light/50 hover:bg-surface-light border border-neon-purple/20 hover:border-neon-purple/40 rounded-lg text-text-secondary hover:text-text-primary transition-all disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {checklist.isChecking ? 'Running checks...' : 'Re-run All Checks'}
        </button>

        <div className="flex gap-4">
          <button
            onClick={onCancel}
            className="px-6 py-3 bg-surface-light/50 hover:bg-surface-light text-text-secondary hover:text-text-primary rounded-lg transition-all border border-neon-purple/10 hover:border-neon-purple/30"
          >
            Cancel
          </button>
          <button
            onClick={() =>
              onProceed(checklist.downloadFolder, checklist.installFolder)
            }
            disabled={!checklist.canProceed || checklist.isChecking}
            className={`px-6 py-3 rounded-lg font-semibold transition-all duration-300 flex items-center gap-2 ${
              !checklist.canProceed || checklist.isChecking
                ? 'bg-surface-light/30 text-text-muted cursor-not-allowed'
                : 'bg-gradient-to-r from-neon-purple to-neon-pink hover:from-neon-pink hover:to-neon-purple text-white neon-glow animate-pulse'
            }`}
          >
            <svg
              className="h-5 w-5"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M13 10V3L4 14h7v7l9-11h-7z"
              />
            </svg>
            LAUNCH
          </button>
        </div>
      </div>
    </div>
  );
}
