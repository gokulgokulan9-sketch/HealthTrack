using HealthTrack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    public class HomeController : Controller
    {
        private readonly HealthTrackDbContext _context;

        public HomeController(HealthTrackDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // DEFAULT HOME / ROLE-BASED DASHBOARD REDIRECTION
        // =========================================================

        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId.Value);

            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            if (user.Role == "ADMIN")
            {
                return RedirectToAction("Dashboard", "Home");
            }

            if (user.Role == "TRAINER")
            {
                return RedirectToAction("TrainerDashboard", "Home");
            }

            if (user.Role == "CLIENT")
            {
                return RedirectToAction("ClientDashboard", "Home");
            }

            HttpContext.Session.Clear();

            return RedirectToAction("Login", "Account");
        }


        // =========================================================
        // ADMIN DASHBOARD
        // URL:
        // /Home/Dashboard
        //
        // VIEW:
        // Views/Admin/Dashboard.cshtml
        // =========================================================

        public async Task<IActionResult> Dashboard()
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // -----------------------------------------------------
            // ADMIN INFORMATION
            // -----------------------------------------------------

            ViewBag.AdminName = RemoveDrPrefix(admin.FullName);
            ViewBag.AdminEmail = admin.Email;
            ViewBag.AdminRole = admin.Role;

            ViewBag.AdminProfileImage =
                string.IsNullOrEmpty(admin.ProfileImage)
                    ? "/images/default-user.png"
                    : GetProfileImage(admin.ProfileImage);


            // -----------------------------------------------------
            // DASHBOARD COUNTS
            // -----------------------------------------------------

            ViewBag.TotalUsers =
                await _context.Users
                    .CountAsync(u => u.Role == "CLIENT");

            ViewBag.TotalTrainers =
                await _context.Users
                    .CountAsync(u => u.Role == "TRAINER");

            ViewBag.TotalAdmins =
                await _context.Users
                    .CountAsync(u => u.Role == "ADMIN");

            ViewBag.TotalAppointments =
                await _context.Appointments.CountAsync();

            ViewBag.TotalWorkoutPlans =
                await _context.WorkoutPlans.CountAsync();

            ViewBag.TotalDietPlans =
                await _context.DietPlans.CountAsync();

            ViewBag.TotalNotifications =
                await _context.Notifications.CountAsync();

            ViewBag.TotalWorkoutLogs =
                await _context.WorkoutLogs.CountAsync();

            ViewBag.TotalMealLogs =
                await _context.MealLogs.CountAsync();

            ViewBag.TotalHealthMetrics =
                await _context.HealthMetrics.CountAsync();

            ViewBag.TotalHealthProfiles =
                await _context.HealthProfiles.CountAsync();


            // -----------------------------------------------------
            // COMMON USER INFORMATION
            // -----------------------------------------------------

            ViewBag.UserName =
                RemoveDrPrefix(admin.FullName);

            ViewBag.UserEmail =
                admin.Email;

            ViewBag.UserRole =
                admin.Role;

            ViewBag.ProfileImage =
                GetProfileImage(admin.ProfileImage);


            return View("~/Views/Admin/Dashboard.cshtml");
        }


        // =========================================================
        // ADMIN - USERS
        //
        // IMPORTANT:
        // OLD URL IS KEPT:
        // /Home/Users
        //
        // Sidebar can display "Clients"
        // but URL remains /Home/Users
        //
        // VIEW:
        // Views/Admin/Clients.cshtml
        // =========================================================

        public async Task<IActionResult> Users(string? search)
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // -----------------------------------------------------
            // GET CLIENTS ONLY
            // -----------------------------------------------------

            var query = _context.Users
                .Where(u => u.Role == "CLIENT")
                .AsQueryable();


            // -----------------------------------------------------
            // SEARCH
            // -----------------------------------------------------

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(u =>
                    u.FullName.Contains(search) ||
                    u.Email.Contains(search) ||
                    (u.Phone != null &&
                     u.Phone.Contains(search))
                );
            }


            // -----------------------------------------------------
            // CLIENT LIST
            // -----------------------------------------------------

            var clients = await query
                .OrderByDescending(u => u.UserId)
                .ToListAsync();


            // -----------------------------------------------------
            // ADMIN INFORMATION
            // -----------------------------------------------------

            SetAdminViewData(admin);


            // -----------------------------------------------------
            // CLIENT COUNTS
            // -----------------------------------------------------

            ViewBag.TotalClients =
                clients.Count;

            ViewBag.ActiveClients =
                clients.Count(u =>
                    !string.IsNullOrWhiteSpace(u.Status) &&
                    u.Status.Equals(
                        "Active",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            ViewBag.InactiveClients =
                clients.Count(u =>
                    string.IsNullOrWhiteSpace(u.Status) ||
                    !u.Status.Equals(
                        "Active",
                        StringComparison.OrdinalIgnoreCase
                    )
                );


            // -----------------------------------------------------
            // SEARCH VALUE
            // -----------------------------------------------------

            ViewBag.Search =
                search ?? "";


            // -----------------------------------------------------
            // NOTIFICATIONS
            // -----------------------------------------------------

            ViewBag.TotalNotifications =
                await _context.Notifications.CountAsync();


            // -----------------------------------------------------
            // SEND CLIENTS TO VIEW
            // -----------------------------------------------------

            ViewBag.Clients = clients;


            return View("~/Views/Admin/Clients.cshtml");
        }


        // =========================================================
        // ADMIN - CLIENTS
        //
        // URL:
        // /Home/Clients
        //
        // NOTE:
        // THIS IS THE ONLY Clients() METHOD.
        // This fixes AmbiguousMatchException.
        //
        // VIEW:
        // Views/Admin/Clients.cshtml
        // =========================================================

        public async Task<IActionResult> Clients(string? search)
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }


            // -----------------------------------------------------
            // GET CLIENTS
            // -----------------------------------------------------

            var query = _context.Users
                .Where(u => u.Role == "CLIENT")
                .AsQueryable();


            // -----------------------------------------------------
            // SEARCH
            // -----------------------------------------------------

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(u =>
                    u.FullName.Contains(search) ||
                    u.Email.Contains(search) ||
                    (u.Phone != null &&
                     u.Phone.Contains(search))
                );
            }


            // -----------------------------------------------------
            // CLIENT LIST
            // -----------------------------------------------------

            var clients = await query
                .OrderByDescending(u => u.UserId)
                .ToListAsync();


            // -----------------------------------------------------
            // ADMIN INFORMATION
            // -----------------------------------------------------

            SetAdminViewData(admin);


            // -----------------------------------------------------
            // CLIENT COUNTS
            // -----------------------------------------------------

            ViewBag.TotalClients =
                clients.Count;

            ViewBag.ActiveClients =
                clients.Count(u =>
                    !string.IsNullOrWhiteSpace(u.Status) &&
                    u.Status.Equals(
                        "Active",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            ViewBag.InactiveClients =
                clients.Count(u =>
                    string.IsNullOrWhiteSpace(u.Status) ||
                    !u.Status.Equals(
                        "Active",
                        StringComparison.OrdinalIgnoreCase
                    )
                );


            // -----------------------------------------------------
            // SEARCH
            // -----------------------------------------------------

            ViewBag.Search =
                search ?? "";


            // -----------------------------------------------------
            // NOTIFICATIONS
            // -----------------------------------------------------

            ViewBag.TotalNotifications =
                await _context.Notifications.CountAsync();


            // -----------------------------------------------------
            // CLIENT DATA
            // -----------------------------------------------------

            ViewBag.Clients =
                clients;


            return View("~/Views/Admin/Clients.cshtml");
        }


        

        // =========================================================
        // ADMIN - TRAINERS
        // URL:
        // /Home/Trainers
        //
        // VIEW:
        // Views/Admin/Trainers.cshtml
        // =========================================================

        public async Task<IActionResult> Trainers(string? search)
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // ---------------------------------------------------------
            // GET TRAINERS ONLY
            // ---------------------------------------------------------

            var query = _context.Users
                .Where(u => u.Role == "TRAINER")
                .AsQueryable();

            // ---------------------------------------------------------
            // SEARCH
            // ---------------------------------------------------------

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(u =>
                    u.FullName.Contains(search) ||
                    u.Email.Contains(search) ||
                    (u.Phone != null && u.Phone.Contains(search))
                );
            }

            // ---------------------------------------------------------
            // GET TRAINERS
            // ---------------------------------------------------------

            var trainers = await query
                .OrderByDescending(u => u.UserId)
                .ToListAsync();

            // ---------------------------------------------------------
            // ADMIN INFORMATION
            // ---------------------------------------------------------

            SetAdminViewData(admin);

            // ---------------------------------------------------------
            // COUNTS
            // ---------------------------------------------------------

            ViewBag.TotalTrainers = trainers.Count;

            ViewBag.ActiveTrainers = trainers.Count(u =>
                !string.IsNullOrWhiteSpace(u.Status) &&
                u.Status.Equals(
                    "Active",
                    StringComparison.OrdinalIgnoreCase
                )
            );

            ViewBag.InactiveTrainers = trainers.Count(u =>
                string.IsNullOrWhiteSpace(u.Status) ||
                !u.Status.Equals(
                    "Active",
                    StringComparison.OrdinalIgnoreCase
                )
            );

            ViewBag.Search = search ?? "";

            ViewBag.TotalNotifications =
                await _context.Notifications.CountAsync();

            // ---------------------------------------------------------
            // SEND DATA TO VIEW
            // ---------------------------------------------------------

            ViewBag.Trainers = trainers;

            return View(
                "~/Views/Admin/Trainers.cshtml",
                trainers
            );
        }


        // =========================================================
        // ADD TRAINER
        // POST:
        // /Home/AddTrainer
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTrainer(
            string FullName,
            string Email,
            string Phone,
            string Password,
            string Status)
        {
            var adminId =
                HttpContext.Session.GetInt32("UserId");

            if (adminId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            var admin =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.UserId == adminId.Value
                    );

            if (admin == null || admin.Role != "ADMIN")
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }

            // ---------------------------------------------------------
            // VALIDATION
            // ---------------------------------------------------------

            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password))
            {
                TempData["Error"] =
                    "Please fill in all required fields.";

                return RedirectToAction(nameof(Trainers));
            }

            if (Password.Length < 6)
            {
                TempData["Error"] =
                    "Password must contain at least 6 characters.";

                return RedirectToAction(nameof(Trainers));
            }

            // ---------------------------------------------------------
            // CHECK EMAIL
            // ---------------------------------------------------------

            var email =
                Email.Trim();

            var existing =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.Email == email
                    );

            if (existing != null)
            {
                TempData["Error"] =
                    "This email address is already registered.";

                return RedirectToAction(nameof(Trainers));
            }

            // ---------------------------------------------------------
            // CREATE TRAINER
            // ---------------------------------------------------------

            var trainer = new User
            {
                FullName =
                    FullName.Trim(),

                Email =
                    email,

                Phone =
                    string.IsNullOrWhiteSpace(Phone)
                        ? null
                        : Phone.Trim(),

                PasswordHash =
                    HashClientPassword(Password),

                Role =
                    "TRAINER",

                Status =
                    string.IsNullOrWhiteSpace(Status)
                        ? "Active"
                        : Status,

                CreatedAt =
                    DateTime.Now
            };

            _context.Users.Add(trainer);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Trainer account created successfully.";

            return RedirectToAction(nameof(Trainers));
        }


        // =========================================================
        // EDIT TRAINER
        // POST:
        // /Home/EditTrainer
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTrainer(
            int UserId,
            string FullName,
            string Email,
            string Phone,
            string Password,
            string Status)
        {
            var adminId =
                HttpContext.Session.GetInt32("UserId");

            if (adminId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            var admin =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.UserId == adminId.Value
                    );

            if (admin == null || admin.Role != "ADMIN")
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }

            // ---------------------------------------------------------
            // GET TRAINER
            // ---------------------------------------------------------

            var trainer =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u =>
                            u.UserId == UserId &&
                            u.Role == "TRAINER"
                    );

            if (trainer == null)
            {
                TempData["Error"] =
                    "Trainer was not found.";

                return RedirectToAction(nameof(Trainers));
            }

            // ---------------------------------------------------------
            // VALIDATION
            // ---------------------------------------------------------

            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Email))
            {
                TempData["Error"] =
                    "Name and email are required.";

                return RedirectToAction(nameof(Trainers));
            }

            FullName =
                FullName.Trim();

            Email =
                Email.Trim();

            // ---------------------------------------------------------
            // DUPLICATE EMAIL
            // ---------------------------------------------------------

            var duplicateEmail =
                await _context.Users
                    .AnyAsync(
                        u =>
                            u.Email == Email &&
                            u.UserId != UserId
                    );

            if (duplicateEmail)
            {
                TempData["Error"] =
                    "Another account already uses this email.";

                return RedirectToAction(nameof(Trainers));
            }

            // ---------------------------------------------------------
            // UPDATE BASIC INFORMATION
            // ---------------------------------------------------------

            trainer.FullName =
                FullName;

            trainer.Email =
                Email;

            trainer.Phone =
                string.IsNullOrWhiteSpace(Phone)
                    ? null
                    : Phone.Trim();

            trainer.Status =
                string.IsNullOrWhiteSpace(Status)
                    ? "Active"
                    : Status;

            // ---------------------------------------------------------
            // UPDATE PASSWORD ONLY IF ENTERED
            // ---------------------------------------------------------

            if (!string.IsNullOrWhiteSpace(Password))
            {
                if (Password.Length < 6)
                {
                    TempData["Error"] =
                        "Password must contain at least 6 characters.";

                    return RedirectToAction(nameof(Trainers));
                }

                trainer.PasswordHash =
                    HashClientPassword(Password);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Trainer updated successfully.";

            return RedirectToAction(nameof(Trainers));
        }


        // =========================================================
        // DELETE TRAINER
        // POST:
        // /Home/DeleteTrainer
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTrainer(
            int id)
        {
            var adminId =
                HttpContext.Session.GetInt32("UserId");

            if (adminId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            var admin =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.UserId == adminId.Value
                    );

            if (admin == null || admin.Role != "ADMIN")
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }

            // ---------------------------------------------------------
            // FIND TRAINER
            // ---------------------------------------------------------

            var trainer =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u =>
                            u.UserId == id &&
                            u.Role == "TRAINER"
                    );

            if (trainer == null)
            {
                TempData["Error"] =
                    "Trainer was not found.";

                return RedirectToAction(nameof(Trainers));
            }

            // ---------------------------------------------------------
            // DELETE
            // ---------------------------------------------------------

            try
            {
                _context.Users.Remove(trainer);

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Trainer deleted successfully.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "This trainer cannot be deleted because related records exist. Please deactivate the trainer instead.";
            }

            return RedirectToAction(nameof(Trainers));
        }


        // =========================================================
        // ADMIN - APPOINTMENTS
        // URL:
        // /Home/Appointments
        // =========================================================

        public async Task<IActionResult> Appointments()
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            SetAdminViewData(admin);

            ViewBag.TotalAppointments =
                await _context.Appointments.CountAsync();


            return View(
                "~/Views/Admin/Appointments.cshtml"
            );
        }


        // =========================================================
        // ADMIN - WORKOUT PLANS
        // URL:
        // /Home/WorkoutPlans
        // =========================================================

        public async Task<IActionResult> WorkoutPlans()
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            SetAdminViewData(admin);

            ViewBag.TotalWorkoutPlans =
                await _context.WorkoutPlans.CountAsync();

            ViewBag.TotalWorkoutLogs =
                await _context.WorkoutLogs.CountAsync();


            return View(
                "~/Views/Admin/WorkoutPlans.cshtml"
            );
        }


        // =========================================================
        // ADMIN - DIET PLANS
        // URL:
        // /Home/DietPlans
        // =========================================================

        public async Task<IActionResult> DietPlans()
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            SetAdminViewData(admin);

            ViewBag.TotalDietPlans =
                await _context.DietPlans.CountAsync();

            ViewBag.TotalMealLogs =
                await _context.MealLogs.CountAsync();


            return View(
                "~/Views/Admin/DietPlans.cshtml"
            );
        }


        // =========================================================
        // ADMIN - HEALTH METRICS
        // URL:
        // /Home/HealthMetrics
        // =========================================================

        public async Task<IActionResult> HealthMetrics()
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            SetAdminViewData(admin);

            ViewBag.TotalHealthMetrics =
                await _context.HealthMetrics.CountAsync();

            ViewBag.TotalHealthProfiles =
                await _context.HealthProfiles.CountAsync();


            return View(
                "~/Views/Admin/HealthMetrics.cshtml"
            );
        }


        // =========================================================
        // ADMIN - REPORTS
        // URL:
        // /Home/Reports
        // =========================================================

        public async Task<IActionResult> Reports()
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            SetAdminViewData(admin);


            // -----------------------------------------------------
            // REPORT DATA
            // -----------------------------------------------------

            ViewBag.TotalClients =
                await _context.Users
                    .CountAsync(u => u.Role == "CLIENT");

            ViewBag.TotalTrainers =
                await _context.Users
                    .CountAsync(u => u.Role == "TRAINER");

            ViewBag.TotalAppointments =
                await _context.Appointments.CountAsync();

            ViewBag.TotalWorkoutPlans =
                await _context.WorkoutPlans.CountAsync();

            ViewBag.TotalDietPlans =
                await _context.DietPlans.CountAsync();

            ViewBag.TotalWorkoutLogs =
                await _context.WorkoutLogs.CountAsync();

            ViewBag.TotalMealLogs =
                await _context.MealLogs.CountAsync();

            ViewBag.TotalHealthMetrics =
                await _context.HealthMetrics.CountAsync();


            return View(
                "~/Views/Admin/Reports.cshtml"
            );
        }


        // =========================================================
        // ADMIN - NOTIFICATIONS
        // URL:
        // /Home/Notifications
        // =========================================================

        public async Task<IActionResult> Notifications()
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            SetAdminViewData(admin);

            ViewBag.TotalNotifications =
                await _context.Notifications.CountAsync();


            return View(
                "~/Views/Admin/Notifications.cshtml"
            );
        }


        // =========================================================
        // ADMIN - SETTINGS
        // URL:
        // /Home/Settings
        // =========================================================

        public async Task<IActionResult> Settings()
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            SetAdminViewData(admin);


            return View(
                "~/Views/Admin/Settings.cshtml"
            );
        }


        // =========================================================
        // TRAINER DASHBOARD
        // URL:
        // /Home/TrainerDashboard
        // =========================================================

        public async Task<IActionResult> TrainerDashboard()
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            var trainer =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.UserId == userId.Value
                    );


            if (trainer == null)
            {
                HttpContext.Session.Clear();

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            if (trainer.Role != "TRAINER")
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            ViewBag.TrainerName =
                RemoveDrPrefix(trainer.FullName);

            ViewBag.TrainerEmail =
                trainer.Email;

            ViewBag.TrainerRole =
                trainer.Role;

            ViewBag.TrainerProfileImage =
                GetProfileImage(
                    trainer.ProfileImage
                );


            ViewBag.UserName =
                RemoveDrPrefix(trainer.FullName);

            ViewBag.UserEmail =
                trainer.Email;

            ViewBag.UserRole =
                trainer.Role;

            ViewBag.ProfileImage =
                GetProfileImage(
                    trainer.ProfileImage
                );


            // -----------------------------------------------------
            // TRAINER DASHBOARD COUNTS
            // -----------------------------------------------------

            ViewBag.TotalClients =
                await _context.Users
                    .CountAsync(
                        u => u.Role == "CLIENT"
                    );

            ViewBag.TotalAppointments =
                await _context.Appointments.CountAsync();

            ViewBag.TotalWorkoutPlans =
                await _context.WorkoutPlans.CountAsync();

            ViewBag.TotalDietPlans =
                await _context.DietPlans.CountAsync();

            ViewBag.TotalNotifications =
                await _context.Notifications.CountAsync();

            ViewBag.TotalWorkoutLogs =
                await _context.WorkoutLogs.CountAsync();

            ViewBag.TotalMealLogs =
                await _context.MealLogs.CountAsync();

            ViewBag.TotalHealthMetrics =
                await _context.HealthMetrics.CountAsync();


            return View(
                "~/Views/Trainer/TrainerDashboard.cshtml"
            );
        }


        // =========================================================
        // CLIENT DASHBOARD
        // URL:
        // /Home/ClientDashboard
        // =========================================================

        public async Task<IActionResult> ClientDashboard()
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            var client =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.UserId == userId.Value
                    );


            if (client == null)
            {
                HttpContext.Session.Clear();

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            if (client.Role != "CLIENT")
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            ViewBag.ClientName =
                RemoveDrPrefix(client.FullName);

            ViewBag.ClientEmail =
                client.Email;

            ViewBag.ClientRole =
                client.Role;

            ViewBag.ClientProfileImage =
                GetProfileImage(
                    client.ProfileImage
                );


            ViewBag.UserName =
                RemoveDrPrefix(client.FullName);

            ViewBag.UserEmail =
                client.Email;

            ViewBag.UserRole =
                client.Role;

            ViewBag.ProfileImage =
                GetProfileImage(
                    client.ProfileImage
                );


            // -----------------------------------------------------
            // CLIENT DASHBOARD COUNTS
            // -----------------------------------------------------

            ViewBag.TotalWorkoutPlans =
                await _context.WorkoutPlans.CountAsync();

            ViewBag.TotalWorkoutLogs =
                await _context.WorkoutLogs.CountAsync();

            ViewBag.TotalDietPlans =
                await _context.DietPlans.CountAsync();

            ViewBag.TotalMealLogs =
                await _context.MealLogs.CountAsync();

            ViewBag.TotalAppointments =
                await _context.Appointments.CountAsync();

            ViewBag.TotalHealthMetrics =
                await _context.HealthMetrics.CountAsync();

            ViewBag.TotalHealthProfiles =
                await _context.HealthProfiles.CountAsync();

            ViewBag.TotalNotifications =
                await _context.Notifications.CountAsync();


            return View(
                "~/Views/Client/ClientDashboard.cshtml"
            );
        }


        // =========================================================
        // ADD CLIENT
        // POST:
        // /Home/AddClient
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddClient(
            string FullName,
            string Email,
            string Phone,
            string Password,
            string Status)
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            var admin =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.UserId == userId.Value
                    );


            if (admin == null ||
                admin.Role != "ADMIN")
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            // -----------------------------------------------------
            // VALIDATION
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password))
            {
                TempData["Error"] =
                    "Please fill in all required fields.";

                return RedirectToAction(
                    nameof(Clients)
                );
            }


            if (Password.Length < 6)
            {
                TempData["Error"] =
                    "Password must contain at least 6 characters.";

                return RedirectToAction(
                    nameof(Clients)
                );
            }


            // -----------------------------------------------------
            // CHECK EMAIL
            // -----------------------------------------------------

            var existing =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.Email == Email.Trim()
                    );


            if (existing != null)
            {
                TempData["Error"] =
                    "This email address is already registered.";

                return RedirectToAction(
                    nameof(Clients)
                );
            }


            // -----------------------------------------------------
            // CREATE CLIENT
            // -----------------------------------------------------

            var client = new User
            {
                FullName =
                    FullName.Trim(),

                Email =
                    Email.Trim(),

                Phone =
                    string.IsNullOrWhiteSpace(Phone)
                        ? null
                        : Phone.Trim(),

                PasswordHash =
                    HashClientPassword(
                        Password
                    ),

                Role =
                    "CLIENT",

                Status =
                    string.IsNullOrWhiteSpace(Status)
                        ? "Active"
                        : Status,

                CreatedAt =
                    DateTime.Now
            };


            _context.Users.Add(client);

            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Client account created successfully.";


            return RedirectToAction(
                nameof(Clients)
            );
        }


        // =========================================================
        // EDIT CLIENT
        // POST:
        // /Home/EditClient
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditClient(
            int UserId,
            string FullName,
            string Email,
            string Phone,
            string Password,
            string Status)
        {
            var adminId =
                HttpContext.Session.GetInt32("UserId");


            if (adminId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            var admin =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.UserId == adminId.Value
                    );


            if (admin == null ||
                admin.Role != "ADMIN")
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            // -----------------------------------------------------
            // GET CLIENT
            // -----------------------------------------------------

            var client =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u =>
                            u.UserId == UserId &&
                            u.Role == "CLIENT"
                    );


            if (client == null)
            {
                TempData["Error"] =
                    "Client was not found.";

                return RedirectToAction(
                    nameof(Clients)
                );
            }


            // -----------------------------------------------------
            // VALIDATION
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Email))
            {
                TempData["Error"] =
                    "Name and email are required.";

                return RedirectToAction(
                    nameof(Clients)
                );
            }


            FullName =
                FullName.Trim();

            Email =
                Email.Trim();


            // -----------------------------------------------------
            // EMAIL DUPLICATE CHECK
            // -----------------------------------------------------

            var duplicateEmail =
                await _context.Users
                    .AnyAsync(
                        u =>
                            u.Email == Email &&
                            u.UserId != UserId
                    );


            if (duplicateEmail)
            {
                TempData["Error"] =
                    "Another account already uses this email.";

                return RedirectToAction(
                    nameof(Clients)
                );
            }


            // -----------------------------------------------------
            // UPDATE BASIC INFORMATION
            // -----------------------------------------------------

            client.FullName =
                FullName;

            client.Email =
                Email;

            client.Phone =
                string.IsNullOrWhiteSpace(Phone)
                    ? null
                    : Phone.Trim();

            client.Status =
                string.IsNullOrWhiteSpace(Status)
                    ? "Active"
                    : Status;


            // -----------------------------------------------------
            // UPDATE PASSWORD ONLY IF ENTERED
            // -----------------------------------------------------

            if (!string.IsNullOrWhiteSpace(Password))
            {
                if (Password.Length < 6)
                {
                    TempData["Error"] =
                        "Password must contain at least 6 characters.";

                    return RedirectToAction(
                        nameof(Clients)
                    );
                }


                client.PasswordHash =
                    HashClientPassword(
                        Password
                    );
            }


            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Client updated successfully.";


            return RedirectToAction(
                nameof(Clients)
            );
        }


        // =========================================================
        // DELETE CLIENT
        // POST:
        // /Home/DeleteClient
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteClient(
            int id)
        {
            var adminId =
                HttpContext.Session.GetInt32("UserId");


            if (adminId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            var admin =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.UserId == adminId.Value
                    );


            if (admin == null ||
                admin.Role != "ADMIN")
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            // -----------------------------------------------------
            // FIND CLIENT
            // -----------------------------------------------------

            var client =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u =>
                            u.UserId == id &&
                            u.Role == "CLIENT"
                    );


            if (client == null)
            {
                TempData["Error"] =
                    "Client was not found.";

                return RedirectToAction(
                    nameof(Clients)
                );
            }


            // -----------------------------------------------------
            // DELETE
            // -----------------------------------------------------

            try
            {
                _context.Users.Remove(client);

                await _context.SaveChangesAsync();


                TempData["Success"] =
                    "Client deleted successfully.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "This client cannot be deleted because related records exist. Please deactivate the client instead.";
            }


            return RedirectToAction(
                nameof(Clients)
            );
        }


        // =========================================================
        // ADMIN AUTHENTICATION HELPER
        // =========================================================

        private async Task<User?> GetLoggedInAdmin()
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");


            if (userId == null)
            {
                return null;
            }


            var admin =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.UserId == userId.Value
                    );


            if (admin == null)
            {
                HttpContext.Session.Clear();

                return null;
            }


            if (admin.Role != "ADMIN")
            {
                return null;
            }


            return admin;
        }


        // =========================================================
        // ADMIN VIEW DATA HELPER
        // =========================================================

        private void SetAdminViewData(User admin)
        {
            ViewBag.UserName =
                RemoveDrPrefix(admin.FullName);

            ViewBag.UserEmail =
                admin.Email;

            ViewBag.UserRole =
                admin.Role;

            ViewBag.ProfileImage =
                GetProfileImage(
                    admin.ProfileImage
                );

            ViewBag.AdminName =
                RemoveDrPrefix(admin.FullName);

            ViewBag.AdminEmail =
                admin.Email;

            ViewBag.AdminRole =
                admin.Role;

            ViewBag.AdminProfileImage =
                GetProfileImage(
                    admin.ProfileImage
                );
        }


        // =========================================================
        // PROFILE IMAGE HELPER
        // =========================================================

        private static string GetProfileImage(
            string? profileImage)
        {
            if (string.IsNullOrWhiteSpace(profileImage))
            {
                return "/images/default-user.png";
            }


            if (profileImage.StartsWith("/"))
            {
                return profileImage;
            }


            return "/images/" + profileImage;
        }


        // =========================================================
        // REMOVE "DR." PREFIX
        // =========================================================

        private static string RemoveDrPrefix(
            string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "User";
            }


            name =
                name.Trim();


            if (name.StartsWith(
                "Dr. ",
                StringComparison.OrdinalIgnoreCase))
            {
                return name
                    .Substring(4)
                    .Trim();
            }


            if (name.StartsWith(
                "Dr ",
                StringComparison.OrdinalIgnoreCase))
            {
                return name
                    .Substring(3)
                    .Trim();
            }


            return name;
        }


        // =========================================================
        // PASSWORD HASH
        // SAME FORMAT USED BY ACCOUNTCONTROLLER
        // =========================================================

        private static string HashClientPassword(
            string password)
        {
            byte[] salt =
                System.Security.Cryptography
                    .RandomNumberGenerator
                    .GetBytes(16);


            byte[] hash =
                System.Security.Cryptography
                    .Rfc2898DeriveBytes.Pbkdf2(
                        password,
                        salt,
                        100000,
                        System.Security.Cryptography
                            .HashAlgorithmName
                            .SHA256,
                        32
                    );


            return Convert.ToBase64String(salt)
                   + "."
                   + Convert.ToBase64String(hash);
        }


        // =========================================================
        // PRIVACY
        // =========================================================

        public IActionResult Privacy()
        {
            return View();
        }
    }
}