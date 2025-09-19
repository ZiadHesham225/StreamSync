import React from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { useNavigate } from 'react-router-dom';
import { LogOut, User, Home, Plus, Play } from 'lucide-react';
import Button from './Button';

interface HeaderProps {
  onLogoClick?: () => void;
  onHomeClick?: () => void;
}

const Header: React.FC<HeaderProps> = ({ onLogoClick, onHomeClick }) => {
  const { user, isAuthenticated, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/');
  };

  const handleLogoClick = () => {
    if (onLogoClick) {
      onLogoClick();
    } else {
      navigate('/');
    }
  };

  const handleHomeClick = () => {
    if (onHomeClick) {
      onHomeClick();
    } else {
      navigate('/');
    }
  };

  return (
    <header className="bg-white/80 dark:bg-gray-900/80 backdrop-blur-lg shadow-xl border-b border-gray-200/50 dark:border-gray-700/50 sticky top-0 z-50">
      <div className="mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex justify-between items-center h-20">
          {/* Logo */}
          <div className="flex items-center">
            <div 
              className="flex items-center gap-3 cursor-pointer hover:scale-105 transition-transform duration-200"
              onClick={handleLogoClick}
            >
              <div className="w-10 h-10 bg-gradient-to-br from-blue-600 to-purple-600 rounded-xl flex items-center justify-center shadow-lg">
                <Play className="w-6 h-6 text-white" />
              </div>
              <h1 className="text-2xl font-bold bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
                StreamSync
              </h1>
            </div>
          </div>

          {/* Navigation */}
          <nav className="flex items-center space-x-4">
            <Button
              onClick={handleHomeClick}
              className="bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-700 border-0 px-4 py-2 rounded-lg transition-all duration-200"
            >
              <Home className="w-4 h-4 mr-2" />
              Home
            </Button>

            {isAuthenticated ? (
              <>
                <Button
                  onClick={() => navigate('/create-room')}
                  className="bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 text-white shadow-lg hover:shadow-xl transition-all duration-200 px-4 py-2"
                >
                  <Plus className="w-4 h-4 mr-2" />
                  Create Room
                </Button>
                
                {/* User Profile */}
                <div className="flex items-center space-x-3 ml-4 pl-4 border-l border-gray-200/50 dark:border-gray-700/50">
                  <button
                    onClick={() => navigate('/account')}
                    className="flex items-center space-x-3 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-xl p-3 transition-all duration-200 group"
                    title="Account Settings"
                  >
                    <div className="w-10 h-10 bg-gradient-to-br from-blue-500 to-purple-600 rounded-full flex items-center justify-center overflow-hidden shadow-lg group-hover:shadow-xl transition-shadow duration-200">
                      {user?.avatarUrl ? (
                        <img 
                          src={user.avatarUrl.startsWith('http') ? user.avatarUrl : `${process.env.REACT_APP_API_URL || 'http://localhost:5099'}${user.avatarUrl}`}
                          alt={user.displayName || user.username}
                          className="w-full h-full object-cover"
                          onError={(e) => {
                            const img = e.target as HTMLImageElement;
                            img.src = 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMjQiIGhlaWdodD0iMjQiIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0ibm9uZSIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj4KPHBhdGggZD0iTTIwIDIxdi0yYTQgNCAwIDAgMC00LTRIOS02YTQgNCAwIDAgMC00IDR2MiIgc3Ryb2tlPSJjdXJyZW50Q29sb3IiIHN0cm9rZS13aWR0aD0iMiIgc3Ryb2tlLWxpbmVjYXA9InJvdW5kIiBzdHJva2UtbGluZWpvaW49InJvdW5kIi8+CjxjaXJjbGUgY3g9IjEyIiBjeT0iNyIgcj0iNCIgc3Ryb2tlPSJjdXJyZW50Q29sb3IiIHN0cm9rZS13aWR0aD0iMiIgc3Ryb2tlLWxpbmVjYXA9InJvdW5kIiBzdHJva2UtbGluZWpvaW49InJvdW5kIi8+Cjwvc3ZnPgo=';
                            img.className = "w-5 h-5 text-white";
                          }}
                        />
                      ) : (
                        <User className="w-5 h-5 text-white" />
                      )}
                    </div>
                    <div className="hidden sm:block text-left">
                      <p className="text-sm font-semibold text-gray-900 dark:text-white">
                        {user?.displayName || user?.username}
                      </p>
                      <p className="text-xs text-gray-500 dark:text-gray-400">Account Settings</p>
                    </div>
                  </button>
                  
                  <Button
                    onClick={handleLogout}
                    className="bg-red-50 dark:bg-red-900/30 text-red-600 dark:text-red-400 hover:bg-red-100 dark:hover:bg-red-900/50 border-red-200 dark:border-red-800 px-3 py-2 transition-all duration-200"
                  >
                    <LogOut className="w-4 h-4 mr-2" />
                    <span className="hidden sm:inline">Logout</span>
                  </Button>
                </div>
              </>
            ) : (
              <div className="flex items-center space-x-3">
                <Button
                  onClick={() => navigate('/login')}
                  className="bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-700 border-0 px-4 py-2"
                >
                  Login
                </Button>
                <Button
                  onClick={() => navigate('/register')}
                  className="bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 text-white shadow-lg hover:shadow-xl transition-all duration-200 px-4 py-2"
                >
                  Sign Up
                </Button>
              </div>
            )}
          </nav>
        </div>
      </div>
    </header>
  );
};

export default Header;
