import React, { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { toast } from 'react-hot-toast';
import { User, Key, ArrowLeft, Edit2, Save, X } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import Button from '../components/common/Button';
import Input from '../components/common/Input';
import { userService } from '../services/userService';
import { UserProfile, UpdateProfileData, ChangePasswordData } from '../types/index';

const AccountPage: React.FC = () => {
    const { user, token, refreshToken } = useAuth();
    const navigate = useNavigate();
    const [loading, setLoading] = useState(false);
    const [isEditingProfile, setIsEditingProfile] = useState(false);
    const [showChangePassword, setShowChangePassword] = useState(false);
    
    const [profile, setProfile] = useState<UserProfile | null>(null);
    const [profileForm, setProfileForm] = useState<UpdateProfileData>({
        displayName: '',
        avatarUrl: ''
    });
    const [passwordForm, setPasswordForm] = useState<ChangePasswordData>({
        currentPassword: '',
        newPassword: '',
        confirmPassword: ''
    });

    useEffect(() => {
        const fetchProfile = async () => {
            if (!user || !token) return;
            
            try {
                const profileData = await userService.getUserProfile(token);
                setProfile(profileData);
                setProfileForm({
                    displayName: profileData.displayName,
                    avatarUrl: profileData.avatarUrl || ''
                });
            } catch (error: any) {
                if (error.message.includes('Authentication failed')) {
                    // Try to refresh token
                    const refreshSuccess = await refreshToken();
                    if (refreshSuccess && token) {
                        // Retry with fresh token
                        try {
                            const profileData = await userService.getUserProfile(token);
                            setProfile(profileData);
                            setProfileForm({
                                displayName: profileData.displayName,
                                avatarUrl: profileData.avatarUrl || ''
                            });
                        } catch (retryError: any) {
                            toast.error(retryError.message || 'Failed to load profile');
                        }
                    } else {
                        toast.error('Session expired. Please log in again.');
                        navigate('/login');
                    }
                } else {
                    toast.error(error.message || 'Error loading profile');
                }
                }
        };

        fetchProfile();
    }, [user, token, refreshToken, navigate]);

    const handleUpdateProfile = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!token) {
            toast.error('Authentication required');
            return;
        }

        setLoading(true);

        try {
            const updatedProfile = await userService.updateUserProfile(token, profileForm);
            setProfile(updatedProfile);
            setIsEditingProfile(false);
            toast.success('Profile updated successfully!');
        } catch (error: any) {
            if (error.message.includes('Authentication failed')) {
                // Try to refresh token
                const refreshSuccess = await refreshToken();
                if (refreshSuccess && token) {
                    // Retry with fresh token
                    try {
                        const updatedProfile = await userService.updateUserProfile(token, profileForm);
                        setProfile(updatedProfile);
                        setIsEditingProfile(false);
                        toast.success('Profile updated successfully!');
                    } catch (retryError: any) {
                        toast.error(retryError.message || 'Failed to update profile');
                    }
                } else {
                    toast.error('Session expired. Please log in again.');
                    navigate('/login');
                }
            } else {
                toast.error(error.message || 'Error updating profile');
            }
            } finally {
            setLoading(false);
        }
    };

    const handleChangePassword = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!token) {
            toast.error('Authentication required');
            return;
        }

        setLoading(true);

        try {
            await userService.changePassword(token, passwordForm);
            setShowChangePassword(false);
            setPasswordForm({
                currentPassword: '',
                newPassword: '',
                confirmPassword: ''
            });
            toast.success('Password changed successfully!');
        } catch (error: any) {
            if (error.message.includes('Authentication failed')) {
                // Try to refresh token
                const refreshSuccess = await refreshToken();
                if (refreshSuccess && token) {
                    // Retry with fresh token
                    try {
                        await userService.changePassword(token, passwordForm);
                        setShowChangePassword(false);
                        setPasswordForm({
                            currentPassword: '',
                            newPassword: '',
                            confirmPassword: ''
                        });
                        toast.success('Password changed successfully!');
                    } catch (retryError: any) {
                        toast.error(retryError.message || 'Failed to change password');
                    }
                } else {
                    toast.error('Session expired. Please log in again.');
                    navigate('/login');
                }
            } else {
                toast.error(error.message || 'Error changing password');
            }
            } finally {
            setLoading(false);
        }
    };

    if (!user) {
        return (
            <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50 dark:from-gray-900 dark:via-gray-800 dark:to-purple-900 flex items-center justify-center relative">
                {/* Background decoration */}
                <div className="absolute inset-0 -z-10">
                    <div className="absolute top-1/4 left-1/4 w-64 h-64 bg-blue-400/20 dark:bg-blue-500/10 rounded-full blur-3xl"></div>
                    <div className="absolute bottom-1/4 right-1/4 w-80 h-80 bg-purple-400/20 dark:bg-purple-500/10 rounded-full blur-3xl"></div>
                </div>
                
                <div className="text-center bg-white/80 dark:bg-gray-900/80 backdrop-blur-xl rounded-2xl shadow-2xl border border-gray-200/50 dark:border-gray-700/50 p-8 relative overflow-hidden">
                    <div className="absolute top-0 right-0 w-24 h-24 bg-gradient-to-br from-red-500/10 to-orange-500/10 rounded-full blur-xl"></div>
                    
                    <h2 className="text-3xl font-bold mb-4">
                        <span className="bg-gradient-to-r from-red-600 to-orange-600 bg-clip-text text-transparent">
                            Access Denied
                        </span>
                    </h2>
                    <p className="text-gray-600 dark:text-gray-300 mb-6">You need to be logged in to access this page.</p>
                    <Link 
                        to="/login" 
                        className="inline-block bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 text-white px-6 py-3 rounded-xl font-semibold shadow-lg hover:shadow-xl transition-all duration-200 transform hover:-translate-y-0.5"
                    >
                        Go to Login
                    </Link>
                </div>
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50 dark:from-gray-900 dark:via-gray-800 dark:to-purple-900 py-8 relative">
            {/* Background decoration */}
            <div className="absolute inset-0 -z-10">
                <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-blue-400/10 dark:bg-blue-500/5 rounded-full blur-3xl"></div>
                <div className="absolute bottom-1/4 right-1/4 w-80 h-80 bg-purple-400/10 dark:bg-purple-500/5 rounded-full blur-3xl"></div>
            </div>
            
            <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
                {/* Header */}
                <div className="mb-8">
                    <div className="flex items-center mb-4">
                        <Button
                            variant="glass"
                            size="sm"
                            icon={ArrowLeft}
                            onClick={() => navigate('/')}
                            className="mr-4"
                        >
                            Back to Home
                        </Button>
                        <h1 className="text-4xl font-bold">
                            <span className="bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
                                Account Settings
                            </span>
                        </h1>
                    </div>
                    <p className="text-gray-600 dark:text-gray-300">Manage your account information and preferences</p>
                </div>

                <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
                    {/* Profile Section */}
                    <div className="lg:col-span-2">
                        <div className="bg-white/80 dark:bg-gray-900/80 backdrop-blur-xl shadow-2xl rounded-2xl border border-gray-200/50 dark:border-gray-700/50 overflow-hidden relative">
                            <div className="absolute top-0 right-0 w-32 h-32 bg-gradient-to-br from-blue-500/10 to-purple-500/10 rounded-full blur-2xl"></div>
                            
                            <div className="px-6 py-5 border-b border-gray-200/50 dark:border-gray-700/50 relative">
                                <div className="flex items-center justify-between">
                                    <h2 className="text-xl font-semibold flex items-center">
                                        <User className="w-6 h-6 mr-3 text-blue-600" />
                                        <span className="bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
                                            Profile Information
                                        </span>
                                    </h2>
                                    {!isEditingProfile && (
                                        <Button
                                            variant="glass"
                                            size="sm"
                                            icon={Edit2}
                                            onClick={() => setIsEditingProfile(true)}
                                        >
                                            Edit
                                        </Button>
                                    )}
                                </div>
                            </div>

                            <div className="p-6 relative">
                                {isEditingProfile ? (
                                    <form onSubmit={handleUpdateProfile} className="space-y-6">
                                        <Input
                                            id="displayName"
                                            label="Display Name"
                                            type="text"
                                            value={profileForm.displayName}
                                            onChange={(e) => setProfileForm({ ...profileForm, displayName: e.target.value })}
                                            required
                                            className="bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border-gray-200/50 dark:border-gray-700/50 focus:border-blue-500 focus:ring-blue-500/20"
                                        />
                                        <Input
                                            id="avatarUrl"
                                            label="Avatar URL (optional)"
                                            type="url"
                                            value={profileForm.avatarUrl}
                                            onChange={(e) => setProfileForm({ ...profileForm, avatarUrl: e.target.value })}
                                            placeholder="https://example.com/avatar.jpg"
                                            className="bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border-gray-200/50 dark:border-gray-700/50 focus:border-blue-500 focus:ring-blue-500/20"
                                        />
                                        <div className="flex space-x-3">
                                            <Button
                                                type="submit"
                                                variant="gradient"
                                                isLoading={loading}
                                                icon={Save}
                                                className="flex-1"
                                            >
                                                Save Changes
                                            </Button>
                                            <Button
                                                type="button"
                                                variant="glass"
                                                icon={X}
                                                onClick={() => {
                                                    setIsEditingProfile(false);
                                                    setProfileForm({
                                                        displayName: profile?.displayName || '',
                                                        avatarUrl: profile?.avatarUrl || ''
                                                    });
                                                }}
                                            >
                                                Cancel
                                            </Button>
                                        </div>
                                    </form>
                                ) : (
                                    <div className="space-y-6">
                                        <div>
                                            <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">
                                                Email
                                            </label>
                                            <p className="text-gray-900 dark:text-white text-lg">{profile?.email}</p>
                                        </div>
                                        <div>
                                            <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">
                                                Display Name
                                            </label>
                                            <p className="text-gray-900 dark:text-white text-lg">{profile?.displayName}</p>
                                        </div>
                                        <div>
                                            <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">
                                                Avatar URL
                                            </label>
                                            <p className="text-gray-500 dark:text-gray-400">
                                                {profile?.avatarUrl || 'No avatar URL set'}
                                            </p>
                                        </div>
                                        <div>
                                            <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">
                                                Member Since
                                            </label>
                                            <p className="text-gray-500 dark:text-gray-400">
                                                {profile ? new Date(profile.createdAt).toLocaleDateString() : 'Loading...'}
                                            </p>
                                        </div>
                                    </div>
                                )}
                            </div>
                        </div>

                        {/* Change Password Section */}
                        <div className="bg-white/80 dark:bg-gray-900/80 backdrop-blur-xl shadow-2xl rounded-2xl border border-gray-200/50 dark:border-gray-700/50 mt-6 overflow-hidden relative">
                            <div className="absolute top-0 right-0 w-32 h-32 bg-gradient-to-br from-purple-500/10 to-blue-500/10 rounded-full blur-2xl"></div>
                            
                            <div className="px-6 py-5 border-b border-gray-200/50 dark:border-gray-700/50 relative">
                                <h2 className="text-xl font-semibold flex items-center">
                                    <Key className="w-6 h-6 mr-3 text-purple-600" />
                                    <span className="bg-gradient-to-r from-purple-600 to-blue-600 bg-clip-text text-transparent">
                                        Change Password
                                    </span>
                                </h2>
                            </div>

                            <div className="p-6 relative">
                                {!showChangePassword ? (
                                    <div>
                                        <p className="text-gray-600 dark:text-gray-300 mb-6">
                                            Update your password to keep your account secure.
                                        </p>
                                        <Button
                                            variant="glass"
                                            onClick={() => setShowChangePassword(true)}
                                        >
                                            Change Password
                                        </Button>
                                    </div>
                                ) : (
                                    <form onSubmit={handleChangePassword} className="space-y-6">
                                        <Input
                                            id="currentPassword"
                                            label="Current Password"
                                            type="password"
                                            value={passwordForm.currentPassword}
                                            onChange={(e) => setPasswordForm({ ...passwordForm, currentPassword: e.target.value })}
                                            required
                                            className="bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border-gray-200/50 dark:border-gray-700/50 focus:border-blue-500 focus:ring-blue-500/20"
                                        />
                                        <Input
                                            id="newPassword"
                                            label="New Password"
                                            type="password"
                                            value={passwordForm.newPassword}
                                            onChange={(e) => setPasswordForm({ ...passwordForm, newPassword: e.target.value })}
                                            required
                                            minLength={6}
                                            className="bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border-gray-200/50 dark:border-gray-700/50 focus:border-blue-500 focus:ring-blue-500/20"
                                        />
                                        <Input
                                            id="confirmPassword"
                                            label="Confirm New Password"
                                            type="password"
                                            value={passwordForm.confirmPassword}
                                            onChange={(e) => setPasswordForm({ ...passwordForm, confirmPassword: e.target.value })}
                                            required
                                            minLength={6}
                                            className="bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border-gray-200/50 dark:border-gray-700/50 focus:border-blue-500 focus:ring-blue-500/20"
                                        />
                                        <div className="flex space-x-3">
                                            <Button
                                                type="submit"
                                                variant="gradient"
                                                isLoading={loading}
                                                className="flex-1"
                                            >
                                                Update Password
                                            </Button>
                                            <Button
                                                type="button"
                                                variant="glass"
                                                onClick={() => {
                                                    setShowChangePassword(false);
                                                    setPasswordForm({
                                                        currentPassword: '',
                                                        newPassword: '',
                                                        confirmPassword: ''
                                                    });
                                                }}
                                            >
                                                Cancel
                                            </Button>
                                        </div>
                                    </form>
                                )}
                            </div>
                        </div>
                    </div>

                    {/* Account Info Sidebar */}
                    <div className="lg:col-span-1">
                        <div className="bg-white/80 dark:bg-gray-900/80 backdrop-blur-xl shadow-2xl rounded-2xl border border-gray-200/50 dark:border-gray-700/50 p-8 relative overflow-hidden">
                            <div className="absolute top-0 right-0 w-24 h-24 bg-gradient-to-br from-blue-500/10 to-purple-500/10 rounded-full blur-xl"></div>
                            
                            <h3 className="text-xl font-semibold mb-6 relative">
                                <span className="bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
                                    Account Overview
                                </span>
                            </h3>
                            <div className="space-y-6 relative">
                                <div className="flex items-center justify-center mb-8">
                                    <div className="w-24 h-24 bg-gradient-to-br from-blue-500 to-purple-600 rounded-full flex items-center justify-center overflow-hidden shadow-xl">
                                        {profile?.avatarUrl ? (
                                            <img
                                                src={profile.avatarUrl}
                                                alt={profile.displayName}
                                                className="w-full h-full object-cover"
                                                onError={(e) => {
                                                    const img = e.target as HTMLImageElement;
                                                    img.style.display = 'none';
                                                }}
                                            />
                                        ) : (
                                            <User className="w-12 h-12 text-white" />
                                        )}
                                    </div>
                                </div>
                                <div className="text-center">
                                    <h4 className="font-semibold text-lg text-gray-900 dark:text-white mb-1">{profile?.displayName}</h4>
                                    <p className="text-gray-500 dark:text-gray-400">{profile?.email}</p>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default AccountPage;