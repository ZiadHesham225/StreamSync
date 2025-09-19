export interface ChatMessage {
    id: string;
    senderId: string;
    roomId: string;
    content: string;
    timestamp: number;
    senderName: string;
    avatarUrl?: string;
    sentAt: string | Date;
}

export interface Room {
    id: string;
    name: string;
    videoUrl: string;
    isPublic: boolean;
    isPrivate: boolean;
    creatorId: string;
    description: string;
    userCount: number;
    createdAt: string | Date;
    adminName: string;
}

export interface RoomParticipant {
    id: string;
    name: string;
    displayName: string;
    avatarUrl?: string;
    hasControl: boolean;
    joinedAt: string | Date;
    isAdmin: boolean;
}

export interface RoomDetail extends Room {
    participants: RoomParticipant[];
    chatHistory: ChatMessage[];
}

export interface PlaybackState {
    isPlaying: boolean;
    progress: number;
    speed: number;
    duration: number;
}

export interface RoomCreateRequest {
    name: string;
    videoUrl?: string;
    isPrivate: boolean;
    password?: string;
    autoPlay: boolean;
    syncMode: string;
}

export interface RoomUpdateRequest {
    id: string;
    name: string;
    videoUrl: string;
    autoPlay: boolean;
    syncMode: string;
}

export interface User {
    id: string;
    email: string;
    username: string;
    displayName: string;
    avatarUrl?: string;
}

export interface LoginRequest {
    email: string;
    password: string;
}

export interface RegisterRequest {
    email: string;
    username: string;
    password: string;
    displayName: string;
}

export interface ApiError {
    message: string;
    status?: number;
    statusCode?: number;
    details?: string;
    errors?: { [key: string]: string[] };
}

export interface TokenResponse {
    accessToken: string;
    refreshToken?: string;
    tokenType?: string;
    expiresIn?: number;
}

export interface ApiResponse<T = any> {
    data: T;
    message?: string;
    success: boolean;
}

export interface JwtClaims {
    sub: string;
    email: string;
    name: string;
    exp: number;
    iat: number;
    nameid?: string;
    unique_name?: string;
    [key: string]: any;
}

// YouTube Types
export interface YouTubeVideo {
    videoId: string;
    title: string;
    description: string;
    thumbnailUrl: string;
    channelTitle: string;
    duration: string;
    publishedAt: string;
    videoUrl: string;
}

export interface YouTubeSearchResponse {
    videos: YouTubeVideo[];
    nextPageToken?: string;
    totalResults: number;
}

export {};