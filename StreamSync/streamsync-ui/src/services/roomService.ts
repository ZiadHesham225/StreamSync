import { apiService } from './api';
import {
  Room,
  RoomDetail,
  RoomCreateRequest,
  RoomUpdateRequest,
  RoomParticipant,
  ChatMessage,
  PagedResult,
  PaginationQuery,
} from '../types/index';
import { paginationUtils } from '../utils/paginationUtils';

export const roomService = {
  async getActiveRooms(): Promise<Room[]> {
    return await apiService.get<Room[]>('/api/room/active');
  },

  async getActiveRoomsPaginated(pagination?: PaginationQuery): Promise<PagedResult<Room>> {
    const queryString = paginationUtils.buildQueryString(pagination);
    const url = `/api/room/active${queryString}`;
    return await apiService.get<PagedResult<Room>>(url);
  },

  async getUserRooms(): Promise<Room[]> {
    return await apiService.get<Room[]>('/api/room/my-rooms');
  },

  async getUserRoomsPaginated(pagination?: PaginationQuery): Promise<PagedResult<Room>> {
    const queryString = paginationUtils.buildQueryString(pagination);
    const url = `/api/room/my-rooms${queryString}`;
    return await apiService.get<PagedResult<Room>>(url);
  },

  async getRoomById(roomId: string): Promise<RoomDetail> {
    return await apiService.get<RoomDetail>(`/api/room/${roomId}`);
  },

  async getRoomByInviteCode(inviteCode: string): Promise<{ data: RoomDetail }> {
    const room = await apiService.get<RoomDetail>(`/api/room/invite/${inviteCode}`);
    return { data: room };
  },

  async createRoom(roomData: RoomCreateRequest): Promise<any> {
    return apiService.post('/api/room/create', roomData);
  },

  async updateRoom(roomData: RoomUpdateRequest): Promise<void> {
    return apiService.put('/api/room/update', roomData);
  },

  async endRoom(roomId: string): Promise<void> {
    return apiService.post(`/api/room/${roomId}/end`);
  },

  async transferControl(roomId: string, newControllerId: string): Promise<void> {
    return apiService.post(`/api/room/${roomId}/transfer-control`, {
      newControllerId,
    });
  },

  async getRoomParticipants(roomId: string): Promise<RoomParticipant[]> {
    return await apiService.get<RoomParticipant[]>(`/api/room/${roomId}/participants`);
  },

  async getRoomMessages(roomId: string): Promise<ChatMessage[]> {
    return await apiService.get<ChatMessage[]>(`/api/room/${roomId}/messages`);
  },

  async joinRoom(roomId: string, password?: string): Promise<void> {
    const payload = password ? { password } : {};
    return apiService.post(`/api/room/${roomId}/join`, payload);
  },
};
