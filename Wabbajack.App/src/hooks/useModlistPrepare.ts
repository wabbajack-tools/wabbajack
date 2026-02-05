import { useState, useCallback, useEffect, useRef } from 'react';
import {
  prepareModlist,
  getModlistStatus,
  getModlistInfo,
  ModlistPrepareRequest,
  ModlistPrepareStatus,
  ModlistPreInstallInfo,
} from '../api/install';
import { getEventsUrl } from '../api/client';

interface DownloadProgressEvent {
  sessionId: string;
  progress: number;
  bytesDownloaded: number;
  totalBytes: number;
}

interface UseModlistPrepareResult {
  status: ModlistPrepareStatus | null;
  info: ModlistPreInstallInfo | null;
  error: string | null;
  isLoading: boolean;
  prepare: (request: ModlistPrepareRequest) => Promise<void>;
  reset: () => void;
}

export function useModlistPrepare(): UseModlistPrepareResult {
  const [status, setStatus] = useState<ModlistPrepareStatus | null>(null);
  const [info, setInfo] = useState<ModlistPreInstallInfo | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const sessionIdRef = useRef<string | null>(null);
  const pollIntervalRef = useRef<number | null>(null);
  const eventSourceRef = useRef<EventSource | null>(null);

  // Clean up on unmount
  useEffect(() => {
    return () => {
      if (pollIntervalRef.current) {
        clearInterval(pollIntervalRef.current);
      }
      if (eventSourceRef.current) {
        eventSourceRef.current.close();
      }
    };
  }, []);

  // Setup SSE listener for download progress
  const setupEventListener = useCallback((sessionId: string) => {
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
    }

    const source = new EventSource(getEventsUrl());

    source.onmessage = (e) => {
      try {
        const event = JSON.parse(e.data);
        if (
          event.type === 'DownloadProgress' &&
          event.data?.sessionId === sessionId
        ) {
          const progressData = event.data as DownloadProgressEvent;
          setStatus((prev) =>
            prev
              ? {
                  ...prev,
                  progress: progressData.progress * 0.9, // Scale to 90% for download phase
                }
              : null
          );
        }
      } catch {
        // Ignore parse errors
      }
    };

    eventSourceRef.current = source;
  }, []);

  // Poll for status updates
  const startPolling = useCallback(
    async (sessionId: string) => {
      const poll = async () => {
        try {
          const response = await getModlistStatus(sessionId);
          if (response.success && response.data) {
            setStatus(response.data);

            if (response.data.status === 'ready') {
              // Stop polling and fetch full info
              if (pollIntervalRef.current) {
                clearInterval(pollIntervalRef.current);
                pollIntervalRef.current = null;
              }
              if (eventSourceRef.current) {
                eventSourceRef.current.close();
                eventSourceRef.current = null;
              }

              const infoResponse = await getModlistInfo(sessionId);
              if (infoResponse.success && infoResponse.data) {
                setInfo(infoResponse.data);
              } else {
                setError(infoResponse.error || 'Failed to get modlist info');
              }
              setIsLoading(false);
            } else if (response.data.status === 'error') {
              // Stop polling on error
              if (pollIntervalRef.current) {
                clearInterval(pollIntervalRef.current);
                pollIntervalRef.current = null;
              }
              if (eventSourceRef.current) {
                eventSourceRef.current.close();
                eventSourceRef.current = null;
              }
              setError(response.data.error || 'Unknown error');
              setIsLoading(false);
            }
          }
        } catch (err) {
          console.error('Poll error:', err);
        }
      };

      // Initial poll
      await poll();

      // Continue polling if not ready/error
      pollIntervalRef.current = window.setInterval(poll, 500);
    },
    []
  );

  const prepare = useCallback(
    async (request: ModlistPrepareRequest) => {
      setError(null);
      setInfo(null);
      setIsLoading(true);

      try {
        const response = await prepareModlist(request);

        if (!response.success || !response.data) {
          setError(response.error || 'Failed to start preparation');
          setIsLoading(false);
          return;
        }

        const { sessionId, status: initialStatus } = response.data;
        sessionIdRef.current = sessionId;
        setStatus(response.data);

        if (initialStatus === 'ready') {
          // Already cached - fetch info immediately
          const infoResponse = await getModlistInfo(sessionId);
          if (infoResponse.success && infoResponse.data) {
            setInfo(infoResponse.data);
          } else {
            setError(infoResponse.error || 'Failed to get modlist info');
          }
          setIsLoading(false);
        } else if (initialStatus === 'error') {
          setError(response.data.error || 'Preparation failed');
          setIsLoading(false);
        } else {
          // Start polling and listening for progress
          setupEventListener(sessionId);
          startPolling(sessionId);
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Unknown error');
        setIsLoading(false);
      }
    },
    [setupEventListener, startPolling]
  );

  const reset = useCallback(() => {
    setStatus(null);
    setInfo(null);
    setError(null);
    setIsLoading(false);
    sessionIdRef.current = null;

    if (pollIntervalRef.current) {
      clearInterval(pollIntervalRef.current);
      pollIntervalRef.current = null;
    }
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }
  }, []);

  return {
    status,
    info,
    error,
    isLoading,
    prepare,
    reset,
  };
}
