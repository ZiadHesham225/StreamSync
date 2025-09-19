using Google;
using Microsoft.EntityFrameworkCore;
using StreamSync.Data;
using StreamSync.DataAccess.Interfaces;
using StreamSync.Models;

namespace StreamSync.DataAccess.Repositories
{
    public class RefreshTokenRepository : GenericRepository<RefreshToken>, IRefreshTokenRepository
    {
        public RefreshTokenRepository(StreamSyncDbContext context) : base(context)
        {
        }
        public async Task<RefreshToken> GetByUserIdAsync(string userId)
        {
            return await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.UserId == userId);
        }
        public async Task<RefreshToken> GetByTokenAsync(string token)
        {
            return await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == token);
        }
        public async Task DeleteByUserIdAsync(string userId)
        {
            var refreshToken = await GetByUserIdAsync(userId);
            if (refreshToken != null)
            {
                _context.RefreshTokens.Remove(refreshToken);
            }
        }
        public async Task DeleteByTokenAsync(string token)
        {
            var refreshToken = await GetByTokenAsync(token);
            if (refreshToken != null)
            {
                _context.RefreshTokens.Remove(refreshToken);
            }
        }
    }
}
