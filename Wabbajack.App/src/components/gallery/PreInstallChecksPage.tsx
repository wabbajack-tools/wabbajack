import { useState } from 'react';
import { ModlistPreInstallInfo } from '../../api/install';

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

function formatNumber(num: number): string {
  return num.toLocaleString();
}

function WarningBadge({ type, message }: { type: string; message: string }) {
  const colorClasses: Record<string, string> = {
    game_not_installed: 'bg-error/20 border-error/50 text-error',
    nsfw: 'bg-warning/20 border-warning/50 text-warning',
    non_automatic_downloads: 'bg-neon-cyan/20 border-neon-cyan/50 text-neon-cyan',
  };

  const iconPaths: Record<string, string> = {
    game_not_installed:
      'M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z',
    nsfw: 'M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z',
    non_automatic_downloads:
      'M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z',
  };

  const classes =
    colorClasses[type] || 'bg-text-muted/20 border-text-muted/50 text-text-muted';
  const iconPath =
    iconPaths[type] || 'M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z';

  return (
    <div className={`flex items-start gap-3 p-4 rounded-xl border ${classes}`}>
      <svg
        className="w-5 h-5 flex-shrink-0 mt-0.5"
        fill="none"
        viewBox="0 0 24 24"
        stroke="currentColor"
      >
        <path
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth={2}
          d={iconPath}
        />
      </svg>
      <p className="text-sm">{message}</p>
    </div>
  );
}

interface FolderInputProps {
  label: string;
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
  icon: React.ReactNode;
  error?: string;
}

function FolderInput({ label, value, onChange, placeholder, icon, error }: FolderInputProps) {
  const handleBrowse = async () => {
    // Use Tauri's dialog if available
    if (window.__TAURI__) {
      try {
        const { open } = await import('@tauri-apps/plugin-dialog');
        const selected = await open({
          directory: true,
          multiple: false,
          title: `Select ${label}`,
        });
        if (selected && typeof selected === 'string') {
          onChange(selected);
        }
      } catch (err) {
        console.error('Failed to open folder dialog:', err);
      }
    }
  };

  return (
    <div className="space-y-2">
      <label className="flex items-center gap-2 text-sm font-medium text-text-primary">
        {icon}
        {label}
      </label>
      <div className="flex gap-2">
        <input
          type="text"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          className={`flex-1 px-4 py-2 bg-surface-light/50 border rounded-lg text-text-primary placeholder-text-muted focus:outline-none focus:border-neon-purple/50 transition-colors ${
            error ? 'border-error/50' : 'border-neon-purple/20'
          }`}
        />
        <button
          onClick={handleBrowse}
          className="px-4 py-2 bg-surface-light/50 hover:bg-surface-light border border-neon-purple/20 hover:border-neon-purple/40 rounded-lg text-text-secondary hover:text-text-primary transition-all duration-200"
        >
          Browse
        </button>
      </div>
      {error && <p className="text-xs text-error">{error}</p>}
    </div>
  );
}

