import React, { useState } from 'react';
import { Search, Play, Clock, ExternalLink, Link, ArrowLeft } from 'lucide-react';
import { YouTubeVideo, YouTubeSearchResponse } from '../../types';
import { youTubeService } from '../../services/youTubeService';
import { VideoUrlUtils } from '../../utils/videoUrlUtils';
import Button from '../common/Button';
import Input from '../common/Input';
import toast from 'react-hot-toast';

interface VideoSelectorProps {
    onVideoSelect: (videoUrl: string, videoTitle: string, videoThumbnail?: string) => void;
    hasControl: boolean;
    onBack?: () => void;
}

const VideoSelector: React.FC<VideoSelectorProps> = ({ onVideoSelect, hasControl, onBack }) => {
    const [searchQuery, setSearchQuery] = useState('');
    const [directUrl, setDirectUrl] = useState('');
    const [searchResults, setSearchResults] = useState<YouTubeVideo[]>([]);
    const [isSearching, setIsSearching] = useState(false);
    const [activeTab, setActiveTab] = useState<'url' | 'search'>('url');
    const [showResults, setShowResults] = useState(false);

    const handleSearch = async () => {
        if (!searchQuery.trim()) return;
        
        setIsSearching(true);
        try {
            const response: YouTubeSearchResponse = await youTubeService.searchVideos(searchQuery, 10);
            setSearchResults(response.videos);
            setShowResults(true);
        } catch (error) {
            toast.error('Failed to search YouTube videos');
            } finally {
            setIsSearching(false);
        }
    };

    const handleVideoSelect = (video: YouTubeVideo) => {
        onVideoSelect(video.videoUrl, video.title, video.thumbnailUrl);
        toast.success(`Selected: ${video.title}`);
    };

    const handleDirectUrlSubmit = () => {
        if (!directUrl.trim()) return;
        
        if (!VideoUrlUtils.validateVideoUrl(directUrl)) {
            toast.error('Please enter a valid video URL');
            return;
        }

        const title = VideoUrlUtils.isYouTubeUrl(directUrl) ? 'YouTube Video' : 'Video';
        const thumbnail = VideoUrlUtils.getThumbnail(directUrl);
        
        onVideoSelect(directUrl, title, thumbnail || undefined);
        toast.success('Video URL set successfully');
    };

    const formatDuration = (duration: string) => {
        return youTubeService.formatDuration(duration);
    };

    if (!hasControl) {
        return (
            <div className="flex flex-col items-center justify-center h-96 bg-gray-100 rounded-lg">
                <Play className="h-16 w-16 text-gray-400 mb-4" />
                <h3 className="text-xl font-semibold text-gray-700 mb-2">Waiting for Video</h3>
                <p className="text-gray-600 text-center max-w-md">
                    The room controller will select a video to play. You'll see it here once they choose one.
                </p>
            </div>
        );
    }

    return (
        <div className="bg-white/90 dark:bg-gray-900/90 backdrop-blur-xl rounded-2xl shadow-2xl border border-gray-200/50 dark:border-gray-700/50 p-10 w-full max-w-3xl mx-auto relative overflow-hidden">
            {/* Background decoration */}
            <div className="absolute top-0 right-0 w-32 h-32 bg-gradient-to-br from-blue-500/10 to-purple-500/10 rounded-full blur-2xl"></div>
            <div className="absolute bottom-0 left-0 w-24 h-24 bg-gradient-to-tr from-purple-500/10 to-blue-500/10 rounded-full blur-xl"></div>
            {onBack && (
                <div className="mb-6">
                    <Button
                        variant="ghost"
                        size="sm"
                        icon={ArrowLeft}
                        onClick={onBack}
                        className="text-gray-400 hover:text-gray-700 font-semibold"
                    >
                        ← Back to Options
                    </Button>
                </div>
            )}
            <div className="text-center mb-6">
                <Play className="h-14 w-14 text-blue-500 mx-auto mb-4 drop-shadow-lg" />
                <h3 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">Choose a Video to Play</h3>
                <p className="text-gray-600 dark:text-gray-300">Search YouTube or enter a direct video URL</p>
            </div>

            {/* Tab Switcher */}
            <div className="flex space-x-1 mb-8 p-1 rounded-lg" style={{background: 'transparent'}}>
                <button
                    className={`flex-1 py-2 px-4 rounded-xl text-sm font-semibold transition-all duration-200 flex items-center justify-center gap-2 ${
                        activeTab === 'url'
                            ? 'bg-gradient-to-r from-blue-600 to-purple-600 text-white shadow-md'
                            : 'text-gray-600 dark:text-gray-300 hover:text-gray-900 dark:hover:text-white bg-white/60 dark:bg-gray-800/60'
                    }`}
                    onClick={() => setActiveTab('url')}
                >
                    <Link className="h-4 w-4 mr-2" />
                    Enter URL
                </button>
                <button
                    className={`flex-1 py-2 px-4 rounded-xl text-sm font-semibold transition-all duration-200 flex items-center justify-center gap-2 ${
                        activeTab === 'search'
                            ? 'bg-gradient-to-r from-blue-600 to-purple-600 text-white shadow-md'
                            : 'text-gray-600 dark:text-gray-300 hover:text-gray-900 dark:hover:text-white bg-white/60 dark:bg-gray-800/60'
                    }`}
                    onClick={() => setActiveTab('search')}
                >
                    <Search className="h-5 w-5 mr-2" />
                    Search YouTube
                </button>
            </div>

            {/* URL Input Tab */}
            {activeTab === 'url' && (
                <div className="space-y-6">
                    <Input
                        label="Video URL"
                        type="url"
                        value={directUrl}
                        onChange={(e) => setDirectUrl(e.target.value)}
                        placeholder="https://youtube.com/watch?v=... or direct video URL"
                        icon={ExternalLink}
                        className="bg-gray-900/80 text-white border border-gray-700/50 rounded-lg px-4 py-3 focus:border-blue-500 focus:ring-blue-500/20"
                    />
                    <Button
                        onClick={handleDirectUrlSubmit}
                        disabled={!directUrl.trim()}
                        className="w-full bg-blue-400/80 hover:bg-blue-500/90 text-white font-semibold rounded-xl py-3 shadow-md"
                    >
                        Set Video
                    </Button>
                </div>
            )}

            {/* YouTube Search Tab */}
            {activeTab === 'search' && (
                <div className="space-y-4">
                    <div className="flex space-x-2">
                        <Input
                            value={searchQuery}
                            onChange={(e) => setSearchQuery(e.target.value)}
                            placeholder="Search for videos..."
                            icon={Search}
                            onKeyPress={(e) => e.key === 'Enter' && handleSearch()}
                            className="flex-1 text-base bg-gray-900/80 text-white border border-gray-700/50 rounded-lg px-4 py-3 focus:border-blue-500 focus:ring-blue-500/20"
                        />
                        <Button
                            onClick={handleSearch}
                            disabled={!searchQuery.trim() || isSearching}
                            isLoading={isSearching}
                            className="bg-blue-400/80 hover:bg-blue-500/90 text-white font-semibold rounded-xl px-6 py-3 shadow-md"
                        >
                            Search
                        </Button>
                    </div>

                    {/* Search Results */}
                    {showResults && (
                        <div className="space-y-3 max-h-96 overflow-y-auto">
                            {searchResults.length === 0 ? (
                                <p className="text-gray-500 text-center py-4">No videos found</p>
                            ) : (
                                searchResults.map((video) => (
                                    <div
                                        key={video.videoId}
                                        className="flex items-start space-x-4 p-4 border border-gray-200/50 dark:border-gray-700/50 rounded-xl bg-white/80 dark:bg-gray-900/80 hover:bg-blue-50 dark:hover:bg-blue-900/30 cursor-pointer transition-all duration-200 shadow-md hover:shadow-xl"
                                        onClick={() => handleVideoSelect(video)}
                                    >
                                        <img
                                            src={video.thumbnailUrl}
                                            alt={video.title}
                                            className="w-40 h-24 object-cover rounded-xl flex-shrink-0 shadow-lg"
                                        />
                                        <div className="flex-1 min-w-0">
                                            <h4 className="text-base font-bold text-gray-900 dark:text-white line-clamp-2 mb-2">
                                                {video.title}
                                            </h4>
                                            <p className="text-sm text-gray-600 dark:text-gray-300 mb-2">{video.channelTitle}</p>
                                            <div className="flex items-center space-x-4 text-sm text-gray-500 dark:text-gray-400">
                                                <span className="flex items-center">
                                                    <Clock className="h-4 w-4 mr-1" />
                                                    {formatDuration(video.duration)}
                                                </span>
                                                <span>
                                                    {new Date(video.publishedAt).toLocaleDateString()}
                                                </span>
                                            </div>
                                        </div>
                                        <Button
                                            size="sm"
                                            variant="outline"
                                            icon={Play}
                                            className="flex-shrink-0 bg-gradient-to-r from-blue-600 to-purple-600 text-white font-semibold rounded-lg shadow-md hover:shadow-xl"
                                        >
                                            Select
                                        </Button>
                                    </div>
                                ))
                            )}
                        </div>
                    )}
                </div>
            )}
        </div>
    );
};

export default VideoSelector;
