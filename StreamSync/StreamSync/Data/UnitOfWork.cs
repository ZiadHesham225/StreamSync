using StreamSync.DataAccess.Interfaces;
using StreamSync.DataAccess.Repositories;
using StreamSync.Models;

namespace StreamSync.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly StreamSyncDbContext _context;
        private IRoomRepository? _rooms;
        private IGenericRepository<Room>? _genericRooms;
        private IVirtualBrowserRepository? _virtualBrowsers;

        public UnitOfWork(StreamSyncDbContext context)
        {
            _context = context;
        }

        public IRoomRepository Rooms => _rooms ??= new RoomRepository(_context);
        public IGenericRepository<Room> GenericRooms => _genericRooms ??= new GenericRepository<Room>(_context);
        public IVirtualBrowserRepository VirtualBrowsers => _virtualBrowsers ??= new VirtualBrowserRepository(_context);
        public IRefreshTokenRepository RefreshTokens => new RefreshTokenRepository(_context);
        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
