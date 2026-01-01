import React, { createContext, useContext, useState, useEffect, useCallback, ReactNode } from 'react';
import signalRService from '../services/signalRService';
import { useAuth } from './AuthContext';
import { HubConnection } from '@microsoft/signalr';

interface SignalRContextType {
    connection: HubConnection | null;
    isConnected: boolean;
    isConnecting: boolean;
    connectAndJoinRoom: (roomId: string, password?: string) => Promise<void>;
    disconnectFromRoom: (roomId: string) => Promise<void>;
}

const SignalRContext = createContext<SignalRContextType | undefined>(undefined);

interface SignalRProviderProps {
    children: ReactNode;
}

export const SignalRProvider: React.FC<SignalRProviderProps> = ({ children }) => {
    const { token, isAuthenticated } = useAuth();
    const [isConnected, setIsConnected] = useState(false);
    const [isConnecting, setIsConnecting] = useState(false);

    useEffect(() => {
        const handleConnectionStateChange = (connected: boolean, connecting: boolean) => {
            setIsConnected(connected);
            setIsConnecting(connecting);
        };

        signalRService.onConnectionStateChange = handleConnectionStateChange;

        return () => {
            signalRService.onConnectionStateChange = () => {};
        };
    }, []);

    // Disconnect when user logs out
    useEffect(() => {
        if (!isAuthenticated) {
            signalRService.disconnect();
        }
    }, [isAuthenticated]);

    // Update token when it changes (for reconnection purposes)
    useEffect(() => {
        if (token && isConnected) {
            signalRService.updateAccessToken(token);
        }
    }, [token, isConnected]);

    // Cleanup on unmount
    useEffect(() => {
        return () => {
            signalRService.disconnect();
        };
    }, []);

    const connectAndJoinRoom = useCallback(async (roomId: string, password?: string) => {
        if (!token) {
            throw new Error('No authentication token available');
        }

        // If already connected, just join the room
        if (signalRService.getIsConnected()) {
            await signalRService.joinRoom(roomId, password);
            return;
        }

        // Connect first, then join
        await signalRService.connect(token);
        
        // Wait a bit for connection to stabilize
        await new Promise(resolve => setTimeout(resolve, 100));
        
        // Verify connection is established
        if (!signalRService.getIsConnected()) {
            throw new Error('Failed to establish SignalR connection');
        }

        // Now join the room
        await signalRService.joinRoom(roomId, password);
    }, [token]);

    const disconnectFromRoom = useCallback(async (roomId: string) => {
        try {
            if (signalRService.getIsConnected()) {
                await signalRService.leaveRoom(roomId);
            }
        } catch (error) {
            console.error('Error leaving room:', error);
        }
        
        // Disconnect the SignalR connection completely
        await signalRService.disconnect();
    }, []);

    const value = {
        connection: signalRService.getConnection(),
        isConnected,
        isConnecting,
        connectAndJoinRoom,
        disconnectFromRoom,
    };

    return (
        <SignalRContext.Provider value={value}>
            {children}
        </SignalRContext.Provider>
    );
};

export const useSignalR = (): SignalRContextType => {
    const context = useContext(SignalRContext);
    if (context === undefined) {
        throw new Error('useSignalR must be used within a SignalRProvider');
    }
    return context;
};
