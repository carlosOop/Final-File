using ManageBooking.Data;
using ManageBooking.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace ManageBooking.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login()
        {
            // Check if user is already logged in
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
            {
                return RedirectToAction("Index", "Home");
            }

            // Prevent caching of login page
            Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            Response.Headers.Add("Pragma", "no-cache");
            Response.Headers.Add("Expires", "0");

            return View("Login");
        }

        // POST: /Account/Login
        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            // Validate input
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Invalid input, please try again.");
                return View("Login");
            }

            // Validate password format
            if (!IsValidPassword(password))
            {
                ModelState.AddModelError("", "Invalid input, please try again.");
                return View("Login");
            }

            var user = _context.Users.SingleOrDefault(u => u.Username == username);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid input, please try again.");
                return View("Login");
            }

            var passwordHasher = new PasswordHasher<User>();
            var result = passwordHasher.VerifyHashedPassword(user, user.Password, password);

            if (result != PasswordVerificationResult.Success)
            {
                ModelState.AddModelError("", "Invalid input, please try again.");
                return View("Login");
            }

            // Store user info in session after successful login
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("FullName", user.Name);
            HttpContext.Session.SetString("ProfilePhoto", user.ProfilePhoto ?? "/images/Logo.jpg");

            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Signup
        [HttpGet]
        public IActionResult Signup()
        {
            // Check if user is already logged in
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
            {
                return RedirectToAction("Index", "Home");
            }

            return View("Login");
        }

        // POST: /Account/Signup
        [HttpPost]
        public async Task<IActionResult> Signup(User user, string ConfirmPassword)
        {
            // Validate input
            if (string.IsNullOrEmpty(user.Name) || string.IsNullOrEmpty(user.Surname) ||
                string.IsNullOrEmpty(user.Username) || string.IsNullOrEmpty(user.Password) ||
                string.IsNullOrEmpty(ConfirmPassword))
            {
                ModelState.AddModelError("", "All fields are required.");
                return View("Login");
            }

            // Validate password format
            if (!IsValidPassword(user.Password))
            {
                ModelState.AddModelError("", "Password must contain at least one uppercase letter, one special character, one number, and be at least 8 characters long.");
                return View("Login");
            }

            if (user.Password != ConfirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match.");
                return View("Login");
            }

            if (_context.Users.Any(u => u.Username == user.Username))
            {
                ModelState.AddModelError("", "Username already exists.");
                return View("Login");
            }

            if (string.IsNullOrEmpty(user.ProfilePhoto))
            {
                user.ProfilePhoto = "/images/default-profile-photo.jpg";
            }

            var passwordHasher = new PasswordHasher<User>();
            user.Password = passwordHasher.HashPassword(user, user.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Automatically log in the user after signup
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("FullName", user.Name);
            HttpContext.Session.SetString("ProfilePhoto", user.ProfilePhoto ?? "/images/Logo.jpg");

            return RedirectToAction("Index", "Home");
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string username, string newPassword, string confirmNewPassword)
        {
            // Validate input
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmNewPassword))
            {
                ModelState.AddModelError("", "All fields are required.");
                return View("Login");
            }

            // Validate password format
            if (!IsValidPassword(newPassword))
            {
                ModelState.AddModelError("", "Password must contain at least one uppercase letter, one special character, one number, and be at least 8 characters long.");
                return View("Login");
            }

            if (newPassword != confirmNewPassword)
            {
                ModelState.AddModelError("", "Passwords do not match.");
                return View("Login");
            }

            var user = _context.Users.SingleOrDefault(u => u.Username == username);

            if (user == null)
            {
                ModelState.AddModelError("", "Username not found.");
                return View("Login");
            }

            // Hash the new password
            var passwordHasher = new PasswordHasher<User>();
            user.Password = passwordHasher.HashPassword(user, newPassword);

            await _context.SaveChangesAsync();

            // Set a success message
            TempData["Success"] = "Password reset successfully. You can now login with your new password.";

            return RedirectToAction("Login");
        }

        // Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // Helper method to validate password
        private bool IsValidPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return false;

            // Check for at least one uppercase letter, one special character, and one digit
            var hasUpperCase = Regex.IsMatch(password, @"[A-Z]");
            var hasSpecialChar = Regex.IsMatch(password, @"[\W_]");
            var hasDigit = Regex.IsMatch(password, @"\d");

            return hasUpperCase && hasSpecialChar && hasDigit;
        }
    }
}