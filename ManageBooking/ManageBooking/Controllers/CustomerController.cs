using Microsoft.AspNetCore.Mvc;
using ManageBooking.Data;
using ManageBooking.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System;
using Microsoft.AspNetCore.Http; 
using Microsoft.EntityFrameworkCore; 

namespace ManageBooking.Controllers
{
    public class CustomerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(AppDbContext context, ILogger<CustomerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Helper method to get current user ID
        private int GetCurrentUserId()
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                // Redirect to login if no session
                throw new UnauthorizedAccessException("User not logged in");
            }

            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null)
            {
                throw new UnauthorizedAccessException("User not found");
            }

            return user.Id;
        }

        // Helper method to check if user is logged in
        private bool IsUserLoggedIn()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("Username"));
        }

        // Helper method to calculate Total Bill
        private decimal CalculateTotalBill(DateTime checkIn, DateTime checkOut, decimal ratePerDay)
        {
            if (checkOut <= checkIn)
            {
                return 0.00m;
            }

            // Calculate the difference in days. Use TotalDays and Math.Ceiling to ensure full days are counted.
            // For example, if check-in is 10:00 AM on Day 1 and check-out is 11:00 AM on Day 2, that's 2 days.
            TimeSpan duration = checkOut - checkIn;
            double totalDays = Math.Ceiling(duration.TotalDays); // Round up to nearest full day

            return (decimal)totalDays * ratePerDay;
        }


        // GET: Customer Registration
        [HttpGet]
        public IActionResult CustomerRegistration()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var currentUserId = GetCurrentUserId();

                var occupiedRooms = _context.Customers
                    .Where(c => c.UserId == currentUserId && !c.IsCheckedOut)
                    .Select(c => c.RoomNumber)
                    .Where(rn => !string.IsNullOrEmpty(rn))
                    .ToList();

                ViewBag.OccupiedRooms = occupiedRooms;
                return View();
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account");
            }
        }

        // POST: Customer Registration
        [HttpPost]
        [ValidateAntiForgeryToken] // Added for security
        public async Task<IActionResult> CustomerRegistration([Bind("CustomerId,Name,MobileNumber,Nationality,Gender,ID,Address,BedType,RoomType,RoomNumber,RatePerDay,BirthDate,CheckIn,CheckOut")] Customer customer)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var currentUserId = GetCurrentUserId();

                _logger.LogInformation($"=== CUSTOMER REGISTRATION DEBUG ===");
                _logger.LogInformation($"Name: {customer.Name}");
                _logger.LogInformation($"MobileNumber: {customer.MobileNumber}");
                _logger.LogInformation($"RoomNumber: {customer.RoomNumber}");
                _logger.LogInformation($"RatePerDay: {customer.RatePerDay}"); // Log the new property
                _logger.LogInformation($"CheckIn: {customer.CheckIn}");
                _logger.LogInformation($"CheckOut: {customer.CheckOut}");
                _logger.LogInformation($"Current UserId: {currentUserId}");
                _logger.LogInformation($"ModelState.IsValid (before custom validation): {ModelState.IsValid}");

                ModelState.Remove("User"); // Remove User from ModelState validation as it's a navigation property

                // Check if the room is already occupied BY CURRENT USER
                if (!string.IsNullOrEmpty(customer.RoomNumber))
                {
                    var isRoomOccupied = _context.Customers
                        .Any(c => c.UserId == currentUserId && c.RoomNumber == customer.RoomNumber && !c.IsCheckedOut);

                    if (isRoomOccupied)
                    {
                        ModelState.AddModelError("RoomNumber", $"Room {customer.RoomNumber} is already occupied.");
                        _logger.LogWarning($"Attempted to book already occupied room: {customer.RoomNumber} for user {currentUserId}");
                    }
                }

                // Server-side validation for dates and RatePerDay
                if (customer.CheckIn == default(DateTime))
                {
                    ModelState.AddModelError("CheckIn", "Check In date and time is required.");
                }
                if (customer.CheckOut == default(DateTime))
                {
                    ModelState.AddModelError("CheckOut", "Check Out date and time is required.");
                }
                if (customer.CheckOut <= customer.CheckIn && customer.CheckOut != default(DateTime) && customer.CheckIn != default(DateTime))
                {
                    ModelState.AddModelError("CheckOut", "Check Out date and time must be after Check In date and time.");
                }
                if (customer.RatePerDay <= 0)
                {
                    ModelState.AddModelError("RatePerDay", "Rate per Day must be greater than zero.");
                }

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("=== CUSTOMER REGISTRATION VALIDATION ERRORS ===");
                    foreach (var modelError in ModelState)
                    {
                        var key = modelError.Key;
                        var errors = modelError.Value.Errors;
                        foreach (var error in errors)
                        {
                            _logger.LogWarning($"Field: {key}, Error: {error.ErrorMessage}");
                        }
                    }

                    // Reload occupied rooms for the view in case of validation errors
                    var occupiedRooms = _context.Customers
                        .Where(c => c.UserId == currentUserId && !c.IsCheckedOut)
                        .Select(c => c.RoomNumber)
                        .Where(rn => !string.IsNullOrEmpty(rn))
                        .ToList();
                    ViewBag.OccupiedRooms = occupiedRooms;

                    return View(customer);
                }

                // Calculate TotalBill server-side BEFORE saving
                customer.TotalBill = CalculateTotalBill(customer.CheckIn, customer.CheckOut, customer.RatePerDay);

                customer.IsCheckedOut = false;
                customer.UserId = currentUserId;

                _context.Customers.Add(customer);
                var result = await _context.SaveChangesAsync();

                if (result > 0)
                {
                    _logger.LogInformation($"Customer {customer.Name} successfully registered in room {customer.RoomNumber} for user {currentUserId} with Total Bill: {customer.TotalBill}");
                    TempData["Success"] = $"Customer {customer.Name} registered successfully! Total Bill: {customer.TotalBill:C}";
                    return RedirectToAction("History");
                }
                else
                {
                    // This else block is for when SaveChangesAsync returns 0, meaning no rows were affected.
                    // This is rare after Add, but possible if entity state somehow changes before saving.
                    _logger.LogWarning("No changes were saved to the database for new customer registration.");
                    ModelState.AddModelError("", "No changes were saved to the database. Please try again.");
                    var occupiedRooms = _context.Customers
                        .Where(c => c.UserId == currentUserId && !c.IsCheckedOut)
                        .Select(c => c.RoomNumber)
                        .Where(rn => !string.IsNullOrEmpty(rn))
                        .ToList();
                    ViewBag.OccupiedRooms = occupiedRooms;
                    return View(customer);
                }
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account");
            }
            catch (DbUpdateException ex) // Catch specific database update exceptions
            {
                _logger.LogError(ex, "DbUpdateException occurred while saving customer registration.");
                // Log inner exception for more details
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    _logger.LogError($"Inner Exception: {inner.Message}");
                    inner = inner.InnerException;
                }
                ModelState.AddModelError("", $"An error occurred while saving the customer. Please check the provided data. Error: {ex.InnerException?.Message ?? ex.Message}");

                // Reload occupied rooms for the view in case of database errors
                try
                {
                    var currentUserId = GetCurrentUserId();
                    var occupiedRooms = _context.Customers
                        .Where(c => c.UserId == currentUserId && !c.IsCheckedOut)
                        .Select(c => c.RoomNumber)
                        .Where(rn => !string.IsNullOrEmpty(rn))
                        .ToList();
                    ViewBag.OccupiedRooms = occupiedRooms;
                }
                catch (UnauthorizedAccessException)
                {
                    return RedirectToAction("Login", "Account"); // Redirect if user session expires during error handling
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Error retrieving occupied rooms during DbUpdateException handling.");
                }
                return View(customer);
            }
            catch (Exception ex) // Catch any other unexpected errors
            {
                _logger.LogError(ex, "An unexpected error occurred during customer registration.");
                ModelState.AddModelError("", $"An unexpected error occurred: {ex.Message}");

                // Attempt to reload occupied rooms, but handle potential UnauthorizedAccessException
                try
                {
                    var currentUserId = GetCurrentUserId();
                    var occupiedRooms = _context.Customers
                        .Where(c => c.UserId == currentUserId && !c.IsCheckedOut)
                        .Select(c => c.RoomNumber)
                        .Where(rn => !string.IsNullOrEmpty(rn))
                        .ToList();
                    ViewBag.OccupiedRooms = occupiedRooms;
                }
                catch (UnauthorizedAccessException)
                {
                    return RedirectToAction("Login", "Account"); // Redirect if user session expires during error handling
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Error retrieving occupied rooms during general exception handling.");
                }
                return View(customer);
            }
        }

        // GET: Customer History (no changes needed here for the new fields, as they're just displayed)
        [HttpGet]
        public async Task<IActionResult> History(string filter)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var currentUserId = GetCurrentUserId();

                var customers = await _context.Customers
                    .Where(c => c.UserId == currentUserId)
                    .ToListAsync(); // Use ToListAsync for async operation

                if (filter == "in-hotel")
                {
                    customers = customers.Where(c => !c.IsCheckedOut).ToList();
                }
                else if (filter == "checkout")
                {
                    customers = customers.Where(c => c.IsCheckedOut).ToList();
                }

                _logger.LogInformation($"Retrieved {customers.Count} customers for user {currentUserId} History view with filter: {filter ?? "none"}");
                return View(customers);
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving customer history");
                return View(new List<Customer>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> ManageRoom()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var currentUserId = GetCurrentUserId();

                var customers = await _context.Customers
                    .Where(c => c.UserId == currentUserId && !c.IsCheckedOut)
                    .ToListAsync(); // Use ToListAsync for async operation
                _logger.LogInformation($"Retrieved {customers.Count} active customers for user {currentUserId} ManageRoom view");
                return View(customers);
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving customers for room management");
                return View(new List<Customer>());
            }
        }

        // GET: Checkout (no changes needed here for the new fields)
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var currentUserId = GetCurrentUserId();

                var checkedOutCustomers = await _context.Customers
                    .Where(c => c.UserId == currentUserId && c.IsCheckedOut)
                    .ToListAsync(); // Use ToListAsync for async operation
                _logger.LogInformation($"Retrieved {checkedOutCustomers.Count} checked out customers for user {currentUserId}");
                return View(checkedOutCustomers);
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving checked out customers");
                return View(new List<Customer>());
            }
        }

        // GET: Edit Customer
        [HttpGet]
        public async Task<IActionResult> Edit(int customerId)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var currentUserId = GetCurrentUserId();

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.UserId == currentUserId); // Use FirstOrDefaultAsync
                if (customer == null)
                {
                    _logger.LogWarning($"Customer with ID {customerId} not found for user {currentUserId} for editing");
                    return NotFound();
                }

                var occupiedRooms = await _context.Customers
                    .Where(c => c.UserId == currentUserId && !c.IsCheckedOut && c.CustomerId != customerId)
                    .Select(c => c.RoomNumber)
                    .Where(rn => !string.IsNullOrEmpty(rn))
                    .ToListAsync(); // Use ToListAsync
                ViewBag.OccupiedRooms = occupiedRooms;

                return View(customer);
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while retrieving customer {customerId} for editing");
                return NotFound();
            }
        }

        // POST: Edit Customer
        [HttpPost]
        [ValidateAntiForgeryToken] // Added for security
        public async Task<IActionResult> Edit(
            [Bind("CustomerId,Name,MobileNumber,Nationality,Gender,ID,Address,BedType,RoomType,RoomNumber,RatePerDay,BirthDate,CheckIn,CheckOut,IsCheckedOut")] Customer customer)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var currentUserId = GetCurrentUserId();

                _logger.LogInformation($"=== EDIT CUSTOMER DEBUG ===");
                _logger.LogInformation($"CustomerId: {customer.CustomerId}");
                _logger.LogInformation($"Name: {customer.Name}");
                _logger.LogInformation($"RoomNumber: {customer.RoomNumber}");
                _logger.LogInformation($"RatePerDay: {customer.RatePerDay}"); // Log the new property
                _logger.LogInformation($"CheckIn: {customer.CheckIn}");
                _logger.LogInformation($"CheckOut: {customer.CheckOut}");
                _logger.LogInformation($"IsCheckedOut: {customer.IsCheckedOut}");
                _logger.LogInformation($"Current UserId: {currentUserId}");
                _logger.LogInformation($"ModelState.IsValid (before custom validation): {ModelState.IsValid}");


                var existingCustomer = await _context.Customers
                    .AsNoTracking() // Use AsNoTracking as we're attaching a new entity or updating properties directly
                    .FirstOrDefaultAsync(c => c.CustomerId == customer.CustomerId && c.UserId == currentUserId);
                if (existingCustomer == null)
                {
                    _logger.LogWarning($"Customer {customer.CustomerId} not found or doesn't belong to user {currentUserId}");
                    TempData["Error"] = "Customer not found or you don't have permission to edit this customer.";
                    return NotFound();
                }

                ModelState.Remove("User"); // Remove User from ModelState validation

                // Check if the room is already occupied by another customer of current user
                if (!string.IsNullOrEmpty(customer.RoomNumber))
                {
                    var isRoomOccupied = await _context.Customers
                        .AnyAsync(c => c.UserId == currentUserId && c.RoomNumber == customer.RoomNumber && !c.IsCheckedOut && c.CustomerId != customer.CustomerId);

                    if (isRoomOccupied)
                    {
                        ModelState.AddModelError("RoomNumber", $"Room {customer.RoomNumber} is already occupied by another customer.");
                        _logger.LogWarning($"Attempted to assign already occupied room: {customer.RoomNumber} to customer {customer.CustomerId} for user {currentUserId}");
                    }
                }

                // Server-side validation for dates and RatePerDay
                if (customer.CheckIn == default(DateTime))
                {
                    ModelState.AddModelError("CheckIn", "Check In date and time is required.");
                }
                if (customer.CheckOut == default(DateTime))
                {
                    ModelState.AddModelError("CheckOut", "Check Out date and time is required.");
                }
                if (customer.CheckOut <= customer.CheckIn && customer.CheckOut != default(DateTime) && customer.CheckIn != default(DateTime))
                {
                    ModelState.AddModelError("CheckOut", "Check Out date and time must be after Check In date and time.");
                }
                if (customer.RatePerDay <= 0)
                {
                    ModelState.AddModelError("RatePerDay", "Rate per Day must be greater than zero.");
                }

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("=== EDIT VALIDATION ERRORS ===");
                    foreach (var modelError in ModelState)
                    {
                        var key = modelError.Key;
                        var errors = modelError.Value.Errors;
                        foreach (var error in errors)
                        {
                            _logger.LogWarning($"Field: {key}, Error: {error.ErrorMessage}");
                        }
                    }

                    // Reload occupied rooms for the view in case of validation errors
                    var occupiedRooms = await _context.Customers
                        .Where(c => c.UserId == currentUserId && !c.IsCheckedOut && c.CustomerId != customer.CustomerId)
                        .Select(c => c.RoomNumber)
                        .Where(rn => !string.IsNullOrEmpty(rn))
                        .ToListAsync();
                    ViewBag.OccupiedRooms = occupiedRooms;

                    return View(customer);
                }
               
                customer.UserId = currentUserId; // Always ensure the UserId matches the logged-in user

                // Recalculate TotalBill server-side before saving
                customer.TotalBill = CalculateTotalBill(customer.CheckIn, customer.CheckOut, customer.RatePerDay);

                // Attach the customer object to the context and mark it as modified
                _context.Update(customer); // This will attach the customer and mark it as Modified

                var result = await _context.SaveChangesAsync();
                _logger.LogInformation($"Customer {customer.CustomerId} update - SaveChanges returned: {result}");

                if (result > 0)
                {
                    _logger.LogInformation($"Customer {customer.CustomerId} updated successfully for user {currentUserId} with Total Bill: {customer.TotalBill}");
                    TempData["Success"] = $"Customer {customer.Name} has been updated successfully. Total Bill: {customer.TotalBill:C}";
                    return RedirectToAction("ManageRoom");
                }
                else
                {
                    _logger.LogWarning($"No changes were saved for customer {customer.CustomerId} during edit.");
                    ModelState.AddModelError("", "No changes were detected to save, or the update failed silently.");

                    var occupiedRooms = await _context.Customers
                        .Where(c => c.UserId == currentUserId && !c.IsCheckedOut && c.CustomerId != customer.CustomerId)
                        .Select(c => c.RoomNumber)
                        .Where(rn => !string.IsNullOrEmpty(rn))
                        .ToListAsync();
                    ViewBag.OccupiedRooms = occupiedRooms;

                    return View(customer);
                }
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account");
            }
            catch (DbUpdateException ex) // Catch specific database update exceptions
            {
                _logger.LogError(ex, $"DbUpdateException occurred while updating customer {customer.CustomerId}");
                // Log inner exception for more details
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    _logger.LogError($"Inner Exception: {inner.Message}");
                    inner = inner.InnerException;
                }
                ModelState.AddModelError("", $"An error occurred while updating the customer. Error: {ex.InnerException?.Message ?? ex.Message}");

                // Reload occupied rooms for the view in case of database errors
                try
                {
                    var currentUserId = GetCurrentUserId();
                    var occupiedRooms = await _context.Customers
                        .Where(c => c.UserId == currentUserId && !c.IsCheckedOut && c.CustomerId != customer.CustomerId)
                        .Select(c => c.RoomNumber)
                        .Where(rn => !string.IsNullOrEmpty(rn))
                        .ToListAsync();
                    ViewBag.OccupiedRooms = occupiedRooms;
                }
                catch (UnauthorizedAccessException)
                {
                    return RedirectToAction("Login", "Account"); // Redirect if user session expires during error handling
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Error retrieving occupied rooms during DbUpdateException handling for edit.");
                }
                return View(customer);
            }
            catch (Exception ex) // Catch any other unexpected errors
            {
                _logger.LogError(ex, $"An unexpected error occurred while updating customer {customer.CustomerId}");
                ModelState.AddModelError("", $"An unexpected error occurred: {ex.Message}");

                // Attempt to reload occupied rooms, but handle potential UnauthorizedAccessException
                try
                {
                    var currentUserId = GetCurrentUserId();
                    var occupiedRooms = await _context.Customers
                        .Where(c => c.UserId == currentUserId && !c.IsCheckedOut && c.CustomerId != customer.CustomerId)
                        .Select(c => c.RoomNumber)
                        .Where(rn => !string.IsNullOrEmpty(rn))
                        .ToListAsync();
                    ViewBag.OccupiedRooms = occupiedRooms;
                }
                catch (UnauthorizedAccessException)
                {
                    return RedirectToAction("Login", "Account"); // Redirect if user session expires during error handling
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Error retrieving occupied rooms during general exception handling for edit.");
                }
                return View(customer);
            }
        }

        // GET: Delete Customer (no changes needed here)
        [HttpGet]
        public async Task<IActionResult> Delete(int customerId)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var currentUserId = GetCurrentUserId();

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.UserId == currentUserId);
                if (customer == null)
                {
                    _logger.LogWarning($"Customer with ID {customerId} not found for user {currentUserId} for deletion");
                    return NotFound();
                }
                return View(customer);
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while retrieving customer {customerId} for deletion");
                return NotFound();
            }
        }

        // POST: Delete Customer
        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken] // Added for security
        public async Task<IActionResult> DeleteConfirmed(int CustomerId)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var currentUserId = GetCurrentUserId();

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerId == CustomerId && c.UserId == currentUserId);

                if (customer != null)
                {
                    _context.Customers.Remove(customer);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Customer {CustomerId} deleted successfully for user {currentUserId}");
                    TempData["Success"] = $"Customer {customer.Name} has been deleted successfully.";
                }
                else
                {
                    _logger.LogWarning($"Customer with ID {CustomerId} not found for user {currentUserId} for deletion");
                    TempData["Error"] = "Customer not found or you don't have permission to delete this customer.";
                }

                return RedirectToAction("ManageRoom");
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account");
            }
            catch (DbUpdateException ex) // Catch specific database update exceptions
            {
                _logger.LogError(ex, $"DbUpdateException occurred while deleting customer {CustomerId}");
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    _logger.LogError($"Inner Exception: {inner.Message}");
                    inner = inner.InnerException;
                }
                TempData["Error"] = $"An error occurred while deleting the customer: {ex.InnerException?.Message ?? ex.Message}";
                return RedirectToAction("ManageRoom");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred while deleting customer {CustomerId}");
                TempData["Error"] = "An unexpected error occurred while deleting the customer.";
                return RedirectToAction("ManageRoom");
            }
        }

        // POST: Checkout Customer
        [HttpPost]
        [ValidateAntiForgeryToken] // Added for security
        public async Task<IActionResult> CheckoutConfirmed(int CustomerId)
        {
            if (!IsUserLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var currentUserId = GetCurrentUserId();

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerId == CustomerId && c.UserId == currentUserId);

                if (customer != null)
                {
                    customer.IsCheckedOut = true;                    

                    _context.Update(customer);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Customer {CustomerId} checked out successfully for user {currentUserId}. Total Bill: {customer.TotalBill}");
                    TempData["Success"] = $"Customer {customer.Name} has been checked out successfully. Total Bill: {customer.TotalBill:C}"; // Display currency
                }
                else
                {
                    _logger.LogWarning($"Customer with ID {CustomerId} not found for user {currentUserId} for checkout");
                    TempData["Error"] = "Customer not found or you don't have permission to check out this customer.";
                }

                return RedirectToAction("ManageRoom");
            }
            catch (UnauthorizedAccessException)
            {
                return RedirectToAction("Login", "Account");
            }
            catch (DbUpdateException ex) // Catch specific database update exceptions
            {
                _logger.LogError(ex, $"DbUpdateException occurred while checking out customer {CustomerId}");
                Exception inner = ex.InnerException;
                while (inner != null)
                {
                    _logger.LogError($"Inner Exception: {inner.Message}");
                    inner = inner.InnerException;
                }
                TempData["Error"] = $"An error occurred during checkout: {ex.InnerException?.Message ?? ex.Message}";
                return RedirectToAction("ManageRoom");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred during checkout of customer {CustomerId}");
                TempData["Error"] = "An unexpected error occurred during checkout.";
                return RedirectToAction("ManageRoom");
            }
        }
        
        // Add this to your CustomerController

        [HttpPost]
        public async Task<IActionResult> ReactivateCustomer(int customerId)
        {
            try
            {
                // Debug: Log the received customerId
                System.Diagnostics.Debug.WriteLine($"Received customerId: {customerId}");

                // Check if customerId is valid
                if (customerId <= 0)
                {
                    TempData["ErrorMessage"] = "Invalid customer ID received.";
                    return RedirectToAction("Checkout");
                }

                // Find the customer by ID
                var customer = _context.Customers.FirstOrDefault(c => c.CustomerId == customerId);

                if (customer == null)
                {
                    TempData["ErrorMessage"] = $"Customer with ID {customerId} not found.";
                    return RedirectToAction("Checkout");
                }

                // Debug: Log customer details
                System.Diagnostics.Debug.WriteLine($"Found customer: {customer.Name}, IsCheckedOut: {customer.IsCheckedOut}");

                // Check if customer is already active
                if (!customer.IsCheckedOut)
                {
                    TempData["InfoMessage"] = $"Customer {customer.Name} is already active.";
                    return RedirectToAction("Checkout");
                }

                // Reactivate the customer
                customer.IsCheckedOut = false;
                
                // Update the database
                _context.Update(customer);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Customer {customer.Name} has been successfully reactivated.";

                return RedirectToAction("Checkout");
            }
            catch (Exception ex)
            {
                // Debug: Log the full exception
                System.Diagnostics.Debug.WriteLine($"Exception in ReactivateCustomer: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                TempData["ErrorMessage"] = $"An error occurred while reactivating the customer: {ex.Message}";
                return RedirectToAction("Checkout");
            }
        }

        // Alternative: Try using a different parameter name
        [HttpPost]
        public async Task<IActionResult> ReactivateCustomerAlt(int id)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"ReactivateCustomerAlt - Received id: {id}");

                var customer = _context.Customers.FirstOrDefault(c => c.CustomerId == id);

                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Customer not found.";
                    return RedirectToAction("Checkout");
                }

                customer.IsCheckedOut = false;
                _context.Update(customer);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Customer {customer.Name} reactivated successfully.";
                return RedirectToAction("Checkout");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction("Checkout");
            }
        }
    }
}