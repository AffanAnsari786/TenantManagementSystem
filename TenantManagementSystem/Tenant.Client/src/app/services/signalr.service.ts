import { Injectable } from '@angular/core';

export const ENTRY_UPDATED_METHOD = 'EntryUpdated';

/** Minimal type for SignalR hub connection to avoid importing @microsoft/signalr at top level (SSR-safe). */
interface IHubConnection {
  state: number;
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke(method: string, ...args: unknown[]): Promise<unknown>;
  on(method: string, callback: (...args: unknown[]) => void): void;
  off(method: string): void;
}

/** HubConnectionState.Connected = 2 (from @microsoft/signalr) */
const CONNECTED = 2;
/** HubConnectionState.Connecting = 1 */
const CONNECTING = 1;

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubUrl = 'http://localhost:5149/hubs/shared-dashboard';
  private connection: IHubConnection | null = null;
  /** Re-join this entry after automatic reconnect (groups are per-connection). */
  private lastJoinedEntryId: string | null = null;

  async connect(): Promise<void> {
    if (this.connection?.state === CONNECTED) {
      return;
    }
    if (this.connection?.state === CONNECTING) {
      return;
    }

    const signalR = await import('@microsoft/signalr');
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl)
      .withAutomaticReconnect()
      .build() as unknown as IHubConnection;

    const conn = this.connection as unknown as { onreconnected?(cb: (id?: string) => void): void };
    conn.onreconnected?.(() => {
      if (this.lastJoinedEntryId != null && this.connection) {
        this.connection.invoke('JoinEntry', this.lastJoinedEntryId).catch(() => {});
      }
    });

    await this.connection.start();
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      this.lastJoinedEntryId = null;
    }
  }

  async joinEntry(entryId: string): Promise<void> {
    if (!this.connection || this.connection.state !== CONNECTED) {
      await this.connect();
    }
    if (this.connection) {
      this.lastJoinedEntryId = entryId;
      await this.connection.invoke('JoinEntry', entryId);
    }
  }

  onEntryUpdated(callback: (entryId: string) => void): void {
    if (!this.connection) return;
    this.connection.on(ENTRY_UPDATED_METHOD, (...args: unknown[]) => callback(String(args[0])));
  }

  offEntryUpdated(): void {
    if (this.connection) {
      this.connection.off(ENTRY_UPDATED_METHOD);
    }
  }

  getState(): number | null {
    return this.connection?.state ?? null;
  }
}
