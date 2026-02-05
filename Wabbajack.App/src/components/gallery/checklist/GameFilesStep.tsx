import { useState } from 'react';
import { type GameFilesCheckResult } from '../../../api/checklist';

interface GameFilesStepProps {
  result: GameFilesCheckResult | null;
  onRescan: () => void;
  isScanning: boolean;
}

export function GameFilesStep({
  result,
  onRescan,
  isScanning,
}: GameFilesStepProps) {
  const [isExpanded, setIsExpanded] = useState(false);

  if (!result || result.files.length === 0) {
    return (
      <p className="text-sm text-text-muted">
        No game files required by this modlist.
      </p>
    );
  }

  const foundCount = result.files.filter((f) => f.status === 'found').length;
  const totalCount = result.files.length;

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
        <div className="space-y-1 max-h-48 overflow-y-auto pr-2">
          {result.files.map((file) => (
            <div
              key={file.relativePath}
              className={`flex items-center gap-2 text-xs p-2 rounded ${
                file.status === 'found'
                  ? 'bg-success/10'
                  : file.status === 'missing'
                    ? 'bg-error/10'
                    : 'bg-warning/10'
              }`}
            >
              <FileStatusIcon status={file.status} />
              <span
                className={`font-mono truncate ${
                  file.status === 'found'
                    ? 'text-success'
                    : file.status === 'missing'
                      ? 'text-error'
                      : 'text-warning'
                }`}
              >
                {file.relativePath}
              </span>
            </div>
          ))}
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
