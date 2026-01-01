import React, { useState, useEffect, useCallback } from 'react';
import { Users, Clock, Lock, Play, Trash2, Search, Filter, SortAsc, SortDesc } from 'lucide-react';
import { Room, PagedResult, PaginationQuery } from '../../types/index';
import { roomService } from '../../services/roomService';
import Button from '../common/Button';
import Input from '../common/Input';
import Pagination from '../common/Pagination';
import { useAuth } from '../../contexts/AuthContext';
import { useSignalR } from '../../contexts/SignalRContext';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import signalRService from '../../services/signalRService';

const RoomList: React.FC = () => {
  const [isLoading, setIsLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<'all' | 'my'>('all');
  
  // Pagination state
  const [pagedRooms, setPagedRooms] = useState<PagedResult<Room>>({
    data: [],
    totalCount: 0,
    currentPage: 1,
    pageSize: 9,
    totalPages: 0,
    hasNextPage: false,
    hasPreviousPage: false,
  });
  const [pagedUserRooms, setPagedUserRooms] = useState<PagedResult<Room>>({
    data: [],
    totalCount: 0,
    currentPage: 1,
    pageSize: 9,
    totalPages: 0,
    hasNextPage: false,
    hasPreviousPage: false,
  });
  
  // Search and filter state
  const [searchQuery, setSearchQuery] = useState('');
  const [sortBy, setSortBy] = useState('createdAt');
  const [sortDescending, setSortDescending] = useState(true);
  const [passwordModal, setPasswordModal] = useState<{
    isOpen: boolean;
    roomId: string | null;
    password: string;
    isValidating: boolean;
    error: string | null;
  }>({
    isOpen: false,
    roomId: null,
    password: '',
    isValidating: false,
    error: null
  });
  const { isAuthenticated, user } = useAuth();
  const { connectAndJoinRoom, disconnectFromRoom } = useSignalR();
  const navigate = useNavigate();

  useEffect(() => {
    // Always fetch both tabs' data on mount and when auth changes
    const fetchInitialRooms = async () => {
      setIsLoading(true);
      const paginationAll: PaginationQuery = {
        page: pagedRooms.currentPage,
        pageSize: pagedRooms.pageSize,
        search: searchQuery || undefined,
        sortBy,
        sortDescending,
      };
      const paginationMy: PaginationQuery = {
        page: pagedUserRooms.currentPage,
        pageSize: pagedUserRooms.pageSize,
        search: searchQuery || undefined,
        sortBy,
        sortDescending,
      };
      const [activeRoomsResult, myRoomsResult] = await Promise.all([
        roomService.getActiveRoomsPaginated(paginationAll),
        isAuthenticated ? roomService.getUserRoomsPaginated(paginationMy) : Promise.resolve({
          data: [],
          totalCount: 0,
          currentPage: 1,
          pageSize: 12,
          totalPages: 0,
          hasNextPage: false,
          hasPreviousPage: false,
        } as PagedResult<Room>)
      ]);
      setPagedRooms(activeRoomsResult);
      setPagedUserRooms(myRoomsResult);
      setIsLoading(false);
    };
    fetchInitialRooms();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated]);

  // Effect to refetch when pagination parameters change for the current tab only
  useEffect(() => {
    if (activeTab === 'all') {
      fetchRoomsPaginated();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pagedRooms.currentPage, pagedRooms.pageSize, activeTab]);
  
  useEffect(() => {
    if (activeTab === 'my') {
      fetchRoomsPaginated();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pagedUserRooms.currentPage, pagedUserRooms.pageSize, activeTab]);

  // Debounced search effect
  useEffect(() => {
    const timer = setTimeout(() => {
      fetchRoomsPaginated();
    }, 300);

    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchQuery, sortBy, sortDescending]);

  const fetchRoomsPaginated = async () => {
    try {
      setIsLoading(true);
      // Always use correct pagination for each tab
      const paginationAll: PaginationQuery = {
        page: pagedRooms.currentPage,
        pageSize: pagedRooms.pageSize,
        search: searchQuery || undefined,
        sortBy,
        sortDescending,
      };
      const paginationMy: PaginationQuery = {
        page: pagedUserRooms.currentPage,
        pageSize: pagedUserRooms.pageSize,
        search: searchQuery || undefined,
        sortBy,
        sortDescending,
      };
      const [activeRoomsResult, myRoomsResult] = await Promise.all([
        roomService.getActiveRoomsPaginated(paginationAll),
        isAuthenticated ? roomService.getUserRoomsPaginated(paginationMy) : Promise.resolve(pagedUserRooms)
      ]);
      setPagedRooms(activeRoomsResult);
      // Only overwrite myRooms if authenticated, otherwise keep previous value
      if (isAuthenticated) {
        setPagedUserRooms(myRoomsResult);
      }
    } catch (error: any) {
      toast.error('Failed to fetch rooms');
    } finally {
      setIsLoading(false);
    }
  };

  const handlePageChange = useCallback((page: number) => {
    if (activeTab === 'all') {
      setPagedRooms(prev => ({ 
        ...prev, 
        currentPage: page 
      }));
    } else {
      setPagedUserRooms(prev => ({ 
        ...prev, 
        currentPage: page 
      }));
    }
  }, [activeTab]);

  const handlePageSizeChange = useCallback((pageSize: number) => {
    if (activeTab === 'all') {
      setPagedRooms(prev => ({ 
        ...prev, 
        pageSize, 
        currentPage: 1 
      }));
    } else {
      setPagedUserRooms(prev => ({ 
        ...prev, 
        pageSize, 
        currentPage: 1 
      }));
    }
  }, [activeTab]);

  const handleSearchChange = useCallback((value: string) => {
    setSearchQuery(value);
    // Reset to first page when searching
    if (activeTab === 'all') {
      setPagedRooms(prev => ({ ...prev, currentPage: 1 }));
    } else {
      setPagedUserRooms(prev => ({ ...prev, currentPage: 1 }));
    }
  }, [activeTab]);

  const handleSortChange = useCallback((newSortBy: string) => {
    setSortBy(newSortBy);
    // Reset to first page when sorting
    if (activeTab === 'all') {
      setPagedRooms(prev => ({ ...prev, currentPage: 1 }));
    } else {
      setPagedUserRooms(prev => ({ ...prev, currentPage: 1 }));
    }
  }, [activeTab]);

  const handleSortDirectionToggle = useCallback(() => {
    setSortDescending(prev => !prev);
    // Reset to first page when changing sort direction
    if (activeTab === 'all') {
      setPagedRooms(prev => ({ ...prev, currentPage: 1 }));
    } else {
      setPagedUserRooms(prev => ({ ...prev, currentPage: 1 }));
    }
  }, [activeTab]);

  const handleTabChange = useCallback((tab: 'all' | 'my') => {
    setActiveTab(tab);
    // Reset to first page when changing tabs
    if (tab === 'all') {
      setPagedRooms(prev => ({ ...prev, currentPage: 1 }));
    } else {
      setPagedUserRooms(prev => ({ ...prev, currentPage: 1 }));
    }
  }, []);

  const handleJoinRoom = (room: Room) => {
    // Check if user is the room creator
    const isRoomCreator = user && room.adminId === user.id;
    
    if (room.isPrivate && !isRoomCreator) {
      // Show password modal for private rooms (except for creators)
      setPasswordModal({
        isOpen: true,
        roomId: room.id,
        password: '',
        isValidating: false,
        error: null
      });
    } else {
      // Direct join for public rooms or room creators
      if (isRoomCreator && room.isPrivate) {
        toast.success('Joining your private room as the creator!');
      }
      navigate(`/room/${room.id}`);
    }
  };

  // Handle room deletion
  const handleDeleteRoom = async (roomId: string) => {
    if (!window.confirm('Are you sure you want to delete this room? This action cannot be undone.')) {
      return;
    }

    try {
      await roomService.endRoom(roomId);
      toast.success('Room deleted successfully');
      
      // Update paginated data immediately
      setPagedRooms(prev => ({
        ...prev,
        data: prev.data.filter(room => room.id !== roomId),
        totalCount: Math.max(0, prev.totalCount - 1)
      }));
      
      setPagedUserRooms(prev => ({
        ...prev,
        data: prev.data.filter(room => room.id !== roomId),
        totalCount: Math.max(0, prev.totalCount - 1)
      }));
      
      // Refresh paginated data from backend
      setTimeout(async () => {
        await fetchRoomsPaginated();
      }, 1000);
    } catch (error) {
      toast.error('Failed to delete room');
    }
  };

  const handlePasswordSubmit = async () => {
    if (!passwordModal.password.trim() || !passwordModal.roomId) return;

    setPasswordModal(prev => ({ ...prev, isValidating: true, error: null }));

    try {
      // Connect and join with password using the context method
      await connectAndJoinRoom(passwordModal.roomId, passwordModal.password);
      
      const passwordData = {
        password: passwordModal.password,
        timestamp: Date.now(),
        isCreator: false
      };
      sessionStorage.setItem(`room_password_${passwordModal.roomId}`, JSON.stringify(passwordData));
      navigate(`/room/${passwordModal.roomId}`);
      
      setPasswordModal({ 
        isOpen: false, 
        roomId: null, 
        password: '', 
        isValidating: false, 
        error: null 
      });
      
    } catch (error: any) {
      const errorMessage = error?.message || 'Incorrect password. Please try again.';
      setPasswordModal(prev => ({ 
        ...prev, 
        isValidating: false, 
        error: errorMessage
      }));
      sessionStorage.removeItem(`room_password_${passwordModal.roomId}`);
      // Disconnect on failure
      if (passwordModal.roomId) {
        try {
          await disconnectFromRoom(passwordModal.roomId);
        } catch (disconnectError) {
        }
      }
    }
  };

  const formatDate = (dateString: string | Date) => {
    const date = typeof dateString === 'string' ? new Date(dateString) : dateString;
    return date.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  const RoomCard: React.FC<{ 
    room: Room; 
    showDeleteButton?: boolean; 
    onDelete?: (roomId: string) => void; 
  }> = React.memo(({ room, showDeleteButton = false, onDelete }) => (
    <div className="group bg-white dark:bg-gray-800 rounded-xl shadow-lg border border-gray-200 dark:border-gray-700 overflow-hidden hover:shadow-xl hover:-translate-y-1 transition-all duration-300">
      {/* Header with gradient background */}
      <div className="bg-gradient-to-r from-blue-500 to-purple-600 h-2"></div>
      
      <div className="p-6">
        {/* Room Name and Privacy */}
        <div className="flex justify-between items-start mb-4">
          <h3 className="text-xl font-bold text-gray-900 dark:text-white truncate pr-2 group-hover:text-blue-600 dark:group-hover:text-blue-400 transition-colors duration-200">
            {room.name}
          </h3>
          {room.isPrivate && (
            <div className="flex-shrink-0 bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400 px-2 py-1 rounded-full flex items-center gap-1">
              <Lock className="w-3 h-3" />
              <span className="text-xs font-medium">Private</span>
            </div>
          )}
        </div>
        
        {/* Room Stats */}
        <div className="flex items-center justify-between text-sm mb-6">
          <div className="flex items-center gap-4">
            <div className="flex items-center gap-1 text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-900/30 px-3 py-1 rounded-full">
              <Users className="w-4 h-4" />
              <span className="font-medium">{room.userCount}</span>
              <span className="text-gray-500 dark:text-gray-400">online</span>
            </div>
            <div className="flex items-center gap-1 text-gray-600 dark:text-gray-400">
              <Clock className="w-4 h-4" />
              <span>{formatDate(room.createdAt)}</span>
            </div>
          </div>
        </div>
        
        {/* Creator Info */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 bg-gradient-to-br from-blue-500 to-purple-600 rounded-full flex items-center justify-center">
              <span className="text-white text-sm font-bold">
                {(user && room.adminId === user.id ? 'Me' : room.adminName).charAt(0).toUpperCase()}
              </span>
            </div>
            <div>
              <p className="text-sm font-medium text-gray-900 dark:text-white">
                {user && room.adminId === user.id ? 'Created by You' : room.adminName}
              </p>
              <p className="text-xs text-gray-500 dark:text-gray-400">Room Creator</p>
            </div>
          </div>
          
          {/* Action Buttons */}
          <div className="flex gap-2">
            <Button
              size="sm"
              onClick={() => handleJoinRoom(room)}
              className="bg-gradient-to-r from-green-500 to-emerald-600 hover:from-green-600 hover:to-emerald-700 text-white shadow-md hover:shadow-lg transition-all duration-200 px-4 py-2"
            >
              <Play className="w-4 h-4 mr-1" />
              Join
            </Button>
            {showDeleteButton && onDelete && (
              <Button
                size="sm"
                onClick={() => onDelete(room.id)}
                className="bg-red-50 dark:bg-red-900/30 text-red-600 dark:text-red-400 border-red-200 dark:border-red-800 hover:bg-red-100 dark:hover:bg-red-900/50 transition-all duration-200 px-3 py-2"
              >
                <Trash2 className="w-4 h-4" />
              </Button>
            )}
          </div>
        </div>
      </div>
    </div>
  ));

  if (isLoading) {
    // Only show spinner if there is no data at all
    const hasAnyRooms = pagedRooms.data.length > 0 || pagedUserRooms.data.length > 0;
    if (!hasAnyRooms) {
      return (
        <div className="min-h-screen bg-gradient-to-br from-slate-50 to-blue-50 dark:from-gray-900 dark:to-slate-900 flex justify-center items-center">
          <div className="text-center">
            <div className="relative">
              <div className="w-16 h-16 border-4 border-blue-200 dark:border-blue-800 border-t-blue-600 dark:border-t-blue-400 rounded-full animate-spin mx-auto"></div>
              <Play className="w-6 h-6 text-blue-600 dark:text-blue-400 absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2" />
            </div>
            <p className="mt-4 text-gray-600 dark:text-gray-400 font-medium">Loading rooms...</p>
          </div>
        </div>
      );
    }
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 to-blue-50 dark:from-gray-900 dark:to-slate-900">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* Header Section */}
        <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 mb-8">
          <div>
            <h1 className="text-3xl font-bold text-gray-900 dark:text-white">Watch Rooms</h1>
            <p className="text-gray-600 dark:text-gray-400 mt-1">Join or create rooms to watch videos together</p>
          </div>
          {isAuthenticated && (
            <Button 
              onClick={() => navigate('/create-room')}
              className="bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 text-white shadow-lg hover:shadow-xl transition-all duration-200"
            >
              <Play className="w-4 h-4 mr-2" />
              Create Room
            </Button>
          )}
        </div>

        {/* Search and Filter Controls */}
        <div className="bg-white dark:bg-gray-800 rounded-xl shadow-lg border border-gray-200 dark:border-gray-700 p-6 mb-8">
          <div className="flex flex-col lg:flex-row gap-4">
            {/* Search Input */}
            <div className="relative flex-1">
              <Search className="absolute left-4 top-1/2 transform -translate-y-1/2 text-gray-400 h-5 w-5" />
              <Input
                type="text"
                placeholder="Search rooms by name or creator..."
                value={searchQuery}
                onChange={(e) => handleSearchChange(e.target.value)}
                className="pl-12 pr-4 py-3 w-full border-gray-300 dark:border-gray-600 rounded-lg bg-gray-50 dark:bg-gray-700 focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all duration-200"
              />
            </div>
            
            {/* Sort Controls */}
            <div className="flex gap-3">
              <div className="relative">
                <Filter className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 h-4 w-4 z-10" />
                <select
                  value={sortBy}
                  onChange={(e) => handleSortChange(e.target.value)}
                  className="pl-10 pr-8 py-3 bg-white/80 dark:bg-gray-800/80 backdrop-blur-sm border border-gray-200/50 dark:border-gray-700/50 rounded-xl text-gray-900 dark:text-gray-100 focus:ring-2 focus:ring-blue-500/50 focus:border-blue-500/50 transition-all duration-200 appearance-none cursor-pointer shadow-lg hover:shadow-xl"
                >
                  <option value="createdAt">Created Date</option>
                  <option value="name">Name</option>
                  <option value="participantCount">Participants</option>
                </select>
              </div>
              
              <button
                onClick={handleSortDirectionToggle}
                className="px-4 py-3 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-600 focus:ring-2 focus:ring-blue-500 transition-all duration-200 flex items-center gap-2"
                title={sortDescending ? 'Sort Ascending' : 'Sort Descending'}
              >
                {sortDescending ? <SortDesc className="h-4 w-4" /> : <SortAsc className="h-4 w-4" />}
                <span className="hidden sm:inline">{sortDescending ? 'Desc' : 'Asc'}</span>
              </button>
            </div>
          </div>
        </div>

        {/* Tab Navigation */}
        {isAuthenticated && (
          <div className="bg-white dark:bg-gray-800 rounded-xl shadow-lg border border-gray-200 dark:border-gray-700 p-1 mb-8 inline-flex">
            <button
              className={`px-6 py-3 rounded-lg font-medium text-sm transition-all duration-200 flex items-center gap-2 ${
                activeTab === 'all'
                  ? 'bg-gradient-to-r from-blue-600 to-purple-600 text-white shadow-md'
                  : 'text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-100 hover:bg-gray-50 dark:hover:bg-gray-700'
              }`}
              onClick={() => handleTabChange('all')}
            >
              <Users className="h-4 w-4" />
              All Rooms 
              <span className="bg-white/20 px-2 py-1 rounded-full text-xs font-semibold">
                {pagedRooms.totalCount}
              </span>
            </button>
            <button
              className={`px-6 py-3 rounded-lg font-medium text-sm transition-all duration-200 flex items-center gap-2 ${
                activeTab === 'my'
                  ? 'bg-gradient-to-r from-blue-600 to-purple-600 text-white shadow-md'
                  : 'text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-100 hover:bg-gray-50 dark:hover:bg-gray-700'
              }`}
              onClick={() => handleTabChange('my')}
            >
              <Lock className="h-4 w-4" />
              My Rooms 
              <span className="bg-white/20 px-2 py-1 rounded-full text-xs font-semibold">
                {pagedUserRooms.totalCount}
              </span>
            </button>
          </div>
        )}

        {/* Room Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6 mb-8">
          {(() => {
            const currentRooms = activeTab === 'all' ? pagedRooms.data : pagedUserRooms.data;
            
            return currentRooms.length === 0 ? (
              <div className="col-span-full">
                <div className="bg-white dark:bg-gray-800 rounded-xl shadow-lg border border-gray-200 dark:border-gray-700 p-12 text-center">
                  <div className="w-20 h-20 bg-gradient-to-br from-blue-100 to-purple-100 dark:from-blue-900/30 dark:to-purple-900/30 rounded-full flex items-center justify-center mx-auto mb-6">
                    <Play className="w-10 h-10 text-blue-600 dark:text-blue-400" />
                  </div>
                  <h3 className="text-xl font-bold text-gray-900 dark:text-white mb-2">
                    No rooms found
                  </h3>
                  <p className="text-gray-600 dark:text-gray-400 mb-6 max-w-md mx-auto">
                    {activeTab === 'all' 
                      ? searchQuery 
                        ? 'No rooms match your search criteria. Try adjusting your filters or search terms.'
                        : 'There are no active rooms at the moment. Be the first to create one!'
                      : searchQuery
                        ? 'No rooms match your search criteria.'
                        : 'You haven\'t created any rooms yet. Start your first watch party!'
                    }
                  </p>
                  {isAuthenticated && activeTab === 'my' && !searchQuery && (
                    <Button 
                      onClick={() => navigate('/create-room')}
                      className="bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 text-white shadow-lg hover:shadow-xl transition-all duration-200"
                    >
                      <Play className="w-4 h-4 mr-2" />
                      Create your first room
                    </Button>
                  )}
                </div>
              </div>
            ) : (
              currentRooms.map((room) => (
                <RoomCard 
                  key={room.id} 
                  room={room} 
                  showDeleteButton={activeTab === 'my'}
                  onDelete={activeTab === 'my' ? handleDeleteRoom : undefined}
                />
              ))
            );
          })()}
        </div>

        {/* Pagination Controls */}
        <div className="bg-white dark:bg-gray-800 rounded-xl shadow-lg border border-gray-200 dark:border-gray-700 p-6">
          <Pagination
            currentPage={activeTab === 'all' ? pagedRooms.currentPage : pagedUserRooms.currentPage}
            totalPages={activeTab === 'all' ? pagedRooms.totalPages : pagedUserRooms.totalPages}
            pageSize={activeTab === 'all' ? pagedRooms.pageSize : pagedUserRooms.pageSize}
            totalCount={activeTab === 'all' ? pagedRooms.totalCount : pagedUserRooms.totalCount}
            onPageChange={handlePageChange}
            onPageSizeChange={handlePageSizeChange}
            isLoading={isLoading}
          />
        </div>

        {/* Password Modal for Private Rooms */}
        {passwordModal.isOpen && (
          <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4 z-50">
            <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl border border-gray-200 dark:border-gray-700 p-8 w-full max-w-md transform transition-all duration-200">
              <div className="text-center mb-6">
                <div className="w-16 h-16 bg-gradient-to-br from-blue-100 to-purple-100 dark:from-blue-900/30 dark:to-purple-900/30 rounded-full flex items-center justify-center mx-auto mb-4">
                  <Lock className="w-8 h-8 text-blue-600 dark:text-blue-400" />
                </div>
                <h3 className="text-2xl font-bold text-gray-900 dark:text-white mb-2">Private Room</h3>
                <p className="text-gray-600 dark:text-gray-400">
                  This room requires a password to join
                </p>
              </div>
              
              {passwordModal.error && (
                <div className="mb-6 p-4 bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-800 rounded-xl">
                  <p className="text-sm text-red-600 dark:text-red-400 text-center">{passwordModal.error}</p>
                </div>
              )}
              
              <Input
                type="password"
                placeholder="Enter room password"
                value={passwordModal.password}
                onChange={(e) => setPasswordModal(prev => ({ ...prev, password: e.target.value, error: null }))}
                className="mb-6 py-3 text-center"
                disabled={passwordModal.isValidating}
                onKeyPress={async (e) => {
                  if (e.key === 'Enter' && !passwordModal.isValidating) {
                    await handlePasswordSubmit();
                  }
                }}
              />
              
              <div className="flex gap-3">
                <Button
                  onClick={() => setPasswordModal({ 
                    isOpen: false, 
                    roomId: null, 
                    password: '', 
                    isValidating: false, 
                    error: null 
                  })}
                  disabled={passwordModal.isValidating}
                  className="flex-1 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-600 border-0"
                >
                  Cancel
                </Button>
                <Button
                  onClick={handlePasswordSubmit}
                  disabled={!passwordModal.password.trim() || passwordModal.isValidating}
                  className="flex-1 bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 text-white shadow-lg"
                >
                  {passwordModal.isValidating ? (
                    <>
                      <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin mr-2"></div>
                      Joining...
                    </>
                  ) : (
                    <>
                      <Play className="w-4 h-4 mr-2" />
                      Join Room
                    </>
                  )}
                </Button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default RoomList;
