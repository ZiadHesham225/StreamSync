import EventEmitter from 'eventemitter3';
import { OPCODE } from './data';
import { EVENT } from './events';
import { 
  WebSocketMessage, 
  WebSocketPayloads,
  SignalProvidePayload,
  SignalCandidatePayload,
  SignalOfferPayload,
  SignalAnswerPayload 
} from './messages';

export abstract class BaseClient extends EventEmitter {
  protected _ws?: WebSocket;
  protected _peer?: RTCPeerConnection;
  protected _channel?: RTCDataChannel;
  protected _timeout?: number;
  protected _displayname?: string;
  protected _state: RTCIceConnectionState = 'disconnected';
  protected _id = '';
  protected _candidates: RTCIceCandidate[] = [];

  get id() {
    return this._id;
  }

  get supported() {
    return (
      typeof RTCPeerConnection !== 'undefined' &&
      typeof RTCPeerConnection.prototype.addTransceiver !== 'undefined'
    );
  }

  get socketOpen() {
    return (
      typeof this._ws !== 'undefined' && this._ws.readyState === WebSocket.OPEN
    );
  }

  get peerConnected() {
    return (
      typeof this._peer !== 'undefined' &&
      ['connected', 'checking', 'completed'].includes(this._state)
    );
  }

  get connected() {
    return this.peerConnected && this.socketOpen;
  }

  public connect(url: string, password: string, displayname: string) {
    if (this.socketOpen) {
      return;
    }

    if (!this.supported) {
      this.onDisconnected(
        new Error('Browser does not support WebRTC (RTCPeerConnection missing)')
      );
      return;
    }

    this._displayname = displayname;
    this[EVENT.CONNECTING]();

    try {
      this._ws = new WebSocket(`${url}?password=${encodeURIComponent(password)}`);
      this._ws.onopen = (event) => {
        };
      
      this._ws.onmessage = this.onMessage.bind(this);
      this._ws.onerror = (error) => {
        this.onDisconnected(new Error('WebSocket error'));
      };
      this._ws.onclose = (event) => {
        this.onDisconnected(new Error(`WebSocket closed: ${event.code} ${event.reason}`));
      };
      
      this._timeout = window.setTimeout(this.onTimeout.bind(this), 15000);
    } catch (err: any) {
      this.onDisconnected(err);
    }
  }

  protected disconnect() {
    if (this._timeout) {
      clearTimeout(this._timeout);
      this._timeout = undefined;
    }

    if (this._ws) {
      this._ws.onmessage = () => {};
      this._ws.onerror = () => {};
      this._ws.onclose = () => {};

      try {
        this._ws.close();
      } catch (err) {
        }
      this._ws = undefined;
    }

    if (this._channel) {
      this._channel.onmessage = () => {};
      this._channel.onerror = () => {};
      this._channel.onclose = () => {};

      try {
        this._channel.close();
      } catch (err) {
        }
      this._channel = undefined;
    }

    if (this._peer) {
      this._peer.onconnectionstatechange = () => {};
      this._peer.onsignalingstatechange = () => {};
      this._peer.oniceconnectionstatechange = () => {};
      this._peer.ontrack = () => {};

      try {
        this._peer.close();
      } catch (err) {
        }
      this._peer = undefined;
    }

    this._state = 'disconnected';
    this._displayname = undefined;
    this._id = '';
  }

  public sendData(event: 'wheel' | 'mousemove', data: { x: number; y: number }): void;
  public sendData(event: 'mousedown' | 'mouseup' | 'keydown' | 'keyup', data: { key: number }): void;
  public sendData(event: string, data: any) {
    if (!this.connected) {
      return;
    }

    if (!this._channel) {
      return;
    }

    if (this._channel.readyState !== 'open') {
      return;
    }

    let buffer: ArrayBuffer;
    let payload: DataView;
    
    switch (event) {
      case 'mousemove':
        buffer = new ArrayBuffer(7);
        payload = new DataView(buffer);
        payload.setUint8(0, OPCODE.MOVE);
        payload.setUint16(1, 4, true);
        payload.setUint16(3, data.x, true);
        payload.setUint16(5, data.y, true);
        break;
      case 'wheel':
        buffer = new ArrayBuffer(7);
        payload = new DataView(buffer);
        payload.setUint8(0, OPCODE.SCROLL);
        payload.setUint16(1, 4, true);
        payload.setInt16(3, data.x, true);
        payload.setInt16(5, data.y, true);
        break;
      case 'keydown':
      case 'mousedown':
        buffer = new ArrayBuffer(11);
        payload = new DataView(buffer);
        payload.setUint8(0, OPCODE.KEY_DOWN);
        payload.setUint16(1, 8, true);
        payload.setBigUint64(3, BigInt(data.key), true);
        break;
      case 'keyup':
      case 'mouseup':
        buffer = new ArrayBuffer(11);
        payload = new DataView(buffer);
        payload.setUint8(0, OPCODE.KEY_UP);
        payload.setUint16(1, 8, true);
        payload.setBigUint64(3, BigInt(data.key), true);
        break;
      default:
        return;
    }

    if (buffer && this._channel) {
      this._channel.send(buffer);
    }
  }

  public sendMessage(event: string, payload?: WebSocketPayloads) {
    if (!this.connected) {
      return;
    }
    
    this._ws!.send(JSON.stringify({ event, ...payload }));
  }

