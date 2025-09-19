import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { Copy, Check, Settings } from 'lucide-react';
import { RoomDetail, ChatMessage, RoomParticipant, PlaybackState, VirtualBrowser, VirtualBrowserQueue} from '../types/index';
import { useAuth } from '../contexts/AuthContext';
import { apiService } from '../services/api';
import { authService } from '../services/authService';
import virtualBrowserService from '../services/virtualBrowserService';
import signalRService from '../services/signalRService';
import Header from '../components/common/Header';
import EnhancedVideoPlayer from '../components/room/EnhancedVideoPlayer';
import VideoSelector from '../components/room/VideoSelector';
import VideoControlPanel from '../components/room/VideoControlPanel';
import ChatPanel from '../components/room/ChatPanel';
import ParticipantsList from '../components/room/ParticipantsList';
import AuthModal from '../components/auth/AuthModal';
import RoomInitialChoice from '../components/room/RoomInitialChoice';
import VirtualBrowserViewer from '../components/room/VirtualBrowserViewer';
import VirtualBrowserQueueStatus from '../components/room/VirtualBrowserQueueStatus';
import VirtualBrowserNotificationModal from '../components/room/VirtualBrowserNotificationModal';
import RoomSettingsModal from '../components/room/RoomSettingsModal';

const RoomPage: React.FC = () => {
    const { id: roomId } = useParams<{ id: string }>();
    const navigate = useNavigate();
    const { user, token, isAuthenticated, isLoading: authLoading, refreshToken } = useAuth();
    
    const [roomData, setRoomData] = useState<RoomDetail | null>(null);
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const [participants, setParticipants] = useState<RoomParticipant[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [currentUserHasControl, setCurrentUserHasControl] = useState(false);
    const [showAuthModal, setShowAuthModal] = useState(false);
    const [isCopied, setIsCopied] = useState(false);
    const [showSettingsModal, setShowSettingsModal] = useState(false);
    
    // Video state
    const [isPlaying, setIsPlaying] = useState(false);
    const [currentPosition, setCurrentPosition] = useState(0);
    const [videoDuration, setVideoDuration] = useState(0);

    // Virtual Browser state
    const [virtualBrowser, setVirtualBrowser] = useState<VirtualBrowser | null>(null);
    const [queueStatus, setQueueStatus] = useState<VirtualBrowserQueue | null>(null);
    const [showNotificationModal, setShowNotificationModal] = useState(false);
    const [notificationTimeRemaining, setNotificationTimeRemaining] = useState(0);
    const [roomMode, setRoomMode] = useState<'initial' | 'video' | 'virtual-browser'>('initial');
    const [cooldownTimeRemaining, setCooldownTimeRemaining] = useState(0);

    useEffect(() => {
        if (authLoading) {
            return;
        }
        
        if (!isAuthenticated) {
            setLoading(false);
            navigate('/login');
            return;
        }

        const initializeRoom = async () => {
            if (!roomId || !user) {
                setError('Missing room ID or user information');
                setLoading(false);
                return;
            }

            try {
                const response = await apiService.get<RoomDetail>(`/api/room/${roomId}`);
                setRoomData(response);
                
                if (response.participants && response.participants.length > 0) {
                    const currentUserParticipant = response.participants.find(p => p.id === user.id);
                    if (currentUserParticipant) {
                        setCurrentUserHasControl(currentUserParticipant.hasControl);
                        const mappedParticipants = response.participants.map(p => ({
                            ...p,
                            avatarUrl: p.avatarUrl || `https://api.dicebear.com/7.x/initials/svg?seed=${p.displayName}`
                        }));
                        setParticipants(mappedParticipants);
                    } else {
                        setCurrentUserHasControl(response.adminId === user.id);
                    }
                } else {
                    // Initially, only the admin has control (unless specified otherwise in the response)
                    setCurrentUserHasControl(response.adminId === user.id);
                }
                
                // Initialize video state from room data
                setIsPlaying(response.isPlaying || false);
                setCurrentPosition(response.currentPosition || 0);

                // Initialize virtual browser state
                try {
                    const vbResponse = await virtualBrowserService.getRoomVirtualBrowser(roomId);
                    if (vbResponse) {
                        if ('id' in vbResponse) {
                            // Room has an active virtual browser
                            setVirtualBrowser(vbResponse as VirtualBrowser);
                            setRoomMode('virtual-browser');
                        } else if ('queue' in vbResponse) {
                            // Room is in queue
                            setQueueStatus(vbResponse.queue);
                            setRoomMode(response.videoUrl ? 'video' : 'initial');
                        }
                    } else {
                        // No virtual browser or queue - determine initial mode
                        setRoomMode(response.videoUrl ? 'video' : 'initial');
                    }
                } catch (error) {
                    // If VB check fails, just set based on video
                    setRoomMode(response.videoUrl ? 'video' : 'initial');
                }

                // Check virtual browser cooldown status
                try {
                    const cooldownResponse = await virtualBrowserService.getRoomCooldownStatus(roomId);
                    if (cooldownResponse.isOnCooldown) {
                        setCooldownTimeRemaining(cooldownResponse.remainingSeconds);
                    }
                } catch (error) {
                    }

                if (!signalRService.getIsConnected()) {
                    const currentToken = token || authService.getStoredToken();
                    await signalRService.connect(currentToken);
                }

                const storedPasswordData = sessionStorage.getItem(`room_password_${roomId}`);
                let password = null;
                
                if (storedPasswordData) {
                    try {
                        const parsed = JSON.parse(storedPasswordData);
                        const maxAge = parsed.isCreator ? 24 * 60 * 60 * 1000 : 60 * 60 * 1000;
                        if (Date.now() - parsed.timestamp < maxAge) {
                            password = parsed.password;
                        } else {
                            sessionStorage.removeItem(`room_password_${roomId}`);
                        }
                    } catch (e) {
                        password = storedPasswordData;
                    }
                }
                
                try {
                    if (password) {
                        await signalRService.joinRoom(roomId, password);
                    } else {
                        await signalRService.joinRoom(roomId);
                    }
                } catch (joinError: any) {
                    // Check if this is an authorization error
                    if (joinError.message?.includes('unauthorized') || joinError.message?.includes('Unauthorized')) {
                        try {
                            // Try to refresh token using AuthContext
                            const refreshSuccess = await refreshToken();
                            
                            if (refreshSuccess) {
                                // Get the fresh token and retry SignalR connection
                                const freshToken = authService.getStoredToken();
                                await signalRService.disconnect();
                                await signalRService.connect(freshToken);
                                
                                // Retry joining the room
                                if (password) {
                                    await signalRService.joinRoom(roomId, password);
                                } else {
                                    await signalRService.joinRoom(roomId);
                                }
                                toast.success('Successfully reconnected to room');
                            } else {
                                throw new Error('Token refresh failed');
                            }
                        } catch (refreshError) {
                            toast.error('Session expired. Please log in again.');
                            setShowAuthModal(true);
                            return;
                        }
                    }
                    else if (joinError.message?.includes('Incorrect password') || joinError.message?.includes('password')) {
                        toast.error('This private room requires a password. Please join from the room list.');
                        navigate('/dashboard');
                        return;
                    }
                    
                    }
                
                
                try {
                    const chatHistory = await apiService.get<ChatMessage[]>(`/api/room/${roomId}/messages`);
                    if (chatHistory && chatHistory.length > 0) {
                        setMessages(chatHistory);
                        toast(`📜 Loaded ${chatHistory.length} previous messages`, {
                            duration: 2000,
                            style: {
                                background: '#f3f4f6',
                                color: '#374151',
                                border: '1px solid #d1d5db'
                            }
                        });
                    }
                } catch (error) {
                }
                
                setLoading(false);
            } catch (error: any) {
                if (error?.response?.status === 404 || error?.message?.includes('404')) {
                    setError('Room not found');
                } else {
                    setError(error.message || 'Failed to load room');
                }
                setLoading(false);
            }
        };

        if (roomId && user && isAuthenticated && !authLoading) {
            initializeRoom();
        }
    }, [roomId, user, isAuthenticated, authLoading, navigate, refreshToken, token]);

    // Set up SignalR event handlers
    useEffect(() => {
        const originalOnReceiveMessage = signalRService.onReceiveMessage;
        const originalOnUserJoined = signalRService.onUserJoined;
        const originalOnUserLeft = signalRService.onUserLeft;
        const originalOnReceivePlaybackState = signalRService.onReceivePlaybackState;
        const originalOnUserKicked = signalRService.onUserKicked;
        const originalOnVideoChanged = signalRService.onVideoChanged;
        const originalOnReceiveRoomParticipants = signalRService.onReceiveRoomParticipants;
        const originalOnControlTransferred = signalRService.onControlTransferred;
        const originalOnSyncModeChanged = signalRService.onSyncModeChanged;

        signalRService.onReceiveMessage = (message: ChatMessage) => {
            setMessages(prev => {
                // Check if message already exists to prevent duplicates
                if (prev.find(m => m.id === message.id)) {
                    return prev;
                }
                return [...prev, message];
            });
        };

        signalRService.onUserJoined = (displayName: string) => {
            const systemMessage: ChatMessage = {
                id: `system-join-${Date.now()}-${Math.random()}`,
                senderId: 'system',
                roomId: roomId || '',
                senderName: 'System',
                avatarUrl: '',
                content: `${displayName} joined the room`,
                timestamp: Date.now(),
                sentAt: new Date().toISOString()
            };
            setMessages(prev => {
                const recentJoinMessages = prev.filter(m => 
                    m.senderId === 'system' && 
                    m.content.includes(`${displayName} joined the room`) &&
                    Date.now() - m.timestamp < 5000
                );
                if (recentJoinMessages.length > 0) {
                    return prev;
                }
                return [...prev, systemMessage];
            });
        };

        signalRService.onUserLeft = (displayName: string) => {
            // Add system message for user leave
            const systemMessage: ChatMessage = {
                id: `system-leave-${Date.now()}-${Math.random()}`,
                senderId: 'system',
                roomId: roomId || '',
                senderName: 'System',
                avatarUrl: '',
                content: `${displayName} left the room`,
                timestamp: Date.now(),
                sentAt: new Date().toISOString()
            };
            setMessages(prev => {
                const recentLeftMessages = prev.filter(m => 
                    m.senderId === 'system' && 
                    m.content.includes(`${displayName} left the room`) &&
                    Date.now() - m.timestamp < 5000
                );
                if (recentLeftMessages.length > 0) {
                    return prev;
                }
                return [...prev, systemMessage];
            });
        };

        signalRService.onReceiveRoomParticipants = (participants: any[]) => {
            const mappedParticipants = participants.map(p => ({
                id: p.id,
                displayName: p.displayName,
                avatarUrl: p.avatarUrl || `https://api.dicebear.com/7.x/initials/svg?seed=${p.displayName}`,
                hasControl: p.hasControl,
                joinedAt: p.joinedAt,
                isAdmin: p.isAdmin
            }));
            setParticipants(mappedParticipants);
            
            // Update current user's control status
            const currentUserParticipant = mappedParticipants.find(p => p.id === user?.id);
            if (currentUserParticipant) {
                setCurrentUserHasControl(currentUserParticipant.hasControl);
            }
        };

        signalRService.onControlTransferred = (newControllerId: string, newControllerName: string) => {
            // Add system message for control transfer
            const systemMessage: ChatMessage = {
                id: `system-control-${Date.now()}-${Math.random()}`, // Add random component to ensure uniqueness
                senderId: 'system',
                roomId: roomId || '',
                senderName: 'System',
                avatarUrl: '',
                content: `Control transferred to ${newControllerName}`,
                timestamp: Date.now(),
                sentAt: new Date().toISOString()
            };
            setMessages(prev => [...prev, systemMessage]);
            
            // Update participants to reflect control change
            setParticipants(prev => prev.map(p => ({
                ...p,
                hasControl: p.id === newControllerId
            })));
            
            // Update current user's control status
            setCurrentUserHasControl(newControllerId === user?.id);
        };

        signalRService.onSyncModeChanged = (syncMode: string) => {
            // Update room data with new sync mode
            setRoomData(prev => prev ? { ...prev, syncMode } : null);
            
            // Show notification to users
            const systemMessage: ChatMessage = {
                id: `system-sync-${Date.now()}-${Math.random()}`,
                senderId: 'system',
                roomId: roomId || '',
                senderName: 'System',
                avatarUrl: '',
                content: `Sync mode changed to: ${syncMode === 'strict' ? 'Strict (Controller Only)' : 'Manual'}`,
                timestamp: Date.now(),
                sentAt: new Date().toISOString()
            };
            setMessages(prev => [...prev, systemMessage]);
            
            toast.success(`Sync mode updated to ${syncMode === 'strict' ? 'Strict' : 'Manual'}`);
        };

        signalRService.onReceivePlaybackState = (state: PlaybackState) => {
            setIsPlaying(state.isPlaying);
            setCurrentPosition(state.progress);
            if (state.duration > 0) {
                setVideoDuration(state.duration);
            }
        };

        signalRService.onUserKicked = (roomId: string, reason: string) => {
            toast.error(reason);
            if (roomData) {
                signalRService.leaveRoom(roomData.id);
            }
            navigate('/');
        };

        signalRService.onVideoChanged = (videoUrl: string, videoTitle: string, videoThumbnail?: string) => {
            setRoomData(prev => prev ? { ...prev, videoUrl } : null);
            
            setRoomMode('video');
            
            setCurrentPosition(0);
            setVideoDuration(0);
            setIsPlaying(false);
            
            toast.success(`Video changed to: ${videoTitle}`);
        };

        const originalOnVirtualBrowserAllocated = signalRService.onVirtualBrowserAllocated;
        const originalOnVirtualBrowserReleased = signalRService.onVirtualBrowserReleased;
        const originalOnVirtualBrowserExpired = signalRService.onVirtualBrowserExpired;
        const originalOnVirtualBrowserQueued = signalRService.onVirtualBrowserQueued;
        const originalOnVirtualBrowserQueueCancelled = signalRService.onVirtualBrowserQueueCancelled;
        const originalOnVirtualBrowserAvailable = signalRService.onVirtualBrowserAvailable;
        const originalOnVirtualBrowserQueueNotificationExpired = signalRService.onVirtualBrowserQueueNotificationExpired;

        signalRService.onVirtualBrowserAllocated = (virtualBrowserData: VirtualBrowser) => {
            setVirtualBrowser(virtualBrowserData);
            setQueueStatus(null);
            setRoomMode('virtual-browser');
            toast.success('Virtual browser allocated! Starting session...');
        };

        signalRService.onVirtualBrowserReleased = () => {
            setVirtualBrowser(null);
            setRoomMode('initial');
            toast('Virtual browser session ended', {
                icon: 'ℹ️',
                style: { background: '#3b82f6', color: 'white' }
            });
            
            if (roomId) {
                virtualBrowserService.getRoomCooldownStatus(roomId)
                    .then(cooldownResponse => {
                        if (cooldownResponse.isOnCooldown) {
                            setCooldownTimeRemaining(cooldownResponse.remainingSeconds);
                        }
                    })
                    .catch(error => {
                        });
            }
        };

        signalRService.onVirtualBrowserExpired = () => {
            setVirtualBrowser(null);
            setRoomMode('initial');
            toast.error('Virtual browser session expired');
            
            if (roomId) {
                virtualBrowserService.getRoomCooldownStatus(roomId)
                    .then(cooldownResponse => {
                        if (cooldownResponse.isOnCooldown) {
                            setCooldownTimeRemaining(cooldownResponse.remainingSeconds);
                        }
                    })
                    .catch(error => {
                        });
            }
        };

        signalRService.onVirtualBrowserQueued = (queueData: VirtualBrowserQueue) => {
            setQueueStatus(queueData);
            toast(`Added to queue at position ${queueData.position}`, {
                icon: '⏳',
                style: { background: '#f59e0b', color: 'white' }
            });
        };

        signalRService.onVirtualBrowserQueueCancelled = () => {
            setQueueStatus(null);
            toast('Removed from virtual browser queue', {
                icon: 'ℹ️',
                style: { background: '#3b82f6', color: 'white' }
            });
        };

        signalRService.onVirtualBrowserAvailable = (queueData: VirtualBrowserQueue) => {
            setQueueStatus(queueData);
            // Ensure we have a valid number for the timer
            const timeRemaining = (queueData.notificationTimeRemaining && !isNaN(queueData.notificationTimeRemaining)) 
                ? queueData.notificationTimeRemaining 
                : 120; // Default 2 minutes
            setNotificationTimeRemaining(timeRemaining);
            setShowNotificationModal(true);
            toast.success('Virtual browser is available!');
        };

        signalRService.onVirtualBrowserQueueNotificationExpired = () => {
            setShowNotificationModal(false);
            setQueueStatus(null);
            toast('Virtual browser notification expired', {
                icon: '⚠️',
                style: { background: '#f59e0b', color: 'white' }
            });
        };

        return () => {
            signalRService.onReceiveMessage = originalOnReceiveMessage;
            signalRService.onUserJoined = originalOnUserJoined;
            signalRService.onUserLeft = originalOnUserLeft;
            signalRService.onReceivePlaybackState = originalOnReceivePlaybackState;
            signalRService.onUserKicked = originalOnUserKicked;
            signalRService.onVideoChanged = originalOnVideoChanged;
            signalRService.onReceiveRoomParticipants = originalOnReceiveRoomParticipants;
            signalRService.onControlTransferred = originalOnControlTransferred;
            signalRService.onSyncModeChanged = originalOnSyncModeChanged;
            
            // Virtual Browser cleanup
            signalRService.onVirtualBrowserAllocated = originalOnVirtualBrowserAllocated;
            signalRService.onVirtualBrowserReleased = originalOnVirtualBrowserReleased;
            signalRService.onVirtualBrowserExpired = originalOnVirtualBrowserExpired;
            signalRService.onVirtualBrowserQueued = originalOnVirtualBrowserQueued;
            signalRService.onVirtualBrowserQueueCancelled = originalOnVirtualBrowserQueueCancelled;
            signalRService.onVirtualBrowserAvailable = originalOnVirtualBrowserAvailable;
            signalRService.onVirtualBrowserQueueNotificationExpired = originalOnVirtualBrowserQueueNotificationExpired;
        };
    }, []);

    useEffect(() => {
        if (user?.id && participants.length > 0) {
            const currentUserParticipant = participants.find(p => p.id === user.id);
            if (currentUserParticipant) {
                setCurrentUserHasControl(currentUserParticipant.hasControl);
            }
        }
    }, [user?.id, participants]);

    // Cooldown countdown effect
    useEffect(() => {
        if (cooldownTimeRemaining > 0) {
            const interval = setInterval(() => {
                setCooldownTimeRemaining(prev => {
                    if (prev <= 1) {
                        clearInterval(interval);
                        return 0;
                    }
                    return prev - 1;
                });
            }, 1000);

            return () => clearInterval(interval);
        }
    }, [cooldownTimeRemaining]);

    useEffect(() => {
        const handleBeforeUnload = (event: BeforeUnloadEvent) => {
        };

        const handlePopState = (event: PopStateEvent) => {
            
        };

        window.addEventListener('beforeunload', handleBeforeUnload);
        window.addEventListener('popstate', handlePopState);

        return () => {
            window.removeEventListener('beforeunload', handleBeforeUnload);
            window.removeEventListener('popstate', handlePopState);
        };
    }, [roomId]);

    const handleNavigateAway = () => {
        if (roomId) {
            signalRService.leaveRoom(roomId);
        }
        navigate('/dashboard');
    };

    const handleSendMessage = async (content: string) => {
        if (!roomId || !user) return;
        
        try {
            await signalRService.sendMessage(roomId, content);
        } catch (error) {
            toast.error('Failed to send message');
        }
    };

    const handleLeaveRoom = () => {
        if (roomId) {
            signalRService.leaveRoom(roomId);
        }
        navigate('/dashboard');
    };

    const handleKickUser = (userId: string) => {
        const participant = participants.find(p => p.id === userId);
        const username = participant?.displayName || 'Unknown User';
        const confirmed = window.confirm(`Are you sure you want to kick ${username} from the room?`);
        if (confirmed && roomData) {            
            const kickedUserHasControl = participant?.hasControl || false;
            
            signalRService.kickUser(roomData.id, userId);
            
            if (kickedUserHasControl && roomData.adminId) {
                setTimeout(() => {
                    signalRService.transferControl(roomData.id, roomData.adminId);
                }, 100);
            }
        }
    };

    const handleTransferControl = async (participantId: string) => {
        if (!roomId) return;
        
        try {
            await signalRService.transferControl(roomId, participantId);
            toast.success('Control transferred successfully');
        } catch (error) {
            toast.error('Failed to transfer control');
        }
    };

    const handleVideoPlay = async () => {
        if (!roomId) return;
        
        if (roomData?.syncMode === 'strict' && !currentUserHasControl) {
            return;
        }
        
        try {
            setIsPlaying(true);
            if (roomData?.syncMode === 'strict') {
                await signalRService.playVideo(roomId);
            }
        } catch (error) {
            setIsPlaying(false);
        }
    };

    const handleVideoPause = async () => {
        if (!roomId) return;
        
        if (roomData?.syncMode === 'strict' && !currentUserHasControl) {
            return;
        }
        
        try {
            setIsPlaying(false);
            if (roomData?.syncMode === 'strict') {
                await signalRService.pauseVideo(roomId);
            }
        } catch (error) {
            setIsPlaying(true);
        }
    };

    const handleVideoSeek = async (position: number) => {
        if (!roomId) return;
        if (roomData?.syncMode === 'strict' && !currentUserHasControl) {
            return;
        }
        try {
            setCurrentPosition(position);
            if (roomData?.syncMode === 'strict') {
                await signalRService.seekVideo(roomId, position);
            }
        } catch (error) {
        }
    };

    const handleVideoTimeUpdate = (position: number) => {
        setCurrentPosition(position);
    };

    const handleVideoDurationUpdate = (duration: number) => {
        setVideoDuration(duration);
    };

    const handleVideoSelect = async (videoUrl: string, videoTitle: string, videoThumbnail?: string) => {
        if (!roomId) return;
        
        try {
            await signalRService.changeVideo(roomId, videoUrl, videoTitle, videoThumbnail);
        } catch (error) {
            toast.error('Failed to change video');
            }
    };

    const handleAuthSuccess = () => {
        setShowAuthModal(false);
        setLoading(true);
    };

    const handlePlayVideo = () => {
        setRoomMode('video');
    };

    const handleStopVideo = () => {
        setRoomMode('initial');
    };

    const handleStartVirtualBrowser = async () => {
        if (!roomId) return;
        
        try {
            const response = await virtualBrowserService.requestVirtualBrowser(roomId);
            
            if ('id' in response) {
                setVirtualBrowser(response as VirtualBrowser);
                setRoomMode('virtual-browser');
                toast.success('Virtual browser allocated immediately!');
            } else {
                // Queued response
                const queueResponse = response as { message: string; queue: VirtualBrowserQueue };
                setQueueStatus(queueResponse.queue);
                setRoomMode('video');
                toast(`${queueResponse.message} - Position ${queueResponse.queue.position}`, {
                    icon: '⏳',
                    style: { background: '#f59e0b', color: 'white' }
                });
            }
        } catch (error: any) {
            toast.error(error.message || 'Failed to request virtual browser');
        }
    };

    const handleReleaseVirtualBrowser = async () => {
        if (!roomId || !virtualBrowser) return;
        
        try {
            await virtualBrowserService.releaseVirtualBrowser(roomId);
            toast.success('Virtual browser released');
            
            try {
                const cooldownResponse = await virtualBrowserService.getRoomCooldownStatus(roomId);
                if (cooldownResponse.isOnCooldown) {
                    setCooldownTimeRemaining(cooldownResponse.remainingSeconds);
                }
            } catch (cooldownError) {
                }
            
        } catch (error: any) {
            toast.error(error.message || 'Failed to release virtual browser');
        }
    };

    const handleCancelQueue = async () => {
        if (!roomId || !queueStatus) return;
        
        try {
            await virtualBrowserService.cancelQueue(roomId);
            setQueueStatus(null);
            toast.success('Removed from queue');
        } catch (error: any) {
            toast.error(error.message || 'Failed to cancel queue');
        }
    };

    const handleAcceptVirtualBrowser = async () => {
        if (!roomId) return;
        
        try {
            await virtualBrowserService.acceptQueueNotification(roomId);
            setShowNotificationModal(false);
            // The allocation will come through SignalR event
        } catch (error: any) {
            toast.error(error.message || 'Failed to accept virtual browser');
            setShowNotificationModal(false);
        }
    };

    const handleDeclineVirtualBrowser = async () => {
        if (!roomId) return;
        
        try {
            await virtualBrowserService.declineQueueNotification(roomId);
            setShowNotificationModal(false);
            setQueueStatus(null);
            toast('Virtual browser declined', {
                icon: 'ℹ️',
                style: { background: '#6b7280', color: 'white' }
            });
        } catch (error: any) {
            toast.error(error.message || 'Failed to decline virtual browser');
            setShowNotificationModal(false);
        }
    };

    const handleAuthModalClose = () => {
        setShowAuthModal(false);
        navigate('/');
    };

    const handleCopyInviteCode = async () => {
        if (!roomData?.inviteCode) return;
        
        try {
            await navigator.clipboard.writeText(roomData.inviteCode);
            setIsCopied(true);
            toast.success('Invite code copied to clipboard!');
            
            // Reset the copy state after 2 seconds
            setTimeout(() => {
                setIsCopied(false);
            }, 2000);
        } catch (error) {
            toast.error('Failed to copy invite code');
        }
    };

    const handleSyncModeChange = async (newSyncMode: 'strict' | 'relaxed') => {
        if (!roomData || !roomId) return;
        
        try {
            await signalRService.updateSyncMode(roomId, newSyncMode);
            
            setShowSettingsModal(false);
            
            toast.success(`Sync mode changed to ${newSyncMode === 'strict' ? 'Strict' : 'Manual'}`);
        } catch (error: any) {
            toast.error(error.message || 'Failed to update sync mode');
        }
    };

    if (showAuthModal) {
        return (
            <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50 dark:from-gray-900 dark:via-gray-800 dark:to-purple-900">
                <Header onLogoClick={handleNavigateAway} onHomeClick={handleNavigateAway} />
                <AuthModal
                    isOpen={showAuthModal}
                    onClose={handleAuthModalClose}
                    onSuccess={handleAuthSuccess}
                    title="Join Watch Party"
                    message="Please login or create an account to join this watch party room."
                />
            </div>
        );
    }

    if (authLoading) {
        return (
            <div className="flex justify-center items-center min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50 dark:from-gray-900 dark:via-gray-800 dark:to-purple-900">
                <div className="w-16 h-16 border-4 border-blue-500/30 border-t-blue-500 rounded-full animate-spin"></div>
            </div>
        );
    }

    if (!isAuthenticated && !showAuthModal) {
        return null;
    }

    if (loading) {
        return (
            <div className="flex justify-center items-center min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50 dark:from-gray-900 dark:via-gray-800 dark:to-purple-900">
                <div className="w-16 h-16 border-4 border-blue-500/30 border-t-blue-500 rounded-full animate-spin"></div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="flex flex-col items-center justify-center min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50 dark:from-gray-900 dark:via-gray-800 dark:to-purple-900">
                <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 text-red-700 dark:text-red-400 px-6 py-4 rounded-xl mb-6 shadow-lg backdrop-blur-sm">
                    <div className="flex items-center space-x-2">
                        <div className="w-5 h-5 bg-red-500 rounded-full flex-shrink-0"></div>
                        <span className="font-medium">Error: {error}</span>
                    </div>
                </div>
                <button
                    onClick={() => navigate('/dashboard')}
                    className="bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 text-white font-semibold py-3 px-6 rounded-xl shadow-lg hover:shadow-xl transition-all duration-200 transform hover:-translate-y-0.5"
                >
                    Back to Dashboard
                </button>
            </div>
        );
    }

    if (!roomData) {
        return (
            <div className="flex justify-center items-center min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50 dark:from-gray-900 dark:via-gray-800 dark:to-purple-900">
                <div className="w-16 h-16 border-4 border-blue-500/30 border-t-blue-500 rounded-full animate-spin"></div>
            </div>
        );
    }

    return (
    <div className="flex flex-col min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50 dark:from-gray-900 dark:via-gray-800 dark:to-purple-900 overflow-y-auto overflow-x-hidden scrollbar-thin scrollbar-thumb-gray-800 scrollbar-track-transparent relative">
            <div className="absolute inset-0 -z-10">
                <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-blue-400/10 dark:bg-blue-500/5 rounded-full blur-3xl"></div>
                <div className="absolute bottom-1/4 right-1/4 w-80 h-80 bg-purple-400/10 dark:bg-purple-500/5 rounded-full blur-3xl"></div>
            </div>
            
            <Header onLogoClick={handleNavigateAway} onHomeClick={handleNavigateAway} />
            
            {/* Room Header - Enhanced with modern styling */}
            <div className="bg-white/80 dark:bg-gray-900/80 backdrop-blur-xl shadow-lg border-b border-gray-200/50 dark:border-gray-700/50 flex-shrink-0 relative">
                {/* Header decoration */}
                <div className="absolute top-0 right-0 w-32 h-32 bg-gradient-to-br from-blue-500/10 to-purple-500/10 rounded-full blur-2xl"></div>
                
                <div className="mx-auto px-6 py-6 relative">
                    <div className="flex justify-between items-center">
                        <div className="flex-1">
                            <div className="flex items-center space-x-4">
                                <div>
                                    <h1 className="text-3xl font-bold">
                                        <span className="bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
                                            {roomData.name}
                                        </span>
                                    </h1>
                                    <div className="flex items-center space-x-4 mt-2">
                                        <p className="text-gray-600 dark:text-gray-300">
                                            Hosted by <span className="font-semibold text-gray-900 dark:text-white">{participants.find(p => p.id === roomData.adminId)?.displayName || roomData.adminName}</span> • 
                                            <span className="text-blue-600 dark:text-blue-400 font-medium"> {participants.length} participant{participants.length !== 1 ? 's' : ''}</span>
                                        </p>
                                        
                                        {/* Invite Code Button */}
                                        <button
                                            onClick={handleCopyInviteCode}
                                            className="inline-flex items-center px-4 py-2 text-sm font-medium bg-gradient-to-r from-blue-50 to-purple-50 dark:from-blue-900/30 dark:to-purple-900/30 text-blue-700 dark:text-blue-300 border border-blue-200/50 dark:border-blue-700/50 rounded-lg hover:from-blue-100 hover:to-purple-100 dark:hover:from-blue-900/50 dark:hover:to-purple-900/50 transition-all duration-200 backdrop-blur-sm shadow-sm hover:shadow-md"
                                            title={`Copy invite code: ${roomData.inviteCode}`}
                                        >
                                            {isCopied ? (
                                                <>
                                                    <Check className="w-4 h-4 mr-2" />
                                                    Copied!
                                                </>
                                            ) : (
                                                <>
                                                    <Copy className="w-4 h-4 mr-2" />
                                                    Code: {roomData.inviteCode}
                                                </>
                                            )}
                                        </button>
                                    </div>
                                </div>
                            </div>
                        </div>
                        
                        <div className="flex items-center space-x-3">
                            {/* Settings button - only show for room admin */}
                            {user?.id === roomData.adminId && (
                                <button
                                    onClick={() => setShowSettingsModal(true)}
                                    className="bg-white/70 dark:bg-gray-800/70 backdrop-blur-sm hover:bg-white/90 dark:hover:bg-gray-800/90 text-gray-700 dark:text-gray-300 font-medium py-3 px-5 rounded-xl transition-all duration-200 shadow-lg hover:shadow-xl border border-gray-200/50 dark:border-gray-700/50 flex items-center space-x-2 transform hover:-translate-y-0.5"
                                    title="Room Settings"
                                >
                                    <Settings className="w-4 h-4" />
                                    <span>Settings</span>
                                </button>
                            )}
                            <button
                                onClick={handleLeaveRoom}
                                className="bg-gradient-to-r from-red-500 to-red-600 hover:from-red-600 hover:to-red-700 text-white font-medium py-3 px-5 rounded-xl transition-all duration-200 shadow-lg hover:shadow-xl transform hover:-translate-y-0.5"
                            >
                                Leave Room
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            {/* Main Content - Enhanced with modern glass-morphism */}
            <div 
                id="fullscreen-container" 
                className="flex-1 flex flex-col lg:flex-row gap-6 px-4 lg:px-6 py-4 lg:py-6 overflow-hidden" 
                style={{ minHeight: 0 }}
            >
                    {/* Video/Virtual Browser Player - Enhanced styling */}
                    <div className="flex-1 lg:flex-[2] min-h-0">
                        <div className="bg-black/90 backdrop-blur-xl rounded-2xl shadow-2xl overflow-hidden h-full relative border border-gray-800/50 min-h-[300px] lg:min-h-0">
                            {/* Video container decoration */}
                            <div className="absolute inset-0 bg-gradient-to-r from-blue-900/20 via-transparent to-purple-900/20 pointer-events-none"></div>
                            
                            <div className="w-full h-full relative z-10 flex items-center justify-center">
                                {roomMode === 'initial' && (
                                    <RoomInitialChoice
                                        onPlayVideo={handlePlayVideo}
                                        onStartVirtualBrowser={handleStartVirtualBrowser}
                                        hasControl={currentUserHasControl}
                                        cooldownTimeRemaining={cooldownTimeRemaining}
                                        isInQueue={queueStatus !== null}
                                    />
                                )}
                                
                                {roomMode === 'video' && (
                                    <>
                                        {roomData.videoUrl ? (
                                            <>
                                                <EnhancedVideoPlayer
                                                    src={roomData.videoUrl}
                                                    isPlaying={isPlaying}
                                                    position={currentPosition}
                                                    duration={videoDuration}
                                                    onPlay={handleVideoPlay}
                                                    onPause={handleVideoPause}
                                                    onSeek={handleVideoSeek}
                                                    onTimeUpdate={handleVideoTimeUpdate}
                                                    onDurationUpdate={handleVideoDurationUpdate}
                                                    hasControl={currentUserHasControl}
                                                    syncMode={roomData.syncMode}
                                                />
                                                <VideoControlPanel
                                                    hasControl={currentUserHasControl}
                                                    onVideoSelect={handleVideoSelect}
                                                    onBack={handleStopVideo}
                                                />
                                            </>
                                        ) : (
                                            <div className="w-full h-full bg-gray-900 flex items-center justify-center">
                                                <VideoSelector
                                                    onVideoSelect={handleVideoSelect}
                                                    hasControl={currentUserHasControl}
                                                    onBack={handleStopVideo}
                                                />
                                            </div>
                                        )}
                                    </>
                                )}
                                
                                {roomMode === 'virtual-browser' && virtualBrowser && (
                                    <VirtualBrowserViewer
                                        virtualBrowser={virtualBrowser}
                                        onRelease={handleReleaseVirtualBrowser}
                                        hasControl={currentUserHasControl}
                                    />
                                )}
                            </div>
                        </div>
                    </div>

                    {/* Sidebar - Enhanced with glass-morphism */}
                    <div className="flex-1 min-h-0 lg:flex-[1] flex flex-col space-y-4 lg:space-y-6">
                        
                        {/* Queue Status (when in queue) */}
                        {queueStatus && roomMode !== 'virtual-browser' && (
                            <div className="bg-white/80 dark:bg-gray-900/80 backdrop-blur-xl rounded-2xl shadow-xl border border-gray-200/50 dark:border-gray-700/50 flex-shrink-0 overflow-hidden relative">
                                <div className="absolute top-0 right-0 w-24 h-24 bg-gradient-to-br from-blue-500/10 to-purple-500/10 rounded-full blur-xl"></div>
                                <div className="relative">
                                    <VirtualBrowserQueueStatus
                                        queueStatus={queueStatus}
                                        onCancelQueue={handleCancelQueue}
                                        hasControl={currentUserHasControl}
                                    />
                                </div>
                            </div>
                        )}
                        
                        {/* Participants Panel */}
                        <div className="bg-white/80 dark:bg-gray-900/80 backdrop-blur-xl rounded-2xl shadow-xl border border-gray-200/50 dark:border-gray-700/50 flex-shrink-0 overflow-hidden relative">
                            <div className="absolute top-0 right-0 w-24 h-24 bg-gradient-to-br from-purple-500/10 to-blue-500/10 rounded-full blur-xl"></div>
                            
                            <div className="px-6 py-4 border-b border-gray-200/50 dark:border-gray-700/50 relative">
                                <h3 className="text-lg font-semibold flex items-center">
                                    <span className="bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
                                        👥 Participants ({participants.length})
                                    </span>
                                </h3>
                            </div>
                            <div className="p-4 max-h-36 lg:max-h-48 overflow-y-auto relative">
                                <ParticipantsList
                                    participants={participants}
                                    currentUserId={user?.id || ''}
                                    roomAdminId={roomData.adminId}
                                    onTransferControl={handleTransferControl}
                                    currentUserHasControl={currentUserHasControl}
                                    onKickUser={handleKickUser}
                                />
                            </div>
                        </div>

                        {/* Chat Panel */}
                        <div className="bg-white/80 dark:bg-gray-900/80 backdrop-blur-xl rounded-2xl shadow-xl border border-gray-200/50 dark:border-gray-700/50 flex flex-col flex-1 min-h-0 overflow-hidden relative">
                            <div className="absolute top-0 right-0 w-24 h-24 bg-gradient-to-br from-blue-500/10 to-purple-500/10 rounded-full blur-xl"></div>
                            
                            <div className="px-6 py-4 border-b border-gray-200/50 dark:border-gray-700/50 flex-shrink-0 relative">
                                <h3 className="text-lg font-semibold flex items-center justify-between">
                                    <span className="bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
                                        💬 Live Chat
                                    </span>
                                    <span className="text-sm bg-gradient-to-r from-green-500 to-emerald-500 bg-clip-text text-transparent font-medium">
                                        Connected
                                    </span>
                                </h3>
                            </div>
                            <div className="flex-1 min-h-0 overflow-hidden relative">
                                <ChatPanel
                                    messages={messages}
                                    onSendMessage={handleSendMessage}
                                    isConnected={signalRService.getIsConnected()}
                                />
                            </div>
                        </div>
                        
                    </div>
            </div>
            
            {/* Virtual Browser Notification Modal */}
            <VirtualBrowserNotificationModal
                isOpen={showNotificationModal}
                timeRemaining={notificationTimeRemaining}
                onAccept={handleAcceptVirtualBrowser}
                onDecline={handleDeclineVirtualBrowser}
            />

            {/* Room Settings Modal */}
            <RoomSettingsModal
                isOpen={showSettingsModal}
                onClose={() => setShowSettingsModal(false)}
                currentSyncMode={roomData?.syncMode || 'strict'}
                onSyncModeChange={handleSyncModeChange}
                roomName={roomData?.name || ''}
            />
        </div>
    );
};

export default RoomPage;
