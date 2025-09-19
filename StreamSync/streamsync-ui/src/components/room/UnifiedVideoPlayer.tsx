import React, { useRef, useEffect, useState } from 'react';
import ReactPlayer from 'react-player';
import { Play, Pause, Volume2, VolumeX, Maximize, Minimize, RotateCcw, RotateCw } from 'lucide-react';
import Button from '../common/Button';
import { toast } from 'react-hot-toast';

interface UnifiedVideoPlayerProps {
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

const UnifiedVideoPlayer: React.FC<UnifiedVideoPlayerProps> = ({
    src,
    isPlaying,
    position,
    duration,
    onPlay,
    onPause,
    onSeek,
    onTimeUpdate,
    onDurationUpdate,
    hasControl = true,
    syncMode = 'strict',
    className = ''
}) => {
    const playerRef = useRef<any>(null);
    const [volume, setVolume] = useState(1);
    const [isMuted, setIsMuted] = useState(false);
    const [isFullscreen, setIsFullscreen] = useState(false);
    const [showControls, setShowControls] = useState(true);
    const [isUserSeeking, setIsUserSeeking] = useState(false);
    const [recentUserAction, setRecentUserAction] = useState(false);
    let controlsTimeout: NodeJS.Timeout;

    useEffect(() => {
        if (playerRef.current && !isUserSeeking && !recentUserAction) {
            const currentTime = playerRef.current.getCurrentTime();
            const timeDiff = Math.abs(currentTime - position);
            
            if (timeDiff > 2) {
                playerRef.current.seekTo(position, 'seconds');
            }
        }
    }, [position, isUserSeeking, recentUserAction]);

    useEffect(() => {
        if (recentUserAction) {
            const timeout = setTimeout(() => setRecentUserAction(false), 1000);
            return () => clearTimeout(timeout);
        }
    }, [recentUserAction]);

    useEffect(() => {
        const handleFullscreenChange = () => {
            setIsFullscreen(!!document.fullscreenElement);
        };

        document.addEventListener('fullscreenchange', handleFullscreenChange);
        document.addEventListener('webkitfullscreenchange', handleFullscreenChange);
        document.addEventListener('mozfullscreenchange', handleFullscreenChange);
        document.addEventListener('MSFullscreenChange', handleFullscreenChange);

        return () => {
            document.removeEventListener('fullscreenchange', handleFullscreenChange);
            document.removeEventListener('webkitfullscreenchange', handleFullscreenChange);
            document.removeEventListener('mozfullscreenchange', handleFullscreenChange);
            document.removeEventListener('MSFullscreenChange', handleFullscreenChange);
        };
    }, []);

    const handleProgress = (state: any) => {
        if (!isUserSeeking && state.playedSeconds) {
            onTimeUpdate(state.playedSeconds);
        }
    };

    const handleDuration = (duration: number) => {
        if (onDurationUpdate) {
            onDurationUpdate(duration);
        }
    };

    const canControlPlayback = syncMode === 'relaxed' || hasControl;

    const handlePlayPause = () => {
        if (!canControlPlayback) {
            toast.error('Only the controller can control playback in strict mode');
            return;
        }
        
        setRecentUserAction(true);
        if (isPlaying) {
            onPause();
        } else {
            onPlay();
        }
    };

    const handleSeekClick = (e: React.MouseEvent<HTMLDivElement>) => {
        if (!canControlPlayback) {
            toast.error('Only the controller can control playback in strict mode');
            return;
        }
        
        const rect = e.currentTarget.getBoundingClientRect();
        const clickX = e.clientX - rect.left;
        const percentage = clickX / rect.width;
        const newTime = percentage * duration;
        
        setIsUserSeeking(true);
        if (playerRef.current) {
            playerRef.current.seekTo(newTime, 'seconds');
        }
        onSeek(newTime);
        setTimeout(() => setIsUserSeeking(false), 1000);
    };

    const toggleMute = () => {
        setIsMuted(!isMuted);
    };

    const handleVolumeChange = (newVolume: number) => {
        setVolume(newVolume);
        setIsMuted(newVolume === 0);
    };

    const toggleFullscreen = () => {
        const videoContainer = document.querySelector('.fullscreen-player') as HTMLElement;
        
        if (!document.fullscreenElement && videoContainer) {
            videoContainer.requestFullscreen()
                .then(() => {
                    setIsFullscreen(true);
                })
                .catch((error) => {
                    });
        } else if (document.fullscreenElement) {
            document.exitFullscreen()
                .then(() => {
                    setIsFullscreen(false);
                })
                .catch((error) => {
                    setIsFullscreen(false);
                });
        }
    };

    const skipBackward = () => {
        if (!canControlPlayback) return;
        
        setIsUserSeeking(true);
        const newTime = Math.max(0, position - 10);
        
        if (playerRef.current) {
            playerRef.current.seekTo(newTime, 'seconds');
        }
        
        onSeek(newTime);
        setTimeout(() => setIsUserSeeking(false), 1000);
    };

    const skipForward = () => {
        if (!canControlPlayback) return;
        
        setIsUserSeeking(true);
        const newTime = Math.min(duration, position + 10);
        
        if (playerRef.current) {
            playerRef.current.seekTo(newTime, 'seconds');
        }
        
        onSeek(newTime);
        setTimeout(() => setIsUserSeeking(false), 1000);
    };

    const formatTime = (seconds: number): string => {
        const mins = Math.floor(seconds / 60);
        const secs = Math.floor(seconds % 60);
        return `${mins}:${secs.toString().padStart(2, '0')}`;
    };

    const showControlsTemporary = () => {
        setShowControls(true);
        clearTimeout(controlsTimeout);
        controlsTimeout = setTimeout(() => setShowControls(false), 3000);
    };

    return (
        <div 
            className={`relative bg-black group ${className} fullscreen-player w-full h-full`}
            onMouseEnter={() => setShowControls(true)}
            onMouseLeave={() => setShowControls(false)}
            onMouseMove={showControlsTemporary}
            style={{ minHeight: '400px' }}
        >
            <ReactPlayer
                ref={playerRef}
                url={src}
                playing={isPlaying}
                volume={isMuted ? 0 : volume}
                width="100%"
                height="100%"
                controls={false}
                onProgress={handleProgress}
                onDuration={handleDuration}
                onPlay={onPlay}
                onPause={onPause}
                onClick={handlePlayPause}
                style={{
                    position: 'absolute',
                    top: 0,
                    left: 0,
                    width: '100%',
                    height: '100%'
                }}
                config={{
                    youtube: {
                        playerVars: {
                            origin: window.location.origin,
                            modestbranding: 1,
                            rel: 0,
                            showinfo: 0
                        }
                    }
                }}
                className="react-player-wrapper"
            />
            {!isPlaying && (
                <div className="absolute inset-0 flex items-center justify-center">
                    <Button
                        variant="secondary"
                        size="lg"
                        icon={Play}
                        onClick={handlePlayPause}
                        disabled={!canControlPlayback}
                        className={`border-0 p-4 rounded-full ${
                            canControlPlayback 
                                ? 'bg-black/50 hover:bg-black/70 text-white' 
                                : 'bg-gray-500/50 text-gray-400 cursor-not-allowed'
                        }`}
                    />
                    {!canControlPlayback && (
                        <div className="absolute top-full mt-2 text-center text-white text-sm bg-black/70 px-3 py-1 rounded">
                            {syncMode === 'relaxed' ? 'Manual mode - control your own playback' : 'You don\'t have control'}
                        </div>
                    )}
                </div>
            )}

            {/* Controls */}
            <div 
                className={`absolute bottom-0 left-0 right-0 bg-gradient-to-t from-black/80 via-black/40 to-transparent p-4 transition-opacity duration-300 ${
                    showControls ? 'opacity-100' : 'opacity-0'
                }`}
            >
                {/* Progress bar */}
                <div className="mb-4">
                    <div 
                        className={`w-full h-2 bg-gray-600 rounded-full relative ${
                            canControlPlayback ? 'cursor-pointer' : 'cursor-not-allowed opacity-60'
                        }`}
                        onClick={handleSeekClick}
                        title={canControlPlayback ? 'Click to seek' : 'You don\'t have control to seek'}
                    >
                        {/* Current progress */}
                        <div 
                            className="bg-red-500 h-2 rounded-full transition-all duration-200 relative z-10"
                            style={{ width: `${duration > 0 ? (position / duration) * 100 : 0}%` }}
                        />
                        {/* Progress thumb */}
                        <div 
                            className="absolute top-0 w-4 h-4 bg-red-500 rounded-full transform -translate-x-2 -translate-y-1 cursor-pointer z-20"
                            style={{ left: `${duration > 0 ? (position / duration) * 100 : 0}%` }}
                        />
                    </div>
                </div>

                {/* Control buttons */}
                <div className="flex items-center justify-between text-white">
                    <div className="flex items-center space-x-2">
                        <Button
                            variant="ghost"
                            size="sm"
                            icon={RotateCcw}
                            onClick={skipBackward}
                            disabled={!canControlPlayback}
                            className={`p-2 ${canControlPlayback ? 'text-white hover:bg-white/20' : 'text-gray-400 cursor-not-allowed opacity-50'}`}
                            title={canControlPlayback ? "Skip back 10 seconds" : "You don't have control"}
                        />
                        
                        <Button
                            variant="ghost"
                            size="sm"
                            icon={isPlaying ? Pause : Play}
                            onClick={handlePlayPause}
                            disabled={!canControlPlayback}
                            className={`p-2 ${canControlPlayback ? 'text-white hover:bg-white/20' : 'text-gray-400 cursor-not-allowed opacity-50'}`}
                            title={canControlPlayback ? (isPlaying ? "Pause" : "Play") : "You don't have control"}
                        />
                        
                        <Button
                            variant="ghost"
                            size="sm"
                            icon={RotateCw}
                            onClick={skipForward}
                            disabled={!canControlPlayback}
                            className={`p-2 ${canControlPlayback ? 'text-white hover:bg-white/20' : 'text-gray-400 cursor-not-allowed opacity-50'}`}
                            title={canControlPlayback ? "Skip forward 10 seconds" : "You don't have control"}
                        />

                        <div className="flex items-center space-x-2 ml-4">
                            <Button
                                variant="ghost"
                                size="sm"
                                icon={isMuted ? VolumeX : Volume2}
                                onClick={toggleMute}
                                className="text-white hover:bg-white/20 p-2"
                            />
                            <div className="relative w-16 h-2">
                                <input
                                    type="range"
                                    min="0"
                                    max="1"
                                    step="0.1"
                                    value={isMuted ? 0 : volume}
                                    onChange={(e) => handleVolumeChange(parseFloat(e.target.value))}
                                    className="absolute inset-0 w-full h-full opacity-0 cursor-pointer z-10"
                                />
                                <div className="w-full h-2 bg-gray-600 rounded-full">
                                    <div 
                                        className="h-full bg-white rounded-full transition-all duration-150"
                                        style={{ width: `${(isMuted ? 0 : volume) * 100}%` }}
                                    />
                                </div>
                            </div>
                        </div>
                    </div>

                    <div className="flex items-center space-x-2">
                        <span className="text-sm">
                            {formatTime(position)} / {formatTime(duration)}
                        </span>
                        
                        <Button
                            variant="ghost"
                            size="sm"
                            icon={isFullscreen ? Minimize : Maximize}
                            onClick={toggleFullscreen}
                            className="text-white hover:bg-white/20 p-2"
                            title={isFullscreen ? "Exit fullscreen" : "Enter fullscreen"}
                        />
                    </div>
                </div>
            </div>
        </div>
    );
};

export default UnifiedVideoPlayer;
