using Microsoft.EntityFrameworkCore;
using StreamSync.Data;
using StreamSync.DataAccess.Interfaces;

namespace StreamSync.DataAccess.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly StreamSyncDbContext _context;
        protected DbSet<T> dbSet;

        public GenericRepository(StreamSyncDbContext context)
        {
            _context = context;
            dbSet = _context.Set<T>();
        }

        public async Task<T> CreateAsync(T entity)
        {
            var newEntity = await dbSet.AddAsync(entity);
            return newEntity.Entity;
        }

        public async Task DeleteAsync(object id)
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
            {
                dbSet.Remove(entity);
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await dbSet.ToListAsync();
        }

        public async Task<T?> GetByIdAsync(object id)
        {
            return await dbSet.FindAsync(id);
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Update(T entity)
        {
            dbSet.Update(entity);
        }
    }
}
