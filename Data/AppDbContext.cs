using Casbin.Persist.Adapter.EFCore;
using casbin_poc.Models;
using Microsoft.EntityFrameworkCore;

namespace casbin_poc.Data
{
    public class AppDbContext : CasbinDbContext<Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Tasks> Tasks { get; set; }
        public DbSet<Users> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Users>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Auth0Sub).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Name).HasMaxLength(255);
                entity.Property(e => e.Email).HasMaxLength(255);
                entity.Property(e => e.Timestamp).IsRequired().HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<Tasks>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.DueDate);
                entity.Property(e => e.Timestamp).IsRequired().HasDefaultValueSql("now()");

                entity.HasOne(task => task.Owner)
                      .WithMany()
                      .HasForeignKey(task => task.OwnerId)
                      .IsRequired();
            });
        }
    }
}
