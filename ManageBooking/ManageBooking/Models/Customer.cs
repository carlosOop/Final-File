using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Add this namespace for [Column]

namespace ManageBooking.Models
{
    public class Customer
    {
        // Primary Key
        public int CustomerId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(15)]
        public string MobileNumber { get; set; }

        [Required]
        public string Nationality { get; set; }

        [Required]
        public string Gender { get; set; }

        [Required]
        [StringLength(50)]
        public string ID { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        public string BedType { get; set; }

        [Required]
        public string RoomType { get; set; }

        [Required]
        public string RoomNumber { get; set; }

        // Change 'Price' to 'RatePerDay' and update its type to decimal
        [Required]
        [Display(Name = "Rate per Day")] // This will make the label display "Rate per Day" in your view
        [Column(TypeName = "decimal(18, 2)")] // Ensure correct decimal precision for currency in database
        public decimal RatePerDay { get; set; } // Changed from double Price to decimal RatePerDay

        // Add new property for Total Bill
        [Display(Name = "Total Bill")]
        [Column(TypeName = "decimal(18, 2)")] // Ensure correct decimal precision for currency in database
        public decimal TotalBill { get; set; } // New property

        [Required]
        public DateTime BirthDate { get; set; }

        [Required]
        public DateTime CheckIn { get; set; }

        [Required]
        public DateTime CheckOut { get; set; }

        public bool IsCheckedOut { get; set; } = false;

        // Foreign key for User - set default value
        public int UserId { get; set; } = 1; // Default to user ID 1

        // Navigation property - make it nullable and non-required
        public virtual User? User { get; set; }
    }
}