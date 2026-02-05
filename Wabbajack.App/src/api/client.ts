const API_BASE = "http://localhost:13373";

// API Response Types
export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  error: string | null;
}

export interface HelloResponse {
  message: string;
  version: string;
  timestamp: string;
}

export interface GameInfo {
  name: string;
  slug: string;
  installed: boolean;
  version: string;
  path: string;
}

export interface ServerStatus {
  status: string;
  version: string;
  platform: string;
  connectedClients: number;
  timestamp: string;
}

// API Client Functions
export async function hello(name?: string): Promise<ApiResponse<HelloResponse>> {
  const url = name
    ? `${API_BASE}/api/hello?name=${encodeURIComponent(name)}`
    : `${API_BASE}/api/hello`;

  const response = await fetch(url);
  return response.json();
}

export async function listGames(): Promise<ApiResponse<GameInfo[]>> {
  const response = await fetch(`${API_BASE}/api/games`);
  return response.json();
}

export async function getStatus(): Promise<ApiResponse<ServerStatus>> {
  const response = await fetch(`${API_BASE}/api/status`);
  return response.json();
}

export function getEventsUrl(): string {
  return `${API_BASE}/api/events`;
}

// Server-Sent Events
export interface ServerEvent {
  type: string;
  timestamp: string;
  data: unknown;
}

/**
 * Subscribe to server-sent events.
 * Returns an unsubscribe function.
 */
export function subscribeToEvents(
  onEvent: (event: ServerEvent) => void
): () => void {
  const eventSource = new EventSource(getEventsUrl());

  eventSource.onmessage = (e) => {
    try {
      const event = JSON.parse(e.data) as ServerEvent;
      onEvent(event);
    } catch (err) {
      console.error('Failed to parse SSE event:', err);
    }
  };

  eventSource.onerror = (err) => {
    console.error('SSE error:', err);
  };

  return () => {
    eventSource.close();
  };
}
