import React from 'react';
import { Users, Crown, RotateCcw, Diamond, UserX } from 'lucide-react';
import { RoomParticipant } from '../../types/index';

interface ParticipantsListProps {
    participants: RoomParticipant[];
    currentUserId?: string;
    roomAdminId?: string;
    onTransferControl?: (participantId: string) => void;
    onKickUser?: (participantId: string) => void;
    currentUserHasControl?: boolean;
}

const ParticipantsList: React.FC<ParticipantsListProps> = ({ 
    participants, 
    currentUserId,
    roomAdminId,
    onTransferControl,
    onKickUser,
    currentUserHasControl
}) => {
    const isCurrentUserAdmin = currentUserId === roomAdminId;
    const canTransferControl = isCurrentUserAdmin || currentUserHasControl;
    return (
        <div className="h-full flex flex-col">
            <div className="flex-1 overflow-y-auto max-h-96 custom-scrollbar">
                {participants.length === 0 ? (
                    <div className="text-center py-8">
                        <Users className="w-12 h-12 text-gray-300 dark:text-gray-600 mx-auto mb-3 drop-shadow-lg filter brightness-110" />
                        <p className="text-gray-500 dark:text-gray-400 font-medium">No participants yet</p>
                        <p className="text-sm text-gray-400 dark:text-gray-500">Waiting for others to join...</p>
                    </div>
                ) : (
                    <div className="space-y-3">
                        {participants
                            .sort((a, b) => {
                                if (a.hasControl !== b.hasControl) return a.hasControl ? -1 : 1;
                                if (a.isAdmin !== b.isAdmin) return a.isAdmin ? -1 : 1;
                                return a.displayName.localeCompare(b.displayName);
                            })
                            .map((participant) => (
                            <div
                                key={participant.id}
                                className={`flex items-center space-x-3 p-3 rounded-xl transition-all duration-200 hover:shadow-lg backdrop-blur-sm border ${
                                    participant.id === currentUserId 
                                        ? 'bg-gradient-to-r from-blue-500/20 to-purple-500/20 border-blue-300/50 dark:border-blue-600/50 shadow-lg' 
                                        : participant.hasControl
                                            ? 'bg-gradient-to-r from-green-500/20 to-emerald-500/20 border-green-300/50 dark:border-green-600/50 shadow-md'
                                            : participant.isAdmin
                                                ? 'bg-gradient-to-r from-yellow-500/20 to-orange-500/20 border-yellow-300/50 dark:border-yellow-600/50 shadow-md'
                                                : 'bg-white/50 dark:bg-gray-800/50 border-gray-200/50 dark:border-gray-700/50 hover:bg-white/70 dark:hover:bg-gray-800/70'
                                }`}
                            >
                                {/* Avatar */}
                                <div className="flex-shrink-0 relative">
                                    {participant.avatarUrl ? (
                                        <img
                                            src={participant.avatarUrl}
                                            alt={participant.displayName}
                                            className={`w-10 h-10 rounded-full object-cover ${
                                                participant.hasControl ? 'ring-2 ring-green-400 ring-offset-2' : ''
                                            }`}
                                        />
                                    ) : (
                                        <div className={`w-10 h-10 bg-gradient-to-br ${
                                            participant.hasControl 
                                                ? 'from-green-400 to-green-600' 
                                                : participant.isAdmin 
                                                    ? 'from-yellow-400 to-yellow-600'
                                                    : 'from-gray-400 to-gray-600'
                                        } rounded-full flex items-center justify-center ring-2 ${
                                            participant.hasControl ? 'ring-green-400' : 'ring-transparent'
                                        } ring-offset-2`}>
                                            <span className="text-sm font-bold text-white">
                                                {participant.displayName.charAt(0).toUpperCase()}
                                            </span>
                                        </div>
                                    )}
                                    {participant.hasControl && (
                                        <div className="absolute -top-1 -right-1">
                                            <Diamond className="w-5 h-5 text-green-500 bg-white dark:bg-gray-800 rounded-full p-0.5 drop-shadow-lg filter brightness-125 glow" />
                                        </div>
                                    )}
                                    {participant.isAdmin && !participant.hasControl && (
                                        <div className="absolute -top-1 -right-1">
                                            <Crown className="w-5 h-5 text-yellow-500 bg-white dark:bg-gray-800 rounded-full p-0.5 drop-shadow-lg filter brightness-125 glow" />
                                        </div>
                                    )}
                                </div>
                                <div className="flex-1 min-w-0">
                                    <div className="flex items-center space-x-2">
                                        <span className={`text-sm font-medium truncate ${
                                            participant.hasControl 
                                                ? 'text-green-700 dark:text-green-400 font-bold' 
                                                : participant.isAdmin 
                                                    ? 'text-yellow-700 dark:text-yellow-400 font-semibold' 
                                                    : 'text-gray-900 dark:text-gray-100'
                                        }`}>
                                            {participant.displayName}
                                            {participant.id === currentUserId && (
                                                <span className="text-xs text-blue-600 dark:text-blue-400 ml-1 font-normal">(You)</span>
                                            )}
                                        </span>
                                    </div>
                                    
                                    <div className="text-xs">
                                        {participant.hasControl && (
                                            <span className="text-green-600 dark:text-green-400 font-medium">
                                                🎮 Currently controlling
                                            </span>
                                        )}
                                        {participant.isAdmin && !participant.hasControl && (
                                            <span className="text-yellow-600 dark:text-yellow-400 font-medium">
                                                👑 Room admin
                                            </span>
                                        )}
                                        {!participant.hasControl && !participant.isAdmin && (
                                            <span className="text-gray-500 dark:text-gray-400">
                                                👁️ Viewer
                                            </span>
                                        )}
                                    </div>
                                </div>
                                <div className="flex space-x-1">
                                    {canTransferControl && 
                                     onTransferControl && 
                                     !participant.hasControl && 
                                     participant.id !== currentUserId && (
                                        <button
                                            onClick={() => onTransferControl(participant.id)}
                                            className="flex-shrink-0 p-2 text-gray-400 hover:text-green-500 hover:bg-green-100/50 dark:hover:bg-green-900/30 rounded-full transition-all duration-200 transform hover:scale-110 active:scale-95 backdrop-blur-sm"
                                            title="Transfer control to this participant"
                                        >
                                            <RotateCcw className="w-4 h-4 drop-shadow-sm filter brightness-110" />
                                        </button>
                                    )}
                                    {isCurrentUserAdmin && 
                                     onKickUser && 
                                     participant.id !== currentUserId && 
                                     participant.id !== roomAdminId && (
                                        <button
                                            onClick={() => onKickUser(participant.id)}
                                            className="flex-shrink-0 p-2 text-gray-400 hover:text-red-500 hover:bg-red-100/50 dark:hover:bg-red-900/30 rounded-full transition-all duration-200 transform hover:scale-110 active:scale-95 backdrop-blur-sm"
                                            title="Kick this participant from the room"
                                        >
                                            <UserX className="w-4 h-4 drop-shadow-sm filter brightness-110" />
                                        </button>
                                    )}
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
};

export default ParticipantsList;
