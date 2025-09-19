import React, { useState } from 'react';
import { Plus, LogIn } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import Header from '../components/common/Header';
import RoomList from '../components/room/RoomList';
import JoinRoomForm from '../components/room/JoinRoomForm';
import Button from '../components/common/Button';

const HomePage: React.FC = () => {
  const [showJoinForm, setShowJoinForm] = useState(false);
  const navigate = useNavigate();

  const handleCreateRoom = () => {
    navigate('/create-room');
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50 dark:from-gray-900 dark:via-gray-800 dark:to-purple-900">
      <Header />
      
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* Hero Section */}
        <div className="text-center mb-12 relative">
          {/* Background decoration */}
          <div className="absolute inset-0 -z-10">
            <div className="absolute top-20 left-1/4 w-72 h-72 bg-blue-400/20 dark:bg-blue-500/10 rounded-full blur-3xl"></div>
            <div className="absolute top-40 right-1/4 w-96 h-96 bg-purple-400/20 dark:bg-purple-500/10 rounded-full blur-3xl"></div>
          </div>
          
          <h1 className="text-5xl lg:text-6xl font-bold mb-6">
            <span className="bg-gradient-to-r from-blue-600 via-purple-600 to-blue-800 bg-clip-text text-transparent">
              Watch Together,
            </span>
            <br />
            <span className="text-gray-900 dark:text-white">
              Stay Connected
            </span>
          </h1>
          <p className="text-xl text-gray-600 dark:text-gray-300 max-w-3xl mx-auto mb-12 leading-relaxed">
            Create or join watch rooms to enjoy videos with friends in real-time. 
            Chat, sync playback, and share the experience no matter where you are.
          </p>
          
          {/* Action Buttons */}
          <div className="flex flex-col sm:flex-row justify-center gap-6 mb-16">
            <button
              onClick={handleCreateRoom}
              className="group relative overflow-hidden bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 text-white px-8 py-4 rounded-xl text-lg font-semibold shadow-xl hover:shadow-2xl transition-all duration-300 transform hover:-translate-y-1"
            >
              <div className="absolute inset-0 bg-white/20 opacity-0 group-hover:opacity-100 transition-opacity duration-300"></div>
              <div className="relative flex items-center justify-center">
                <Plus className="w-5 h-5 mr-2" />
                Create Room
              </div>
            </button>
            <button
              onClick={() => setShowJoinForm(true)}
              className="group bg-white/70 dark:bg-gray-800/70 backdrop-blur-lg border border-gray-200/50 dark:border-gray-700/50 text-gray-900 dark:text-white hover:bg-white/90 dark:hover:bg-gray-800/90 px-8 py-4 rounded-xl text-lg font-semibold shadow-lg hover:shadow-xl transition-all duration-300 transform hover:-translate-y-1"
            >
              <div className="flex items-center justify-center">
                <LogIn className="w-5 h-5 mr-2" />
                Join with Code
              </div>
            </button>
          </div>
        </div>
        
        {/* Browse Public Rooms Section */}
        <div className="bg-white/80 dark:bg-gray-900/80 backdrop-blur-xl rounded-2xl shadow-2xl border border-gray-200/50 dark:border-gray-700/50 p-8 mb-8 relative overflow-hidden">
          {/* Background decoration */}
          <div className="absolute top-0 right-0 w-32 h-32 bg-gradient-to-br from-blue-500/10 to-purple-500/10 rounded-full blur-2xl"></div>
          <div className="absolute bottom-0 left-0 w-24 h-24 bg-gradient-to-tr from-purple-500/10 to-blue-500/10 rounded-full blur-2xl"></div>
          
          <div className="relative">
            <h2 className="text-3xl font-bold text-center mb-8">
              <span className="bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
                Browse Public Rooms
              </span>
            </h2>
            <RoomList />
          </div>
        </div>
      </main>

      {/* Join Room Modal */}
      {showJoinForm && (
        <JoinRoomForm onClose={() => setShowJoinForm(false)} />
      )}
    </div>
  );
};

export default HomePage;
