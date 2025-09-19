import { apiService } from './api';
import { VirtualBrowser, VirtualBrowserQueue, VirtualBrowserRequest } from '../types/index';

export interface VirtualBrowserResponse {
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
  timeRemaining?: number;
}

export interface VirtualBrowserQueueResponse {
  id: string;
  roomId: string;
  requestedAt: string;
  position: number;
  status: string;
  notifiedAt?: string;
  notificationExpiresAt?: string;
  notificationTimeRemaining?: number;
}

export const virtualBrowserService = {
  requestVirtualBrowser: async (roomId: string): Promise<VirtualBrowserResponse | { message: string; queue: VirtualBrowserQueueResponse }> => {
    const response = await apiService.post<VirtualBrowserResponse | { message: string; queue: VirtualBrowserQueueResponse }>('/api/VirtualBrowser/request', { roomId });
    return response;
  },

  releaseVirtualBrowser: async (roomId: string): Promise<{ message: string }> => {
    const response = await apiService.post<{ message: string }>(`/api/VirtualBrowser/release/${roomId}`);
    return response;
  },

  getRoomVirtualBrowser: async (roomId: string): Promise<VirtualBrowserResponse | { queue: VirtualBrowserQueueResponse } | null> => {
    try {
      const response = await apiService.get<VirtualBrowserResponse | { queue: VirtualBrowserQueueResponse }>(`/api/VirtualBrowser/room/${roomId}`);
      return response;
    } catch (error: any) {
      if (error.statusCode === 404) {
        return null;
      }
      throw error;
    }
  },

  getRoomCooldownStatus: async (roomId: string): Promise<{ isOnCooldown: boolean; remainingSeconds: number }> => {
    try {
      const response = await apiService.get<{ isOnCooldown: boolean; remainingSeconds: number }>(`/api/VirtualBrowser/cooldown/${roomId}`);
      return response;
    } catch (error: any) {
      return { isOnCooldown: false, remainingSeconds: 0 };
    }
  },

  acceptQueueNotification: async (roomId: string): Promise<{ message: string }> => {
    const response = await apiService.post<{ message: string }>(`/api/VirtualBrowser/queue/accept/${roomId}`);
    return response;
  },

  declineQueueNotification: async (roomId: string): Promise<{ message: string }> => {
    const response = await apiService.post<{ message: string }>(`/api/VirtualBrowser/queue/decline/${roomId}`);
    return response;
  },

  cancelQueue: async (roomId: string): Promise<{ message: string }> => {
    const response = await apiService.post<{ message: string }>(`/api/VirtualBrowser/queue/cancel/${roomId}`);
    return response;
  },

  navigateVirtualBrowser: async (virtualBrowserId: string, url: string): Promise<{ message: string }> => {
    const response = await apiService.post<{ message: string }>('/api/VirtualBrowser/navigate', { 
      virtualBrowserId, 
      url 
    });
    return response;
  },

  controlVirtualBrowser: async (virtualBrowserId: string, action: string, data?: any): Promise<{ message: string }> => {
    const response = await apiService.post<{ message: string }>('/api/VirtualBrowser/control', {
      virtualBrowserId,
      action,
      data
    });
    return response;
  }
};

export default virtualBrowserService;
