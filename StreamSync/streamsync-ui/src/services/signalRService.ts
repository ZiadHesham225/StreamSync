import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { ChatMessage, PlaybackState, VirtualBrowser, VirtualBrowserQueue } from "../types/index";

class SignalRService {
    private connection: HubConnection | null = null;
    private isConnecting: boolean = false;
    private joiningRoom: boolean = false;
    private currentRoomId: string | null = null;
    
    // Store the current token for use in accessTokenFactory
    private currentToken: string | null = null;
    // Callbacks for token refresh notification
    private tokenRefreshCallbacks: Set<() => void> = new Set();

    private cleanupEventListeners = () => {
        if (!this.connection) return;
        
        this.connection.off("ReceiveMessage");
        this.connection.off("ReceiveChatHistory");
        this.connection.off("RoomJoined");
        this.connection.off("UserJoined");
        this.connection.off("UserLeft");
        this.connection.off("ReceivePlaybackUpdate");
        this.connection.off("Error");
        this.connection.off("ForceSyncPlayback");
        this.connection.off("ReceiveRoomParticipants");
        this.connection.off("ControlTransferred");
        this.connection.off("RoomClosed");
        this.connection.off("VideoChanged");
        this.connection.off("UserKicked");
        this.connection.off("SyncModeChanged");
    }

    private connectPromise: Promise<void> | null = null;
    public onConnectionStateChange: (isConnected: boolean, isConnecting: boolean) => void = () => { };

    public getConnection(): HubConnection | null {
        return this.connection;
    }

    public onReceiveMessage: (message: ChatMessage) => void = () => { };
    public onReceiveChatHistory: (messages: ChatMessage[]) => void = () => { };
    public onUserJoined: (displayName: string) => void = () => { };
    public onUserLeft: (displayName: string) => void = () => { };
    public onReceivePlaybackState: (state: PlaybackState) => void = () => { };
    public onError: (message: string) => void = () => { };
    public onReceiveRoomParticipants: (participants: any[]) => void = () => { };
    public onControlTransferred: (newControllerId: string, newControllerName: string) => void = () => { };
    public onRoomClosed: (roomId: string, reason: string) => void = () => { };
    public onVideoChanged: (videoUrl: string, videoTitle: string, videoThumbnail?: string) => void = () => { };
    public onRoomJoined: (roomId: string, participantId: string, displayName: string, avatarUrl: string) => void = () => { };
    public onUserKicked: (roomId: string, reason: string) => void = () => { };
    public onSyncModeChanged: (syncMode: string) => void = () => { };

    // Virtual Browser event handlers
    public onVirtualBrowserAllocated: (virtualBrowser: VirtualBrowser) => void = () => { };
    public onVirtualBrowserReleased: () => void = () => { };
    public onVirtualBrowserExpired: () => void = () => { };
    public onVirtualBrowserQueued: (queueStatus: VirtualBrowserQueue) => void = () => { };
    public onVirtualBrowserQueueCancelled: () => void = () => { };
    public onVirtualBrowserAvailable: (queueStatus: VirtualBrowserQueue) => void = () => { };
    public onVirtualBrowserQueueNotificationExpired: () => void = () => { };

    public connect = async (token: string | null) => {
        if (this.connection?.state === HubConnectionState.Connected) {
            return;
        }
        
        if (this.connectPromise) {
            return this.connectPromise;
        }

        this.connectPromise = this._doConnect(token);
        try {
            await this.connectPromise;
        } finally {
            this.connectPromise = null;
        }
    }

