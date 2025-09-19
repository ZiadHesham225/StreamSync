import React, { useState } from 'react';
import { X, Settings } from 'lucide-react';
import Button from '../common/Button';

interface RoomSettingsModalProps {
    isOpen: boolean;
    onClose: () => void;
    currentSyncMode: string;
    onSyncModeChange: (newSyncMode: 'strict' | 'relaxed') => Promise<void>;
    roomName: string;
}

const RoomSettingsModal: React.FC<RoomSettingsModalProps> = ({
    isOpen,
    onClose,
    currentSyncMode,
    onSyncModeChange,
    roomName
}) => {
    const [selectedSyncMode, setSelectedSyncMode] = useState<'strict' | 'relaxed'>(
        currentSyncMode as 'strict' | 'relaxed'
    );
    const [isLoading, setIsLoading] = useState(false);

    if (!isOpen) return null;

    const handleSave = async () => {
        if (selectedSyncMode === currentSyncMode) {
            onClose();
            return;
        }

        setIsLoading(true);
        try {
            await onSyncModeChange(selectedSyncMode);
            onClose();
        } catch (error) {
            } finally {
            setIsLoading(false);
        }
    };

    const getSyncModeDescription = (mode: string) => {
        switch (mode) {
            case 'strict':
                return 'Only the room controller can control playback. All participants see synchronized video.';
            case 'relaxed':
                return 'Everyone can control their own playback independently. Video changes still require controller permission.';
            default:
                return '';
        }
    };

    return (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4 z-50">
            <div className="bg-white/90 dark:bg-gray-900/90 backdrop-blur-xl rounded-2xl shadow-2xl border border-gray-200/50 dark:border-gray-700/50 max-w-md w-full relative overflow-hidden">
                {/* Background decoration */}
                <div className="absolute top-0 right-0 w-32 h-32 bg-gradient-to-br from-blue-500/10 to-purple-500/10 rounded-full blur-2xl"></div>
                <div className="absolute bottom-0 left-0 w-24 h-24 bg-gradient-to-tr from-purple-500/10 to-blue-500/10 rounded-full blur-xl"></div>
                
                <div className="flex justify-between items-center p-6 border-b border-gray-200/50 dark:border-gray-700/50 relative z-10">
                    <div className="flex items-center space-x-2">
                        <Settings className="w-5 h-5 text-blue-600 dark:text-blue-400 drop-shadow-lg filter brightness-110" />
                        <h2 className="text-xl font-semibold">
                            <span className="bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
                                Room Settings
                            </span>
                        </h2>
                    </div>
                    <button
                        onClick={onClose}
                        className="text-gray-400 hover:text-gray-600 dark:text-gray-300 dark:hover:text-gray-100 transition-all duration-200 w-8 h-8 flex items-center justify-center rounded-full hover:bg-gray-100/50 dark:hover:bg-gray-800/50"
                    >
                        <X className="w-6 h-6" />
                    </button>
                </div>

                <div className="p-6 relative z-10">
                    <div className="mb-4">
                        <h3 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Room Name</h3>
                        <p className="text-gray-600 dark:text-gray-400 font-semibold">{roomName}</p>
                    </div>

                    <div className="mb-6">
                        <label className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3 block">
                            Synchronization Mode
                        </label>
                        
                        <div className="space-y-3">
                            <label className="flex items-start space-x-3 cursor-pointer p-4 rounded-xl bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border border-gray-200/50 dark:border-gray-700/50 hover:bg-white/70 dark:hover:bg-gray-800/70 transition-all duration-200 shadow-lg hover:shadow-xl">
                                <div className="relative flex-shrink-0 mt-1">
                                  <input
                                      type="radio"
                                      name="syncMode"
                                      value="strict"
                                      checked={selectedSyncMode === 'strict'}
                                      onChange={(e) => setSelectedSyncMode(e.target.value as 'strict' | 'relaxed')}
                                      className="peer sr-only"
                                  />
                                  <div className="w-5 h-5 bg-white/80 dark:bg-gray-700/80 border-2 border-gray-200/50 dark:border-gray-600/50 rounded-full peer-checked:bg-gradient-to-r peer-checked:from-blue-600 peer-checked:to-purple-600 peer-checked:border-blue-500 peer-focus:ring-2 peer-focus:ring-blue-500/50 transition-all duration-200 flex items-center justify-center">
                                    {selectedSyncMode === 'strict' && (
                                      <div className="w-2 h-2 bg-white rounded-full"></div>
                                    )}
                                  </div>
                                </div>
                                <div>
                                    <div className="font-semibold text-gray-900 dark:text-white">Strict Mode</div>
                                    <div className="text-sm text-gray-600 dark:text-gray-400">
                                        Force sync all participants
                                    </div>
                                </div>
                            </label>

                            <label className="flex items-start space-x-3 cursor-pointer p-4 rounded-xl bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border border-gray-200/50 dark:border-gray-700/50 hover:bg-white/70 dark:hover:bg-gray-800/70 transition-all duration-200 shadow-lg hover:shadow-xl">
                                <div className="relative flex-shrink-0 mt-1">
                                  <input
                                      type="radio"
                                      name="syncMode"
                                      value="relaxed"
                                      checked={selectedSyncMode === 'relaxed'}
                                      onChange={(e) => setSelectedSyncMode(e.target.value as 'strict' | 'relaxed')}
                                      className="peer sr-only"
                                  />
                                  <div className="w-5 h-5 bg-white/80 dark:bg-gray-700/80 border-2 border-gray-200/50 dark:border-gray-600/50 rounded-full peer-checked:bg-gradient-to-r peer-checked:from-blue-600 peer-checked:to-purple-600 peer-checked:border-blue-500 peer-focus:ring-2 peer-focus:ring-blue-500/50 transition-all duration-200 flex items-center justify-center">
                                    {selectedSyncMode === 'relaxed' && (
                                      <div className="w-2 h-2 bg-white rounded-full"></div>
                                    )}
                                  </div>
                                </div>
                                <div>
                                    <div className="font-semibold text-gray-900 dark:text-white">Manual Mode</div>
                                    <div className="text-sm text-gray-600 dark:text-gray-400">
                                        Users control their own playback
                                    </div>
                                </div>
                            </label>
                        </div>

                        <div className="mt-4 p-4 bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm rounded-xl border border-gray-200/50 dark:border-gray-700/50">
                            <p className="text-sm text-blue-800 dark:text-blue-300">
                                {getSyncModeDescription(selectedSyncMode)}
                            </p>
                        </div>
                    </div>
                </div>

                <div className="flex justify-end space-x-3 p-6 border-t border-gray-200/50 dark:border-gray-700/50 bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm relative z-10">
                    <Button
                        variant="outline"
                        onClick={onClose}
                        disabled={isLoading}
                    >
                        Cancel
                    </Button>
                    <Button
                        onClick={handleSave}
                        disabled={isLoading}
                        className="min-w-[80px]"
                    >
                        {isLoading ? (
                            <div className="flex items-center space-x-2">
                                <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></div>
                                <span>Saving...</span>
                            </div>
                        ) : (
                            'Save'
                        )}
                    </Button>
                </div>
            </div>
        </div>
    );
};

export default RoomSettingsModal;