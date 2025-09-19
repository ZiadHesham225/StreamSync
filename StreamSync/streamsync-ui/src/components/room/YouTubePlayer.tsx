import React, { useRef, useEffect, useState } from 'react';
import { VideoUrlUtils } from '../../utils/videoUrlUtils';

// YouTube API type declarations
declare global {
    interface Window {
        YT: any;
        onYouTubeIframeAPIReady: () => void;
    }
}

interface YouTubePlayerProps {
    videoId: string;
    isPlaying: boolean;
    position: number;
    onPlay: () => void;
    onPause: () => void;
    onSeek: (position: number) => void;
    onTimeUpdate: (position: number) => void;
    onDurationUpdate?: (duration: number) => void;
    hasControl?: boolean;
    className?: string;
}

const YouTubePlayer: React.FC<YouTubePlayerProps> = ({
    videoId,
    isPlaying,
    position,
    onPlay,
    onPause,
    onSeek,
    onTimeUpdate,
    onDurationUpdate,
    hasControl = true,
    className = ''
}) => {
    const playerRef = useRef<any>(null);
    const containerRef = useRef<HTMLDivElement>(null);
    const [isReady, setIsReady] = useState(false);
    const [isUserSeeking, setIsUserSeeking] = useState(false);
    const [recentUserAction, setRecentUserAction] = useState(false);

    useEffect(() => {
        if (!window.YT) {
            const script = document.createElement('script');
            script.src = 'https://www.youtube.com/iframe_api';
            document.body.appendChild(script);
            
            window.onYouTubeIframeAPIReady = initializePlayer;
        } else {
            initializePlayer();
        }

        return () => {
            if (playerRef.current) {
                playerRef.current.destroy();
            }
        };
    }, []);

    useEffect(() => {
        if (videoId && isReady) {
            playerRef.current?.loadVideoById(videoId);
        }
    }, [videoId, isReady]);

    useEffect(() => {
        if (!playerRef.current || !isReady) return;

        const timeDiff = Math.abs(playerRef.current.getCurrentTime() - position);
        const isControllingUser = hasControl;
        
        let shouldSeek = false;
        
        if (isControllingUser) {
            shouldSeek = timeDiff > 2.5 && !isUserSeeking && !recentUserAction;
        } else {
            shouldSeek = timeDiff > 1.5 && !isUserSeeking;
        }
        
        if (shouldSeek || (isUserSeeking && timeDiff > 5.0)) {
            playerRef.current.seekTo(position, true);
        }

        if (isPlaying && playerRef.current.getPlayerState() !== 1) {
            playerRef.current.playVideo();
        } else if (!isPlaying && playerRef.current.getPlayerState() === 1) {
            playerRef.current.pauseVideo();
        }
    }, [isPlaying, position]);

    const initializePlayer = () => {
        if (!containerRef.current) return;

        playerRef.current = new window.YT.Player(containerRef.current, {
            height: '100%',
            width: '100%',
            videoId: videoId,
            playerVars: {
                controls: hasControl ? 1 : 0,
                disablekb: !hasControl ? 1 : 0,
                fs: 1,
                rel: 0,
                modestbranding: 1,
                iv_load_policy: 3,
                cc_load_policy: 0,
                playsinline: 1,
                origin: window.location.origin
            },
            events: {
                onReady: onPlayerReady,
                onStateChange: onPlayerStateChange,
                onError: onPlayerError
            }
        });
    };

    const onPlayerReady = () => {
        setIsReady(true);
        if (onDurationUpdate) {
            const duration = playerRef.current.getDuration();
            onDurationUpdate(duration);
        }
    };

    const onPlayerStateChange = (event: any) => {
        const state = event.data;
        if (hasControl) {
            if (state === 1 && !isPlaying) {
                setRecentUserAction(true);
                setTimeout(() => setRecentUserAction(false), 1500);
                onPlay();
            } else if (state === 2 && isPlaying) {
                setRecentUserAction(true);
                setTimeout(() => setRecentUserAction(false), 1500);
                onPause();
            }
        }
        if (state === 1) {
            const interval = setInterval(() => {
                if (playerRef.current && playerRef.current.getPlayerState() === 1) {
                    if (!isUserSeeking) {
                        onTimeUpdate(playerRef.current.getCurrentTime());
                    }
                } else {
                    clearInterval(interval);
                }
            }, 500);
        }
    };

    const onPlayerError = (event: any) => {
        };

    return (
        <div className={`relative bg-black ${className}`}>
            <div ref={containerRef} className="w-full h-full" />
            
            {!isReady && (
                <div className="absolute inset-0 flex items-center justify-center bg-black">
                    <div className="text-white text-lg">Loading YouTube player...</div>
                </div>
            )}
            
            {!hasControl && (
                <div className="absolute top-4 right-4 bg-black/70 text-white px-3 py-1 rounded text-sm">
                    You don't have control
                </div>
            )}
        </div>
    );
};

export default YouTubePlayer;
