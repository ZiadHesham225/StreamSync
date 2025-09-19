import { BaseClient } from './BaseClient';
import { EVENT } from './events';
import {
  ControlClipboardPayload,
  ControlPayload,
  ControlTargetPayload,
  ScreenConfigurationsPayload,
  ScreenResolutionPayload,
  ScreenSetPayload,
  SystemMessagePayload,
} from './messages';

export class NekoClient extends BaseClient {
  private _keepAliveInterval?: number;

  login(url: string, password: string, displayname: string) {
    this.connect(url, password, displayname);
  }

  logout() {
    if (this._keepAliveInterval) {
      clearInterval(this._keepAliveInterval);
      this._keepAliveInterval = undefined;
    }
    this.disconnect();
  }

  public takeControl() {
    if (this.connected) {
      this.sendMessage(EVENT.ADMIN.CONTROL);
    } else {
      this.once(EVENT.CONNECTED, () => {
        this.sendMessage(EVENT.ADMIN.CONTROL);
      });
    }
  }

  public changeResolution(width: number, height: number, rate: number, quality: number) {
    this.sendMessage(EVENT.SCREEN.SET, {
      width,
      height,
      rate,
      quality,
    });
  }

  /////////////////////////////
  // Internal Events
  /////////////////////////////
  protected [EVENT.RECONNECTING]() {
    this.emit(EVENT.RECONNECTING);
  }

  protected [EVENT.CONNECTING]() {
    this.emit(EVENT.CONNECTING);
  }

  protected onClientConnected() {
    // Start keep-alive messages
    this._keepAliveInterval = window.setInterval(() => {
      if (this.connected) {
        this.sendMessage('chat/message');
      }
    }, 30000);

    this.emit(EVENT.CONNECTED);
  }

  protected onClientDisconnected(reason?: Error) {
    if (this._keepAliveInterval) {
      clearInterval(this._keepAliveInterval);
      this._keepAliveInterval = undefined;
    }
    
    this.emit(EVENT.DISCONNECTED, reason);
  }

  protected [EVENT.TRACK](event: RTCTrackEvent) {
    this.emit(EVENT.TRACK, event);
  }

  protected [EVENT.DATA](data: any) {
    this.emit(EVENT.DATA, data);
  }

  /////////////////////////////
  // System Events
  /////////////////////////////
  protected [EVENT.SYSTEM.DISCONNECT]({ message }: SystemMessagePayload) {
    this.onDisconnected(new Error(message));
  }

  /////////////////////////////
  // Control Events
  /////////////////////////////
  protected [EVENT.CONTROL.LOCKED]({ id }: ControlPayload) {
    this.emit(EVENT.CONTROL.LOCKED, id);
  }

  protected [EVENT.CONTROL.RELEASE]({ id }: ControlPayload) {
    this.emit(EVENT.CONTROL.RELEASE, id);
  }

  protected [EVENT.CONTROL.GIVE]({ id, target }: ControlTargetPayload) {
    this.emit(EVENT.CONTROL.GIVE, id, target);
  }

  protected [EVENT.CONTROL.CLIPBOARD]({ text }: ControlClipboardPayload) {
    this.emit(EVENT.CONTROL.CLIPBOARD, text);
  }

  /////////////////////////////
  // Screen Events
  /////////////////////////////
  protected [EVENT.SCREEN.CONFIGURATIONS]({ configurations }: ScreenConfigurationsPayload) {
    this.emit(EVENT.SCREEN.CONFIGURATIONS, configurations);
  }

  protected [EVENT.SCREEN.RESOLUTION]({ 
    id, 
    width, 
    height, 
    rate, 
    quality 
  }: ScreenResolutionPayload) {
    this.emit(EVENT.SCREEN.RESOLUTION, { width, height, rate, quality });
  }
}