  public async createPeer(lite: boolean, servers: RTCIceServer[]) {
    if (!this.socketOpen) {
      return;
    }

    if (this.peerConnected) {
      return;
    }

    this._peer = new RTCPeerConnection(lite ? {} : { iceServers: servers });

    this._peer.onconnectionstatechange = () => {
      };

    this._peer.onsignalingstatechange = () => {
      };

    this._peer.oniceconnectionstatechange = () => {
      this._state = this._peer!.iceConnectionState;
      switch (this._state) {
        case 'checking':
          if (this._timeout) {
            clearTimeout(this._timeout);
            this._timeout = undefined;
          }
          break;
        case 'connected':
          this.onConnected();
          break;
        case 'disconnected':
          this[EVENT.RECONNECTING]();
          break;
        case 'failed':
          this.onDisconnected(new Error('Peer connection failed'));
          break;
        case 'closed':
          this.onDisconnected(new Error('Peer connection closed'));
          break;
      }
    };

    this._peer.ontrack = this.onTrack.bind(this);

    this._peer.onicecandidate = (event: RTCPeerConnectionIceEvent) => {
      if (!event.candidate) {
        return;
      }

      const init = event.candidate.toJSON();
      this._ws!.send(JSON.stringify({
        event: EVENT.SIGNAL.CANDIDATE,
        data: JSON.stringify(init),
      }));
    };

    this._peer.onnegotiationneeded = async () => {
      const offer = await this._peer!.createOffer();
      await this._peer!.setLocalDescription(offer);

      this._ws!.send(JSON.stringify({
        event: EVENT.SIGNAL.OFFER,
        sdp: offer.sdp,
      }));
    };

    this._channel = this._peer.createDataChannel('data');
    if (this._channel) {
      this._channel.onerror = (error) => {
        this._channel!.onmessage = this.onData.bind(this);
      };
    }
    this._channel.onclose = () => {
      this.onDisconnected(new Error('Peer data channel closed'));
    };
  }

  public async setRemoteOffer(sdp: string) {
    if (!this._peer) {
      return;
    }

    await this._peer.setRemoteDescription({ type: 'offer', sdp });

    for (const candidate of this._candidates) {
      await this._peer.addIceCandidate(candidate);
    }
    this._candidates = [];

    try {
      const answer = await this._peer.createAnswer();

      // Add stereo audio support for Chromium
      answer.sdp = answer.sdp?.replace(
        /(stereo=1;)?useinbandfec=1/,
        'useinbandfec=1;stereo=1'
      );

      await this._peer.setLocalDescription(answer);

      this._ws!.send(JSON.stringify({
        event: EVENT.SIGNAL.ANSWER,
        sdp: answer.sdp,
        displayname: this._displayname,
      }));
    } catch (err) {
      }
  }

  public async setRemoteAnswer(sdp: string) {
    if (!this._peer) {
      return;
    }
    await this._peer.setRemoteDescription({ type: 'answer', sdp });
  }

  private async onMessage(e: MessageEvent) {
    const message: WebSocketMessage = JSON.parse(e.data);
    const { event, ...payload } = message;

    if (event === EVENT.SIGNAL.PROVIDE) {
      const { sdp, lite, ice, id } = payload as SignalProvidePayload;
      this._id = id;
      await this.createPeer(lite, ice);
      await this.setRemoteOffer(sdp);
      return;
    }

    if (event === EVENT.SIGNAL.OFFER) {
      const { sdp } = payload as SignalOfferPayload;
      await this.setRemoteOffer(sdp);
      return;
    }

    if (event === EVENT.SIGNAL.ANSWER) {
      const { sdp } = payload as SignalAnswerPayload;
      await this.setRemoteAnswer(sdp);
      return;
    }

    if (event === EVENT.SIGNAL.CANDIDATE) {
      const { data } = payload as SignalCandidatePayload;
      const candidate: RTCIceCandidate = JSON.parse(data);
      if (this._peer) {
        this._peer.addIceCandidate(candidate);
      } else {
        this._candidates.push(candidate);
      }
      return;
    }

    // Handle other events through abstract methods
    if (typeof (this as any)[event] === 'function') {
      (this as any)[event](payload);
    } else {
      this[EVENT.MESSAGE](event, payload);
    }
  }

  private onData(e: MessageEvent) {
    this[EVENT.DATA](e.data);
  }

  private onTrack(event: RTCTrackEvent) {
    this[EVENT.TRACK](event);
  }

  private onConnected() {
    if (this._timeout) {
      clearTimeout(this._timeout);
      this._timeout = undefined;
    }

    if (!this.connected) {
      return;
    }

    this.onClientConnected();
  }

  private onTimeout() {
    if (this._timeout) {
      clearTimeout(this._timeout);
      this._timeout = undefined;
    }
    this.onDisconnected(new Error('Connection timeout'));
  }

  protected onDisconnected(reason?: Error) {
    this.disconnect();
    this.onClientDisconnected(reason);
  }

  protected [EVENT.MESSAGE](event: string, payload: any) {
    // Handle common Neko events silently to avoid console warnings
    switch (event) {
      case 'member/list':
        // List of current members - can be handled if needed
        break;
      case 'member/connected':
        // A member connected - can be handled if needed  
        break;
      case 'member/disconnected':
        // A member disconnected - can be handled if needed
        break;
      case 'system/init':
        // System initialization data - can be handled if needed
        break;
      case 'broadcast/status':
        // Broadcast status update - can be handled if needed
        break;
      case 'filetransfer/list':
        // File transfer list - can be handled if needed
        break;
      default:
        break;
    }
  }

  // Abstract methods that must be implemented by subclasses
  protected abstract [EVENT.RECONNECTING](): void;
  protected abstract [EVENT.CONNECTING](): void;
  protected abstract onClientConnected(): void;
  protected abstract onClientDisconnected(reason?: Error): void;
  protected abstract [EVENT.TRACK](event: RTCTrackEvent): void;
  protected abstract [EVENT.DATA](data: any): void;
}