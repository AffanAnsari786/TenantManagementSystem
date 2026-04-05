import { Inject, Injectable } from '@angular/core';
import { HUB_BASE_URL } from '../core/tokens/api-tokens';
import { AuthService } from './auth.service';

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
  private readonly hubUrl: string;
  private connection: IHubConnection | null = null;
  /** Re-join this entry after automatic reconnect (groups are per-connection). */
  private lastJoinedEntryId: string | null = null;
  /** Optional share token used for anonymous viewers; replayed on reconnect. */
  private lastShareToken: string | null = null;

  constructor(
    @Inject(HUB_BASE_URL) hubBaseUrl: string,
    private auth: AuthService
  ) {
    this.hubUrl = `${hubBaseUrl}/shared-dashboard`;
  }

  async connect(): Promise<void> {
    if (this.connection?.state === CONNECTED) {
      return;
    }
    if (this.connection?.state === CONNECTING) {
      return;
    }

    const signalR = await import('@microsoft/signalr');
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, {
        // Pass the JWT when available so authenticated owners are recognised
        // by the hub. Anonymous viewers will simply connect without a token.
        accessTokenFactory: () => this.auth.getToken() ?? ''
      })
      .withAutomaticReconnect()
      .build() as unknown as IHubConnection;

    const conn = this.connection as unknown as { onreconnected?(cb: (id?: string) => void): void };
    conn.onreconnected?.(() => {
      if (this.lastJoinedEntryId != null && this.connection) {
        this.connection
          .invoke('JoinEntry', this.lastJoinedEntryId, this.lastShareToken)
          .catch(() => {});
      }
    });

    await this.connection.start();
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      this.lastJoinedEntryId = null;
      this.lastShareToken = null;
    }
  }

  /**
   * Join the realtime group for an Entry.
   * Pass a `shareToken` when the caller is an anonymous viewer of a SharedLink.
   */
  async joinEntry(entryId: string, shareToken?: string | null): Promise<void> {
    if (!this.connection || this.connection.state !== CONNECTED) {
      await this.connect();
    }
    if (this.connection) {
      this.lastJoinedEntryId = entryId;
      this.lastShareToken = shareToken ?? null;
      await this.connection.invoke('JoinEntry', entryId, this.lastShareToken);
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
