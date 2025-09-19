import React from 'react';
import { Play, Monitor } from 'lucide-react';

interface RoomInitialChoiceProps {
  onPlayVideo: () => void;
  onStartVirtualBrowser: () => void;
  hasControl: boolean;
  cooldownTimeRemaining?: number;
  isInQueue?: boolean;
}

const RoomInitialChoice: React.FC<RoomInitialChoiceProps> = ({
  onPlayVideo,
  onStartVirtualBrowser,
  hasControl,
  cooldownTimeRemaining = 0,
  isInQueue = false
}) => {
  if (!hasControl) {
    return (
      <div className="w-full h-full bg-gray-900 flex items-center justify-center">
        <div className="text-center text-white">
          <div className="text-6xl mb-4">🎬</div>
          <h3 className="text-xl font-semibold mb-2">Waiting for host to start</h3>
          <p className="text-gray-400">The room controller will choose how to watch content</p>
        </div>
      </div>
    );
  }

  return (
    <div className="w-full h-full bg-gray-900 flex items-center justify-center">
      <div className="text-center text-white max-w-xl">
        <div className="text-6xl mb-6">🎬</div>
        <h3 className="text-2xl font-bold mb-4">Ready to Watch?</h3>
        <p className="text-gray-400 mb-8">Choose how you'd like to watch content together</p>
        
        <div className="space-y-4">
          {/* Play Video Option */}
          <button
            onClick={onPlayVideo}
            className="w-full bg-blue-600 hover:bg-blue-700 text-white py-4 px-5 rounded-xl transition-colors flex items-center justify-center space-x-3"
          >
            <Play className="w-5 h-5" />
            <div className="text-left">
              <div className="font-semibold">Play Video</div>
              <div className="text-sm text-blue-200">Watch YouTube videos or direct links</div>
            </div>
          </button>
          
          {/* Virtual Browser Option */}
          <button
            onClick={cooldownTimeRemaining > 0 || isInQueue ? undefined : onStartVirtualBrowser}
            disabled={cooldownTimeRemaining > 0 || isInQueue}
            className={`w-full py-4 px-5 rounded-xl transition-colors flex items-center justify-center space-x-3 ${
              cooldownTimeRemaining > 0 || isInQueue
                ? 'bg-gray-600 cursor-not-allowed text-gray-300'
                : 'bg-purple-600 hover:bg-purple-700 text-white'
            }`}
          >
            <Monitor className="w-5 h-5" />
            <div className="text-left">
              <div className="font-semibold">
                {isInQueue
                  ? 'Virtual Browser (In Queue)'
                  : cooldownTimeRemaining > 0 
                    ? `Virtual Browser (Cooldown: ${Math.floor(cooldownTimeRemaining / 60)}:${(cooldownTimeRemaining % 60).toString().padStart(2, '0')})`
                    : 'Start Virtual Browser'
                }
              </div>
              <div className={`text-sm ${cooldownTimeRemaining > 0 || isInQueue ? 'text-gray-400' : 'text-purple-200'}`}>
                {isInQueue
                  ? 'You are already waiting in the virtual browser queue'
                  : cooldownTimeRemaining > 0 
                    ? 'Please wait before requesting another virtual browser'
                    : 'Browse any website together'
                }
              </div>
            </div>
          </button>
        </div>
        
        <p className="text-xs text-gray-500 mt-6">
          Virtual browsers have a 3-hour time limit and may have a queue during peak times
        </p>
      </div>
    </div>
  );
};

export default RoomInitialChoice;
