using HealthTrack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace HealthTrack.Controllers
{
    public class AccountController : Controller
    {
        private readonly HealthTrackDbContext _context;

        public AccountController(HealthTrackDbContext context)
        {
            _context = context;
        }

        // =========================================
        // LOGIN - GET
        // =========================================
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }


        // =========================================
        // LOGIN - POST
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Please enter your email and password.";
                return View();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                ViewBag.Error = "Invalid email or password.";
                return View();
            }

            if (user.Status != "Active")
            {
                ViewBag.Error = "Your account is not active.";
                return View();
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                ViewBag.Error = "Invalid email or password.";
                return View();
            }


            // =========================================
            // SAVE LOGGED-IN USER INFORMATION
            // =========================================

            HttpContext.Session.SetInt32(
                "UserId",
                user.UserId
            );

            HttpContext.Session.SetString(
                "FullName",
                user.FullName ?? ""
            );

            HttpContext.Session.SetString(
                "Email",
                user.Email ?? ""
            );

            HttpContext.Session.SetString(
                "Role",
                user.Role ?? ""
            );

            HttpContext.Session.SetString(
                "ProfileImage",
                user.ProfileImage ?? ""
            );


            // =========================================
            // ROLE BASED REDIRECTION
            // =========================================

            if (user.Role == "ADMIN")
            {
                return RedirectToAction(
                    "Dashboard",
                    "Home"
                );
            }

            if (user.Role == "TRAINER")
            {
                return RedirectToAction(
                    "TrainerDashboard",
                    "Home"
                );
            }

            if (user.Role == "CLIENT")
            {
                return RedirectToAction(_
                    "ClientDashboard",
                    "Home"
                );
            }


            // Unknown role
            HttpContext.Session.Clear();

            ViewBag.Error = "Invalid user role.";

            return View();
        }


        // =========================================
        // REGISTER - GET
        // =========================================
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }


        // =========================================
        // REGISTER - POST
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string fullName,
            string email,
            string phone,
            string password,
            string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                ViewBag.Error = "Please fill in all fields.";
                return View();
            }


            if (password != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                return View();
            }


            if (password.Length < 6)
            {
                ViewBag.Error =
                    "Password must contain at least 6 characters.";

                return View();
            }


            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (existingUser != null)
            {
                ViewBag.Error =
                    "An account with this email already exists.";

                return View();
            }


            var user = new User
            {
                FullName = fullName,
                Email = email,

                PasswordHash = HashPassword(password),

                Role = "CLIENT",

                Phone = phone,

                Status = "Active"
            };


            _context.Users.Add(user);

            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Registration successful. Please login with your new account.";


            return RedirectToAction("Login");
        }


        // =========================================
        // LOGOUT
        // =========================================
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            return RedirectToAction("Login");
        }


        // =========================================
        // PASSWORD HASH
        // =========================================
        private static string HashPassword(string password)
        {
            byte[] salt =
                RandomNumberGenerator.GetBytes(16);


            byte[] hash =
                Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    100000,
                    HashAlgorithmName.SHA256,
                    32
                );


            return Convert.ToBase64String(salt)
                   + "."
                   + Convert.ToBase64String(hash);
        }


        // =========================================
        // PASSWORD VERIFY
        // =========================================
        private static bool VerifyPassword(
            string password,
            string storedPassword)
        {
            try
            {
                var parts =
                    storedPassword.Split('.');


                if (parts.Length != 2)
                    return false;


                byte[] salt =
                    Convert.FromBase64String(parts[0]);


                byte[] storedHash =
                    Convert.FromBase64String(parts[1]);


                byte[] hash =
                    Rfc2898DeriveBytes.Pbkdf2(
                        password,
                        salt,
                        100000,
                        HashAlgorithmName.SHA256,
                        32
                    );


                return CryptographicOperations
                    .FixedTimeEquals(
                        hash,
                        storedHash
                    );
            }
            catch
            {
                return false;
            }
        }
    }
}