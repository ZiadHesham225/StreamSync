import { UserProfile, UpdateProfileData, ChangePasswordData } from '../types/index';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'https://localhost:7189';

export class UserService {
  private getHeaders(token: string): HeadersInit {
    return {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    };
  }

  async getUserProfile(token: string): Promise<UserProfile> {
    if (!token) {
      throw new Error('Authentication token is required');
    }

    const response = await fetch(`${API_BASE_URL}/api/user/profile`, {
      headers: this.getHeaders(token)
    });

    if (!response.ok) {
      if (response.status === 401) {
        throw new Error('Authentication failed. Please log in again.');
      }
      throw new Error(`Failed to load profile: ${response.statusText}`);
    }

    return response.json();
  }

  async updateUserProfile(token: string, profileData: UpdateProfileData): Promise<UserProfile> {
    if (!token) {
      throw new Error('Authentication token is required');
    }

    const response = await fetch(`${API_BASE_URL}/api/user/profile`, {
      method: 'PUT',
      headers: this.getHeaders(token),
      body: JSON.stringify(profileData)
    });

    if (!response.ok) {
      if (response.status === 401) {
        throw new Error('Authentication failed. Please log in again.');
      }
      const errorData = await response.json().catch(() => ({ message: 'Failed to update profile' }));
      throw new Error(errorData.message || 'Failed to update profile');
    }

    return response.json();
  }

  async changePassword(token: string, passwordData: ChangePasswordData): Promise<void> {
    if (!token) {
      throw new Error('Authentication token is required');
    }

    if (passwordData.newPassword !== passwordData.confirmPassword) {
      throw new Error('New passwords do not match');
    }

    if (passwordData.newPassword.length < 6) {
      throw new Error('New password must be at least 6 characters long');
    }

    const response = await fetch(`${API_BASE_URL}/api/user/change-password`, {
      method: 'POST',
      headers: this.getHeaders(token),
      body: JSON.stringify({
        currentPassword: passwordData.currentPassword,
        newPassword: passwordData.newPassword,
        confirmPassword: passwordData.confirmPassword
      })
    });

    if (!response.ok) {
      if (response.status === 401) {
        throw new Error('Authentication failed. Please log in again.');
      }
      const errorData = await response.json().catch(() => ({ message: 'Failed to change password' }));
      throw new Error(errorData.message || 'Failed to change password');
    }
  }
}

export const userService = new UserService();