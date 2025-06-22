using Microsoft.EntityFrameworkCore;
using ManageBooking.Models;

namespace ManageBooking.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Customer entity
            modelBuilder.Entity<Customer>(entity =>
            {
                // Set primary key
                entity.HasKey(e => e.CustomerId);

                // Configure the relationship with User - make it optional
                entity.HasOne(d => d.User)
                      .WithMany() // Assuming User can have many customers
                      .HasForeignKey(d => d.UserId)
                      .OnDelete(DeleteBehavior.Restrict) // Prevent cascade delete
                      .IsRequired(false); // Make the relationship optional

                // Configure properties
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.MobileNumber).IsRequired().HasMaxLength(15);
                entity.Property(e => e.Nationality).IsRequired();
                entity.Property(e => e.Gender).IsRequired();
                entity.Property(e => e.ID).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Address).IsRequired();
                entity.Property(e => e.BedType).IsRequired();
                entity.Property(e => e.RoomType).IsRequired();
                entity.Property(e => e.RoomNumber).IsRequired();
                entity.Property(e => e.RatePerDay).IsRequired();
                entity.Property(e => e.BirthDate).IsRequired();
                entity.Property(e => e.CheckIn).IsRequired();
                entity.Property(e => e.CheckOut).IsRequired();
                entity.Property(e => e.IsCheckedOut).HasDefaultValue(false);
                entity.Property(e => e.UserId).HasDefaultValue(1);
            });
        }
    }
}