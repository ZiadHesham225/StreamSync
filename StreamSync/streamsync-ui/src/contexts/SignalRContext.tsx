import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import signalRService from '../services/signalRService';
import { useAuth } from './AuthContext';
import { HubConnection } from '@microsoft/signalr';

interface SignalRContextType {
    connection: HubConnection | null;
    isConnected: boolean;
    isConnecting: boolean;
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
        let isMounted = true;

        const handleConnectionStateChange = (connected: boolean, connecting: boolean) => {
            if (isMounted) {
                setIsConnected(connected);
                setIsConnecting(connecting);
            }
        };

        signalRService.onConnectionStateChange = handleConnectionStateChange;

        const establishConnection = async () => {
            await signalRService.disconnect();
            
            await new Promise(resolve => setTimeout(resolve, 200));
            
            if (isMounted && isAuthenticated && token) {
                try {
                    await signalRService.connect(token);
                } catch (error) {
                    }
            }
        };

        establishConnection();

        return () => {
            isMounted = false;
        };
    }, [token, isAuthenticated]);

    useEffect(() => {
        return () => {
            signalRService.disconnect();
            signalRService.onConnectionStateChange = () => {};
        };
    }, []);

    const value = {
        connection: signalRService.getConnection(),
        isConnected,
        isConnecting,
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
