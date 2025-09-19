import React, { useState, useEffect } from 'react';
import { Monitor, Clock, CheckCircle, XCircle } from 'lucide-react';

interface VirtualBrowserNotificationModalProps {
  isOpen: boolean;
  timeRemaining: number; // in seconds
  onAccept: () => void;
  onDecline: () => void;
}

const VirtualBrowserNotificationModal: React.FC<VirtualBrowserNotificationModalProps> = ({
  isOpen,
  timeRemaining,
  onAccept,
  onDecline
}) => {
  const [countdown, setCountdown] = useState(timeRemaining);

  useEffect(() => {
    if (isOpen) {
      const validTimeRemaining = isNaN(timeRemaining) || timeRemaining < 0 ? 120 : timeRemaining;
      setCountdown(validTimeRemaining);
      
      const interval = setInterval(() => {
        setCountdown(prev => {
          if (prev <= 1) {
            clearInterval(interval);
            onDecline();
            return 0;
          }
          return prev - 1;
        });
      }, 1000);

      return () => clearInterval(interval);
    }
  }, [isOpen, timeRemaining, onDecline]);

  if (!isOpen) return null;

  const formatTime = (seconds: number) => {
    const validSeconds = isNaN(seconds) || seconds < 0 ? 0 : Math.floor(seconds);
    const mins = Math.floor(validSeconds / 60);
    const secs = validSeconds % 60;
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  };

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="bg-white/90 dark:bg-gray-900/90 backdrop-blur-xl rounded-2xl shadow-2xl border border-gray-200/50 dark:border-gray-700/50 p-8 max-w-md w-full mx-4 relative overflow-hidden">
        {/* Background decoration */}
        <div className="absolute top-0 right-0 w-32 h-32 bg-gradient-to-br from-green-500/10 to-blue-500/10 rounded-full blur-2xl"></div>
        <div className="absolute bottom-0 left-0 w-24 h-24 bg-gradient-to-tr from-blue-500/10 to-green-500/10 rounded-full blur-xl"></div>
        
        <div className="text-center relative z-10">
          {/* Icon */}
          <div className="mx-auto w-16 h-16 bg-gradient-to-r from-green-500 to-emerald-500 rounded-full flex items-center justify-center mb-4 shadow-lg">
            <Monitor className="w-8 h-8 text-white" />
          </div>
          
          {/* Title */}
          <h3 className="text-xl font-bold mb-2">
            <span className="bg-gradient-to-r from-green-600 to-emerald-600 bg-clip-text text-transparent">
              Virtual Browser Available!
            </span>
          </h3>
          
          {/* Description */}
          <p className="text-gray-600 dark:text-gray-300 mb-6">
            Your virtual browser is ready. Would you like to start your session now?
          </p>
          
          {/* Countdown */}
          <div className="bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm rounded-xl p-4 mb-6 border border-gray-200/50 dark:border-gray-700/50">
            <div className="flex items-center justify-center space-x-2 text-gray-700 dark:text-gray-300">
              <Clock className="w-5 h-5" />
              <span className="font-mono text-xl font-semibold">{formatTime(countdown)}</span>
            </div>
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">Time remaining to respond</p>
          </div>
          
          {/* Buttons */}
          <div className="flex space-x-3">
            <button
              onClick={onDecline}
              className="flex-1 bg-white/80 dark:bg-gray-700/80 backdrop-blur-sm hover:bg-white dark:hover:bg-gray-700 text-gray-800 dark:text-gray-200 font-semibold py-3 px-4 rounded-xl transition-all duration-200 flex items-center justify-center space-x-2 border border-gray-200/50 dark:border-gray-600/50 shadow-lg hover:shadow-xl transform hover:-translate-y-0.5"
            >
              <XCircle className="w-5 h-5" />
              <span>Decline</span>
            </button>
            
            <button
              onClick={onAccept}
              className="flex-1 bg-gradient-to-r from-green-600 to-emerald-600 hover:from-green-700 hover:to-emerald-700 text-white font-semibold py-3 px-4 rounded-xl transition-all duration-200 flex items-center justify-center space-x-2 shadow-lg hover:shadow-xl transform hover:-translate-y-0.5"
            >
              <CheckCircle className="w-5 h-5" />
              <span>Accept</span>
            </button>
          </div>
          
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-4">
            If you don't respond, the virtual browser will be offered to the next person in queue
          </p>
        </div>
      </div>
    </div>
  );
};

export default VirtualBrowserNotificationModal;
