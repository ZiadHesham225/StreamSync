import React, { useState } from 'react';
import { LogIn } from 'lucide-react';
import Button from '../common/Button';
import Input from '../common/Input';
import { roomService } from '../../services/roomService';
import signalRService from '../../services/signalRService';
import { useAuth } from '../../contexts/AuthContext';
import toast from 'react-hot-toast';
import { useNavigate } from 'react-router-dom';

interface JoinRoomFormProps {
  onClose: () => void;
}

const JoinRoomForm: React.FC<JoinRoomFormProps> = ({ onClose }) => {
  const [formData, setFormData] = useState({
    inviteCode: '',
    password: ''
  });
  const [isLoading, setIsLoading] = useState(false);
  const [requiresPassword, setRequiresPassword] = useState(false);
  const navigate = useNavigate();
  const { user } = useAuth();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);

    try {
      const roomInfo = await roomService.getRoomByInviteCode(formData.inviteCode);
      
      const isRoomCreator = user && roomInfo.data.adminId === user.id;
      
      if (roomInfo.data.isPrivate && !requiresPassword && !isRoomCreator) {
        setRequiresPassword(true);
        setIsLoading(false);
        return;
      }
      if (!user) {
        toast.error('Please log in to join a room');
        setIsLoading(false);
        return;
      }
      
      if (!signalRService.getIsConnected()) {
        const token = localStorage.getItem('token');
        await signalRService.connect(token);
      }
      
      await signalRService.joinRoom(
        roomInfo.data.id,
        formData.password || undefined
      );
      
      sessionStorage.setItem('joinedRoom', roomInfo.data.id);
      
      if (isRoomCreator && roomInfo.data.isPrivate) {
        toast.success('Joined your private room as the creator!');
      } else {
        toast.success('Joined room successfully!');
      }
      
      onClose();
      navigate(`/room/${roomInfo.data.id}`);
      
    } catch (error: any) {
      const errorMessage = error.message || '';
      
      if (errorMessage.includes('Invalid password') || errorMessage.includes('Incorrect password')) {
        toast.error('Incorrect password. Please try again.');
        setRequiresPassword(true);
      } else if (errorMessage.includes('password') && errorMessage.includes('required')) {
        setRequiresPassword(true);
        toast.error('This is a private room. Please enter the password.');
      } else if (errorMessage.includes('timeout') || errorMessage.includes('Join room timeout')) {
        toast.success('Joining room, please wait...');
        onClose();
        navigate(`/room/${formData.inviteCode}`);
      } else {
        // Generic error
        toast.error(`Failed to join room: ${errorMessage}`);
      }
    } finally {
      setIsLoading(false);
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: value
    }));
  };

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4 z-50">
      <div className="bg-white/90 dark:bg-gray-900/90 backdrop-blur-xl rounded-2xl shadow-2xl border border-gray-200/50 dark:border-gray-700/50 max-w-md w-full relative overflow-hidden">
        {/* Background decoration */}
        <div className="absolute top-0 right-0 w-32 h-32 bg-gradient-to-br from-blue-500/10 to-purple-500/10 rounded-full blur-2xl"></div>
        <div className="absolute bottom-0 left-0 w-24 h-24 bg-gradient-to-tr from-purple-500/10 to-blue-500/10 rounded-full blur-xl"></div>
        
        <div className="p-8 relative z-10">
          <div className="flex items-center justify-between mb-6">
            <h2 className="text-2xl font-bold">
              <span className="bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
                Join Room
              </span>
            </h2>
            <button
              onClick={onClose}
              className="text-gray-400 hover:text-gray-600 dark:text-gray-300 dark:hover:text-gray-100 text-2xl w-8 h-8 flex items-center justify-center rounded-full hover:bg-gray-100/50 dark:hover:bg-gray-800/50 transition-all duration-200"
            >
              ×
            </button>
          </div>

          <form onSubmit={handleSubmit} className="space-y-5">
            <Input
              label="Invite Code"
              type="text"
              name="inviteCode"
              value={formData.inviteCode}
              onChange={handleChange}
              required
              placeholder="Enter room invite code"
            />

            {requiresPassword && (
              <Input
                label="Room Password"
                type="password"
                name="password"
                value={formData.password}
                onChange={handleChange}
                required
                placeholder="Enter room password"
              />
            )}

            <div className="flex space-x-3 pt-4">
              <Button
                type="button"
                variant="outline"
                onClick={onClose}
                className="flex-1"
              >
                Cancel
              </Button>
              <button
                type="submit"
                className="flex-1 bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 disabled:from-blue-300 disabled:to-purple-300 text-white font-semibold px-4 py-3 rounded-xl shadow-lg hover:shadow-xl transition-all duration-200 transform hover:-translate-y-0.5 disabled:transform-none inline-flex items-center justify-center"
                disabled={isLoading}
              >
                <LogIn className="w-4 h-4 mr-2" />
                {isLoading ? (
                  <div className="flex items-center space-x-2">
                    <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin"></div>
                    <span>Joining...</span>
                  </div>
                ) : (
                  'Join Room'
                )}
              </button>
            </div>
          </form>

          <div className="mt-6 pt-4 border-t border-gray-200/50 dark:border-gray-700/50">
            <p className="text-sm text-gray-600 dark:text-gray-300 text-center">
              Don't have an invite code? Browse public rooms below or ask a friend to share their room code.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default JoinRoomForm;
