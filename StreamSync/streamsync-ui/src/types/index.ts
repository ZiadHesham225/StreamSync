export interface ApiResponse<T = any> {
  data?: T;
  message?: string;
  success: boolean;
}
export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  displayName: string;
  username: string;
  email: string;
  password: string;
}

export interface TokenResponse {
  accessToken: string;
  accessTokenExpiration: string;
  refreshToken: string;
  refreshTokenExpiration: string;
}

export interface RefreshTokenRequest {
  accessToken: string;
  refreshToken: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  newPassword: string;
  token: string;
}

export interface JwtClaims {
  [key: string]: any;
  sub?: string;
  iat?: number;
  exp?: number;
  jti?: string;
  iss?: string;
  aud?: string;
  name?: string;
  email?: string;
  unique_name?: string;
  nameId?: string;
  Image?: string;
}

export interface User {
  id: string;
  displayName: string;
  username: string;
  email: string;
  avatarUrl?: string;
}

// Room Types

export interface Room {
  id: string;
  name: string;
  videoUrl: string;
  adminId: string;
  adminName: string;
  isActive: boolean;
  createdAt: string;
  inviteCode: string;
  isPrivate: boolean;
  hasPassword: boolean;
  userCount: number;
}

export interface RoomDetail extends Room {
  description?: string;
  currentPosition: number;
  isPlaying: boolean;
  syncMode: string;
  autoPlay: boolean;
  participants: RoomParticipant[];
}

export interface RoomCreateRequest {
  name: string;
  videoUrl: string;
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

export interface RoomParticipant {
  id: string;
  displayName: string;
  avatarUrl?: string;
  hasControl: boolean;
  joinedAt: string;
  isAdmin: boolean;
}

// Chat Types
export interface ChatMessage {
  id: string;
  senderId: string;
  roomId: string;
  senderName: string;
  avatarUrl?: string;
  content: string;
  timestamp: number;
  sentAt: string;
}

// SignalR Types
export interface JoinRoomRequest {
  roomId: string;
  displayName: string;
  avatarUrl?: string;
  password?: string;
}

export interface PlaybackState {
  isPlaying: boolean;
  progress: number;
  speed: number;
  duration: number;
}

// Error Types
export interface ApiError {
  message: string;
  details?: string;
  statusCode?: number;
}

// Virtual Browser Types
export interface VirtualBrowser {
  id: string;
  roomId: string;
  containerId: string;
  containerName: string;
  browserUrl: string;
  containerIndex: number;
  slotIndex: number;
  status: string;
  createdAt: string;
  allocatedAt?: string;
  deallocatedAt?: string;
  expiresAt: string;
  lastAccessedUrl?: string;
  nekoPassword?: string;
  nekoAdminPassword?: string;
  timeRemaining?: number;
}

export interface VirtualBrowserQueue {
  id: string;
  roomId: string;
  requestedAt: string;
  position: number;
  status: string;
  notifiedAt?: string;
  notificationExpiresAt?: string;
  notificationTimeRemaining?: number;
}

export interface VirtualBrowserRequest {
  roomId: string;
}

export interface VirtualBrowserControl {
  virtualBrowserId: string;
  action: string;
  data?: any;
}

export interface VirtualBrowserNavigate {
  virtualBrowserId: string;
  url: string;
}

// User Profile Types
export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  avatarUrl?: string;
  createdAt: string;
}

export interface UpdateProfileData {
  displayName: string;
  avatarUrl?: string;
}

export interface ChangePasswordData {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

// Pagination Types
export interface PaginationQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortDescending?: boolean;
}

export interface PagedResult<T> {
  data: T[];
  totalCount: number;
  currentPage: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}
