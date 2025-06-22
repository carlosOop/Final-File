using ManageBooking.Data;
using ManageBooking.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace ManageBooking.Controllers
{
    public class ProfileController : Controller
    {
        private readonly AppDbContext _context;

        public ProfileController(AppDbContext context)
        {
            _context = context;
        }

        // Change Name
        [HttpPost]
        public async Task<IActionResult> ChangeName(string FullName)
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrEmpty(FullName))
            {
                TempData["Error"] = "Name cannot be empty.";
                return RedirectToAction("Index", "Home");
            }

            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user != null)
            {
                user.Name = FullName;
                await _context.SaveChangesAsync();

                // Update session
                HttpContext.Session.SetString("FullName", FullName);

                TempData["Success"] = "Name updated successfully.";
            }
            else
            {
                TempData["Error"] = "User not found.";
            }

            return RedirectToAction("Index", "Home");
        }

        // Change Username
        [HttpPost]
        public async Task<IActionResult> ChangeUsername(string Username)
        {
            var currentUsername = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(currentUsername))
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrEmpty(Username))
            {
                TempData["Error"] = "Username cannot be empty.";
                return RedirectToAction("Index", "Home");
            }

            // Check if new username already exists
            if (_context.Users.Any(u => u.Username == Username && u.Username != currentUsername))
            {
                TempData["Error"] = "Username already exists.";
                return RedirectToAction("Index", "Home");
            }

            var user = _context.Users.FirstOrDefault(u => u.Username == currentUsername);
            if (user != null)
            {
                user.Username = Username;
                await _context.SaveChangesAsync();

                // Update session
                HttpContext.Session.SetString("Username", Username);

                TempData["Success"] = "Username updated successfully.";
            }
            else
            {
                TempData["Error"] = "User not found.";
            }

            return RedirectToAction("Index", "Home");
        }

        // Change Password
        [HttpPost]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword)
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
            {
                TempData["Error"] = "Both current and new passwords are required.";
                return RedirectToAction("Index", "Home");
            }

            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Index", "Home");
            }

            // Verify current password
            var passwordHasher = new PasswordHasher<User>();
            var verificationResult = passwordHasher.VerifyHashedPassword(user, user.Password, currentPassword);

            if (verificationResult != PasswordVerificationResult.Success)
            {
                TempData["Error"] = "Current password is incorrect.";
                return RedirectToAction("Index", "Home");
            }

            // Hash and save new password
            user.Password = passwordHasher.HashPassword(user, newPassword);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Password updated successfully.";
            return RedirectToAction("Index", "Home");
        }

        // Change Photo
        [HttpPost]
        public async Task<IActionResult> ChangePhoto(IFormFile ProfilePhoto)
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Account");
            }

            if (ProfilePhoto == null || ProfilePhoto.Length == 0)
            {
                TempData["Error"] = "Please select a valid image file.";
                return RedirectToAction("Index", "Home");
            }

            // Check file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var fileExtension = Path.GetExtension(ProfilePhoto.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(fileExtension))
            {
                TempData["Error"] = "Only image files (JPG, JPEG, PNG, GIF) are allowed.";
                return RedirectToAction("Index", "Home");
            }

            // Check file size (max 5MB)
            if (ProfilePhoto.Length > 5 * 1024 * 1024)
            {
                TempData["Error"] = "File size must be less than 5MB.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                // Create uploads directory if it doesn't exist
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                // Generate unique filename
                var uniqueFileName = Guid.NewGuid().ToString() + fileExtension;
                var filePath = Path.Combine(uploadsDir, uniqueFileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await ProfilePhoto.CopyToAsync(stream);
                }

                // Update user in database
                var user = _context.Users.FirstOrDefault(u => u.Username == username);
                if (user != null)
                {
                    // Delete old profile photo if it's not the default
                    if (!string.IsNullOrEmpty(user.ProfilePhoto) &&
                        !user.ProfilePhoto.Contains("Logo.jpg") &&
                        !user.ProfilePhoto.Contains("default-profile-photo.jpg"))
                    {
                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfilePhoto.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    user.ProfilePhoto = "/uploads/profiles/" + uniqueFileName;
                    await _context.SaveChangesAsync();

                    // Update session
                    HttpContext.Session.SetString("ProfilePhoto", user.ProfilePhoto);

                    TempData["Success"] = "Profile photo updated successfully.";
                }
                else
                {
                    TempData["Error"] = "User not found.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while uploading the file.";
                // Log the exception if you have logging configured
            }

            return RedirectToAction("Index", "Home");
        }
    }
}