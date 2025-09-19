import React, { useState } from 'react';
import { Video, ArrowLeft } from 'lucide-react';
import VideoSelector from './VideoSelector';
import Button from '../common/Button';

interface VideoControlPanelProps {
    hasControl: boolean;
    onVideoSelect: (videoUrl: string, videoTitle: string, videoThumbnail?: string) => void;
    onBack?: () => void;
}

const VideoControlPanel: React.FC<VideoControlPanelProps> = ({ hasControl, onVideoSelect, onBack }) => {
    const [showVideoSelector, setShowVideoSelector] = useState(false);

    if (!hasControl) {
        return null;
    }

    return (
        <>
            <div className="absolute top-4 right-4 z-10 flex space-x-2">
                {onBack && (
                    <Button
                        size="sm"
                        variant="secondary"
                        icon={ArrowLeft}
                        onClick={onBack}
                        className="bg-black/70 text-white hover:bg-black/90"
                    >
                        Back to Options
                    </Button>
                )}
                <Button
                    size="sm"
                    variant="secondary"
                    icon={Video}
                    onClick={() => setShowVideoSelector(true)}
                    className="bg-black/70 text-white hover:bg-black/90"
                >
                    Change Video
                </Button>
            </div>

            {showVideoSelector && (
                <div className="fixed inset-0 bg-black/50 flex items-center justify-center p-4 z-50">
                    <div className="bg-gray-900 rounded-2xl shadow-2xl border border-gray-800 max-w-6xl w-full max-h-[90vh] overflow-y-auto">
                        <div className="flex justify-between items-center p-4 border-b border-gray-800">        
                            <h2 className="text-xl font-semibold text-white">Change Video</h2>
                            <button
                                onClick={() => setShowVideoSelector(false)}
                                className="text-gray-400 hover:text-gray-200 text-2xl"
                            >
                                ×
                            </button>
                        </div>
                        <div className="p-4 bg-gray-900">
                            <VideoSelector
                                onVideoSelect={(url, title, thumbnail) => {
                                    onVideoSelect(url, title, thumbnail);
                                    setShowVideoSelector(false);
                                }}
                                hasControl={hasControl}
                            />
                        </div>
                    </div>
                </div>
            )}
        </>
    );
};

export default VideoControlPanel;
