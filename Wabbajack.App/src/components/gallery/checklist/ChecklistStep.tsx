import { type ReactNode } from 'react';
import { type ChecklistStepStatus } from '../../../hooks/usePreInstallChecklist';

interface ChecklistStepProps {
  stepNumber: number;
  title: string;
  status: ChecklistStepStatus;
  subtitle?: string;
  progress?: number;
  progressText?: string;
  children?: ReactNode;
  isActive?: boolean;
}

export function ChecklistStep({
  stepNumber,
  title,
  status,
  subtitle,
  progress,
  progressText,
  children,
  isActive = false,
}: ChecklistStepProps) {
  return (
    <div
      className={`glass rounded-xl border transition-all duration-300 ${
        isActive
          ? 'border-neon-purple/40 shadow-lg shadow-neon-purple/10'
          : status === 'passed'
            ? 'border-success/30'
            : status === 'failed'
              ? 'border-error/30'
              : 'border-neon-purple/10'
      }`}
    >
      <div className="p-4">
        <div className="flex items-center gap-4">
          {/* Status Icon */}
          <div className="flex-shrink-0">
            <StatusIcon status={status} />
          </div>

          {/* Content */}
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2">
              <span className="text-text-muted text-sm font-mono">
                {String(stepNumber).padStart(2, '0')}
              </span>
              <h3
                className={`font-semibold font-display ${
                  status === 'pending' ? 'text-text-muted' : 'text-text-primary'
                }`}
              >
                {title}
              </h3>
            </div>
            {subtitle && (
              <p
                className={`text-sm mt-0.5 truncate ${
                  status === 'passed'
                    ? 'text-success'
                    : status === 'failed'
                      ? 'text-error'
                      : 'text-text-muted'
                }`}
              >
                {subtitle}
              </p>
            )}

            {/* Progress bar */}
            {status === 'checking' && progress !== undefined && (
              <div className="mt-2">
                <div className="h-1.5 bg-surface-light/50 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-gradient-to-r from-neon-purple to-neon-cyan transition-all duration-300"
                    style={{ width: `${progress * 100}%` }}
                  />
                </div>
                {progressText && (
                  <p className="text-xs text-text-muted mt-1">{progressText}</p>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Expandable content */}
        {children && (
          <div className="mt-4 ml-12 border-t border-neon-purple/10 pt-4">
            {children}
          </div>
        )}
      </div>
    </div>
  );
}

function StatusIcon({ status }: { status: ChecklistStepStatus }) {
  const baseClasses =
    'w-8 h-8 rounded-lg flex items-center justify-center transition-all duration-300';

  switch (status) {
    case 'pending':
      return (
        <div className={`${baseClasses} bg-surface-light/50 text-text-muted`}>
          <span className="text-sm font-mono">--</span>
        </div>
      );

    case 'checking':
      return (
        <div
          className={`${baseClasses} bg-neon-purple/20 text-neon-purple animate-pulse`}
        >
          <svg
            className="w-5 h-5 animate-spin"
            fill="none"
            viewBox="0 0 24 24"
          >
            <circle
              className="opacity-25"
              cx="12"
              cy="12"
              r="10"
              stroke="currentColor"
              strokeWidth="4"
            />
            <path
              className="opacity-75"
              fill="currentColor"
              d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
            />
          </svg>
        </div>
      );

    case 'passed':
      return (
        <div
          className={`${baseClasses} bg-success/20 text-success animate-[pop_0.3s_ease-out]`}
        >
          <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2.5}
              d="M5 13l4 4L19 7"
            />
          </svg>
        </div>
      );

    case 'failed':
      return (
        <div
          className={`${baseClasses} bg-error/20 text-error animate-[shake_0.3s_ease-out]`}
        >
          <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2.5}
              d="M6 18L18 6M6 6l12 12"
            />
          </svg>
        </div>
      );

    case 'warning':
      return (
        <div className={`${baseClasses} bg-warning/20 text-warning`}>
          <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
            />
          </svg>
        </div>
      );
  }
}
