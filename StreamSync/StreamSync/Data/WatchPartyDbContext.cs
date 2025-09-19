using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StreamSync.Models;

namespace StreamSync.Data
{
    public class StreamSyncDbContext : IdentityDbContext<ApplicationUser>
    {
        public StreamSyncDbContext(DbContextOptions<StreamSyncDbContext> options)
        : base(options)
        {
        }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<VirtualBrowser> VirtualBrowsers { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Room>()
                .HasOne(r => r.Admin)
                .WithMany(u => u.CreatedRooms)
                .HasForeignKey(r => r.AdminId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Room>()
                .HasIndex(r => r.InviteCode)
                .IsUnique(true);

            modelBuilder.Entity<VirtualBrowser>()
                .HasOne(vb => vb.Room)
                .WithMany()
                .HasForeignKey(vb => vb.RoomId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
            
            modelBuilder.Entity<RefreshToken>()
                .HasIndex(rt => rt.Token)
                .IsUnique(true);
        }
    }
}
