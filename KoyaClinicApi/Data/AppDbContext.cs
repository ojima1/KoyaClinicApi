using Microsoft.EntityFrameworkCore;
using KoyaClinicApi.Models;

namespace KoyaClinicApi.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Slot> Slots { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Slot>().HasKey(s => s.Id);
            modelBuilder.Entity<Booking>().HasKey(b => b.Id);
            modelBuilder.Entity<IdempotencyRecord>().HasKey(i => i.Key);

            // This is a database-level safety net. It prevents two "confirmed" 
            // bookings from existing for the same slot, even if the app logic fails.
            modelBuilder.Entity<Booking>()
                .HasIndex(b => b.SlotId)
                .IsUnique()
                .HasFilter("\"Status\" = 'confirmed'"); // SQL Server syntax
        }
    }
}