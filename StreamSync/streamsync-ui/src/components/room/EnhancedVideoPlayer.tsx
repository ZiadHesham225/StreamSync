import React from 'react';
import UnifiedVideoPlayer from './UnifiedVideoPlayer';

interface EnhancedVideoPlayerProps {
    src: string;
    isPlaying: boolean;
    position: number;
    duration: number;
    onPlay: () => void;
    onPause: () => void;
    onSeek: (position: number) => void;
    onTimeUpdate: (position: number) => void;
    onDurationUpdate?: (duration: number) => void;
    hasControl?: boolean;
    syncMode?: string;
    className?: string;
}

const EnhancedVideoPlayer: React.FC<EnhancedVideoPlayerProps> = (props) => {
    return <UnifiedVideoPlayer {...props} />;
};

export default EnhancedVideoPlayer;
