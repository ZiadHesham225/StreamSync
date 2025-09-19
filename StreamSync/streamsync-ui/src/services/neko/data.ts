// Data opcodes for binary WebRTC data channel communication
export const OPCODE = {
  MOVE: 0x01,      // Mouse move
  SCROLL: 0x02,    // Mouse scroll/wheel
  KEY_DOWN: 0x03,  // Key/mouse button down
  KEY_UP: 0x04,    // Key/mouse button up
} as const;