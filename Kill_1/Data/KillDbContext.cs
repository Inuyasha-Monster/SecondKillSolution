using Kill_1.Data.Model;
using Microsoft.EntityFrameworkCore;

namespace Kill_1.Data
{
    public class KillDbContext : DbContext
    {
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<Order> Orders { get; set; }

        public KillDbContext(DbContextOptions<KillDbContext> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Stock>().Property(x => x.Name).HasMaxLength(32).IsRequired();
            modelBuilder.Entity<Order>().HasKey(x => x.Id);
            modelBuilder.Entity<Order>().Property(x => x.CreatedTime).ValueGeneratedOnAdd();
            modelBuilder.Entity<Order>().Property(x => x.Name).HasMaxLength(32).IsRequired();
            base.OnModelCreating(modelBuilder);
        }
    }
}