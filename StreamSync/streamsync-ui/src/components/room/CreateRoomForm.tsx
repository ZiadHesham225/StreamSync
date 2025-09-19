import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Video, Lock } from 'lucide-react';
import { roomService } from '../../services/roomService';
import { RoomCreateRequest } from '../../types/index';
import Input from '../common/Input';
import Button from '../common/Button';
import toast from 'react-hot-toast';

interface CreateRoomFormProps {
  onClose?: () => void;
}

const CreateRoomForm: React.FC<CreateRoomFormProps> = ({ onClose }) => {
  const [formData, setFormData] = useState<RoomCreateRequest>({
    name: '',
    videoUrl: '',
    isPrivate: false,
    password: '',
    autoPlay: true,
    syncMode: 'strict'
  });
  const [isLoading, setIsLoading] = useState(false);
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    try {
      const response = await roomService.createRoom(formData);
      toast.success('Room created successfully!');
      
      const roomId = response.Id || response.id;
      if (roomId) {
        if (formData.isPrivate && formData.password) {
          const passwordData = {
            password: formData.password,
            timestamp: Date.now(),
            isCreator: true
          };
          sessionStorage.setItem(`room_password_${roomId}`, JSON.stringify(passwordData));
        }
        navigate(`/room/${roomId}`);
      } else {
        toast.error('Room created but could not navigate to it');
      }
    } catch (error: any) {
      toast.error(error.message || 'Failed to create room');
    } finally {
      setIsLoading(false);
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value, type } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: type === 'checkbox' ? (e.target as HTMLInputElement).checked : value
    }));
  };

  const isFormValid = formData.name;

  const formContent = (
    <div className="bg-white/80 dark:bg-gray-900/80 backdrop-blur-xl rounded-2xl shadow-2xl border border-gray-200/50 dark:border-gray-700/50 p-8 relative overflow-hidden">
      {/* Background decoration */}
      <div className="absolute top-0 right-0 w-32 h-32 bg-gradient-to-br from-blue-500/10 to-purple-500/10 rounded-full blur-2xl"></div>
      <div className="absolute bottom-0 left-0 w-24 h-24 bg-gradient-to-tr from-purple-500/10 to-blue-500/10 rounded-full blur-2xl"></div>
      
      <form onSubmit={handleSubmit} className="space-y-6 relative">
        <Input
          label="Room Name"
          name="name"
          value={formData.name}
          onChange={handleChange}
          placeholder="Give your room a catchy name"
          required
          className="bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border-gray-200/50 dark:border-gray-700/50 focus:border-blue-500 focus:ring-blue-500/20"
        />
        
        <Input
          label="Video URL (Optional)"
          type="url"
          name="videoUrl"
          value={formData.videoUrl}
          onChange={handleChange}
          icon={Video}
          placeholder="https://youtube.com/watch?v=... or direct video URL (leave empty to choose later)"
          className="bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border-gray-200/50 dark:border-gray-700/50 focus:border-blue-500 focus:ring-blue-500/20"
        />
        
        {/* Room Settings */}
        <div className="space-y-4 p-4 bg-gray-50/50 dark:bg-gray-800/30 rounded-xl border border-gray-200/30 dark:border-gray-700/30">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-3">Room Settings</h3>
          
          <div className="flex items-center space-x-3">
            <div className="relative">
              <input
                type="checkbox"
                id="isPrivate"
                name="isPrivate"
                checked={formData.isPrivate}
                onChange={handleChange}
                className="peer sr-only"
              />
              <div className="w-5 h-5 bg-white/80 dark:bg-gray-800/80 backdrop-blur-sm border-2 border-gray-200/50 dark:border-gray-700/50 rounded-md peer-checked:bg-gradient-to-r peer-checked:from-blue-600 peer-checked:to-purple-600 peer-checked:border-blue-500 peer-focus:ring-2 peer-focus:ring-blue-500/50 transition-all duration-200 cursor-pointer flex items-center justify-center">
                {formData.isPrivate && (
                  <svg className="w-3 h-3 text-white" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                  </svg>
                )}
              </div>
            </div>
            <label htmlFor="isPrivate" className="text-sm font-medium text-gray-900 dark:text-gray-300 flex items-center cursor-pointer">
              <Lock className="w-4 h-4 mr-2 text-gray-500" />
              Private Room
            </label>
          </div>
          
          {formData.isPrivate && (
            <Input
              label="Password"
              type="password"
              name="password"
              value={formData.password}
              onChange={handleChange}
              icon={Lock}
              required
              className="bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border-gray-200/50 dark:border-gray-700/50 focus:border-blue-500 focus:ring-blue-500/20"
            />
          )}
          
          <div className="flex items-center space-x-3">
            <div className="relative">
              <input
                type="checkbox"
                id="autoPlay"
                name="autoPlay"
                checked={formData.autoPlay}
                onChange={handleChange}
                className="peer sr-only"
              />
              <div className="w-5 h-5 bg-white/80 dark:bg-gray-800/80 backdrop-blur-sm border-2 border-gray-200/50 dark:border-gray-700/50 rounded-md peer-checked:bg-gradient-to-r peer-checked:from-blue-600 peer-checked:to-purple-600 peer-checked:border-blue-500 peer-focus:ring-2 peer-focus:ring-blue-500/50 transition-all duration-200 cursor-pointer flex items-center justify-center">
                {formData.autoPlay && (
                  <svg className="w-3 h-3 text-white" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                  </svg>
                )}
              </div>
            </div>
            <label htmlFor="autoPlay" className="text-sm font-medium text-gray-900 dark:text-gray-300 flex items-center cursor-pointer">
              <Video className="w-4 h-4 mr-2 text-gray-500" />
              Auto-play video on join
            </label>
          </div>
          
          <div className="space-y-2">
            <label htmlFor="syncMode" className="text-sm font-medium text-gray-900 dark:text-gray-300 block">
              Sync Mode
            </label>
            <select
              id="syncMode"
              name="syncMode"
              value={formData.syncMode}
              onChange={handleChange}
              className="w-full bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm border border-gray-200/50 dark:border-gray-700/50 rounded-lg px-3 py-2 text-gray-900 dark:text-white focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 transition-colors duration-200"
            >
              <option value="strict">Strict - Force sync all participants</option>
              <option value="relaxed">Manual - Users control their own playback</option>
            </select>
          </div>
        </div>
        
        {/* Action Buttons */}
        <div className="flex justify-end space-x-4 pt-6 border-t border-gray-200/30 dark:border-gray-700/30">
          <button
            type="button"
            onClick={() => onClose ? onClose() : navigate('/')}
            className="px-6 py-3 bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-700 rounded-lg transition-all duration-200 font-medium"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={!isFormValid || isLoading}
            className="px-8 py-3 bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 text-white rounded-lg font-semibold shadow-lg hover:shadow-xl transition-all duration-200 transform hover:-translate-y-0.5 disabled:opacity-50 disabled:cursor-not-allowed disabled:transform-none disabled:hover:shadow-lg"
          >
            {isLoading ? (
              <div className="flex items-center space-x-2">
                <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></div>
                <span>Creating...</span>
              </div>
            ) : (
              'Create Room'
            )}
          </button>
        </div>
      </form>
    </div>
  );

  return onClose ? (
    <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center p-4 z-50">
      <div className="w-full max-w-2xl max-h-[90vh] overflow-y-auto">
        {formContent}
      </div>
    </div>
  ) : (
    <div className="max-w-2xl mx-auto">
      {formContent}
    </div>
  );
};

export default CreateRoomForm;