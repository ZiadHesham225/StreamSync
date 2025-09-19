import React from 'react';
import Header from '../components/common/Header';
import CreateRoomForm from '../components/room/CreateRoomForm';
import ProtectedRoute from '../components/common/ProtectedRoute';

const CreateRoomPage: React.FC = () => {
  return (
    <ProtectedRoute>
      <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50 dark:from-gray-900 dark:via-gray-800 dark:to-purple-900">
        <Header />
        <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
          {/* Hero Section */}
          <div className="text-center mb-8 relative">
            {/* Background decoration */}
            <div className="absolute inset-0 -z-10">
              <div className="absolute top-10 left-1/4 w-32 h-32 bg-blue-400/20 dark:bg-blue-500/10 rounded-full blur-2xl"></div>
              <div className="absolute top-20 right-1/3 w-40 h-40 bg-purple-400/20 dark:bg-purple-500/10 rounded-full blur-2xl"></div>
            </div>
            
            <h1 className="text-4xl lg:text-5xl font-bold mb-4">
              <span className="bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
                Create Your Room
              </span>
            </h1>
            <p className="text-lg text-gray-600 dark:text-gray-300 max-w-2xl mx-auto">
              Set up a new watch party room and start enjoying videos with friends
            </p>
          </div>
          
          <CreateRoomForm />
        </main>
      </div>
    </ProtectedRoute>
  );
};

export default CreateRoomPage;
