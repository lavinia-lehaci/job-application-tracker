using JobAppTrackerApi.Models;
using Microsoft.EntityFrameworkCore;

namespace JobAppTrackerApi.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<JobApplication> JobApplications => Set<JobApplication>();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

            modelBuilder.Entity<JobApplication>(entity => {
                entity.Property(app => app.Status).HasConversion<string>();
                entity.HasIndex(app => app.UserId);
                entity.HasOne(app => app.User)
                    .WithMany()
                    .HasForeignKey(a => a.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
