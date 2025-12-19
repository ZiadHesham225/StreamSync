import { jwtDecode } from 'jwt-decode';
import { apiService } from './api';
import {
  LoginRequest,
  RegisterRequest,
  TokenResponse,
  RefreshTokenRequest,
  ForgotPasswordRequest,
  ResetPasswordRequest,
  ApiResponse,
  User,
  JwtClaims,
} from '../types/index';
import { storageUtils } from '../utils/storageUtils';

export const authService = {
  decodeToken(token: string): User | null {
    try {
      const decoded: JwtClaims = jwtDecode(token);
      
      const user: User = {
        id: decoded['nameid'],
        username: decoded['unique_name'] || '',
        displayName: decoded['unique_name'] ||
                    decoded['name'] || '',
        email: decoded['email'] || '',
        avatarUrl: decoded['Image'] || `https://api.dicebear.com/7.x/initials/svg?seed=${decoded['unique_name'] || decoded['name'] || 'User'}`
      };
      
      return user;
    } catch (error) {
      return null;
    }
  },

  async login(credentials: LoginRequest): Promise<TokenResponse> {
    const response = await apiService.post<TokenResponse>('/api/auth/login', credentials);
    if (response.accessToken) {
      storageUtils.setTokenData({
        accessToken: response.accessToken,
        refreshToken: response.refreshToken,
        accessTokenExpiration: response.accessTokenExpiration,
        refreshTokenExpiration: response.refreshTokenExpiration
      });
      
      const user = this.decodeToken(response.accessToken);
      if (user) {
        storageUtils.setUser(user);
      }
    }
    return response;
  },

  async register(userData: RegisterRequest): Promise<ApiResponse> {
    return apiService.post<ApiResponse>('/api/auth/register', userData);
  },

  async refreshToken(): Promise<TokenResponse | null> {
    const accessToken = this.getStoredToken();
    const refreshToken = this.getStoredRefreshToken();
    
    if (!accessToken || !refreshToken) {
      return null;
    }

    try {
      const request: RefreshTokenRequest = {
        accessToken,
        refreshToken
      };
      
      const response = await apiService.post<TokenResponse>('/api/auth/refresh-token', request);
      
      if (response.accessToken) {
        storageUtils.setTokenData({
          accessToken: response.accessToken,
          refreshToken: response.refreshToken,
          accessTokenExpiration: response.accessTokenExpiration,
          refreshTokenExpiration: response.refreshTokenExpiration
        });
        
        const user = this.decodeToken(response.accessToken);
        if (user) {
          storageUtils.setUser(user);
        }
      }
      
      return response;
    } catch (error) {
      this.logout();
      return null;
    }
  },

  async revokeToken(): Promise<void> {
    try {
      await apiService.post('/api/auth/revoke', {});
    } catch (error) {
      } finally {
      this.logout();
    }
  },

  async forgotPassword(email: string): Promise<ApiResponse> {
    const request: ForgotPasswordRequest = { email };
    return apiService.post<ApiResponse>('/api/auth/forgot-password', request);
  },

  async resetPassword(email: string, newPassword: string, token: string): Promise<ApiResponse> {
    const request: ResetPasswordRequest = {
      email,
      newPassword,
      token
    };
    return apiService.post<ApiResponse>('/api/auth/reset-password', request);
  },

  logout(): void {
    storageUtils.clearAllTokens();
  },

  getStoredUser(): User | null {
    const user = storageUtils.getUser<User>();
    if (user) {
      return user;
    }
    
    const token = this.getStoredToken();
    if (token) {
      const decodedUser = this.decodeToken(token);
      if (decodedUser) {
        storageUtils.setUser(decodedUser);
        return decodedUser;
      }
    }
    
    return null;
  },

  getStoredToken(): string | null {
    return storageUtils.getAccessToken();
  },

  getStoredRefreshToken(): string | null {
    return storageUtils.getRefreshToken();
  },

  isAuthenticated(): boolean {
    const token = this.getStoredToken();
    if (!token) return false;
    
    try {
      const decoded: JwtClaims = jwtDecode(token);
      const now = Date.now() / 1000;
      
      if (decoded.exp && decoded.exp < now) {
        const refreshToken = this.getStoredRefreshToken();
        if (refreshToken) {
          this.refreshToken().catch(() => {
            this.logout();
          });
          return false;
        } else {
          this.logout();
          return false;
        }
      }
      
      return true;
    } catch (error) {
      this.logout();
      return false;
    }
  },

  async isTokenExpiringSoon(): Promise<boolean> {
    const token = this.getStoredToken();
    if (!token) return true;
    
    try {
      const decoded: JwtClaims = jwtDecode(token);
      const now = Date.now() / 1000;
      const fiveMinutesFromNow = now + (5 * 60);
      
      return decoded.exp ? decoded.exp < fiveMinutesFromNow : true;
    } catch (error) {
      return true;
    }
  },

  refreshUserFromToken(): User | null {
    const token = this.getStoredToken();
    if (token) {
      const user = this.decodeToken(token);
      if (user) {
        storageUtils.setUser(user);
        return user;
      }
    }
    return null;
  },
};
