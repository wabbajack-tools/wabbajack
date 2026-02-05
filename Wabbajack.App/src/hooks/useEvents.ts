import { useEffect, useRef, useCallback, useState } from "react";
import { getEventsUrl } from "../api/client";

export interface ServerEvent {
  type: string;
  timestamp: string;
  data: unknown;
}

export interface UseEventsOptions {
  onEvent?: (event: ServerEvent) => void;
  autoConnect?: boolean;
}

export function useEvents(options: UseEventsOptions = {}) {
  const { onEvent, autoConnect = true } = options;
  const [isConnected, setIsConnected] = useState(false);
  const [events, setEvents] = useState<ServerEvent[]>([]);
  const eventSourceRef = useRef<EventSource | null>(null);

  const connect = useCallback(() => {
    if (eventSourceRef.current) {
      return;
    }

    const source = new EventSource(getEventsUrl());

    source.onopen = () => {
      setIsConnected(true);
    };

    source.onmessage = (e) => {
      try {
        const event: ServerEvent = JSON.parse(e.data);
        setEvents((prev) => [...prev.slice(-99), event]); // Keep last 100 events
        onEvent?.(event);
      } catch (err) {
        console.error("Failed to parse SSE event:", err);
      }
    };

    source.onerror = () => {
      setIsConnected(false);
      source.close();
      eventSourceRef.current = null;

      // Attempt to reconnect after 5 seconds
      setTimeout(() => {
        if (autoConnect) {
          connect();
        }
      }, 5000);
    };

    eventSourceRef.current = source;
  }, [onEvent, autoConnect]);

  const disconnect = useCallback(() => {
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
      setIsConnected(false);
    }
  }, []);

  const clearEvents = useCallback(() => {
    setEvents([]);
  }, []);

  useEffect(() => {
    if (autoConnect) {
      connect();
    }

    return () => {
      disconnect();
    };
  }, [autoConnect, connect, disconnect]);

  return {
    isConnected,
    events,
    connect,
    disconnect,
    clearEvents,
  };
}