    private _doConnect = async (token: string | null) => {
        this.onConnectionStateChange(false, true);
        
        // Store the initial token
        this.currentToken = token;

        const hubUrl = `${process.env.REACT_APP_API_URL || 'http://localhost:5099'}/hubs/roomhub`;

        const connectionBuilder = new HubConnectionBuilder()
            .withUrl(hubUrl, {
                // Use a factory function that always returns the latest token
                // This ensures reconnections use the refreshed token
                accessTokenFactory: () => this.currentToken || ""
            })
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: retryContext => {
                    // Custom retry policy with exponential backoff
                    if (retryContext.previousRetryCount < 5) {
                        return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
                    }
                    return null; // Stop retrying after 5 attempts
                }
            })
            .configureLogging(LogLevel.Information);

        this.connection = connectionBuilder.build();
        this.registerClientEvents();
        
        // Handle reconnecting event - token will be automatically used from currentToken
        this.connection.onreconnecting(error => {
            this.onConnectionStateChange(false, true);
        });
        
        this.connection.onreconnected(connectionId => {
            this.onConnectionStateChange(true, false);
            // Re-join the room if we were in one
            if (this.currentRoomId) {
                const roomId = this.currentRoomId;
                this.currentRoomId = null; // Reset to allow re-join
                this.joiningRoom = false;
                this.joinRoom(roomId).catch(err => {
                    console.error('Failed to rejoin room after reconnection:', err);
                });
            }
        });

        this.connection.onclose(error => {
            this.onConnectionStateChange(false, false);
        });

        try {
            await this.connection.start();
            this.onConnectionStateChange(true, false);
        } catch (error) {
            this.onConnectionStateChange(false, false);
            throw error;
        }
    }

    /**
     * Update the access token without disconnecting.
     * The new token will be used for:
     * 1. Any automatic reconnections triggered by SignalR
     * 2. The next manual reconnection if needed
     * 
     * Note: SignalR doesn't support changing the token on an active connection,
     * but since we use accessTokenFactory with this.currentToken, the new token
     * will be automatically used when SignalR needs to reconnect.
     */
    public updateAccessToken = (newToken: string) => {
        this.currentToken = newToken;
        
        // Notify any listeners that the token has been updated
        this.tokenRefreshCallbacks.forEach(callback => callback());
    }
    
    /**
     * Register a callback to be notified when the token is updated
     */
    public onTokenRefresh = (callback: () => void): (() => void) => {
        this.tokenRefreshCallbacks.add(callback);
        return () => {
            this.tokenRefreshCallbacks.delete(callback);
        };
    }
    
    /**
     * Get the current token (useful for debugging or manual operations)
     */
    public getCurrentToken = (): string | null => {
        return this.currentToken;
    }

    public disconnect = async () => {
        this.connectPromise = null;
        this.joiningRoom = false;
        this.currentRoomId = null;
        this.currentToken = null;
        if (this.connection) {
            try {
                this.removeEventListeners();
                await this.connection.stop();
            } catch (error) {
            } finally {
                this.connection = null;
            }
        }
    }

    private removeEventListeners = () => {
        if (!this.connection) return;
        
        this.connection.off("ReceiveMessage");
        this.connection.off("ReceiveChatHistory");
        this.connection.off("RoomJoined");
        this.connection.off("UserJoined");
        this.connection.off("UserLeft");
        this.connection.off("ReceivePlaybackUpdate");
        this.connection.off("Error");
        this.connection.off("ForceSyncPlayback");
        this.connection.off("ReceiveRoomParticipants");
        this.connection.off("ControlTransferred");
        this.connection.off("RoomClosed");
        this.connection.off("VideoChanged");
        this.connection.off("UserKicked");
        
        // Virtual Browser events cleanup
        this.connection.off("VirtualBrowserAllocated");
        this.connection.off("VirtualBrowserReleased");
        this.connection.off("VirtualBrowserExpired");
        this.connection.off("VirtualBrowserQueued");
        this.connection.off("VirtualBrowserAvailable");
        this.connection.off("VirtualBrowserQueueNotificationExpired");
    }

    private registerClientEvents = () => {
        if (!this.connection) return;

        // Remove any existing event listeners to prevent duplicates
        this.connection.off("ReceiveMessage");
        this.connection.off("ReceiveChatHistory");
        this.connection.off("RoomJoined");
        this.connection.off("UserJoined");
        this.connection.off("UserLeft");
        this.connection.off("ReceivePlaybackUpdate");
        this.connection.off("Error");
        this.connection.off("ForceSyncPlayback");
        this.connection.off("ReceiveRoomParticipants");
        this.connection.off("ControlTransferred");
        this.connection.off("RoomClosed");
        this.connection.off("VideoChanged");
        this.connection.off("UserKicked");
        this.connection.off("SyncModeChanged");
        
        // Virtual Browser events cleanup
        this.connection.off("VirtualBrowserAllocated");
        this.connection.off("VirtualBrowserReleased");
        this.connection.off("VirtualBrowserExpired");
        this.connection.off("VirtualBrowserQueued");
        this.connection.off("VirtualBrowserAvailable");
        this.connection.off("VirtualBrowserQueueNotificationExpired");

        this.connection.on("ReceiveMessage", (senderId: string, senderName: string, avatarUrl: string, message: string, timestamp: string) => {
            const chatMessage: ChatMessage = {
                id: `msg-${senderId}-${new Date(timestamp).getTime()}`,
                senderId,
                roomId: "",
                content: message,
                timestamp: new Date(timestamp).getTime(),
                senderName,
                avatarUrl,
                sentAt: timestamp
            };
            this.onReceiveMessage(chatMessage);
        });

        this.connection.on("ReceiveChatHistory", (messageDtos: any[]) => {
            const chatMessages: ChatMessage[] = messageDtos.map(dto => ({
                id: dto.Id,
                senderId: dto.SenderId,
                roomId: "",
                content: dto.Content,
                timestamp: new Date(dto.SentAt).getTime(),
                senderName: dto.SenderName,
                avatarUrl: dto.AvatarUrl,
                sentAt: dto.SentAt
            }));
            this.onReceiveChatHistory(chatMessages);
        });

        this.connection.on("RoomJoined", (roomId: string, participantId: string, displayName: string, avatarUrl: string) => {
            this.onRoomJoined(roomId, participantId, displayName, avatarUrl);
        });

        this.connection.on("UserJoined", (displayName: string) => {
            this.onUserJoined(displayName);
        });

        this.connection.on("UserLeft", (displayName: string) => {
            this.onUserLeft(displayName);
        });

        this.connection.on("ReceivePlaybackUpdate", (position: number, isPlaying: boolean) => {
            const state: PlaybackState = {
                progress: position,
                isPlaying,
                speed: 1.0,
                duration: 0
            };
            this.onReceivePlaybackState(state);
        });

        this.connection.on("Error", (message: string) => {
            this.onError(message);
        });

        this.connection.on("ParticipantJoinedNotification", (displayName: string) => {
            this.onUserJoined(displayName);
        });

        this.connection.on("ParticipantLeftNotification", (displayName: string) => {
            this.onUserLeft(displayName);
        });

        this.connection.on("ForceSyncPlayback", (position: number, isPlaying: boolean) => {
            const state: PlaybackState = {
                progress: position,
                isPlaying,
                speed: 1.0,
                duration: 0
            };
            this.onReceivePlaybackState(state);
        });

        this.connection.on("ReceiveRoomParticipants", (participants: any[]) => {
            this.onReceiveRoomParticipants(participants);
        });

        this.connection.on("ControlTransferred", (newControllerId: string, newControllerName: string) => {
            this.onControlTransferred(newControllerId, newControllerName);
        });

        this.connection.on("RoomClosed", (roomId: string, reason: string) => {
            this.onRoomClosed(roomId, reason);
        });

        this.connection.on("VideoChanged", (videoUrl: string, videoTitle: string, videoThumbnail?: string) => {
            this.onVideoChanged(videoUrl, videoTitle, videoThumbnail);
        });

        this.connection.on("UserKicked", (roomId: string, reason: string) => {
            this.onUserKicked(roomId, reason);
        });

        this.connection.on("SyncModeChanged", (syncMode: string) => {
            this.onSyncModeChanged(syncMode);
        });

        // Virtual Browser event handlers
        this.connection.on("VirtualBrowserAllocated", (virtualBrowserDto: any) => {
            const virtualBrowser: VirtualBrowser = {
                id: virtualBrowserDto.id,
                roomId: virtualBrowserDto.roomId,
                containerId: virtualBrowserDto.containerId,
                containerName: virtualBrowserDto.containerName,
                browserUrl: virtualBrowserDto.browserUrl,
                containerIndex: virtualBrowserDto.containerIndex,
                slotIndex: virtualBrowserDto.slotIndex,
                status: virtualBrowserDto.status,
                createdAt: virtualBrowserDto.createdAt,
                allocatedAt: virtualBrowserDto.allocatedAt,
                deallocatedAt: virtualBrowserDto.deallocatedAt,
                expiresAt: virtualBrowserDto.expiresAt,
                lastAccessedUrl: virtualBrowserDto.lastAccessedUrl,
                timeRemaining: virtualBrowserDto.timeRemaining
            };
            this.onVirtualBrowserAllocated(virtualBrowser);
        });

        this.connection.on("VirtualBrowserReleased", () => {
            this.onVirtualBrowserReleased();
        });

        this.connection.on("VirtualBrowserExpired", () => {
            this.onVirtualBrowserExpired();
        });

        this.connection.on("VirtualBrowserQueued", (queueDto: any) => {
            const queueStatus: VirtualBrowserQueue = {
                id: queueDto.id,
                roomId: queueDto.roomId,
                requestedAt: queueDto.requestedAt,
                position: queueDto.position,
                status: queueDto.status,
                notifiedAt: queueDto.notifiedAt,
                notificationExpiresAt: queueDto.notificationExpiresAt,
                notificationTimeRemaining: queueDto.notificationTimeRemaining
            };
            this.onVirtualBrowserQueued(queueStatus);
        });

        this.connection.on("VirtualBrowserQueueCancelled", () => {
            this.onVirtualBrowserQueueCancelled();
        });

        this.connection.on("VirtualBrowserAvailable", (queueDto: any) => {
            const queueStatus: VirtualBrowserQueue = {
                id: queueDto.id,
                roomId: queueDto.roomId,
                requestedAt: queueDto.requestedAt,
                position: queueDto.position,
                status: queueDto.status,
                notifiedAt: queueDto.notifiedAt,
                notificationExpiresAt: queueDto.notificationExpiresAt,
                notificationTimeRemaining: queueDto.notificationTimeRemaining
            };
            this.onVirtualBrowserAvailable(queueStatus);
        });

        this.connection.on("VirtualBrowserQueueNotificationExpired", () => {
            this.onVirtualBrowserQueueNotificationExpired();
        });
    }

    public getIsConnected = (): boolean => {
        return this.connection?.state === HubConnectionState.Connected;
    }

    // Invocation methods
    public async joinRoom(roomId: string, password?: string) {
        if (!this.connection) {
            throw new Error('SignalR connection is not established');
        }
        
        if (this.connection.state !== HubConnectionState.Connected) {
            throw new Error(`SignalR connection is not in Connected state. Current state: ${this.connection.state}`);
        }
        
        if (this.joiningRoom) {
            return;
        }
        
        if (this.currentRoomId === roomId) {
            return;
        }
        
        this.joiningRoom = true;
        
        try {
            await new Promise(resolve => setTimeout(resolve, 500));
            const joinPromise = new Promise<void>((resolve, reject) => {
                const errorHandler = (message: string) => {
                    this.connection?.off("Error", errorHandler);
                    reject(new Error(message));
                };
                
                const successHandler = (joinedRoomId: string) => {
                    this.connection?.off("Error", errorHandler);
                    this.connection?.off("RoomJoined", successHandler);
                    
                    this.currentRoomId = joinedRoomId;
                    resolve();
                };
                
                this.connection?.on("Error", errorHandler);
                this.connection?.on("RoomJoined", successHandler);
                
                this.connection?.invoke("JoinRoom", roomId, password);
            });
            
            const timeoutPromise = new Promise<never>((_, reject) => {
                setTimeout(() => reject(new Error('Join room timeout')), 10000);
            });
            
            await Promise.race([joinPromise, timeoutPromise]);
        } catch (error) {
            throw error;
        } finally {
            this.joiningRoom = false;
        }
    }

    public async leaveRoom(roomId: string) {
        this.currentRoomId = null;
        await this.connection?.invoke("LeaveRoom", roomId);
    }

    public async sendMessage(roomId: string, message: string) {
        await this.connection?.invoke("SendMessage", roomId, message);
    }

    public async playVideo(roomId: string) {
        await this.connection?.invoke("PlayVideo", roomId);
    }

    public async pauseVideo(roomId: string) {
        await this.connection?.invoke("PauseVideo", roomId);
    }

    public async seekVideo(roomId: string, time: number) {
        await this.connection?.invoke("SeekVideo", roomId, time);
    }

    public async transferControl(roomId: string, newControllerId: string) {
        await this.connection?.invoke("TransferControl", roomId, newControllerId);
    }

    public async kickUser(roomId: string, userIdToKick: string) {
        await this.connection?.invoke("KickUser", roomId, userIdToKick);
    }

    public async changeVideo(roomId: string, videoUrl: string, videoTitle: string, videoThumbnail?: string) {
        await this.connection?.invoke("ChangeVideo", roomId, videoUrl, videoTitle, videoThumbnail);
    }

    public async updateSyncMode(roomId: string, syncMode: string) {
        await this.connection?.invoke("UpdateSyncMode", roomId, syncMode);
    }
}

const signalRService = new SignalRService();
export default signalRService;