export function PreInstallChecksPage({
  info,
  onProceed,
  onCancel,
}: PreInstallChecksPageProps) {
  const { modlist, requirements, warnings } = info;
  const [downloadFolder, setDownloadFolder] = useState('');
  const [installFolder, setInstallFolder] = useState('');

  // Validation
  const gameNotInstalled = !requirements.gameInstalled;
  const downloadFolderMissing = !downloadFolder.trim();
  const installFolderMissing = !installFolder.trim();

  const canProceed = !gameNotInstalled && !downloadFolderMissing && !installFolderMissing;

  // Build validation errors list
  const validationErrors: string[] = [];
  if (gameNotInstalled) {
    validationErrors.push('Game must be installed');
  }
  if (downloadFolderMissing) {
    validationErrors.push('Download folder must be set');
  }
  if (installFolderMissing) {
    validationErrors.push('Installation folder must be set');
  }

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

      {/* Header */}
      <div className="glass rounded-2xl p-6 border border-neon-purple/20">
        <div className="flex items-center gap-4">
          <div className="w-12 h-12 bg-gradient-to-br from-neon-purple to-neon-pink rounded-xl flex items-center justify-center">
            <svg
              className="w-6 h-6 text-white"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"
              />
            </svg>
          </div>
          <div>
            <h1 className="text-2xl font-bold font-display text-text-primary">
              Pre-Install Checks
            </h1>
            <p className="text-text-muted">
              Review the requirements before installing
            </p>
          </div>
        </div>
      </div>

      {/* Modlist Info */}
      <div className="glass rounded-2xl p-6 space-y-4 border border-neon-purple/10">
        <h2 className="text-lg font-semibold font-display text-text-primary">
          {modlist.name}
        </h2>
        <div className="flex flex-wrap gap-4 text-sm">
          <span className="bg-surface-light/50 text-text-secondary px-3 py-1 rounded-lg border border-neon-purple/10">
            {modlist.gameDisplayName}
          </span>
          <span className="text-text-muted">Version {modlist.version}</span>
          <span className="text-text-muted">by {modlist.author}</span>
        </div>
        <p className="text-text-secondary text-sm leading-relaxed">
          {modlist.description}
        </p>
      </div>

      {/* Warnings */}
      {warnings.length > 0 && (
        <div className="space-y-3">
          <h2 className="text-lg font-semibold font-display text-text-primary">
            Warnings
          </h2>
          {warnings.map((warning, index) => (
            <WarningBadge
              key={index}
              type={warning.type}
              message={warning.message}
            />
          ))}
        </div>
      )}

      {/* Folder Configuration */}
      <div className="glass rounded-2xl p-6 space-y-6 border border-neon-purple/10">
        <h3 className="font-semibold font-display text-text-primary flex items-center gap-2">
          <svg
            className="w-5 h-5 text-neon-purple"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z"
            />
          </svg>
          Installation Folders
        </h3>

        <FolderInput
          label="Download Folder"
          value={downloadFolder}
          onChange={setDownloadFolder}
          placeholder="Select where to store downloaded archives..."
          error={downloadFolderMissing ? 'Required' : undefined}
          icon={
            <svg className="w-4 h-4 text-neon-cyan" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
            </svg>
          }
        />

        <FolderInput
          label="Installation Folder"
          value={installFolder}
          onChange={setInstallFolder}
          placeholder="Select where to install the modlist..."
          error={installFolderMissing ? 'Required' : undefined}
          icon={
            <svg className="w-4 h-4 text-neon-pink" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 8h14M5 8a2 2 0 110-4h14a2 2 0 110 4M5 8v10a2 2 0 002 2h10a2 2 0 002-2V8m-9 4h4" />
            </svg>
          }
        />
      </div>

      {/* Requirements Grid */}
      <div className="grid gap-4 md:grid-cols-2">
        {/* Download Requirements */}
        <div className="glass rounded-2xl p-6 space-y-4 border border-neon-purple/10">
          <h3 className="font-semibold font-display text-text-primary flex items-center gap-2">
            <svg
              className="w-5 h-5 text-neon-purple"
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
            Download Requirements
          </h3>
          <div className="space-y-3 text-sm">
            <div className="flex justify-between">
              <span className="text-text-muted">Archives to Download</span>
              <span className="text-text-secondary font-mono">
                {formatNumber(requirements.archiveCount)}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-text-muted">Total Download Size</span>
              <span className="text-text-secondary font-mono">
                {formatSize(requirements.totalArchiveSize)}
              </span>
            </div>
            {requirements.nonAutomaticDownloadCount > 0 && (
              <div className="flex justify-between">
                <span className="text-text-muted">Non-Automatic Downloads</span>
                <span className="text-warning font-mono">
                  {formatNumber(requirements.nonAutomaticDownloadCount)}
                </span>
              </div>
            )}
          </div>
        </div>

        {/* Installation Requirements */}
        <div className="glass rounded-2xl p-6 space-y-4 border border-neon-purple/10">
          <h3 className="font-semibold font-display text-text-primary flex items-center gap-2">
            <svg
              className="w-5 h-5 text-neon-pink"
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
            Installation Requirements
          </h3>
          <div className="space-y-3 text-sm">
            <div className="flex justify-between">
              <span className="text-text-muted">Files to Install</span>
              <span className="text-text-secondary font-mono">
                {formatNumber(requirements.directiveCount)}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-text-muted">Installed Size</span>
              <span className="text-text-secondary font-mono">
                {formatSize(requirements.totalInstalledSize)}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-text-muted">Temp Space Required</span>
              <span className="text-text-secondary font-mono">
                ~{formatSize(requirements.estimatedTempSpace)}
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Game Status */}
      <div className="glass rounded-2xl p-6 space-y-4 border border-neon-purple/10">
        <h3 className="font-semibold font-display text-text-primary flex items-center gap-2">
          <svg
            className="w-5 h-5 text-neon-cyan"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M15 5v2m0 4v2m0 4v2M5 5a2 2 0 00-2 2v3a2 2 0 110 4v3a2 2 0 002 2h14a2 2 0 002-2v-3a2 2 0 110-4V7a2 2 0 00-2-2H5z"
            />
          </svg>
          Game Status
        </h3>
        <div className="flex items-center justify-between">
          <div>
            <span className="text-text-secondary">
              {modlist.gameDisplayName}
            </span>
            {requirements.gamePath && (
              <p className="text-xs text-text-muted mt-1 font-mono truncate max-w-md">
                {requirements.gamePath}
              </p>
            )}
          </div>
          {requirements.gameInstalled ? (
            <span className="inline-flex items-center gap-1 bg-success/20 text-success px-3 py-1 rounded-lg text-sm">
              <svg
                className="w-4 h-4"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M5 13l4 4L19 7"
                />
              </svg>
              Installed
            </span>
          ) : (
            <span className="inline-flex items-center gap-1 bg-error/20 text-error px-3 py-1 rounded-lg text-sm">
              <svg
                className="w-4 h-4"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M6 18L18 6M6 6l12 12"
                />
              </svg>
              Not Installed
            </span>
          )}
        </div>
      </div>

      {/* Validation Summary */}
      {!canProceed && (
        <div className="glass rounded-2xl p-4 border border-error/20 bg-error/5">
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
              <p className="text-sm font-medium text-error">Cannot proceed with installation:</p>
              <ul className="mt-1 text-sm text-error/80 list-disc list-inside">
                {validationErrors.map((error, index) => (
                  <li key={index}>{error}</li>
                ))}
              </ul>
            </div>
          </div>
        </div>
      )}

      {/* Action Buttons */}
      <div className="flex gap-4 justify-end">
        <button
          onClick={onCancel}
          className="px-6 py-3 bg-surface-light/50 hover:bg-surface-light text-text-secondary hover:text-text-primary rounded-lg transition-all duration-200 border border-neon-purple/10 hover:border-neon-purple/30"
        >
          Cancel
        </button>
        <button
          onClick={() => onProceed(downloadFolder, installFolder)}
          disabled={!canProceed}
          className={`px-6 py-3 rounded-lg font-semibold transition-all duration-300 flex items-center gap-2 ${
            !canProceed
              ? 'bg-surface-light/30 text-text-muted cursor-not-allowed'
              : 'bg-gradient-to-r from-neon-purple to-neon-pink hover:from-neon-pink hover:to-neon-purple text-white neon-glow'
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
              d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"
            />
          </svg>
          Proceed with Installation
        </button>
      </div>
    </div>
  );
}
