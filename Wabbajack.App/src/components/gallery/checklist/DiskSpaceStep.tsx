import { type DiskSpaceCheckResult } from '../../../api/checklist';

interface DiskSpaceStepProps {
  result: DiskSpaceCheckResult | null;
}

export function DiskSpaceStep({ result }: DiskSpaceStepProps) {
  if (!result) {
    return null;
  }

  return (
    <div className="space-y-4">
      {/* Download drive */}
      <DriveSpaceBar
        label="Downloads"
        drive={result.downloadDrive}
        accentColor="neon-cyan"
      />

      {/* Install drive */}
      <DriveSpaceBar
        label="Installation"
        drive={result.installDrive}
        accentColor="neon-pink"
      />

      {/* Same drive warning */}
      {result.areSameDrive && (
        <p className="text-xs text-text-muted">
          Both folders are on the same drive.
        </p>
      )}
    </div>
  );
}

interface DriveSpaceBarProps {
  label: string;
  drive: {
    drivePath: string;
    availableSpace: number;
    requiredSpace: number;
    hasEnoughSpace: boolean;
  };
  accentColor: 'neon-cyan' | 'neon-pink' | 'neon-purple';
}

function DriveSpaceBar({ label, drive, accentColor }: DriveSpaceBarProps) {
  const usedPercent = drive.requiredSpace / (drive.availableSpace + drive.requiredSpace);
  const requiredPercent = Math.min(usedPercent * 100, 100);

  const colorClasses = {
    'neon-cyan': {
      bar: 'from-neon-cyan to-neon-purple',
      text: 'text-neon-cyan',
    },
    'neon-pink': {
      bar: 'from-neon-pink to-neon-purple',
      text: 'text-neon-pink',
    },
    'neon-purple': {
      bar: 'from-neon-purple to-neon-pink',
      text: 'text-neon-purple',
    },
  };

  const colors = colorClasses[accentColor];

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between text-xs">
        <span className="text-text-secondary">{label}</span>
        <span className="text-text-muted font-mono">{drive.drivePath}</span>
      </div>

      {/* Progress bar */}
      <div className="relative h-3 bg-surface-light/50 rounded-full overflow-hidden">
        {/* Required space indicator */}
        <div
          className={`absolute inset-y-0 left-0 bg-gradient-to-r ${colors.bar} transition-all duration-500`}
          style={{ width: `${requiredPercent}%` }}
        />

        {/* Threshold line at required amount */}
        {drive.hasEnoughSpace && (
          <div
            className="absolute inset-y-0 w-0.5 bg-text-primary/50"
            style={{ left: `${requiredPercent}%` }}
          />
        )}
      </div>

      {/* Labels */}
      <div className="flex items-center justify-between text-xs">
        <span className={drive.hasEnoughSpace ? colors.text : 'text-error'}>
          Required: {formatSize(drive.requiredSpace)}
        </span>
        <span className="text-text-muted">
          Available: {formatSize(drive.availableSpace)}
        </span>
      </div>

      {/* Warning for insufficient space */}
      {!drive.hasEnoughSpace && (
        <div className="flex items-center gap-1 text-xs text-error">
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
              d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
            />
          </svg>
          Need {formatSize(drive.requiredSpace - drive.availableSpace)} more space
        </div>
      )}
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
