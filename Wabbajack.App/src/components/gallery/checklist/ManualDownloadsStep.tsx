import { type ManualDownloadsCheckResult } from '../../../api/checklist';
import { getSiteNameForUrl } from '../../../utils/favicons';

interface ManualDownloadsStepProps {
  result: ManualDownloadsCheckResult | null;
  onMoveFile: (sourcePath: string) => Promise<boolean>;
  onRescan: () => void;
  isScanning: boolean;
}

export function ManualDownloadsStep({
  result,
  onMoveFile,
  onRescan,
  isScanning,
}: ManualDownloadsStepProps) {
  if (!result || result.files.length === 0) {
    return (
      <p className="text-sm text-text-muted">
        No manual downloads required by this modlist.
      </p>
    );
  }

  const readyCount = result.files.filter((f) => f.status === 'ready').length;
  const totalCount = result.files.length;
  const missingFiles = result.files.filter((f) => f.status !== 'ready');

  return (
    <div className="space-y-4">
      {/* Summary */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <span className="text-sm text-text-secondary">
            {readyCount} of {totalCount} downloads ready
          </span>
          {readyCount === totalCount && (
            <span className="text-xs bg-success/20 text-success px-2 py-0.5 rounded">
              All ready
            </span>
          )}
        </div>
        <button
          onClick={onRescan}
          disabled={isScanning}
          className="text-xs text-neon-cyan hover:text-neon-purple transition-colors disabled:opacity-50"
        >
          {isScanning ? 'Scanning...' : 'Recheck'}
        </button>
      </div>

      {/* Missing downloads */}
      {missingFiles.length > 0 && (
        <div className="space-y-3">
          {missingFiles.map((file) => (
            <ManualDownloadItem
              key={file.name}
              file={file}
              onMoveFile={onMoveFile}
            />
          ))}
        </div>
      )}

      {/* Ready downloads (collapsed) */}
      {result.files.filter((f) => f.status === 'ready').length > 0 && (
        <details className="text-xs text-text-muted">
          <summary className="cursor-pointer hover:text-text-secondary transition-colors">
            {readyCount} download(s) already in place
          </summary>
          <ul className="mt-2 space-y-1 pl-4">
            {result.files
              .filter((f) => f.status === 'ready')
              .map((f) => (
                <li key={f.name} className="text-success">
                  {f.name}
                </li>
              ))}
          </ul>
        </details>
      )}
    </div>
  );
}

interface ManualDownloadItemProps {
  file: {
    name: string;
    url: string;
    prompt: string;
    status: string;
    expectedSize: number;
    favicon?: string;
    foundPath?: string;
  };
  onMoveFile: (sourcePath: string) => Promise<boolean>;
}

function ManualDownloadItem({ file, onMoveFile }: ManualDownloadItemProps) {
  const siteName = getSiteNameForUrl(file.url);
  const isFoundInOsDownloads = file.status === 'found_in_os_downloads';

  const handleDownload = () => {
    window.open(file.url, '_blank');
  };

  const handleMove = async () => {
    if (file.foundPath) {
      await onMoveFile(file.foundPath);
    }
  };

  return (
    <div className="bg-surface-light/30 border border-neon-purple/10 rounded-lg p-3">
      <div className="flex items-start gap-3">
        {/* Favicon */}
        {file.favicon && (
          <img
            src={file.favicon}
            alt=""
            className="w-5 h-5 mt-0.5 rounded"
            onError={(e) => {
              (e.target as HTMLImageElement).style.display = 'none';
            }}
          />
        )}

        {/* Content */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium text-text-primary truncate">
              {file.name}
            </span>
            <span className="text-xs text-text-muted">
              ({formatSize(file.expectedSize)})
            </span>
          </div>

          {file.prompt && (
            <p className="text-xs text-text-muted mt-1">{file.prompt}</p>
          )}

          {/* Status indicator */}
          {isFoundInOsDownloads && (
            <div className="flex items-center gap-1 mt-2 text-xs text-warning">
              <svg
                className="w-3.5 h-3.5"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
                />
              </svg>
              Found in your Downloads folder
            </div>
          )}
        </div>

        {/* Actions */}
        <div className="flex gap-2">
          {isFoundInOsDownloads ? (
            <button
              onClick={handleMove}
              className="px-3 py-1.5 text-xs bg-warning/20 hover:bg-warning/30 text-warning rounded transition-colors"
            >
              Move to Downloads
            </button>
          ) : (
            <button
              onClick={handleDownload}
              className="px-3 py-1.5 text-xs bg-neon-purple/20 hover:bg-neon-purple/30 text-neon-purple rounded transition-colors flex items-center gap-1"
            >
              <svg
                className="w-3.5 h-3.5"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14"
                />
              </svg>
              {siteName}
            </button>
          )}
        </div>
      </div>
    </div>
  );
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
