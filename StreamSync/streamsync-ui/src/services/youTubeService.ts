import { apiService } from './api';
import { YouTubeSearchResponse, YouTubeVideo } from '../types';

class YouTubeService {
    async searchVideos(query: string, maxResults: number = 10): Promise<YouTubeSearchResponse> {
        return apiService.get<YouTubeSearchResponse>(`/api/youtube/search?query=${encodeURIComponent(query)}&maxResults=${maxResults}`);
    }

    async getVideoDetails(videoId: string): Promise<YouTubeVideo> {
        return apiService.get<YouTubeVideo>(`/api/youtube/video/${videoId}`);
    }

    extractVideoIdFromUrl(url: string): string | null {
        if (!url) return null;

        // YouTube URL patterns
        const patterns = [
            /(?:youtube\.com\/watch\?v=|youtu\.be\/|youtube\.com\/embed\/)([a-zA-Z0-9_-]{11})/,
            /youtube\.com\/watch\?.*v=([a-zA-Z0-9_-]{11})/
        ];

        for (const pattern of patterns) {
            const match = url.match(pattern);
            if (match) {
                return match[1];
            }
        }

        return null;
    }

    isYouTubeUrl(url: string): boolean {
        if (!url) return false;
        return /(?:youtube\.com|youtu\.be)/i.test(url);
    }

    // Convert YouTube URL to embeddable format for video player
    convertToEmbedUrl(url: string): string {
        const videoId = this.extractVideoIdFromUrl(url);
        if (!videoId) return url;
        
        // For our video player, we'll use a direct YouTube embed URL
        // This will work with HTML5 video elements that support it
        return `https://www.youtube.com/embed/${videoId}?enablejsapi=1&origin=${window.location.origin}`;
    }

    // Get direct video stream URL (for HTML5 video elements)
    // Note: This is complex due to YouTube's restrictions. 
    // For now, we'll return the embed URL and handle it in the video player
    getVideoStreamUrl(url: string): string {
        return this.convertToEmbedUrl(url);
    }

    // Get YouTube thumbnail URL
    getThumbnailUrl(videoId: string, quality: 'default' | 'medium' | 'high' = 'medium'): string {
        const qualityMap = {
            default: 'default',
            medium: 'mqdefault',
            high: 'hqdefault'
        };
        
        return `https://img.youtube.com/vi/${videoId}/${qualityMap[quality]}.jpg`;
    }

    // Format duration from ISO 8601 format to readable format
    formatDuration(duration: string): string {
        try {
            // Handle ISO 8601 duration format (PT4M13S)
            const match = duration.match(/PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?/);
            if (!match) return duration;

            const hours = parseInt(match[1]) || 0;
            const minutes = parseInt(match[2]) || 0;
            const seconds = parseInt(match[3]) || 0;

            const parts = [];
            if (hours > 0) parts.push(`${hours}:${minutes.toString().padStart(2, '0')}`);
            else parts.push(minutes.toString());
            parts.push(seconds.toString().padStart(2, '0'));

            return parts.join(':');
        } catch {
            return duration;
        }
    }
}

export const youTubeService = new YouTubeService();
