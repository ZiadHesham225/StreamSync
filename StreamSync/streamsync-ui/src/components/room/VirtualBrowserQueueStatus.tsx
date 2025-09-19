import React, { useState, useEffect } from 'react';
import { Clock, Users } from 'lucide-react';
import { VirtualBrowserQueue } from '../../types/index';

interface VirtualBrowserQueueStatusProps {
  queueStatus: VirtualBrowserQueue;
  onCancelQueue: () => void;
  hasControl: boolean;
}

const VirtualBrowserQueueStatus: React.FC<VirtualBrowserQueueStatusProps> = ({
  queueStatus,
  onCancelQueue,
  hasControl
}) => {
  const [estimatedWaitTime, setEstimatedWaitTime] = useState<string>('');

  useEffect(() => {
    const averageSessionMinutes = 120;
    const estimatedMinutes = queueStatus.position * (averageSessionMinutes / 4);
    
    if (estimatedMinutes < 60) {
      setEstimatedWaitTime(`~${Math.round(estimatedMinutes)} minutes`);
    } else {
      const hours = Math.floor(estimatedMinutes / 60);
      const minutes = Math.round(estimatedMinutes % 60);
      setEstimatedWaitTime(`~${hours}h ${minutes}m`);
    }
  }, [queueStatus.position]);

  return (
    <div className="p-4">
      <div className="bg-gradient-to-br from-indigo-900/80 via-purple-900/80 to-blue-900/80 backdrop-blur-xl rounded-2xl shadow-xl border border-indigo-700/40 p-4 relative">
        <div className="absolute top-0 right-0 w-16 h-16 bg-gradient-to-br from-blue-500/20 to-purple-600/20 rounded-full blur-xl pointer-events-none"></div>
        <div className="flex items-start justify-between relative z-10">
          <div className="flex items-center space-x-2 flex-1 min-w-0">
            <div className="flex-shrink-0">
              <Clock className="w-5 h-5 text-yellow-600" />
            </div>
            <div className="min-w-0 flex-1">
              <h4 className="font-semibold text-blue-300 text-sm bg-gradient-to-r from-blue-400 to-purple-400 bg-clip-text text-transparent">Virtual Browser Queue</h4>
              <div className="text-xs text-blue-200 space-y-1">
                <div className="flex items-center space-x-1">
                  <Users className="w-3 h-3 text-blue-400" />
                  <span>Position <strong className="text-yellow-300">#{queueStatus.position}</strong></span>
                </div>
                <div className="flex items-center space-x-1">
                  <Clock className="w-3 h-3 text-blue-400" />
                  <span>Wait: <strong className="text-yellow-300">{estimatedWaitTime}</strong></span>
                </div>
                <p className="text-xs opacity-80 text-blue-100">You can continue using the video player while waiting</p>
              </div>
            </div>
          </div>
          
          {hasControl && (
            <button
              onClick={onCancelQueue}
              className="relative z-20 flex-shrink-0 ml-2 px-3 py-1.5 text-xs font-medium bg-gradient-to-r from-blue-500 to-purple-600 hover:from-blue-600 hover:to-purple-700 text-white rounded-xl shadow-lg transition-all duration-200 cursor-pointer touch-manipulation"
              title="Cancel waiting"
              style={{ pointerEvents: 'auto' }}
            >
              Cancel
            </button>
          )}
        </div>
      </div>
    </div>
  );
};

export default VirtualBrowserQueueStatus;