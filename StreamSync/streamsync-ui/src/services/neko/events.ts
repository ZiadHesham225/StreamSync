// Event constants for Neko client communication
export const EVENT = {
  // Connection events
  CONNECTING: 'connecting',
  CONNECTED: 'connected',
  DISCONNECTED: 'disconnected',
  RECONNECTING: 'reconnecting',

  // WebRTC events
  TRACK: 'track',
  DATA: 'data',
  MESSAGE: 'message',

  // WebSocket signal events
  SIGNAL: {
    PROVIDE: 'signal/provide',
    OFFER: 'signal/offer',
    ANSWER: 'signal/answer',
    CANDIDATE: 'signal/candidate',
  },

  // Control events
  CONTROL: {
    LOCKED: 'control/locked',
    RELEASE: 'control/release',
    GIVE: 'control/give',
    CLIPBOARD: 'control/clipboard',
    KEYBOARD: 'control/keyboard',
  },

  // Screen events
  SCREEN: {
    CONFIGURATIONS: 'screen/configurations',
    RESOLUTION: 'screen/resolution',
    SET: 'screen/set',
  },

  // System events
  SYSTEM: {
    DISCONNECT: 'system/disconnect',
    INIT: 'system/init',
  },

  // Member events
  MEMBER: {
    LIST: 'member/list',
    CONNECTED: 'member/connected',
    DISCONNECTED: 'member/disconnected',
  },

  // Broadcast events
  BROADCAST: {
    STATUS: 'broadcast/status',
  },

  // File transfer events
  FILETRANSFER: {
    LIST: 'filetransfer/list',
  },

  // Admin events
  ADMIN: {
    CONTROL: 'admin/control',
  },
} as const;

export type WebSocketEvents = typeof EVENT[keyof typeof EVENT] | string;