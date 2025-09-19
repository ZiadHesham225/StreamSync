using StreamSync.DataAccess.Interfaces;
using StreamSync.Models;

namespace StreamSync.Data
{
    public interface IUnitOfWork : IDisposable
    {
        IRoomRepository Rooms { get; }
        IGenericRepository<Room> GenericRooms { get; }
        IVirtualBrowserRepository VirtualBrowsers { get; }
        IRefreshTokenRepository RefreshTokens { get; }

        Task SaveAsync();
    }
}
