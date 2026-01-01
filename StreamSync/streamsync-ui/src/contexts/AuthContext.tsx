import React, { createContext, useContext, useState, useEffect, useCallback, ReactNode } from 'react';
import { authService } from '../services/authService';
import signalRService from '../services/signalRService';
import { User, LoginRequest, RegisterRequest, ForgotPasswordRequest, ResetPasswordRequest } from '../types/index';

interface AuthContextType {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (credentials: LoginRequest) => Promise<void>;
  register: (userData: RegisterRequest) => Promise<void>;
  logout: () => void;
  forgotPassword: (request: ForgotPasswordRequest) => Promise<void>;
  resetPassword: (request: ResetPasswordRequest) => Promise<void>;
  refreshToken: () => Promise<boolean>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

interface AuthProviderProps {
  children: ReactNode;
}

export const AuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const isAuthenticated = !!user;

  const logout = useCallback(() => {
    authService.logout();
    setUser(null);
    setToken(null);
  }, []);

  const refreshToken = useCallback(async (): Promise<boolean> => {
    try {
      const response = await authService.refreshToken();
      if (response) {
        const user = authService.getStoredUser();
        setUser(user);
        setToken(response.accessToken);
        
        // Update SignalR with the new token without disconnecting
        // This ensures the connection stays alive and uses the new token for any reconnections
        signalRService.updateAccessToken(response.accessToken);
        
        return true;
      }
      return false;
    } catch (error) {
      logout();
      return false;
    }
  }, [logout]);

  useEffect(() => {
    const initAuth = async () => {
      const storedToken = authService.getStoredToken();
      if (storedToken && authService.isAuthenticated()) {
        const storedUser = authService.getStoredUser();
        setUser(storedUser);
        setToken(storedToken);
      } else {
        // Clear invalid/expired tokens
        authService.logout();
        setUser(null);
        setToken(null);
      }
      setIsLoading(false);
    };

    initAuth();
  }, []);
  useEffect(() => {
    let refreshInterval: NodeJS.Timeout;

    if (isAuthenticated && !isLoading) {
      refreshInterval = setInterval(async () => {
        try {
          const isExpiring = await authService.isTokenExpiringSoon();
          if (isExpiring) {
            const success = await refreshToken();
            if (!success) {
              logout();
            }
          }
        } catch (error) {
          }
      }, 4 * 60 * 1000);
    }

    return () => {
      if (refreshInterval) {
        clearInterval(refreshInterval);
      }
    };
  }, [isAuthenticated, isLoading, refreshToken, logout]);

  const login = async (credentials: LoginRequest) => {
    try {
      const response = await authService.login(credentials);
      const user = authService.getStoredUser();
      setUser(user);
      setToken(response.accessToken);
    } catch (error) {
      throw error;
    }
  };

  const register = async (userData: RegisterRequest) => {
    try {
      await authService.register(userData);
    } catch (error) {
      throw error;
    }
  };

  const forgotPassword = async (request: ForgotPasswordRequest) => {
    try {
      await authService.forgotPassword(request.email);
    } catch (error) {
      throw error;
    }
  };

  const resetPassword = async (request: ResetPasswordRequest) => {
    try {
      await authService.resetPassword(request.email, request.newPassword, request.token);
    } catch (error) {
      throw error;
    }
  };

  const value: AuthContextType = {
    user,
    token,
    isAuthenticated,
    isLoading,
    login,
    register,
    logout,
    forgotPassword,
    resetPassword,
    refreshToken,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

export default AuthContext;
