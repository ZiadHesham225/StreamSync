export const storageUtils = {
  // Token management
  setTokenData(tokenData: {
    accessToken: string;
    refreshToken: string;
    accessTokenExpiration: string;
    refreshTokenExpiration: string;
  }): void {
    localStorage.setItem('accessToken', tokenData.accessToken);
    localStorage.setItem('refreshToken', tokenData.refreshToken);
    localStorage.setItem('accessTokenExpiration', tokenData.accessTokenExpiration);
    localStorage.setItem('refreshTokenExpiration', tokenData.refreshTokenExpiration);
  },

  getAccessToken(): string | null {
    return localStorage.getItem('accessToken') || localStorage.getItem('token');
  },

  getRefreshToken(): string | null {
    return localStorage.getItem('refreshToken');
  },

  clearAllTokens(): void {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('accessTokenExpiration');
    localStorage.removeItem('refreshTokenExpiration');
    localStorage.removeItem('user');
    localStorage.removeItem('token');
  },

  // User data management
  setUser(user: any): void {
    localStorage.setItem('user', JSON.stringify(user));
  },

  getUser<T>(): T | null {
    const userStr = localStorage.getItem('user');
    if (userStr) {
      try {
        return JSON.parse(userStr) as T;
      } catch (error) {
        return null;
      }
    }
    return null;
  }
};
