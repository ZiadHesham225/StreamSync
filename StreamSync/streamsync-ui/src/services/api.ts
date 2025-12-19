import axios, { AxiosResponse } from 'axios';
import { ApiError } from '../types/index';
import { storageUtils } from '../utils/storageUtils';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'https://localhost:7189';
const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
  },
  ...(process.env.NODE_ENV === 'development' && {
    httpsAgent: undefined,
  }),
});

api.interceptors.request.use(
  (config) => {
    const token = storageUtils.getAccessToken();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

api.interceptors.response.use(
  (response: AxiosResponse) => {
    return response;
  },
  (error) => {
    const apiError: ApiError = {
      message: error.response?.data?.message || error.message || 'An unexpected error occurred',
      details: error.response?.data?.details || error.response?.statusText,
      statusCode: error.response?.status,
    };
    if (error.code === 'NETWORK_ERROR' || error.message.includes('Network Error')) {
      apiError.message = 'Unable to connect to server. Please check if the backend is running on http://localhost:5099';
    }

    if (error.message.includes('CORS')) {
      apiError.message = 'CORS error: Please ensure the backend allows requests from http://localhost:3000';
    }

    if (error.response?.status === 401) {
      storageUtils.clearAllTokens();
      
      if (window.location.pathname !== '/login' && window.location.pathname !== '/register' && window.location.pathname !== '/reset-password') {
        window.location.href = '/login';
      }
    }

    return Promise.reject(apiError);
  }
);

export { api };

export const apiService = {
  get: <T>(url: string): Promise<T> => 
    api.get<T>(url).then(response => response.data),
    
  post: <T>(url: string, data?: any): Promise<T> => 
    api.post<T>(url, data).then(response => response.data),
    
  put: <T>(url: string, data?: any): Promise<T> => 
    api.put<T>(url, data).then(response => response.data),
    
  delete: <T>(url: string): Promise<T> => 
    api.delete<T>(url).then(response => response.data),
};

export default api;
