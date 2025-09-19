import React, { useState, useEffect, useRef } from 'react';
import { Send } from 'lucide-react';
import { ChatMessage } from '../../types/index';
import { useAuth } from '../../contexts/AuthContext';
import Button from '../common/Button';
import Input from '../common/Input';

interface ChatPanelProps {
    messages: ChatMessage[];
    onSendMessage: (message: string) => void;
    isConnected: boolean;
}

const ChatPanel: React.FC<ChatPanelProps> = ({ messages, onSendMessage, isConnected }) => {
    const [newMessage, setNewMessage] = useState('');
    const { user } = useAuth();
    const messagesEndRef = useRef<HTMLDivElement>(null);
    const messagesContainerRef = useRef<HTMLDivElement>(null);

    const scrollToBottom = () => {
        if (messagesContainerRef.current) {
            messagesContainerRef.current.scrollTop = messagesContainerRef.current.scrollHeight;
        }
    };

    useEffect(() => {
        scrollToBottom();
    }, [messages]);

    const handleSendMessage = (e: React.FormEvent) => {
        e.preventDefault();
        if (!newMessage.trim() || !isConnected) return;
        onSendMessage(newMessage.trim());
        setNewMessage('');
    };

    const formatTimestamp = (timestamp: string | Date | undefined) => {
        if (!timestamp) {
            return new Date().toLocaleTimeString('en-US', {
                hour: '2-digit',
                minute: '2-digit'
            });
        }
        const date = typeof timestamp === 'string' ? new Date(timestamp) : timestamp;
        if (isNaN(date.getTime())) {
            return 'Invalid Date';
        }
        return date.toLocaleTimeString('en-US', {
            hour: '2-digit',
            minute: '2-digit'
        });
    };

    return (
    <div className="flex-1 min-h-0 flex flex-col overflow-hidden">
            {/* Messages */}
            <div ref={messagesContainerRef} className="flex-1 overflow-y-auto p-4 space-y-3 chat-panel custom-scrollbar">
                {messages.length === 0 ? (
                    <div className="text-center text-gray-400 dark:text-gray-500 py-8">
                        <div className="text-2xl mb-2 drop-shadow-lg filter brightness-110">💬</div>
                        <p className="text-sm">No messages yet.</p>
                        <p className="text-xs text-gray-500 dark:text-gray-600 mt-1">Be the first to say something!</p>
                    </div>
                ) : (
                    messages
                        .filter(message => message && message.id && message.content)
                        .map((message) => {
                            const isCurrentUser = user?.id === message.senderId;
                            const isSystemMessage = message.senderId === 'system';
                            
                            if (isSystemMessage) {
                                return (
                                    <div key={message.id} className="flex justify-center fade-in">
                                        <div className="bg-gradient-to-r from-blue-500/20 to-purple-500/20 backdrop-blur-sm px-4 py-2 rounded-full text-xs text-gray-700 dark:text-gray-300 font-medium border border-blue-200/50 dark:border-blue-600/50 shadow-lg">
                                            <span className="text-blue-600 dark:text-blue-400 mr-1 drop-shadow-sm filter brightness-110">✨</span>
                                            {message.content}
                                        </div>
                                    </div>
                                );
                            }
                            
                            return (
                                <div key={message.id} className={`flex chat-message ${isCurrentUser ? 'justify-end' : 'justify-start'}`}>
                                    <div className={`flex max-w-[70%] ${isCurrentUser ? 'flex-row-reverse' : 'flex-row'} space-x-2 ${isCurrentUser ? 'space-x-reverse' : ''}`}>
                                        {/* Avatar - only show for other users */}
                                        {!isCurrentUser && (
                                            <div className="flex-shrink-0">
                                                {message.avatarUrl ? (
                                                    <img className="w-8 h-8 rounded-full object-cover ring-2 ring-white/50 dark:ring-gray-700/50 shadow-lg" src={message.avatarUrl} alt={message.senderName || 'User'} />
                                                ) : (
                                                    <div className="w-8 h-8 rounded-full bg-gradient-to-br from-blue-400 to-blue-600 flex items-center justify-center text-white font-bold text-xs ring-2 ring-white/50 dark:ring-gray-700/50 shadow-lg">
                                                        {((message.senderName && message.senderName !== 'Unknown User') ? message.senderName : 'U').charAt(0).toUpperCase()}
                                                    </div>
                                                )}
                                            </div>
                                        )}
                                        
                                        {/* Message bubble */}
                                        <div className={`p-3 rounded-lg backdrop-blur-sm shadow-lg border ${
                                            isCurrentUser 
                                                ? 'bg-gradient-to-r from-blue-500 to-purple-500 text-white rounded-br-sm border-blue-400/50' 
                                                : 'bg-white/80 dark:bg-gray-800/80 text-gray-800 dark:text-gray-200 rounded-bl-sm border-gray-200/50 dark:border-gray-700/50'
                                        }`}>
                                            {/* Sender name - only for other users */}
                                            {!isCurrentUser && (
                                                <div className="mb-1">
                                                    <p className="text-xs font-semibold text-blue-600 dark:text-blue-400">
                                                        {message.senderName && message.senderName !== 'Unknown User' ? message.senderName : 'Anonymous User'}
                                                    </p>
                                                </div>
                                            )}
                                            
                                            <p className="text-sm">{message.content}</p>
                                            
                                            <div className={`text-xs mt-1 ${
                                                isCurrentUser ? 'text-blue-100' : 'text-gray-500 dark:text-gray-400'
                                            }`}>
                                                {formatTimestamp(message.sentAt)}
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            );
                        })
                )}
                <div ref={messagesEndRef} />
            </div>

            {/* Message Input */}
            <div className="p-4 border-t border-gray-200/50 dark:border-gray-700/50 bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm flex-shrink-0">
                <form onSubmit={handleSendMessage} className="flex space-x-3">
                    <input
                        type="text"
                        placeholder="Type your message..."
                        value={newMessage}
                        onChange={(e) => setNewMessage(e.target.value)}
                        className="flex-1 px-4 py-3 bg-white/80 dark:bg-gray-800/80 backdrop-blur-sm border border-gray-200/50 dark:border-gray-700/50 rounded-xl focus:border-blue-500/50 focus:ring-2 focus:ring-blue-500/20 outline-none text-sm text-gray-900 dark:text-white placeholder-gray-500 dark:placeholder-gray-400 transition-all duration-200"
                        disabled={!isConnected}
                    />
                    <button 
                        type="submit" 
                        disabled={!isConnected || !newMessage.trim()} 
                        className="px-4 py-3 bg-gradient-to-r from-blue-500 to-purple-500 hover:from-blue-600 hover:to-purple-600 disabled:from-gray-300 disabled:to-gray-400 text-white rounded-xl transition-all duration-200 flex items-center justify-center shadow-lg hover:shadow-xl transform hover:-translate-y-0.5 disabled:transform-none"
                    >
                        <Send className="h-4 w-4 drop-shadow-sm filter brightness-110" />
                    </button>
                </form>
                {!isConnected && (
                    <p className="text-xs text-red-500 dark:text-red-400 mt-2 flex items-center">
                        <span className="w-2 h-2 bg-red-500 rounded-full mr-2 animate-pulse"></span>
                        Connecting to chat...
                    </p>
                )}
            </div>
        </div>
    );
};

export default ChatPanel;
