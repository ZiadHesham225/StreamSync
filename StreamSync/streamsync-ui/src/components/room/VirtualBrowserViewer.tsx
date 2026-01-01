import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Monitor, Clock, Volume2, VolumeX, Maximize, Minimize } from 'lucide-react';
import { VirtualBrowser } from '../../types/index';
import { NekoClient, EVENT } from '../../services/neko';

interface VirtualBrowserViewerProps {
  virtualBrowser: VirtualBrowser;
  onRelease: () => void;
  hasControl: boolean;
}

const VirtualBrowserViewer: React.FC<VirtualBrowserViewerProps> = ({
  virtualBrowser,
  onRelease,
  hasControl
}) => {
  const [isMuted, setIsMuted] = useState(false);
  const [timeRemaining, setTimeRemaining] = useState<string>('');
  const [isLoading, setIsLoading] = useState(true);
  const [isConnected, setIsConnected] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isInteracting, setIsInteracting] = useState(false);
  
  const videoRef = useRef<HTMLVideoElement>(null);
  const overlayRef = useRef<HTMLDivElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const clientRef = useRef<NekoClient | null>(null);
  
  // Track previous hasControl to detect changes
  const prevHasControlRef = useRef<boolean>(hasControl);
  // Track if control take is pending
  const controlPendingRef = useRef<boolean>(false);

  const [nekoClient, setNekoClient] = useState<NekoClient | null>(null);

  const getWebSocketUrl = (browserUrl: string): string => {
    try {
      const url = new URL(browserUrl);
      const wsProtocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
      return `${wsProtocol}//${url.host}/ws`;
    } catch (error) {
      return '';
    }
  };

  useEffect(() => {
    const initializeNekoClient = () => {
      if (!virtualBrowser.browserUrl) {
        return;
      }

      const wsUrl = getWebSocketUrl(virtualBrowser.browserUrl);
      if (!wsUrl) {
        return;
      }

      const client = new NekoClient();
      clientRef.current = client;
      setNekoClient(client);

      client.on(EVENT.CONNECTING, () => {
        setIsLoading(true);
        setIsConnected(false);
      });

      client.on(EVENT.CONNECTED, () => {
        setIsLoading(false);
        setIsConnected(true);
        
        // Take control if we have it when connected
        // Use ref to get current value without adding dependency
        if (prevHasControlRef.current) {
          client.takeControl();
        }
        
        // Process any pending control request
        if (controlPendingRef.current) {
          client.takeControl();
          controlPendingRef.current = false;
        }
      });

      client.on(EVENT.DISCONNECTED, (error?: Error) => {
        setIsConnected(false);
        setIsLoading(false);
      });

      client.on(EVENT.TRACK, (event: RTCTrackEvent) => {
        const stream = event.streams[0];
        
        if (videoRef.current && stream) {
          videoRef.current.srcObject = stream;
          videoRef.current.play().catch(error => {
            if (error.name === 'NotAllowedError') {
              videoRef.current!.muted = true;
              setIsMuted(true);
              videoRef.current!.play().catch(e => {
              });
            }
          });
        }
      });

      client.on(EVENT.SCREEN.RESOLUTION, ({ width, height, rate, quality }) => {
        });

      client.on(EVENT.CONTROL.CLIPBOARD, (text: string) => {
      });
      const username = 'admin';
      const password = 'neko-admin';
      client.login(wsUrl, password, username);
    };

    initializeNekoClient();

    // Cleanup on unmount
    return () => {
      if (clientRef.current) {
        clientRef.current.logout();
        clientRef.current.removeAllListeners();
        clientRef.current = null;
      }
      setNekoClient(null);
    };
    // Only reconnect when browserUrl changes, NOT when hasControl changes
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [virtualBrowser.browserUrl]);

  // Handle control changes without reconnecting - just take control when granted
  useEffect(() => {
    // Update the ref for use in other callbacks
    prevHasControlRef.current = hasControl;
    
    // Only take control when hasControl becomes true and we're connected
    if (hasControl && nekoClient && isConnected) {
      nekoClient.takeControl();
    }
  }, [hasControl, nekoClient, isConnected]);

  // Calculate time remaining
  useEffect(() => {
    const updateTimeRemaining = () => {
      if (virtualBrowser.expiresAt) {
        const now = new Date().getTime();
        const expiresAt = new Date(virtualBrowser.expiresAt).getTime();
        const remaining = Math.max(0, expiresAt - now);
        
        if (remaining <= 0) {
          setTimeRemaining('Expired');
          // Automatically trigger cleanup when expired
          if (hasControl) {
            onRelease();
          }
        } else {
          const hours = Math.floor(remaining / (1000 * 60 * 60));
          const minutes = Math.floor((remaining % (1000 * 60 * 60)) / (1000 * 60));
          const seconds = Math.floor((remaining % (1000 * 60)) / 1000);
          setTimeRemaining(`${hours}h ${minutes}m ${seconds}s`);
        }
      }
    };

    updateTimeRemaining();
    const interval = setInterval(updateTimeRemaining, 1000);

    return () => clearInterval(interval);
  }, [virtualBrowser.expiresAt, hasControl, onRelease]);

  const toggleMute = () => {
    if (videoRef.current) {
      const newMutedState = !videoRef.current.muted;
      videoRef.current.muted = newMutedState;
      setIsMuted(newMutedState);
    }
  };

  // Handle video click to ensure audio can play
  const handleVideoClick = () => {
    if (videoRef.current && videoRef.current.paused) {
      videoRef.current.play().catch(error => {
        });
    }
    // If video is muted due to autoplay policy, try to unmute on user interaction
    if (videoRef.current && isMuted) {
      videoRef.current.muted = false;
      setIsMuted(false);
    }
  };

  // Fullscreen functionality
  const toggleFullscreen = async () => {
    if (!containerRef.current) return;

    try {
      if (!isFullscreen) {
        if (containerRef.current.requestFullscreen) {
          await containerRef.current.requestFullscreen();
        }
      } else {
        if (document.exitFullscreen) {
          await document.exitFullscreen();
        }
      }
    } catch (error) {
      }
  };

  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };

    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () => document.removeEventListener('fullscreenchange', handleFullscreenChange);
  }, []);

  const handleMouseEnter = () => {
    if (hasControl && isConnected) {
      setIsInteracting(true);
      if (overlayRef.current) {
        overlayRef.current.focus();
      }
    }
  };

  const handleMouseLeave = () => {
    setIsInteracting(false);
  };

  const handleMouseMove = (e: React.MouseEvent) => {
    if (!nekoClient || !hasControl || !isConnected || !overlayRef.current) return;
    
    const rect = overlayRef.current.getBoundingClientRect();
    const x = Math.round((1920 / rect.width) * (e.clientX - rect.left));
    const y = Math.round((1080 / rect.height) * (e.clientY - rect.top));
    nekoClient?.sendData('mousemove', { x, y });
  };

  const handleMouseDown = (e: React.MouseEvent) => {
    if (!nekoClient || !hasControl || !isConnected) return;
    
    e.preventDefault();
    nekoClient?.sendData('mousedown', { key: e.button + 1 });
  };

  const handleMouseUp = (e: React.MouseEvent) => {
    if (!nekoClient || !hasControl || !isConnected) return;
    
    e.preventDefault();
    nekoClient?.sendData('mouseup', { key: e.button + 1 });
  };

  const handleWheel = (e: React.WheelEvent) => {
    if (!nekoClient || !hasControl || !isConnected) return;
    
    e.preventDefault();
    const x = Math.min(Math.max(e.deltaX, -5), 5);
    const y = Math.min(Math.max(e.deltaY, -5), 5);
    
    nekoClient?.sendData('wheel', { x: -x, y: -y });
  };

  const getKeyCode = (e: React.KeyboardEvent): number => {

  
    if (e.key.length === 1) {
      const char = e.key;
      const code = char.charCodeAt(0);
      
      if (code >= 32 && code <= 126) {
        return code;
      }
    }
    
    switch (e.code) {
      case 'Enter': 
      case 'NumpadEnter': 
        return 0xff0d;
      case 'Backspace': 
        return 0xff08; 
      case 'Delete': 
        return 0xffff;
      case 'Tab': 
        return 0xff09;
      case 'Escape': 
        return 0xff1b;
      case 'Space': 
        return 0x0020;

      // Arrow keys  
      case 'ArrowUp': 
        return 0xff52;
      case 'ArrowDown': 
        return 0xff54;
      case 'ArrowLeft': 
        return 0xff51;
      case 'ArrowRight': 
        return 0xff53;

      // Home/End/Page keys
      case 'Home': 
        return 0xff50;
      case 'End': 
        return 0xff57;
      case 'PageUp': 
        return 0xff55;
      case 'PageDown': 
        return 0xff56;

      // Function keys
      case 'F1': return 0xffbe;
      case 'F2': return 0xffbf;
      case 'F3': return 0xffc0;
      case 'F4': return 0xffc1;
      case 'F5': return 0xffc2;
      case 'F6': return 0xffc3;
      case 'F7': return 0xffc4;
      case 'F8': return 0xffc5;
      case 'F9': return 0xffc6;
      case 'F10': return 0xffc7;
      case 'F11': return 0xffc8;
      case 'F12': return 0xffc9;
        
      // Modifier keys
      case 'ShiftLeft':
      case 'ShiftRight':
        return 0xffe1;
      case 'ControlLeft':
      case 'ControlRight':
        return 0xffe3;
      case 'AltLeft':
      case 'AltRight':
        return 0xffe9;

      default:
        if (e.key.length === 1) {
          return e.key.charCodeAt(0);
        }
        return 0;
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (!nekoClient || !hasControl || !isConnected) return;
    
    e.preventDefault();
    e.stopPropagation();
    
    if (overlayRef.current) {
      const rect = overlayRef.current.getBoundingClientRect();
      const centerX = Math.round((1920 / rect.width) * (rect.width / 2));
      const centerY = Math.round((1080 / rect.height) * (rect.height / 2));
      
      nekoClient?.sendData('mousemove', { x: centerX, y: centerY });
      
      setTimeout(() => {
        const key = getKeyCode(e);
        nekoClient?.sendData('keydown', { key });
      }, 1);
    } else {
      const key = getKeyCode(e);
      nekoClient?.sendData('keydown', { key });
    }
  };

  const handleKeyUp = (e: React.KeyboardEvent) => {
    if (!nekoClient || !hasControl || !isConnected) return;
    
    e.preventDefault();
    e.stopPropagation();
    
    setTimeout(() => {
      const key = getKeyCode(e);
      nekoClient?.sendData('keyup', { key });
    }, 10);
  };

  return (
    <div 
      ref={containerRef}
      className={`w-full h-full bg-gray-900 ${isFullscreen ? '' : 'rounded-xl'} overflow-hidden relative`}
    >
      {/* Header Controls - Hidden in fullscreen mode */}
      {!isFullscreen && (
        <div className="absolute top-0 left-0 right-0 z-10 bg-black bg-opacity-75 text-white p-3 flex justify-between items-center">
          <div className="flex items-center space-x-4">
            <div className="flex items-center space-x-2">
              <Monitor className="w-4 h-4 text-green-400" />
              <span className="text-sm font-medium">
                {isConnected ? 'Virtual Browser Active' : 'Virtual Browser Connecting...'}
              </span>
              {hasControl && isConnected && (
                <span className="text-xs bg-blue-600 px-2 py-1 rounded">
                  Controlling
                </span>
              )}
            </div>
            <div className="flex items-center space-x-2">
              <Clock className="w-4 h-4 text-blue-400" />
              <span className="text-sm">{timeRemaining}</span>
            </div>
          </div>
          
          <div className="flex items-center space-x-2">
            {/* Volume Control */}
            <button
              onClick={toggleMute}
              className="p-2 rounded-lg bg-white bg-opacity-20 hover:bg-opacity-30 transition-colors"
              title={isMuted ? 'Unmute' : 'Mute'}
              disabled={!isConnected}
            >
              {isMuted ? (
                <VolumeX className="w-4 h-4" />
              ) : (
                <Volume2 className="w-4 h-4" />
              )}
            </button>

            {/* Fullscreen Control */}
            <button
              onClick={toggleFullscreen}
              className="p-2 rounded-lg bg-white bg-opacity-20 hover:bg-opacity-30 transition-colors"
              title={isFullscreen ? 'Exit Fullscreen' : 'Enter Fullscreen'}
              disabled={!isConnected}
            >
              {isFullscreen ? (
                <Minimize className="w-4 h-4" />
              ) : (
                <Maximize className="w-4 h-4" />
              )}
            </button>
            
            {/* Stop Virtual Browser Button - Only for controllers */}
            {hasControl && (
              <button
                onClick={onRelease}
                className="px-3 py-1.5 bg-red-600 hover:bg-red-700 rounded-lg text-sm font-medium transition-colors"
              >
                Stop Virtual Browser
              </button>
            )}
          </div>
        </div>
      )}

      {/* Browser Frame */}
      <div className={`w-full h-full ${isFullscreen ? '' : 'pt-16'} relative`}>
        <video
          ref={videoRef}
          className="w-full h-full bg-black object-contain"
          autoPlay
          playsInline
          muted={isMuted}
          onClick={handleVideoClick}
        />
        
        {hasControl && isConnected && (
          <div
            ref={overlayRef}
            className={`absolute inset-0 ${isInteracting ? 'cursor-none bg-blue-500 bg-opacity-5' : 'cursor-pointer hover:bg-blue-500 hover:bg-opacity-10'} outline-none transition-all`}
            tabIndex={0}
            onMouseEnter={handleMouseEnter}
            onMouseLeave={handleMouseLeave}
            onMouseMove={handleMouseMove}
            onMouseDown={handleMouseDown}
            onMouseUp={handleMouseUp}
            onWheel={handleWheel}
            onKeyDown={handleKeyDown}
            onKeyUp={handleKeyUp}
            onFocus={() => {}}
          />
        )}
      </div>
    </div>
  );
}
export default VirtualBrowserViewer;