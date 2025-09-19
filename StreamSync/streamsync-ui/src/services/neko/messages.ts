// WebSocket message types and payloads for Neko communication

export interface WebSocketMessage {
  event: string;
  [key: string]: any;
}

export interface SignalProvidePayload {
  sdp: string;
  lite: boolean;
  ice: RTCIceServer[];
  id: string;
}

export interface SignalOfferPayload {
  sdp: string;
}

export interface SignalAnswerPayload {
  sdp: string;
  displayname?: string;
}

export interface SignalCandidatePayload {
  data: string;
}

export interface ControlPayload {
  id: string;
}

export interface ControlTargetPayload {
  id: string;
  target: string;
}

export interface ControlClipboardPayload {
  text: string;
}

export interface ScreenConfigurationsPayload {
  configurations: any[];
}

export interface ScreenResolutionPayload {
  id?: string;
  width: number;
  height: number;
  rate: number;
  quality: number;
}

export interface ScreenSetPayload {
  width: number;
  height: number;
  rate: number;
  quality: number;
}

export interface SystemMessagePayload {
  message: string;
}

export type WebSocketPayloads = 
  | SignalProvidePayload 
  | SignalOfferPayload 
  | SignalAnswerPayload 
  | SignalCandidatePayload
  | ControlPayload
  | ControlTargetPayload
  | ControlClipboardPayload
  | ScreenConfigurationsPayload
  | ScreenResolutionPayload
  | ScreenSetPayload
  | SystemMessagePayload;