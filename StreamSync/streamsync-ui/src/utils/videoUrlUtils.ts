import { youTubeService } from '../services/youTubeService';

export class VideoUrlUtils {
    static isYouTubeUrl(url: string): boolean {
        return youTubeService.isYouTubeUrl(url);
    }

    static extractVideoId(url: string): string | null {
        return youTubeService.extractVideoIdFromUrl(url);
    }

    static convertForPlayer(url: string): string {
        if (this.isYouTubeUrl(url)) {
            return url;
        }
        return url;
    }

    static getThumbnail(url: string): string | null {
        if (this.isYouTubeUrl(url)) {
            const videoId = this.extractVideoId(url);
            if (videoId) {
                return youTubeService.getThumbnailUrl(videoId);
            }
        }
        return null;
    }

    static validateVideoUrl(url: string): boolean {
        if (!url) return false;
        
        if (this.isYouTubeUrl(url)) {
            return this.extractVideoId(url) !== null;
        }
        
        try {
            const urlObj = new URL(url);
            return ['http:', 'https:'].includes(urlObj.protocol);
        } catch {
            return false;
        }
    }

    static getVideoType(url: string): 'youtube' | 'direct' | 'unknown' {
        if (this.isYouTubeUrl(url)) {
            return 'youtube';
        }
        
        if (this.validateVideoUrl(url)) {
            return 'direct';
        }
        
        return 'unknown';
    }
}
