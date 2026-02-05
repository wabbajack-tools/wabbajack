import { useState, useMemo } from 'react';
import { type GameFilesCheckResult, type GameFileStatus } from '../../../api/checklist';

interface GameFilesStepProps {
  result: GameFilesCheckResult | null;
  onRescan: () => void;
  isScanning: boolean;
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

function formatFileListForClipboard(files: GameFileStatus[]): string {
  return files
    .filter(f => f.status !== 'found')
    .map(f => {
      const statusText = f.status === 'missing' ? 'MISSING' :
                        f.status === 'hash_mismatch' ? 'HASH MISMATCH' :
                        f.status === 'size_mismatch' ? 'SIZE MISMATCH' : f.status.toUpperCase();
      let line = `${f.relativePath} (${statusText})\n`;
      line += `  Full Path: ${f.absolutePath}\n`;
      line += `  Expected Size: ${formatFileSize(f.expectedSize)}\n`;
      line += `  Expected Hash: ${f.expectedHash}`;
      if (f.actualHash && f.status === 'hash_mismatch') {
        line += `\n  Actual Hash: ${f.actualHash}`;
      }
      return line;
    })
    .join('\n\n');
}

export function GameFilesStep({
  result,
  onRescan,
  isScanning,
}: GameFilesStepProps) {
  const [isExpanded, setIsExpanded] = useState(false);
  const [showOnlyProblems, setShowOnlyProblems] = useState(true);
  const [expandedFiles, setExpandedFiles] = useState<Set<string>>(new Set());
  const [copyFeedback, setCopyFeedback] = useState(false);

  const problemFiles = useMemo(() =>
    result?.files.filter(f => f.status !== 'found') ?? [],
    [result]
  );

  const hasProblems = problemFiles.length > 0;

  if (!result || result.files.length === 0) {
    return (
      <p className="text-sm text-text-muted">
        No game files required by this modlist.
      </p>
    );
  }

  const foundCount = result.files.filter((f) => f.status === 'found').length;
  const totalCount = result.files.length;

  const displayedFiles = showOnlyProblems && hasProblems
    ? problemFiles
    : result.files;

  const toggleFileExpanded = (path: string) => {
    setExpandedFiles(prev => {
      const next = new Set(prev);
      if (next.has(path)) {
        next.delete(path);
      } else {
        next.add(path);
      }
      return next;
    });
  };

  const handleCopyToClipboard = async () => {
    const text = formatFileListForClipboard(result.files);
    if (text) {
      await navigator.clipboard.writeText(text);
      setCopyFeedback(true);
      setTimeout(() => setCopyFeedback(false), 2000);
    }
  };

  return (
    <div className="space-y-3">
      {/* Summary */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <span className="text-sm text-text-secondary">
            {foundCount} of {totalCount} files verified
          </span>
          {foundCount === totalCount && (
            <span className="text-xs bg-success/20 text-success px-2 py-0.5 rounded">
              All found
            </span>
          )}
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => setIsExpanded(!isExpanded)}
            className="text-xs text-neon-purple hover:text-neon-pink transition-colors"
          >
            {isExpanded ? 'Hide details' : 'Show details'}
          </button>
          <button
            onClick={onRescan}
            disabled={isScanning}
            className="text-xs text-neon-cyan hover:text-neon-purple transition-colors disabled:opacity-50"
          >
            {isScanning ? 'Scanning...' : 'Rescan'}
          </button>
        </div>
      </div>

      {/* File list */}
      {isExpanded && (
        <div className="space-y-2">
          {/* Controls */}
          {hasProblems && (
            <div className="flex items-center justify-between pb-2 border-b border-border">
              <label className="flex items-center gap-2 text-xs text-text-secondary cursor-pointer">
                <input
                  type="checkbox"
                  checked={showOnlyProblems}
                  onChange={(e) => setShowOnlyProblems(e.target.checked)}
                  className="w-3.5 h-3.5 rounded border-border bg-surface-elevated accent-neon-purple"
                />
                Show only problems ({problemFiles.length})
              </label>
              <button
                onClick={handleCopyToClipboard}
                className="flex items-center gap-1 text-xs text-neon-cyan hover:text-neon-purple transition-colors"
              >
                {copyFeedback ? (
                  <>
                    <CheckIcon />
                    Copied!
                  </>
                ) : (
                  <>
                    <CopyIcon />
                    Copy to clipboard
                  </>
                )}
              </button>
            </div>
          )}

          {/* File list */}
          <div className="space-y-1 max-h-64 overflow-y-auto pr-2">
            {displayedFiles.map((file) => {
              const isFileExpanded = expandedFiles.has(file.relativePath);
              const isProblem = file.status !== 'found';

              return (
                <div
                  key={file.relativePath}
                  className={`rounded transition-colors ${
                    file.status === 'found'
                      ? 'bg-success/10'
                      : file.status === 'missing'
                        ? 'bg-error/10'
                        : 'bg-warning/10'
                  }`}
                >
                  {/* File header */}
                  <button
                    onClick={() => isProblem && toggleFileExpanded(file.relativePath)}
                    disabled={!isProblem}
                    className={`w-full flex items-center gap-2 text-xs p-2 text-left ${
                      isProblem ? 'cursor-pointer hover:bg-white/5' : 'cursor-default'
                    }`}
                  >
                    <FileStatusIcon status={file.status} />
                    <span
                      className={`font-mono truncate flex-1 ${
                        file.status === 'found'
                          ? 'text-success'
                          : file.status === 'missing'
                            ? 'text-error'
                            : 'text-warning'
                      }`}
                    >
                      {file.relativePath}
                    </span>
                    {isProblem && (
                      <span className="text-text-muted">
                        {formatFileSize(file.expectedSize)}
                      </span>
                    )}
                    {isProblem && (
                      <ChevronIcon expanded={isFileExpanded} />
                    )}
                  </button>

                  {/* Expanded details */}
                  {isFileExpanded && isProblem && (
                    <div className="px-2 pb-2 pt-1 ml-5 border-l-2 border-border/50 space-y-1">
                      <div className="flex items-center gap-2 text-xs">
                        <span className="text-text-muted w-24">Status:</span>
                        <span className={`font-medium ${
                          file.status === 'missing' ? 'text-error' : 'text-warning'
                        }`}>
                          {file.status === 'missing' ? 'Missing' :
                           file.status === 'hash_mismatch' ? 'Hash Mismatch' :
                           file.status === 'size_mismatch' ? 'Size Mismatch' : file.status}
                        </span>
                      </div>
                      <div className="flex items-start gap-2 text-xs">
                        <span className="text-text-muted w-24 flex-shrink-0">Full Path:</span>
                        <span className="font-mono text-text-secondary break-all">
                          {file.absolutePath}
                        </span>
                      </div>
                      <div className="flex items-center gap-2 text-xs">
                        <span className="text-text-muted w-24">Expected Size:</span>
                        <span className="font-mono text-text-secondary">
                          {formatFileSize(file.expectedSize)}
                        </span>
                      </div>
                      <div className="flex items-start gap-2 text-xs">
                        <span className="text-text-muted w-24 flex-shrink-0">Expected Hash:</span>
                        <span className="font-mono text-text-secondary break-all">
                          {file.expectedHash}
                        </span>
                      </div>
                      {file.actualHash && file.status === 'hash_mismatch' && (
                        <div className="flex items-start gap-2 text-xs">
                          <span className="text-text-muted w-24 flex-shrink-0">Actual Hash:</span>
                          <span className="font-mono text-warning break-all">
                            {file.actualHash}
                          </span>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Help text for failed files */}
      {foundCount < totalCount && (
        <div className="bg-error/10 border border-error/20 rounded-lg p-3">
          <p className="text-xs text-error">
            Some game files are missing or have been modified. Try verifying your
            game files through Steam/GOG, then click "Rescan".
          </p>
        </div>
      )}
    </div>
  );
}

function FileStatusIcon({ status }: { status: string }) {
  switch (status) {
    case 'found':
      return (
        <svg
          className="w-3.5 h-3.5 text-success flex-shrink-0"
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
      );
    case 'missing':
      return (
        <svg
          className="w-3.5 h-3.5 text-error flex-shrink-0"
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
      );
    default:
      return (
        <svg
          className="w-3.5 h-3.5 text-warning flex-shrink-0"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M12 9v2m0 4h.01"
          />
        </svg>
      );
  }
}

function ChevronIcon({ expanded }: { expanded: boolean }) {
  return (
    <svg
      className={`w-3.5 h-3.5 text-text-muted transition-transform ${expanded ? 'rotate-180' : ''}`}
      fill="none"
      viewBox="0 0 24 24"
      stroke="currentColor"
    >
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth={2}
        d="M19 9l-7 7-7-7"
      />
    </svg>
  );
}

function CopyIcon() {
  return (
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
        d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"
      />
    </svg>
  );
}

function CheckIcon() {
  return (
    <svg
      className="w-3.5 h-3.5 text-success"
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
  );
}
