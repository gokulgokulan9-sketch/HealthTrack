using HealthTrack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Collections.Concurrent;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HealthTrack.Controllers
{
    public class HomeController : Controller
    {
        private readonly HealthTrackDbContext _context;

        private static readonly ConcurrentDictionary<string, List<CallSignalPacket>> _callSignals = new();

        private sealed class CallSignalPacket
        {
            public int SenderId { get; set; }
            public int ReceiverId { get; set; }
            public string RoomId { get; set; } = "";
            public string CallType { get; set; } = "";
            public string SignalType { get; set; } = "";
            public string Payload { get; set; } = "";
            public DateTime CreatedAt { get; set; }
        }

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
                await _context.Notifications.CountAsync(
                    n => n.IsRead != true
                );

            ViewBag.UnreadNotifications =
                ViewBag.TotalNotifications;

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
                await _context.Notifications.CountAsync(
                    n => n.IsRead != true
                );


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
                await _context.Notifications.CountAsync(
                    n => n.IsRead != true
                );


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
                await _context.Notifications.CountAsync(
                    n => n.IsRead != true
                );

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
        //
        // VIEW:
        // Views/Admin/Appointments.cshtml
        // =========================================================

        public async Task<IActionResult> Appointments(string? search)
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // ---------------------------------------------------------
            // APPOINTMENTS
            // ---------------------------------------------------------

            var query = _context.Appointments
                .Include(a => a.Client)
                .Include(a => a.Trainer)
                .AsQueryable();

            // ---------------------------------------------------------
            // SEARCH
            // ---------------------------------------------------------

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(a =>
                    (a.Client != null &&
                     a.Client.FullName.Contains(search))
                    ||
                    (a.Trainer != null &&
                     a.Trainer.FullName.Contains(search))
                    ||
                    (a.Reason != null &&
                     a.Reason.Contains(search))
                    ||
                    (a.Status != null &&
                     a.Status.Contains(search))
                );
            }

            // ---------------------------------------------------------
            // GET APPOINTMENTS
            // ---------------------------------------------------------

            var appointments = await query
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.AppointmentTime)
                .ToListAsync();

            // ---------------------------------------------------------
            // CLIENTS
            // ---------------------------------------------------------

            var clients = await _context.Users
                .Where(u => u.Role == "CLIENT")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            // ---------------------------------------------------------
            // TRAINERS
            // ---------------------------------------------------------

            var trainers = await _context.Users
                .Where(u => u.Role == "TRAINER")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            // ---------------------------------------------------------
            // ADMIN INFORMATION
            // ---------------------------------------------------------

            SetAdminViewData(admin);

            // ---------------------------------------------------------
            // COUNTS
            // ---------------------------------------------------------

            ViewBag.TotalAppointments =
                appointments.Count;

            ViewBag.PendingAppointments =
                appointments.Count(a =>
                    string.Equals(
                        a.Status,
                        "Pending",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            ViewBag.ConfirmedAppointments =
                appointments.Count(a =>
                    string.Equals(
                        a.Status,
                        "Confirmed",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            ViewBag.CompletedAppointments =
                appointments.Count(a =>
                    string.Equals(
                        a.Status,
                        "Completed",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            ViewBag.CancelledAppointments =
                appointments.Count(a =>
                    string.Equals(
                        a.Status,
                        "Cancelled",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            // ---------------------------------------------------------
            // SEARCH
            // ---------------------------------------------------------

            ViewBag.Search =
                search ?? "";

            // ---------------------------------------------------------
            // NOTIFICATIONS
            // ---------------------------------------------------------

            ViewBag.TotalNotifications =
                await _context.Notifications.CountAsync(
                    n => n.IsRead != true
                );

            // ---------------------------------------------------------
            // SEND DATA TO VIEW
            // ---------------------------------------------------------

            ViewBag.Appointments =
                appointments;

            ViewBag.Clients =
                clients;

            ViewBag.Trainers =
                trainers;

            return View(
                "~/Views/Admin/Appointments.cshtml"
            );
        }

        // =========================================================
        // ADD APPOINTMENT
        // POST: /Home/AddAppointment
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAppointment(
            int ClientId,
            int TrainerId,
            string AppointmentDate,
            string AppointmentTime,
            string? Reason,
            string? Status,
            string? Notes)
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // -----------------------------
            // REQUIRED FIELDS
            // -----------------------------

            if (ClientId <= 0 ||
                TrainerId <= 0 ||
                string.IsNullOrWhiteSpace(AppointmentDate) ||
                string.IsNullOrWhiteSpace(AppointmentTime))
            {
                TempData["Error"] =
                    "Please fill in all required appointment fields.";

                return RedirectToAction(nameof(Appointments));
            }

            // -----------------------------
            // DATE
            // -----------------------------

            if (!DateOnly.TryParse(
                AppointmentDate,
                out DateOnly appointmentDate))
            {
                TempData["Error"] = "Invalid appointment date.";

                return RedirectToAction(nameof(Appointments));
            }

            // -----------------------------
            // TIME
            // -----------------------------

            if (!TimeOnly.TryParse(
                AppointmentTime,
                out TimeOnly appointmentTime))
            {
                TempData["Error"] = "Invalid appointment time.";

                return RedirectToAction(nameof(Appointments));
            }

            // -----------------------------
            // CLIENT
            // -----------------------------

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == ClientId &&
                    u.Role == "CLIENT");

            if (client == null)
            {
                TempData["Error"] =
                    "Selected client was not found.";

                return RedirectToAction(nameof(Appointments));
            }

            // -----------------------------
            // TRAINER
            // -----------------------------

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == TrainerId &&
                    u.Role == "TRAINER");

            if (trainer == null)
            {
                TempData["Error"] =
                    "Selected trainer was not found.";

                return RedirectToAction(nameof(Appointments));
            }

            // -----------------------------
            // CHECK DUPLICATE APPOINTMENT
            // -----------------------------
            // IMPORTANT:
            // Do not use StringComparison inside EF query.
            // -----------------------------

            var existingAppointments =
                await _context.Appointments
                    .Where(a =>
                        a.TrainerId == TrainerId &&
                        a.AppointmentDate == appointmentDate &&
                        a.AppointmentTime == appointmentTime)
                    .Select(a => a.Status)
                    .ToListAsync();

            bool alreadyBooked =
                existingAppointments.Any(status =>
                    !string.Equals(
                        status,
                        "Cancelled",
                        StringComparison.OrdinalIgnoreCase));

            if (alreadyBooked)
            {
                TempData["Error"] =
                    "This trainer already has an appointment at the selected date and time.";

                return RedirectToAction(nameof(Appointments));
            }

            // -----------------------------
            // CREATE APPOINTMENT
            // -----------------------------

            var appointment = new Appointment
            {
                ClientId = ClientId,
                TrainerId = TrainerId,

                AppointmentDate =
                    appointmentDate,

                AppointmentTime =
                    appointmentTime,

                Reason =
                    string.IsNullOrWhiteSpace(Reason)
                        ? null
                        : Reason.Trim(),

                Status =
                    string.IsNullOrWhiteSpace(Status)
                        ? "Pending"
                        : Status.Trim(),

                Notes =
                    string.IsNullOrWhiteSpace(Notes)
                        ? null
                        : Notes.Trim(),

                CreatedAt = DateTime.Now
            };

            _context.Appointments.Add(appointment);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Appointment created successfully.";

            return RedirectToAction(nameof(Appointments));
        }


        // =========================================================
        // EDIT APPOINTMENT
        // POST: /Home/EditAppointment
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAppointment(
            int AppointmentId,
            int ClientId,
            int TrainerId,
            string AppointmentDate,
            string AppointmentTime,
            string? Reason,
            string? Status,
            string? Notes)
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // -----------------------------
            // FIND APPOINTMENT
            // -----------------------------

            var appointment =
                await _context.Appointments
                    .FirstOrDefaultAsync(a =>
                        a.AppointmentId == AppointmentId);

            if (appointment == null)
            {
                TempData["Error"] =
                    "Appointment was not found.";

                return RedirectToAction(nameof(Appointments));
            }

            // -----------------------------
            // REQUIRED FIELDS
            // -----------------------------

            if (ClientId <= 0 ||
                TrainerId <= 0 ||
                string.IsNullOrWhiteSpace(AppointmentDate) ||
                string.IsNullOrWhiteSpace(AppointmentTime))
            {
                TempData["Error"] =
                    "Please fill in all required appointment fields.";

                return RedirectToAction(nameof(Appointments));
            }

            // -----------------------------
            // DATE
            // -----------------------------

            if (!DateOnly.TryParse(
                AppointmentDate,
                out DateOnly appointmentDate))
            {
                TempData["Error"] =
                    "Invalid appointment date.";

                return RedirectToAction(nameof(Appointments));
            }

            // -----------------------------
            // TIME
            // -----------------------------

            if (!TimeOnly.TryParse(
                AppointmentTime,
                out TimeOnly appointmentTime))
            {
                TempData["Error"] =
                    "Invalid appointment time.";

                return RedirectToAction(nameof(Appointments));
            }

            // -----------------------------
            // CLIENT CHECK
            // -----------------------------

            bool clientExists =
                await _context.Users.AnyAsync(u =>
                    u.UserId == ClientId &&
                    u.Role == "CLIENT");

            if (!clientExists)
            {
                TempData["Error"] =
                    "Selected client was not found.";

                return RedirectToAction(nameof(Appointments));
            }

            // -----------------------------
            // TRAINER CHECK
            // -----------------------------

            bool trainerExists =
                await _context.Users.AnyAsync(u =>
                    u.UserId == TrainerId &&
                    u.Role == "TRAINER");

            if (!trainerExists)
            {
                TempData["Error"] =
                    "Selected trainer was not found.";

                return RedirectToAction(nameof(Appointments));
            }

            // -----------------------------
            // CHECK DUPLICATE
            // -----------------------------
            // First get matching records
            // from SQL Server.
            // Then compare Status in C#.
            // -----------------------------

            var matchingAppointments =
                await _context.Appointments
                    .Where(a =>
                        a.AppointmentId != AppointmentId &&
                        a.TrainerId == TrainerId &&
                        a.AppointmentDate == appointmentDate &&
                        a.AppointmentTime == appointmentTime)
                    .Select(a => a.Status)
                    .ToListAsync();

            bool alreadyBooked =
                matchingAppointments.Any(status =>
                    !string.Equals(
                        status,
                        "Cancelled",
                        StringComparison.OrdinalIgnoreCase));

            if (alreadyBooked)
            {
                TempData["Error"] =
                    "This trainer already has another appointment at the selected date and time.";

                return RedirectToAction(nameof(Appointments));
            }

            // -----------------------------
            // UPDATE
            // -----------------------------

            appointment.ClientId =
                ClientId;

            appointment.TrainerId =
                TrainerId;

            appointment.AppointmentDate =
                appointmentDate;

            appointment.AppointmentTime =
                appointmentTime;

            appointment.Reason =
                string.IsNullOrWhiteSpace(Reason)
                    ? null
                    : Reason.Trim();

            appointment.Status =
                string.IsNullOrWhiteSpace(Status)
                    ? "Pending"
                    : Status.Trim();

            appointment.Notes =
                string.IsNullOrWhiteSpace(Notes)
                    ? null
                    : Notes.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Appointment updated successfully.";

            return RedirectToAction(nameof(Appointments));
        }

        // =========================================================
        // DELETE APPOINTMENT
        // POST:
        // /Home/DeleteAppointment
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAppointment(
            int id)
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            // ---------------------------------------------------------
            // FIND APPOINTMENT
            // ---------------------------------------------------------

            var appointment =
                await _context.Appointments
                    .FirstOrDefaultAsync(
                        a =>
                            a.AppointmentId == id
                    );

            if (appointment == null)
            {
                TempData["Error"] =
                    "Appointment was not found.";

                return RedirectToAction(
                    nameof(Appointments)
                );
            }

            // ---------------------------------------------------------
            // DELETE
            // ---------------------------------------------------------

            try
            {
                _context.Appointments.Remove(
                    appointment
                );

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Appointment deleted successfully.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "This appointment cannot be deleted because it is being used by another record.";
            }

            return RedirectToAction(
                nameof(Appointments)
            );
        }
        // ============================================================
        // ADMIN - WORKOUT & DIET PLANS
        // ============================================================

        private readonly string _connectionString =
            @"Server=localhost\SQLEXPRESS;Database=HealthTrackDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

        // ============================================================
        // ADMIN - WORKOUT PLANS
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> WorkoutPlans(string? search)
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
                return RedirectToAction("Login", "Account");

            var clients = await _context.Users
                .Where(u => u.Role == "CLIENT")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var trainers = await _context.Users
                .Where(u => u.Role == "TRAINER")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var query = _context.WorkoutPlans
                .AsNoTracking()
                .Include(w => w.Client)
                .Include(w => w.Trainer)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(w =>
                    w.PlanName.Contains(search) ||
                    (w.Description != null && w.Description.Contains(search)) ||
                    (w.Client != null && w.Client.FullName.Contains(search)) ||
                    (w.Trainer != null && w.Trainer.FullName.Contains(search)));
            }

            var workoutPlans = await query
                .OrderByDescending(w => w.WorkoutPlanId)
                .ToListAsync();

            SetAdminViewData(admin);
            ViewBag.Clients = clients;
            ViewBag.Trainers = trainers;
            ViewBag.WorkoutPlans = workoutPlans;
            ViewBag.Search = search ?? "";
            ViewBag.TotalWorkoutPlans = workoutPlans.Count;
            ViewBag.ActiveWorkoutPlans = workoutPlans.Count(w =>
                string.Equals(w.Status, "Active", StringComparison.OrdinalIgnoreCase));
            ViewBag.CompletedWorkoutPlans = workoutPlans.Count(w =>
                string.Equals(w.Status, "Completed", StringComparison.OrdinalIgnoreCase));
            ViewBag.OtherWorkoutPlans = workoutPlans.Count(w =>
                !string.Equals(w.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(w.Status, "Completed", StringComparison.OrdinalIgnoreCase));

            return View("~/Views/Admin/WorkoutPlans.cshtml");
        }

        [HttpGet]
        public IActionResult CreateWorkoutPlan()
        {
            LoadWorkoutPlanUsers();
            return View("~/Views/Admin/CreateWorkoutPlan.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWorkoutPlan(
            int TrainerId,
            int ClientId,
            string PlanName,
            string? Description,
            DateTime? StartDate,
            DateTime? EndDate,
            string? Status)
        {
            var admin = await GetLoggedInAdmin();
            if (admin == null)
                return RedirectToAction("Login", "Account");

            if (TrainerId <= 0 || ClientId <= 0 || string.IsNullOrWhiteSpace(PlanName))
            {
                TempData["Error"] = "Trainer, client and plan name are required.";
                LoadWorkoutPlanUsers();
                return View("~/Views/Admin/CreateWorkoutPlan.cshtml");
            }

            if (EndDate.HasValue && StartDate.HasValue && EndDate.Value.Date < StartDate.Value.Date)
            {
                TempData["Error"] = "End date cannot be earlier than start date.";
                LoadWorkoutPlanUsers();
                return View("~/Views/Admin/CreateWorkoutPlan.cshtml");
            }

            var trainerExists = await _context.Users.AnyAsync(u => u.UserId == TrainerId && u.Role == "TRAINER");
            var clientExists = await _context.Users.AnyAsync(u => u.UserId == ClientId && u.Role == "CLIENT");

            if (!trainerExists || !clientExists)
            {
                TempData["Error"] = "Selected trainer or client was not found.";
                LoadWorkoutPlanUsers();
                return View("~/Views/Admin/CreateWorkoutPlan.cshtml");
            }

            var plan = new WorkoutPlan
            {
                TrainerId = TrainerId,
                ClientId = ClientId,
                PlanName = PlanName.Trim(),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                StartDate = StartDate.HasValue ? DateOnly.FromDateTime(StartDate.Value) : DateOnly.FromDateTime(DateTime.Today),
                EndDate = EndDate.HasValue ? DateOnly.FromDateTime(EndDate.Value) : null,
                Status = string.IsNullOrWhiteSpace(Status) ? "Active" : Status.Trim(),
                CreatedAt = DateTime.Now
            };

            _context.WorkoutPlans.Add(plan);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Workout plan created successfully.";
            return RedirectToAction(nameof(WorkoutPlans));
        }

        [HttpGet]
        public async Task<IActionResult> ViewWorkoutPlan(int id)
        {
            var access = await GetPlanEditorUser();

            if (access == null)
                return RedirectToAction("Login", "Account");

            if (access.Role != "ADMIN" && access.Role != "TRAINER" && access.Role != "CLIENT")
                return RedirectToAction("Login", "Account");

            var plan = await _context.WorkoutPlans
                .Include(w => w.Client)
                .Include(w => w.Trainer)
                .Include(w => w.WorkoutPlanDays.OrderBy(d => d.DayNumber))
                    .ThenInclude(d => d.WorkoutExercises.OrderBy(e => e.SortOrder))
                .FirstOrDefaultAsync(w => w.WorkoutPlanId == id);

            if (plan == null)
            {
                TempData["Error"] = "Workout plan was not found.";
                return RedirectToAction(
                    access.Role == "CLIENT"
                        ? nameof(ClientWorkoutPlans)
                        : access.Role == "TRAINER"
                            ? nameof(TrainerWorkoutPlans)
                            : nameof(WorkoutPlans));
            }

            // ADMIN: can view every plan.
            // TRAINER: can view every workout plan.
            // CLIENT: can view only his/her own assigned plan.
            if (access.Role == "CLIENT" && plan.ClientId != access.UserId)
            {
                TempData["Error"] = "You are not authorized to view this workout plan.";
                return RedirectToAction(nameof(ClientWorkoutPlans));
            }

            SetAdminViewData(access);
            ViewBag.IsTrainer = access.Role == "TRAINER";
            ViewBag.IsClient = access.Role == "CLIENT";
            ViewBag.IsReadOnly = access.Role != "ADMIN";

            // Role-aware Back button for the shared workout detail page.
            ViewBag.BackUrl = access.Role switch
            {
                "CLIENT" => Url.Action(nameof(ClientWorkoutPlans), "Home"),
                "TRAINER" => Url.Action(nameof(TrainerWorkoutPlans), "Home"),
                _ => Url.Action(nameof(WorkoutPlans), "Home")
            };

            ViewBag.TotalNotifications = await _context.Notifications.CountAsync(
                n => n.IsRead != true);
            ViewBag.WorkoutPlan = plan;
            ViewBag.WorkoutPlanDays = plan.WorkoutPlanDays
                .OrderBy(d => d.DayNumber)
                .ToList();

            return View("~/Views/Admin/ViewWorkoutPlan.cshtml");
        }


        [HttpGet]
        public async Task<IActionResult> EditWorkoutPlan(int id)
        {
            var admin = await GetLoggedInAdmin();
            if (admin == null)
                return RedirectToAction("Login", "Account");

            var plan = await _context.WorkoutPlans.FirstOrDefaultAsync(w => w.WorkoutPlanId == id);
            if (plan == null)
            {
                TempData["Error"] = "Workout plan was not found.";
                return RedirectToAction(nameof(WorkoutPlans));
            }

            LoadWorkoutPlanUsers();
            return View("~/Views/Admin/EditWorkoutPlan.cshtml", plan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditWorkoutPlan(
            int WorkoutPlanId,
            int TrainerId,
            int ClientId,
            string PlanName,
            string? Description,
            DateOnly StartDate,
            DateOnly? EndDate,
            string? Status)
        {
            var admin = await GetLoggedInAdmin();
            if (admin == null)
                return RedirectToAction("Login", "Account");

            var plan = await _context.WorkoutPlans.FirstOrDefaultAsync(w => w.WorkoutPlanId == WorkoutPlanId);
            if (plan == null)
            {
                TempData["Error"] = "Workout plan was not found.";
                return RedirectToAction(nameof(WorkoutPlans));
            }

            if (TrainerId <= 0 || ClientId <= 0 || string.IsNullOrWhiteSpace(PlanName))
            {
                TempData["Error"] = "Trainer, client and plan name are required.";
                return RedirectToAction(nameof(EditWorkoutPlan), new { id = WorkoutPlanId });
            }

            if (EndDate.HasValue && EndDate.Value < StartDate)
            {
                TempData["Error"] = "End date cannot be earlier than start date.";
                return RedirectToAction(nameof(EditWorkoutPlan), new { id = WorkoutPlanId });
            }

            var validUsers = await _context.Users.CountAsync(u =>
                (u.UserId == TrainerId && u.Role == "TRAINER") ||
                (u.UserId == ClientId && u.Role == "CLIENT"));

            if (validUsers != 2)
            {
                TempData["Error"] = "Selected trainer or client was not found.";
                return RedirectToAction(nameof(EditWorkoutPlan), new { id = WorkoutPlanId });
            }

            plan.TrainerId = TrainerId;
            plan.ClientId = ClientId;
            plan.PlanName = PlanName.Trim();
            plan.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
            plan.StartDate = StartDate;
            plan.EndDate = EndDate;
            plan.Status = string.IsNullOrWhiteSpace(Status) ? "Active" : Status.Trim();

            await _context.SaveChangesAsync();
            TempData["Success"] = "Workout plan updated successfully.";
            return RedirectToAction(nameof(WorkoutPlans));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWorkoutPlan(int id)
        {
            var admin = await GetLoggedInAdmin();
            if (admin == null)
                return RedirectToAction("Login", "Account");

            var plan = await _context.WorkoutPlans.FirstOrDefaultAsync(w => w.WorkoutPlanId == id);
            if (plan == null)
            {
                TempData["Error"] = "Workout plan was not found.";
                return RedirectToAction(nameof(WorkoutPlans));
            }

            try
            {
                _context.WorkoutPlans.Remove(plan);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Workout plan deleted successfully.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "This workout plan cannot be deleted because related records exist.";
            }

            return RedirectToAction(nameof(WorkoutPlans));
        }

        // ============================================================
        // WORKOUT DETAIL - DAY
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddWorkoutPlanDay(
            int workoutPlanId,
            int dayNumber,
            string dayName,
            string? notes)
        {
            var access = await GetPlanEditorUser();
            if (access == null)
                return RedirectToAction("Login", "Account");

            // Plan details are read-only for TRAINER and CLIENT.
            // Only ADMIN can add, edit, or delete plan details.
            if (access.Role != "ADMIN")
                return RedirectToAction(
                    access.Role == "CLIENT"
                        ? nameof(ClientWorkoutPlans)
                        : nameof(TrainerWorkoutPlans));

            var plan = await _context.WorkoutPlans.FirstOrDefaultAsync(w => w.WorkoutPlanId == workoutPlanId);
            if (plan == null || !(access.Role == "ADMIN" || plan.TrainerId == access.UserId))
                return RedirectToAction(access.Role == "TRAINER" ? nameof(TrainerWorkoutPlans) : nameof(WorkoutPlans));

            if (dayNumber <= 0 || string.IsNullOrWhiteSpace(dayName))
            {
                TempData["Error"] = "Day number and day name are required.";
                return RedirectToAction(nameof(ViewWorkoutPlan), new { id = workoutPlanId });
            }

            var day = new WorkoutPlanDay
            {
                WorkoutPlanId = workoutPlanId,
                DayNumber = dayNumber,
                DayName = dayName.Trim(),
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            };

            _context.WorkoutPlanDays.Add(day);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Workout day added successfully.";

            return RedirectToWorkoutPlanAfterEdit(access.Role, workoutPlanId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditWorkoutPlanDay(
            int workoutPlanDayId,
            int dayNumber,
            string dayName,
            string? notes)
        {
            var access = await GetPlanEditorUser();
            if (access == null)
                return RedirectToAction("Login", "Account");

            // Plan details are read-only for TRAINER and CLIENT.
            // Only ADMIN can add, edit, or delete plan details.
            if (access.Role != "ADMIN")
                return RedirectToAction(
                    access.Role == "CLIENT"
                        ? nameof(ClientWorkoutPlans)
                        : nameof(TrainerWorkoutPlans));

            var day = await _context.WorkoutPlanDays
                .Include(d => d.WorkoutPlan)
                .FirstOrDefaultAsync(d => d.WorkoutPlanDayId == workoutPlanDayId);

            if (day == null || !(access.Role == "ADMIN" || day.WorkoutPlan.TrainerId == access.UserId))
                return RedirectToAction(access.Role == "TRAINER" ? nameof(TrainerWorkoutPlans) : nameof(WorkoutPlans));

            day.DayNumber = dayNumber;
            day.DayName = dayName.Trim();
            day.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            await _context.SaveChangesAsync();

            TempData["Success"] = "Workout day updated successfully.";
            return RedirectToWorkoutPlanAfterEdit(access.Role, day.WorkoutPlanId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWorkoutPlanDay(int id)
        {
            var access = await GetPlanEditorUser();
            if (access == null)
                return RedirectToAction("Login", "Account");

            // Plan details are read-only for TRAINER and CLIENT.
            // Only ADMIN can add, edit, or delete plan details.
            if (access.Role != "ADMIN")
                return RedirectToAction(
                    access.Role == "CLIENT"
                        ? nameof(ClientWorkoutPlans)
                        : nameof(TrainerWorkoutPlans));

            var day = await _context.WorkoutPlanDays
                .Include(d => d.WorkoutPlan)
                .FirstOrDefaultAsync(d => d.WorkoutPlanDayId == id);

            if (day == null)
                return RedirectToAction(access.Role == "TRAINER" ? nameof(TrainerWorkoutPlans) : nameof(WorkoutPlans));

            if (access.Role != "ADMIN" && day.WorkoutPlan.TrainerId != access.UserId)
                return RedirectToAction(nameof(TrainerWorkoutPlans));

            var planId = day.WorkoutPlanId;
            _context.WorkoutPlanDays.Remove(day);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Workout day deleted successfully.";
            return RedirectToWorkoutPlanAfterEdit(access.Role, planId);
        }

        // ============================================================
        // WORKOUT DETAIL - EXERCISE
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddWorkoutExercise(
            int workoutPlanDayId,
            string exerciseName,
            int? sets,
            int? reps,
            int? duration,
            int? restSeconds,
            string? notes,
            int sortOrder = 0)
        {
            var access = await GetPlanEditorUser();
            if (access == null)
                return RedirectToAction("Login", "Account");

            // Plan details are read-only for TRAINER and CLIENT.
            // Only ADMIN can add, edit, or delete plan details.
            if (access.Role != "ADMIN")
                return RedirectToAction(
                    access.Role == "CLIENT"
                        ? nameof(ClientWorkoutPlans)
                        : nameof(TrainerWorkoutPlans));

            var day = await _context.WorkoutPlanDays
                .Include(d => d.WorkoutPlan)
                .FirstOrDefaultAsync(d => d.WorkoutPlanDayId == workoutPlanDayId);

            if (day == null || !(access.Role == "ADMIN" || day.WorkoutPlan.TrainerId == access.UserId))
                return RedirectToAction(access.Role == "TRAINER" ? nameof(TrainerWorkoutPlans) : nameof(WorkoutPlans));

            if (string.IsNullOrWhiteSpace(exerciseName))
            {
                TempData["Error"] = "Exercise name is required.";
                return RedirectToWorkoutPlanAfterEdit(access.Role, day.WorkoutPlanId);
            }

            var exercise = new WorkoutExercise
            {
                WorkoutPlanDayId = workoutPlanDayId,
                ExerciseName = exerciseName.Trim(),
                Sets = sets,
                Reps = reps,
                Duration = duration,
                RestSeconds = restSeconds,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                SortOrder = sortOrder
            };

            _context.WorkoutExercises.Add(exercise);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Exercise added successfully.";
            return RedirectToWorkoutPlanAfterEdit(access.Role, day.WorkoutPlanId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditWorkoutExercise(
            int workoutExerciseId,
            string exerciseName,
            int? sets,
            int? reps,
            int? duration,
            int? restSeconds,
            string? notes,
            int sortOrder = 0)
        {
            var access = await GetPlanEditorUser();
            if (access == null)
                return RedirectToAction("Login", "Account");

            // Plan details are read-only for TRAINER and CLIENT.
            // Only ADMIN can add, edit, or delete plan details.
            if (access.Role != "ADMIN")
                return RedirectToAction(
                    access.Role == "CLIENT"
                        ? nameof(ClientWorkoutPlans)
                        : nameof(TrainerWorkoutPlans));

            var exercise = await _context.WorkoutExercises
                .Include(e => e.WorkoutPlanDay)
                    .ThenInclude(d => d.WorkoutPlan)
                .FirstOrDefaultAsync(e => e.WorkoutExerciseId == workoutExerciseId);

            if (exercise == null)
                return RedirectToAction(access.Role == "TRAINER" ? nameof(TrainerWorkoutPlans) : nameof(WorkoutPlans));

            if (access.Role != "ADMIN" && exercise.WorkoutPlanDay.WorkoutPlan.TrainerId != access.UserId)
                return RedirectToAction(nameof(TrainerWorkoutPlans));

            if (string.IsNullOrWhiteSpace(exerciseName))
            {
                TempData["Error"] = "Exercise name is required.";
                return RedirectToWorkoutPlanAfterEdit(access.Role, exercise.WorkoutPlanDay.WorkoutPlanId);
            }

            exercise.ExerciseName = exerciseName.Trim();
            exercise.Sets = sets;
            exercise.Reps = reps;
            exercise.Duration = duration;
            exercise.RestSeconds = restSeconds;
            exercise.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            exercise.SortOrder = sortOrder;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Exercise updated successfully.";
            return RedirectToWorkoutPlanAfterEdit(access.Role, exercise.WorkoutPlanDay.WorkoutPlanId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWorkoutExercise(int id)
        {
            var access = await GetPlanEditorUser();
            if (access == null)
                return RedirectToAction("Login", "Account");

            // Plan details are read-only for TRAINER and CLIENT.
            // Only ADMIN can add, edit, or delete plan details.
            if (access.Role != "ADMIN")
                return RedirectToAction(
                    access.Role == "CLIENT"
                        ? nameof(ClientWorkoutPlans)
                        : nameof(TrainerWorkoutPlans));

            var exercise = await _context.WorkoutExercises
                .Include(e => e.WorkoutPlanDay)
                    .ThenInclude(d => d.WorkoutPlan)
                .FirstOrDefaultAsync(e => e.WorkoutExerciseId == id);

            if (exercise == null)
                return RedirectToAction(access.Role == "TRAINER" ? nameof(TrainerWorkoutPlans) : nameof(WorkoutPlans));

            if (access.Role != "ADMIN" && exercise.WorkoutPlanDay.WorkoutPlan.TrainerId != access.UserId)
                return RedirectToAction(nameof(TrainerWorkoutPlans));

            var planId = exercise.WorkoutPlanDay.WorkoutPlanId;
            _context.WorkoutExercises.Remove(exercise);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Exercise deleted successfully.";
            return RedirectToWorkoutPlanAfterEdit(access.Role, planId);
        }

        // ============================================================
        // ADMIN - DIET PLANS
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> DietPlans(string? search)
        {
            var admin = await GetLoggedInAdmin();
            if (admin == null)
                return RedirectToAction("Login", "Account");

            var clients = await _context.Users
                .Where(u => u.Role == "CLIENT")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var trainers = await _context.Users
                .Where(u => u.Role == "TRAINER")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var query = _context.DietPlans
                .AsNoTracking()
                .Include(d => d.Client)
                .Include(d => d.Trainer)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(d =>
                    d.PlanName.Contains(search) ||
                    (d.Description != null && d.Description.Contains(search)) ||
                    (d.Client != null && d.Client.FullName.Contains(search)) ||
                    (d.Trainer != null && d.Trainer.FullName.Contains(search)));
            }

            var dietPlans = await query
                .OrderByDescending(d => d.DietPlanId)
                .ToListAsync();

            SetAdminViewData(admin);
            ViewBag.Clients = clients;
            ViewBag.Trainers = trainers;
            ViewBag.DietPlans = dietPlans;
            ViewBag.Search = search ?? "";
            ViewBag.TotalDietPlans = dietPlans.Count;
            ViewBag.ActiveDietPlans = dietPlans.Count(d =>
                string.Equals(d.Status, "Active", StringComparison.OrdinalIgnoreCase));
            ViewBag.CompletedDietPlans = dietPlans.Count(d =>
                string.Equals(d.Status, "Completed", StringComparison.OrdinalIgnoreCase));
            ViewBag.OtherDietPlans = dietPlans.Count(d =>
                !string.Equals(d.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(d.Status, "Completed", StringComparison.OrdinalIgnoreCase));

            return View("~/Views/Admin/DietPlans.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDietPlan(
            int TrainerId,
            int ClientId,
            string PlanName,
            string? Description,
            DateOnly StartDate,
            DateOnly? EndDate,
            string? Status)
        {
            var admin = await GetLoggedInAdmin();
            if (admin == null)
                return RedirectToAction("Login", "Account");

            if (TrainerId <= 0 || ClientId <= 0 || string.IsNullOrWhiteSpace(PlanName))
            {
                TempData["Error"] = "Trainer, client and plan name are required.";
                return RedirectToAction(nameof(DietPlans));
            }

            if (EndDate.HasValue && EndDate.Value < StartDate)
            {
                TempData["Error"] = "End date cannot be earlier than start date.";
                return RedirectToAction(nameof(DietPlans));
            }

            var validUsers = await _context.Users.CountAsync(u =>
                (u.UserId == TrainerId && u.Role == "TRAINER") ||
                (u.UserId == ClientId && u.Role == "CLIENT"));

            if (validUsers != 2)
            {
                TempData["Error"] = "Selected trainer or client was not found.";
                return RedirectToAction(nameof(DietPlans));
            }

            var dietPlan = new DietPlan
            {
                TrainerId = TrainerId,
                ClientId = ClientId,
                PlanName = PlanName.Trim(),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                StartDate = StartDate,
                EndDate = EndDate,
                Status = string.IsNullOrWhiteSpace(Status) ? "Active" : Status.Trim(),
                CreatedAt = DateTime.Now
            };

            _context.DietPlans.Add(dietPlan);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Diet plan created successfully.";
            return RedirectToAction(nameof(DietPlans));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDietPlan(
            int DietPlanId,
            int TrainerId,
            int ClientId,
            string PlanName,
            string? Description,
            DateOnly StartDate,
            DateOnly? EndDate,
            string? Status)
        {
            var admin = await GetLoggedInAdmin();
            if (admin == null)
                return RedirectToAction("Login", "Account");

            var dietPlan = await _context.DietPlans.FirstOrDefaultAsync(d => d.DietPlanId == DietPlanId);
            if (dietPlan == null)
            {
                TempData["Error"] = "Diet plan was not found.";
                return RedirectToAction(nameof(DietPlans));
            }

            if (TrainerId <= 0 || ClientId <= 0 || string.IsNullOrWhiteSpace(PlanName))
            {
                TempData["Error"] = "Trainer, client and plan name are required.";
                return RedirectToAction(nameof(DietPlans));
            }

            if (EndDate.HasValue && EndDate.Value < StartDate)
            {
                TempData["Error"] = "End date cannot be earlier than start date.";
                return RedirectToAction(nameof(DietPlans));
            }

            var validUsers = await _context.Users.CountAsync(u =>
                (u.UserId == TrainerId && u.Role == "TRAINER") ||
                (u.UserId == ClientId && u.Role == "CLIENT"));

            if (validUsers != 2)
            {
                TempData["Error"] = "Selected trainer or client was not found.";
                return RedirectToAction(nameof(DietPlans));
            }

            dietPlan.TrainerId = TrainerId;
            dietPlan.ClientId = ClientId;
            dietPlan.PlanName = PlanName.Trim();
            dietPlan.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
            dietPlan.StartDate = StartDate;
            dietPlan.EndDate = EndDate;
            dietPlan.Status = string.IsNullOrWhiteSpace(Status) ? "Active" : Status.Trim();

            await _context.SaveChangesAsync();
            TempData["Success"] = "Diet plan updated successfully.";
            return RedirectToAction(nameof(DietPlans));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDietPlan(int id)
        {
            var admin = await GetLoggedInAdmin();
            if (admin == null)
                return RedirectToAction("Login", "Account");

            var dietPlan = await _context.DietPlans.FirstOrDefaultAsync(d => d.DietPlanId == id);
            if (dietPlan == null)
            {
                TempData["Error"] = "Diet plan was not found.";
                return RedirectToAction(nameof(DietPlans));
            }

            try
            {
                _context.DietPlans.Remove(dietPlan);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Diet plan deleted successfully.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "This diet plan cannot be deleted because related records exist.";
            }

            return RedirectToAction(nameof(DietPlans));
        }

        [HttpGet]
        public async Task<IActionResult> ViewDietPlan(int id)
        {
            var access = await GetPlanEditorUser();

            if (access == null)
                return RedirectToAction("Login", "Account");

            if (access.Role != "ADMIN" && access.Role != "TRAINER" && access.Role != "CLIENT")
                return RedirectToAction("Login", "Account");

            var dietPlan = await _context.DietPlans
                .Include(d => d.Client)
                .Include(d => d.Trainer)
                .Include(d => d.MealLogs)
                .Include(d => d.DietPlanDays.OrderBy(day => day.DayNumber))
                    .ThenInclude(day => day.DietMeals.OrderBy(meal => meal.SortOrder))
                .FirstOrDefaultAsync(d => d.DietPlanId == id);

            if (dietPlan == null)
            {
                TempData["Error"] = "Diet plan was not found.";
                return RedirectToAction(
                    access.Role == "CLIENT"
                        ? nameof(ClientDietPlans)
                        : access.Role == "TRAINER"
                            ? nameof(TrainerDietPlans)
                            : nameof(DietPlans));
            }

            // ADMIN: can view every plan.
            // TRAINER: can view every diet plan.
            // CLIENT: can view only his/her own assigned plan.
            if (access.Role == "CLIENT" && dietPlan.ClientId != access.UserId)
            {
                TempData["Error"] = "You are not authorized to view this diet plan.";
                return RedirectToAction(nameof(ClientDietPlans));
            }

            SetAdminViewData(access);
            ViewBag.IsTrainer = access.Role == "TRAINER";
            ViewBag.IsClient = access.Role == "CLIENT";
            ViewBag.IsReadOnly = access.Role != "ADMIN";

            // Role-aware Back button for the shared diet detail page.
            ViewBag.BackUrl = access.Role switch
            {
                "CLIENT" => Url.Action(nameof(ClientDietPlans), "Home"),
                "TRAINER" => Url.Action(nameof(TrainerDietPlans), "Home"),
                _ => Url.Action(nameof(DietPlans), "Home")
            };

            ViewBag.TotalNotifications = await _context.Notifications.CountAsync(
                n => n.IsRead != true);
            ViewBag.DietPlan = dietPlan;
            ViewBag.DietPlanDays = dietPlan.DietPlanDays
                .OrderBy(d => d.DayNumber)
                .ToList();

            return View("~/Views/Admin/ViewDietPlan.cshtml", dietPlan);
        }

        // ============================================================
        // DIET DETAIL - DAY
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDietPlanDay(
            int dietPlanId,
            int dayNumber,
            string dayName,
            string? notes)
        {
            var access = await GetPlanEditorUser();
            if (access == null)
                return RedirectToAction("Login", "Account");

            // Plan details are read-only for TRAINER and CLIENT.
            // Only ADMIN can add, edit, or delete plan details.
            if (access.Role != "ADMIN")
                return RedirectToAction(
                    access.Role == "CLIENT"
                        ? nameof(ClientDietPlans)
                        : nameof(TrainerDietPlans));

            var plan = await _context.DietPlans.FirstOrDefaultAsync(d => d.DietPlanId == dietPlanId);
            if (plan == null || !(access.Role == "ADMIN" || plan.TrainerId == access.UserId))
                return RedirectToAction(access.Role == "TRAINER" ? nameof(TrainerDietPlans) : nameof(DietPlans));

            if (dayNumber <= 0 || string.IsNullOrWhiteSpace(dayName))
            {
                TempData["Error"] = "Day number and day name are required.";
                return RedirectToDietPlanAfterEdit(access.Role, dietPlanId);
            }

            _context.DietPlanDays.Add(new DietPlanDay
            {
                DietPlanId = dietPlanId,
                DayNumber = dayNumber,
                DayName = dayName.Trim(),
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Diet day added successfully.";
            return RedirectToDietPlanAfterEdit(access.Role, dietPlanId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDietPlanDay(
            int dietPlanDayId,
            int dayNumber,
            string dayName,
            string? notes)
        {
            var access = await GetPlanEditorUser();
            if (access == null)
                return RedirectToAction("Login", "Account");

            // Plan details are read-only for TRAINER and CLIENT.
            // Only ADMIN can add, edit, or delete plan details.
            if (access.Role != "ADMIN")
                return RedirectToAction(
                    access.Role == "CLIENT"
                        ? nameof(ClientDietPlans)
                        : nameof(TrainerDietPlans));

            var day = await _context.DietPlanDays
                .Include(d => d.DietPlan)
                .FirstOrDefaultAsync(d => d.DietPlanDayId == dietPlanDayId);

            if (day == null || !(access.Role == "ADMIN" || day.DietPlan.TrainerId == access.UserId))
                return RedirectToAction(access.Role == "TRAINER" ? nameof(TrainerDietPlans) : nameof(DietPlans));

            day.DayNumber = dayNumber;
            day.DayName = dayName.Trim();
            day.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            await _context.SaveChangesAsync();

            TempData["Success"] = "Diet day updated successfully.";
            return RedirectToDietPlanAfterEdit(access.Role, day.DietPlanId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDietPlanDay(int id)
        {
            var access = await GetPlanEditorUser();
            if (access == null)
                return RedirectToAction("Login", "Account");

            // Plan details are read-only for TRAINER and CLIENT.
            // Only ADMIN can add, edit, or delete plan details.
            if (access.Role != "ADMIN")
                return RedirectToAction(
                    access.Role == "CLIENT"
                        ? nameof(ClientDietPlans)
                        : nameof(TrainerDietPlans));

            var day = await _context.DietPlanDays
                .Include(d => d.DietPlan)
                .FirstOrDefaultAsync(d => d.DietPlanDayId == id);

            if (day == null)
                return RedirectToAction(access.Role == "TRAINER" ? nameof(TrainerDietPlans) : nameof(DietPlans));

            if (access.Role != "ADMIN" && day.DietPlan.TrainerId != access.UserId)
                return RedirectToAction(nameof(TrainerDietPlans));

            var planId = day.DietPlanId;
            _context.DietPlanDays.Remove(day);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Diet day deleted successfully.";
            return RedirectToDietPlanAfterEdit(access.Role, planId);
        }

        // ============================================================
        // DIET DETAIL - MEAL
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDietMeal(
            int dietPlanDayId,
            string mealType,
            string foodName,
            string? quantity,
            int? calories,
            string? notes,
            int sortOrder = 0)
        {
            var access = await GetPlanEditorUser();
            if (access == null)
                return RedirectToAction("Login", "Account");

            // Plan details are read-only for TRAINER and CLIENT.
            // Only ADMIN can add, edit, or delete plan details.
            if (access.Role != "ADMIN")
                return RedirectToAction(
                    access.Role == "CLIENT"
                        ? nameof(ClientDietPlans)
                        : nameof(TrainerDietPlans));

            var day = await _context.DietPlanDays
                .Include(d => d.DietPlan)
                .FirstOrDefaultAsync(d => d.DietPlanDayId == dietPlanDayId);

            if (day == null || !(access.Role == "ADMIN" || day.DietPlan.TrainerId == access.UserId))
                return RedirectToAction(access.Role == "TRAINER" ? nameof(TrainerDietPlans) : nameof(DietPlans));

            if (string.IsNullOrWhiteSpace(mealType) || string.IsNullOrWhiteSpace(foodName))
            {
                TempData["Error"] = "Meal type and food name are required.";
                return RedirectToDietPlanAfterEdit(access.Role, day.DietPlanId);
            }

            _context.DietMeals.Add(new DietMeal
            {
                DietPlanDayId = dietPlanDayId,
                MealType = mealType.Trim(),
                FoodName = foodName.Trim(),
                Quantity = string.IsNullOrWhiteSpace(quantity) ? null : quantity.Trim(),
                Calories = calories,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                SortOrder = sortOrder
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Meal added successfully.";
            return RedirectToDietPlanAfterEdit(access.Role, day.DietPlanId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDietMeal(
            int dietMealId,
            string mealType,
            string foodName,
            string? quantity,
            int? calories,
            string? notes,
            int sortOrder = 0)
        {
            var access = await GetPlanEditorUser();
            if (access == null)
                return RedirectToAction("Login", "Account");

            // Plan details are read-only for TRAINER and CLIENT.
            // Only ADMIN can add, edit, or delete plan details.
            if (access.Role != "ADMIN")
                return RedirectToAction(
                    access.Role == "CLIENT"
                        ? nameof(ClientDietPlans)
                        : nameof(TrainerDietPlans));

            var meal = await _context.DietMeals
                .Include(m => m.DietPlanDay)
                    .ThenInclude(d => d.DietPlan)
                .FirstOrDefaultAsync(m => m.DietMealId == dietMealId);

            if (meal == null)
                return RedirectToAction(access.Role == "TRAINER" ? nameof(TrainerDietPlans) : nameof(DietPlans));

            if (access.Role != "ADMIN" && meal.DietPlanDay.DietPlan.TrainerId != access.UserId)
                return RedirectToAction(nameof(TrainerDietPlans));

            if (string.IsNullOrWhiteSpace(mealType) || string.IsNullOrWhiteSpace(foodName))
            {
                TempData["Error"] = "Meal type and food name are required.";
                return RedirectToDietPlanAfterEdit(access.Role, meal.DietPlanDay.DietPlanId);
            }

            meal.MealType = mealType.Trim();
            meal.FoodName = foodName.Trim();
            meal.Quantity = string.IsNullOrWhiteSpace(quantity) ? null : quantity.Trim();
            meal.Calories = calories;
            meal.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            meal.SortOrder = sortOrder;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Meal updated successfully.";
            return RedirectToDietPlanAfterEdit(access.Role, meal.DietPlanDay.DietPlanId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDietMeal(int id)
        {
            var access = await GetPlanEditorUser();
            if (access == null)
                return RedirectToAction("Login", "Account");

            // Plan details are read-only for TRAINER and CLIENT.
            // Only ADMIN can add, edit, or delete plan details.
            if (access.Role != "ADMIN")
                return RedirectToAction(
                    access.Role == "CLIENT"
                        ? nameof(ClientDietPlans)
                        : nameof(TrainerDietPlans));

            var meal = await _context.DietMeals
                .Include(m => m.DietPlanDay)
                    .ThenInclude(d => d.DietPlan)
                .FirstOrDefaultAsync(m => m.DietMealId == id);

            if (meal == null)
                return RedirectToAction(access.Role == "TRAINER" ? nameof(TrainerDietPlans) : nameof(DietPlans));

            if (access.Role != "ADMIN" && meal.DietPlanDay.DietPlan.TrainerId != access.UserId)
                return RedirectToAction(nameof(TrainerDietPlans));

            var planId = meal.DietPlanDay.DietPlanId;
            _context.DietMeals.Remove(meal);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Meal deleted successfully.";
            return RedirectToDietPlanAfterEdit(access.Role, planId);
        }

        // ============================================================
        // PLAN EDITOR HELPERS
        // ============================================================

        private async Task<User?> GetPlanEditorUser()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return null;

            return await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId.Value);
        }

        private IActionResult RedirectToWorkoutPlanAfterEdit(string role, int planId)
        {
            if (role == "TRAINER")
                return RedirectToAction(nameof(TrainerWorkoutPlans));

            return RedirectToAction(nameof(ViewWorkoutPlan), new { id = planId });
        }

        private IActionResult RedirectToDietPlanAfterEdit(string role, int planId)
        {
            if (role == "TRAINER")
                return RedirectToAction(nameof(TrainerDietPlans));

            return RedirectToAction(nameof(ViewDietPlan), new { id = planId });
        }

        private void LoadWorkoutPlanUsers()
        {
            using (SqlConnection con = new SqlConnection(_connectionString))
            {
                con.Open();

                DataTable trainers = new DataTable();
                DataTable clients = new DataTable();

                string trainerSql = @"
                    SELECT UserId, FullName
                    FROM Users
                    WHERE Role = 'TRAINER'
                    ORDER BY FullName;";

                string clientSql = @"
                    SELECT UserId, FullName
                    FROM Users
                    WHERE Role = 'CLIENT'
                    ORDER BY FullName;";

                using (SqlCommand cmd = new SqlCommand(trainerSql, con))
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    adapter.Fill(trainers);

                using (SqlCommand cmd = new SqlCommand(clientSql, con))
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    adapter.Fill(clients);

                ViewBag.Trainers = trainers;
                ViewBag.Clients = clients;
            }
        }


        // =========================================================
        // ADMIN - HEALTH METRICS
        // URL:
        // /Home/HealthMetrics
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> HealthMetrics(
            string? search,
            string? dateFilter)
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            SetAdminViewData(admin);

            DataTable metrics = new DataTable();

            try
            {
                using (SqlConnection con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    string sql = @"
                SELECT
                    hm.MetricId,
                    hm.ClientId,
                    u.FullName AS ClientName,
                    hm.RecordDate,
                    hm.Weight,
                    hm.Height,
                    hm.BMI,
                    hm.CaloriesBurned,
                    hm.BodyFat,
                    hm.Notes
                FROM HealthMetrics hm
                INNER JOIN Users u
                    ON u.UserId = hm.ClientId
                WHERE 1 = 1
            ";

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        sql += @"
                    AND (
                        u.FullName LIKE @Search
                        OR CAST(hm.RecordDate AS nvarchar(20)) LIKE @Search
                    )
                ";
                    }

                    if (dateFilter == "7" ||
                        dateFilter == "30" ||
                        dateFilter == "90")
                    {
                        sql += @"
                    AND hm.RecordDate >= DATEADD(
                        DAY,
                        -@Days,
                        CAST(GETDATE() AS date)
                    )
                ";
                    }

                    sql += @"
                ORDER BY hm.RecordDate DESC, hm.MetricId DESC;
            ";

                    using (SqlCommand cmd = new SqlCommand(sql, con))
                    {
                        if (!string.IsNullOrWhiteSpace(search))
                        {
                            cmd.Parameters.Add(
                                "@Search",
                                SqlDbType.NVarChar,
                                200
                            ).Value =
                                "%" + search.Trim() + "%";
                        }

                        if (dateFilter == "7" ||
                            dateFilter == "30" ||
                            dateFilter == "90")
                        {
                            cmd.Parameters.Add(
                                "@Days",
                                SqlDbType.Int
                            ).Value =
                                int.Parse(dateFilter);
                        }

                        using (SqlDataAdapter adapter =
                            new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(metrics);
                        }
                    }
                }


                // =====================================================
                // STATISTICS
                // =====================================================

                ViewBag.TotalHealthMetrics =
                    await _context.HealthMetrics.CountAsync();

                ViewBag.TotalClients =
                    await _context.Users
                        .CountAsync(u => u.Role == "CLIENT");


                // -----------------------------------------------------
                // LATEST HEALTH METRICS - LAST 30 DAYS
                // -----------------------------------------------------

                ViewBag.LatestHealthMetrics = 0;

                using (SqlConnection latestCon =
                    new SqlConnection(_connectionString))
                {
                    await latestCon.OpenAsync();

                    string latestSql = @"
                SELECT COUNT(*)
                FROM HealthMetrics
                WHERE RecordDate >= DATEADD(
                    DAY,
                    -30,
                    CAST(GETDATE() AS date)
                );
            ";

                    using (SqlCommand latestCmd =
                        new SqlCommand(latestSql, latestCon))
                    {
                        ViewBag.LatestHealthMetrics =
                            Convert.ToInt32(
                                await latestCmd.ExecuteScalarAsync()
                            );
                    }
                }


                ViewBag.TotalHealthProfiles =
                    await _context.HealthProfiles.CountAsync();

                ViewBag.TotalNotifications =
                    await _context.Notifications.CountAsync(
                        n => n.IsRead != true
                    );

                ViewBag.UnreadNotifications =
                    ViewBag.TotalNotifications;


                ViewBag.Search =
                    search ?? "";

                ViewBag.DateFilter =
                    dateFilter ?? "all";


                // =====================================================
                // BMI STATISTICS
                // =====================================================

                DataTable bmiTable =
                    new DataTable();

                using (SqlConnection con =
                    new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    string bmiSql = @"
                SELECT BMI
                FROM HealthMetrics
                WHERE BMI IS NOT NULL;
            ";

                    using (SqlCommand cmd =
                        new SqlCommand(bmiSql, con))
                    using (SqlDataAdapter adapter =
                        new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(bmiTable);
                    }
                }


                int normalBmi = 0;
                int overweightBmi = 0;
                int underweightBmi = 0;
                int obeseBmi = 0;


                foreach (DataRow row in bmiTable.Rows)
                {
                    if (!decimal.TryParse(
                            Convert.ToString(row["BMI"]),
                            out decimal bmi))
                    {
                        continue;
                    }


                    if (bmi < 18.5m)
                    {
                        underweightBmi++;
                    }
                    else if (bmi < 25m)
                    {
                        normalBmi++;
                    }
                    else if (bmi < 30m)
                    {
                        overweightBmi++;
                    }
                    else
                    {
                        obeseBmi++;
                    }
                }


                ViewBag.NormalBmi =
                    normalBmi;

                ViewBag.OverweightBmi =
                    overweightBmi;

                ViewBag.UnderweightBmi =
                    underweightBmi;

                ViewBag.ObeseBmi =
                    obeseBmi;


                // =====================================================
                // CLIENT DROPDOWN
                // =====================================================

                var clients =
                    await _context.Users
                        .Where(u => u.Role == "CLIENT")
                        .OrderBy(u => u.FullName)
                        .ToListAsync();


                ViewBag.Clients =
                    clients;

                ViewBag.HealthMetrics =
                    metrics;


                return View(
                    "~/Views/Admin/HealthMetrics.cshtml"
                );
            }
            catch (Exception ex)
            {
                ViewBag.TotalHealthMetrics = 0;
                ViewBag.TotalClients = 0;
                ViewBag.LatestHealthMetrics = 0;
                ViewBag.TotalHealthProfiles = 0;
                ViewBag.TotalNotifications = 0;
                ViewBag.UnreadNotifications = 0;

                ViewBag.NormalBmi = 0;
                ViewBag.OverweightBmi = 0;
                ViewBag.UnderweightBmi = 0;
                ViewBag.ObeseBmi = 0;

                ViewBag.Search =
                    search ?? "";

                ViewBag.DateFilter =
                    dateFilter ?? "all";


                ViewBag.Clients =
                    await _context.Users
                        .Where(u => u.Role == "CLIENT")
                        .OrderBy(u => u.FullName)
                        .ToListAsync();


                ViewBag.HealthMetrics =
                    metrics;


                TempData["Error"] =
                    "Unable to load health metrics: " +
                    ex.Message;


                return View(
                    "~/Views/Admin/HealthMetrics.cshtml"
                );
            }
        }



        // =========================================================
        // ADD HEALTH METRIC
        // POST: /Home/AddHealthMetric
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddHealthMetric(
            int ClientId,
            string RecordDate,
            decimal Weight,
            decimal Height,
            decimal? BMI,
            decimal? CaloriesBurned,
            decimal? BodyFat,
            string? Notes)
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            if (ClientId <= 0 ||
                string.IsNullOrWhiteSpace(RecordDate) ||
                Weight <= 0 ||
                Height <= 0)
            {
                TempData["Error"] =
                    "Please fill in all required health metric fields.";

                return RedirectToAction(
                    nameof(HealthMetrics)
                );
            }


            if (!DateOnly.TryParse(
                    RecordDate,
                    out DateOnly recordDate))
            {
                TempData["Error"] =
                    "Invalid measurement date.";

                return RedirectToAction(
                    nameof(HealthMetrics)
                );
            }


            bool clientExists =
                await _context.Users.AnyAsync(u =>
                    u.UserId == ClientId &&
                    u.Role == "CLIENT");


            if (!clientExists)
            {
                TempData["Error"] =
                    "Selected client was not found.";

                return RedirectToAction(
                    nameof(HealthMetrics)
                );
            }


            // =====================================================
            // AUTO BMI CALCULATION
            // Weight = KG
            // Height = CM
            // BMI = Weight / Height(m)^2
            // =====================================================

            decimal calculatedBmi =
                Weight /
                ((Height / 100m) *
                 (Height / 100m));


            try
            {
                using (SqlConnection con =
                    new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();


                    string sql = @"
                INSERT INTO HealthMetrics
                (
                    ClientId,
                    RecordDate,
                    Weight,
                    Height,
                    BMI,
                    CaloriesBurned,
                    BodyFat,
                    Notes
                )
                VALUES
                (
                    @ClientId,
                    @RecordDate,
                    @Weight,
                    @Height,
                    @BMI,
                    @CaloriesBurned,
                    @BodyFat,
                    @Notes
                );
            ";


                    using (SqlCommand cmd =
                        new SqlCommand(sql, con))
                    {
                        cmd.Parameters.Add(
                            "@ClientId",
                            SqlDbType.Int
                        ).Value =
                            ClientId;


                        cmd.Parameters.Add(
                            "@RecordDate",
                            SqlDbType.Date
                        ).Value =
                            recordDate.ToDateTime(
                                TimeOnly.MinValue
                            );


                        cmd.Parameters.Add(
                            "@Weight",
                            SqlDbType.Decimal
                        ).Value =
                            Weight;


                        cmd.Parameters.Add(
                            "@Height",
                            SqlDbType.Decimal
                        ).Value =
                            Height;


                        // AUTO CALCULATED BMI
                        cmd.Parameters.Add(
                            "@BMI",
                            SqlDbType.Decimal
                        ).Value =
                            calculatedBmi;


                        cmd.Parameters.Add(
                            "@CaloriesBurned",
                            SqlDbType.Decimal
                        ).Value =
                            CaloriesBurned.HasValue
                                ? CaloriesBurned.Value
                                : DBNull.Value;


                        cmd.Parameters.Add(
                            "@BodyFat",
                            SqlDbType.Decimal
                        ).Value =
                            BodyFat.HasValue
                                ? BodyFat.Value
                                : DBNull.Value;


                        cmd.Parameters.Add(
                            "@Notes",
                            SqlDbType.NVarChar,
                            500
                        ).Value =
                            string.IsNullOrWhiteSpace(Notes)
                                ? DBNull.Value
                                : Notes.Trim();


                        await cmd.ExecuteNonQueryAsync();
                    }
                }


                TempData["Success"] =
                    "Health metric added successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to add health metric: " +
                    ex.Message;
            }


            return RedirectToAction(
                nameof(HealthMetrics)
            );
        }



        // =========================================================
        // VIEW HEALTH METRIC
        // GET: /Home/ViewHealthMetric?id=1
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> ViewHealthMetric(
            int id)
        {
            var admin =
                await GetLoggedInAdmin();


            if (admin == null)
            {
                return Json(new
                {
                    success = false,
                    message =
                        "You are not authorized to view this record."
                });
            }


            try
            {
                using (SqlConnection con =
                    new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();


                    string sql = @"
                SELECT
                    hm.MetricId,
                    hm.ClientId,
                    u.FullName AS ClientName,
                    hm.RecordDate,
                    hm.Weight,
                    hm.Height,
                    hm.BMI,
                    hm.CaloriesBurned,
                    hm.BodyFat,
                    hm.Notes
                FROM HealthMetrics hm
                INNER JOIN Users u
                    ON u.UserId = hm.ClientId
                WHERE hm.MetricId = @MetricId;
            ";


                    using (SqlCommand cmd =
                        new SqlCommand(sql, con))
                    {
                        cmd.Parameters.Add(
                            "@MetricId",
                            SqlDbType.Int
                        ).Value =
                            id;


                        using (SqlDataReader reader =
                            await cmd.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                            {
                                return Json(new
                                {
                                    success = false,
                                    message =
                                        "Health metric was not found."
                                });
                            }


                            DateTime recordDate =
                                Convert.ToDateTime(
                                    reader["RecordDate"]
                                );


                            return Json(new
                            {
                                success = true,

                                metricId =
                                    Convert.ToInt32(
                                        reader["MetricId"]
                                    ),

                                clientId =
                                    Convert.ToInt32(
                                        reader["ClientId"]
                                    ),

                                clientName =
                                    Convert.ToString(
                                        reader["ClientName"]
                                    ) ?? "",

                                recordDate =
                                    recordDate.ToString(
                                        "yyyy-MM-dd"
                                    ),

                                weight =
                                    Convert.ToDecimal(
                                        reader["Weight"]
                                    ),

                                height =
                                    Convert.ToDecimal(
                                        reader["Height"]
                                    ),

                                bmi =
                                    reader["BMI"] == DBNull.Value
                                        ? (decimal?)null
                                        : Convert.ToDecimal(
                                            reader["BMI"]
                                        ),

                                caloriesBurned =
                                    reader["CaloriesBurned"] ==
                                        DBNull.Value
                                        ? (decimal?)null
                                        : Convert.ToDecimal(
                                            reader["CaloriesBurned"]
                                        ),

                                bodyFat =
                                    reader["BodyFat"] ==
                                        DBNull.Value
                                        ? (decimal?)null
                                        : Convert.ToDecimal(
                                            reader["BodyFat"]
                                        ),

                                notes =
                                    reader["Notes"] ==
                                        DBNull.Value
                                        ? null
                                        : Convert.ToString(
                                            reader["Notes"]
                                        )
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message =
                        "Unable to load health metric: " +
                        ex.Message
                });
            }
        }



        // =========================================================
        // EDIT HEALTH METRIC
        // POST: /Home/EditHealthMetric
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditHealthMetric(
            int MetricId,
            int ClientId,
            string RecordDate,
            decimal Weight,
            decimal Height,
            decimal? BMI,
            decimal? CaloriesBurned,
            decimal? BodyFat,
            string? Notes)
        {
            var admin =
                await GetLoggedInAdmin();


            if (admin == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            if (MetricId <= 0 ||
                ClientId <= 0 ||
                string.IsNullOrWhiteSpace(RecordDate) ||
                Weight <= 0 ||
                Height <= 0)
            {
                TempData["Error"] =
                    "Please fill in all required health metric fields.";

                return RedirectToAction(
                    nameof(HealthMetrics)
                );
            }


            if (!DateOnly.TryParse(
                    RecordDate,
                    out DateOnly recordDate))
            {
                TempData["Error"] =
                    "Invalid measurement date.";

                return RedirectToAction(
                    nameof(HealthMetrics)
                );
            }


            bool clientExists =
                await _context.Users.AnyAsync(u =>
                    u.UserId == ClientId &&
                    u.Role == "CLIENT");


            if (!clientExists)
            {
                TempData["Error"] =
                    "Selected client was not found.";

                return RedirectToAction(
                    nameof(HealthMetrics)
                );
            }


            // AUTO BMI CALCULATION
            decimal calculatedBmi =
                Weight /
                ((Height / 100m) *
                 (Height / 100m));


            try
            {
                using (SqlConnection con =
                    new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();


                    string sql = @"
                UPDATE HealthMetrics
                SET
                    ClientId = @ClientId,
                    RecordDate = @RecordDate,
                    Weight = @Weight,
                    Height = @Height,
                    BMI = @BMI,
                    CaloriesBurned = @CaloriesBurned,
                    BodyFat = @BodyFat,
                    Notes = @Notes
                WHERE MetricId = @MetricId;
            ";


                    using (SqlCommand cmd =
                        new SqlCommand(sql, con))
                    {
                        cmd.Parameters.Add(
                            "@MetricId",
                            SqlDbType.Int
                        ).Value =
                            MetricId;


                        cmd.Parameters.Add(
                            "@ClientId",
                            SqlDbType.Int
                        ).Value =
                            ClientId;


                        cmd.Parameters.Add(
                            "@RecordDate",
                            SqlDbType.Date
                        ).Value =
                            recordDate.ToDateTime(
                                TimeOnly.MinValue
                            );


                        cmd.Parameters.Add(
                            "@Weight",
                            SqlDbType.Decimal
                        ).Value =
                            Weight;


                        cmd.Parameters.Add(
                            "@Height",
                            SqlDbType.Decimal
                        ).Value =
                            Height;


                        cmd.Parameters.Add(
                            "@BMI",
                            SqlDbType.Decimal
                        ).Value =
                            calculatedBmi;


                        cmd.Parameters.Add(
                            "@CaloriesBurned",
                            SqlDbType.Decimal
                        ).Value =
                            CaloriesBurned.HasValue
                                ? CaloriesBurned.Value
                                : DBNull.Value;


                        cmd.Parameters.Add(
                            "@BodyFat",
                            SqlDbType.Decimal
                        ).Value =
                            BodyFat.HasValue
                                ? BodyFat.Value
                                : DBNull.Value;


                        cmd.Parameters.Add(
                            "@Notes",
                            SqlDbType.NVarChar,
                            500
                        ).Value =
                            string.IsNullOrWhiteSpace(Notes)
                                ? DBNull.Value
                                : Notes.Trim();


                        int rows =
                            await cmd.ExecuteNonQueryAsync();


                        if (rows == 0)
                        {
                            TempData["Error"] =
                                "Health metric was not found.";

                            return RedirectToAction(
                                nameof(HealthMetrics)
                            );
                        }
                    }
                }


                TempData["Success"] =
                    "Health metric updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to update health metric: " +
                    ex.Message;
            }


            return RedirectToAction(
                nameof(HealthMetrics)
            );
        }



        // =========================================================
        // DELETE HEALTH METRIC
        // POST: /Home/DeleteHealthMetric
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHealthMetric(
            int id)
        {
            var admin =
                await GetLoggedInAdmin();


            if (admin == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            try
            {
                using (SqlConnection con =
                    new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();


                    string sql = @"
                DELETE FROM HealthMetrics
                WHERE MetricId = @MetricId;
            ";


                    using (SqlCommand cmd =
                        new SqlCommand(sql, con))
                    {
                        cmd.Parameters.Add(
                            "@MetricId",
                            SqlDbType.Int
                        ).Value =
                            id;


                        int rows =
                            await cmd.ExecuteNonQueryAsync();


                        if (rows == 0)
                        {
                            TempData["Error"] =
                                "Health metric was not found.";
                        }
                        else
                        {
                            TempData["Success"] =
                                "Health metric deleted successfully.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    "Unable to delete health metric: " +
                    ex.Message;
            }


            return RedirectToAction(
                nameof(HealthMetrics)
            );
        }


        // =========================================================
        // ADMIN - REPORTS
        // =========================================================

        public async Task<IActionResult> Reports()
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            SetAdminViewData(admin);

            ViewBag.Clients = await _context.Users
                .Where(u => u.Role == "CLIENT")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.TotalClients =
                await _context.Users.CountAsync(u => u.Role == "CLIENT");

            ViewBag.TotalTrainers =
                await _context.Users.CountAsync(u => u.Role == "TRAINER");

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

            ViewBag.TotalNotifications =
                await _context.Notifications.CountAsync(
                    n => n.IsRead != true
                );

            var bmi = await _context.HealthMetrics
    .Where(x => x.Bmi.HasValue)
    .Select(x => x.Bmi!.Value)
    .ToListAsync();

            ViewBag.UnderweightBmi =
                bmi.Count(x => x < 18.5m);

            ViewBag.NormalBmi =
                bmi.Count(x => x >= 18.5m && x < 25m);

            ViewBag.OverweightBmi =
                bmi.Count(x => x >= 25m && x < 30m);

            ViewBag.ObeseBmi =
                bmi.Count(x => x >= 30m);

            ViewBag.RecentAppointments =
                await _context.Appointments
                    .Include(x => x.Client)
                    .Include(x => x.Trainer)
                    .OrderByDescending(x => x.AppointmentDate)
                    .ThenByDescending(x => x.AppointmentTime)
                    .Take(5)
                    .ToListAsync();

            ViewBag.RecentHealthMetrics =
                await _context.HealthMetrics
                    .Include(x => x.Client)
                    .OrderByDescending(x => x.RecordDate)
                    .Take(5)
                    .ToListAsync();

            return View("~/Views/Admin/Reports.cshtml");
        }


        // =========================================================
        // EXPORT PDF
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> ExportReportPdf(
            string reportType,
            int? clientId,
            string dateRange = "all")
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction("Login", "Account");
            }

            reportType =
                (reportType ?? "")
                .Trim()
                .ToLowerInvariant();

            string[] validReports =
            {
        "overall",
        "clients",
        "trainers",
        "appointments",
        "workoutplans",
        "workoutlogs",
        "dietplans",
        "meallogs",
        "healthmetrics",
        "fullclient"
    };

            if (!validReports.Contains(reportType))
            {
                TempData["Error"] =
                    "Please select a valid report type.";

                return RedirectToAction(nameof(Reports));
            }

            if (reportType == "fullclient" &&
                !clientId.HasValue)
            {
                TempData["Error"] =
                    "Please select a client for Full Client Report.";

                return RedirectToAction(nameof(Reports));
            }

            string? clientName = null;

            if (clientId.HasValue)
            {
                clientName =
                    await _context.Users
                        .Where(u =>
                            u.UserId == clientId.Value &&
                            u.Role == "CLIENT")
                        .Select(u => u.FullName)
                        .FirstOrDefaultAsync();

                if (clientName == null)
                {
                    TempData["Error"] =
                        "Selected client was not found.";

                    return RedirectToAction(nameof(Reports));
                }
            }

            DateOnly? fromDate =
                GetReportFromDate(dateRange);

            string dateText =
                GetReportDateText(dateRange);

            try
            {
                DataTable reportData =
                    await GetReportDataAsync(
                        reportType,
                        clientId,
                        fromDate
                    );

                DataTable? extraData = null;

                DataTable? appointments = null;

                DataTable? healthMetrics = null;

                DataTable? workoutLogs = null;

                DataTable? mealLogs = null;

                DataTable? workoutPlans = null;

                DataTable? dietPlans = null;


                // ---------------------------------------------------------
                // OVERALL REPORT
                // ---------------------------------------------------------

                if (reportType == "overall")
                {
                    extraData =
                        await GetOverallSummaryAsync(
                            clientId,
                            fromDate
                        );

                    appointments =
                        await GetReportDataAsync(
                            "appointments",
                            clientId,
                            fromDate
                        );

                    healthMetrics =
                        await GetReportDataAsync(
                            "healthmetrics",
                            clientId,
                            fromDate
                        );
                }


                // ---------------------------------------------------------
                // FULL CLIENT REPORT
                // ---------------------------------------------------------

                if (reportType == "fullclient")
                {
                    appointments =
                        await GetReportDataAsync(
                            "appointments",
                            clientId,
                            fromDate
                        );

                    workoutPlans =
                        await GetReportDataAsync(
                            "workoutplans",
                            clientId,
                            fromDate
                        );

                    dietPlans =
                        await GetReportDataAsync(
                            "dietplans",
                            clientId,
                            fromDate
                        );

                    healthMetrics =
                        await GetReportDataAsync(
                            "healthmetrics",
                            clientId,
                            fromDate
                        );

                    workoutLogs =
                        await GetReportDataAsync(
                            "workoutlogs",
                            clientId,
                            fromDate
                        );

                    mealLogs =
                        await GetReportDataAsync(
                            "meallogs",
                            clientId,
                            fromDate
                        );
                }


                // ---------------------------------------------------------
                // BUILD PDF
                // ---------------------------------------------------------

                byte[] pdf =
                    BuildReportPdf(
                        reportType,
                        clientName,
                        dateText,
                        reportData,
                        extraData,
                        appointments,
                        healthMetrics,
                        workoutPlans,
                        dietPlans,
                        workoutLogs,
                        mealLogs
                    );


                string reportName =
                    GetReportTitle(reportType);


                string clientPart =
                    string.IsNullOrWhiteSpace(clientName)
                        ? "All_Clients"
                        : SafeFileName(clientName);


                string fileName =
                    $"HealthTrack_{reportName}_{clientPart}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";


                return File(
                    pdf,
                    "application/pdf",
                    fileName
                );
            }
            catch (Exception ex)
            {
                return Content(
                    "PDF GENERATION ERROR:\n\n" +
                    ex.ToString(),
                    "text/plain"
                );
            }
        }


        // =========================================================
        // REPORT DATA
        // =========================================================

        private async Task<DataTable> GetReportDataAsync(
            string reportType,
            int? clientId,
            DateOnly? fromDate)
        {
            string sql;

            switch (reportType)
            {
                // =====================================================
                // CLIENTS
                // =====================================================

                case "fullclient":
                case "clients":

                    sql = @"
                SELECT
                    UserId AS [Client ID],
                    FullName AS [Full Name],
                    Email,
                    ISNULL(Phone, '-') AS Phone,
                    Status,
                    CreatedAt AS [Created]
                FROM Users
                WHERE Role = 'CLIENT'
            ";

                    if (clientId.HasValue)
                    {
                        sql +=
                            " AND UserId = @ClientId ";
                    }

                    if (fromDate.HasValue &&
                        reportType == "clients")
                    {
                        sql +=
                            " AND CreatedAt >= @FromDateTime ";
                    }

                    sql +=
                        " ORDER BY FullName;";

                    return await QueryTableAsync(
                        sql,
                        clientId,
                        fromDate
                    );


                // =====================================================
                // TRAINERS
                // =====================================================

                case "trainers":

                    sql = @"
                SELECT
                    UserId AS [Trainer ID],
                    FullName AS [Full Name],
                    Email,
                    ISNULL(Phone, '-') AS Phone,
                    Status,
                    CreatedAt AS [Created]
                FROM Users
                WHERE Role = 'TRAINER'
            ";

                    if (fromDate.HasValue)
                    {
                        sql +=
                            " AND CreatedAt >= @FromDateTime ";
                    }

                    sql +=
                        " ORDER BY FullName;";

                    return await QueryTableAsync(
                        sql,
                        null,
                        fromDate
                    );


                // =====================================================
                // APPOINTMENTS
                // =====================================================

                case "appointments":

                    sql = @"
                SELECT
                    a.AppointmentId AS [Appointment ID],
                    a.AppointmentDate AS [Date],
                    a.AppointmentTime AS [Time],
                    c.FullName AS [Client],
                    t.FullName AS [Trainer],
                    ISNULL(a.Status, '-') AS [Status],
                    ISNULL(a.Reason, '-') AS [Reason],
                    ISNULL(a.Notes, '-') AS [Notes]
                FROM Appointments a
                INNER JOIN Users c
                    ON c.UserId = a.ClientId
                INNER JOIN Users t
                    ON t.UserId = a.TrainerId
                WHERE 1 = 1
            ";

                    if (clientId.HasValue)
                    {
                        sql +=
                            " AND a.ClientId = @ClientId ";
                    }

                    if (fromDate.HasValue)
                    {
                        sql +=
                            " AND a.AppointmentDate >= @FromDate ";
                    }

                    sql +=
                        " ORDER BY " +
                        "a.AppointmentDate DESC, " +
                        "a.AppointmentTime DESC;";

                    return await QueryTableAsync(
                        sql,
                        clientId,
                        fromDate
                    );


                // =====================================================
                // WORKOUT PLANS
                // =====================================================

                case "workoutplans":

                    sql = @"
                SELECT
                    w.WorkoutPlanId AS [Plan ID],
                    w.PlanName AS [Plan],
                    c.FullName AS [Client],
                    t.FullName AS [Trainer],
                    w.StartDate AS [Start Date],
                    w.EndDate AS [End Date],
                    ISNULL(w.Status, '-') AS [Status],
                    ISNULL(w.Description, '-') AS [Description]
                FROM WorkoutPlans w
                INNER JOIN Users c
                    ON c.UserId = w.ClientId
                INNER JOIN Users t
                    ON t.UserId = w.TrainerId
                WHERE 1 = 1
            ";

                    if (clientId.HasValue)
                    {
                        sql +=
                            " AND w.ClientId = @ClientId ";
                    }

                    if (fromDate.HasValue)
                    {
                        sql +=
                            " AND w.StartDate >= @FromDate ";
                    }

                    sql +=
                        " ORDER BY w.StartDate DESC;";

                    return await QueryTableAsync(
                        sql,
                        clientId,
                        fromDate
                    );


                // =====================================================
                // DIET PLANS
                // =====================================================

                case "dietplans":

                    sql = @"
                SELECT
                    d.DietPlanId AS [Plan ID],
                    d.PlanName AS [Plan],
                    c.FullName AS [Client],
                    t.FullName AS [Trainer],
                    d.StartDate AS [Start Date],
                    d.EndDate AS [End Date],
                    ISNULL(d.Status, '-') AS [Status],
                    ISNULL(d.Description, '-') AS [Description]
                FROM DietPlans d
                INNER JOIN Users c
                    ON c.UserId = d.ClientId
                INNER JOIN Users t
                    ON t.UserId = d.TrainerId
                WHERE 1 = 1
            ";

                    if (clientId.HasValue)
                    {
                        sql +=
                            " AND d.ClientId = @ClientId ";
                    }

                    if (fromDate.HasValue)
                    {
                        sql +=
                            " AND d.StartDate >= @FromDate ";
                    }

                    sql +=
                        " ORDER BY d.StartDate DESC;";

                    return await QueryTableAsync(
                        sql,
                        clientId,
                        fromDate
                    );


                // =====================================================
                // HEALTH METRICS
                // =====================================================

                case "healthmetrics":

                    sql = @"
                SELECT
                    h.MetricId AS [Metric ID],
                    h.RecordDate AS [Date],
                    c.FullName AS [Client],
                    h.Weight,
                    h.Height,
                    h.BMI,
                    h.CaloriesBurned AS [Calories Burned],
                    h.BodyFat AS [Body Fat],
                    ISNULL(h.Notes, '-') AS [Notes]
                FROM HealthMetrics h
                INNER JOIN Users c
                    ON c.UserId = h.ClientId
                WHERE 1 = 1
            ";

                    if (clientId.HasValue)
                    {
                        sql +=
                            " AND h.ClientId = @ClientId ";
                    }

                    if (fromDate.HasValue)
                    {
                        sql +=
                            " AND h.RecordDate >= @FromDate ";
                    }

                    sql +=
                        " ORDER BY h.RecordDate DESC;";

                    return await QueryTableAsync(
                        sql,
                        clientId,
                        fromDate
                    );


                // =====================================================
                // WORKOUT LOGS
                // =====================================================

                case "workoutlogs":

                    return await GetLogTableAsync(
                        "WorkoutLogs",
                        clientId,
                        fromDate
                    );


                // =====================================================
                // MEAL LOGS
                // =====================================================

                case "meallogs":

                    return await GetLogTableAsync(
                        "MealLogs",
                        clientId,
                        fromDate
                    );


                default:

                    return new DataTable();
            }
        }


        // =========================================================
        // OVERALL SUMMARY
        // =========================================================

        private async Task<DataTable> GetOverallSummaryAsync(
            int? clientId,
            DateOnly? fromDate)
        {
            DataTable table =
                new DataTable();

            table.Columns.Add("Report");

            table.Columns.Add("Count");


            int clientsCount =
                clientId.HasValue
                    ? 1
                    : await CountReportTableAsync(
                        "Users",
                        "Role = 'CLIENT'",
                        null,
                        fromDate,
                        "CreatedAt"
                    );


            table.Rows.Add(
                "Clients",
                clientsCount
            );


            table.Rows.Add(
                "Trainers",
                await CountReportTableAsync(
                    "Users",
                    "Role = 'TRAINER'",
                    null,
                    fromDate,
                    "CreatedAt"
                )
            );


            table.Rows.Add(
                "Appointments",
                await CountReportTableAsync(
                    "Appointments",
                    "1 = 1",
                    clientId,
                    fromDate,
                    "AppointmentDate"
                )
            );


            table.Rows.Add(
                "Workout Plans",
                await CountReportTableAsync(
                    "WorkoutPlans",
                    "1 = 1",
                    clientId,
                    fromDate,
                    "StartDate"
                )
            );


            table.Rows.Add(
                "Diet Plans",
                await CountReportTableAsync(
                    "DietPlans",
                    "1 = 1",
                    clientId,
                    fromDate,
                    "StartDate"
                )
            );


            table.Rows.Add(
                "Workout Logs",
                await CountLogTableAsync(
                    "WorkoutLogs",
                    clientId,
                    fromDate
                )
            );


            table.Rows.Add(
                "Meal Logs",
                await CountLogTableAsync(
                    "MealLogs",
                    clientId,
                    fromDate
                )
            );


            table.Rows.Add(
                "Health Metrics",
                await CountReportTableAsync(
                    "HealthMetrics",
                    "1 = 1",
                    clientId,
                    fromDate,
                    "RecordDate"
                )
            );


            return table;
        }


        // =========================================================
        // COUNT HELPERS
        // =========================================================

        private async Task<int> CountReportTableAsync(
            string tableName,
            string condition,
            int? clientId,
            DateOnly? fromDate,
            string dateColumn)
        {
            string sql =
                $"SELECT COUNT(*) " +
                $"FROM [{tableName}] " +
                $"WHERE {condition}";


            if (clientId.HasValue &&
                tableName != "Users")
            {
                sql +=
                    " AND ClientId = @ClientId ";
            }


            if (fromDate.HasValue)
            {
                sql +=
                    $" AND [{dateColumn}] >= @FromDate ";
            }


            return await QueryScalarIntAsync(
                sql,
                clientId,
                fromDate
            );
        }


        // =========================================================
        // LOG COUNT
        // =========================================================

        private async Task<int> CountLogTableAsync(
            string tableName,
            int? clientId,
            DateOnly? fromDate)
        {
            string dateColumn =
                await FindLogDateColumnAsync(
                    tableName
                );


            string sql =
                $"SELECT COUNT(*) " +
                $"FROM [{tableName}] " +
                $"WHERE 1 = 1";


            if (clientId.HasValue)
            {
                sql +=
                    " AND ClientId = @ClientId ";
            }


            if (fromDate.HasValue &&
                !string.IsNullOrWhiteSpace(dateColumn))
            {
                sql +=
                    $" AND [{dateColumn}] >= @FromDate ";
            }


            return await QueryScalarIntAsync(
                sql,
                clientId,
                fromDate
            );
        }


        // =========================================================
        // GET LOG TABLE
        // =========================================================

        private async Task<DataTable> GetLogTableAsync(
            string tableName,
            int? clientId,
            DateOnly? fromDate)
        {
            string dateColumn =
                await FindLogDateColumnAsync(
                    tableName
                );


            string sql =
                $"SELECT * " +
                $"FROM [{tableName}] " +
                $"WHERE 1 = 1";


            if (clientId.HasValue)
            {
                sql +=
                    " AND ClientId = @ClientId ";
            }


            if (fromDate.HasValue &&
                !string.IsNullOrWhiteSpace(dateColumn))
            {
                sql +=
                    $" AND [{dateColumn}] >= @FromDate ";
            }


            if (!string.IsNullOrWhiteSpace(
                dateColumn))
            {
                sql +=
                    $" ORDER BY [{dateColumn}] DESC;";
            }


            return await QueryTableAsync(
                sql,
                clientId,
                fromDate
            );
        }


        // =========================================================
        // FIND LOG DATE COLUMN
        // =========================================================

        private async Task<string?>
            FindLogDateColumnAsync(
                string tableName)
        {
            string[] allowed =
            {
        "WorkoutLogs",
        "MealLogs"
    };


            if (!allowed.Contains(tableName))
            {
                return null;
            }


            string sql = @"
        SELECT TOP 1 COLUMN_NAME
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = @TableName
          AND COLUMN_NAME IN
          (
              'LogDate',
              'WorkoutDate',
              'MealDate',
              'RecordDate',
              'Date',
              'CreatedAt'
          )
        ORDER BY
            CASE COLUMN_NAME
                WHEN 'LogDate' THEN 1
                WHEN 'WorkoutDate' THEN 2
                WHEN 'MealDate' THEN 3
                WHEN 'RecordDate' THEN 4
                WHEN 'Date' THEN 5
                WHEN 'CreatedAt' THEN 6
                ELSE 99
            END;";


            object? result =
                await QueryScalarAsync(
                    sql,
                    new SqlParameter(
                        "@TableName",
                        SqlDbType.NVarChar,
                        128)
                    {
                        Value = tableName
                    }
                );


            return result == null ||
                   result == DBNull.Value
                ? null
                : Convert.ToString(result);
        }


        // =========================================================
        // DATABASE TABLE QUERY
        // =========================================================

        private async Task<DataTable>
            QueryTableAsync(
                string sql,
                int? clientId,
                DateOnly? fromDate)
        {
            DataTable table =
                new DataTable();


            using SqlConnection con =
                new SqlConnection(
                    _connectionString
                );


            await con.OpenAsync();


            using SqlCommand cmd =
                new SqlCommand(
                    sql,
                    con
                );


            if (clientId.HasValue)
            {
                cmd.Parameters.Add(
                    "@ClientId",
                    SqlDbType.Int
                ).Value =
                    clientId.Value;
            }


            if (fromDate.HasValue)
            {
                cmd.Parameters.Add(
                    "@FromDate",
                    SqlDbType.Date
                ).Value =
                    fromDate.Value.ToDateTime(
                        TimeOnly.MinValue
                    );


                cmd.Parameters.Add(
                    "@FromDateTime",
                    SqlDbType.DateTime
                ).Value =
                    fromDate.Value.ToDateTime(
                        TimeOnly.MinValue
                    );
            }


            using SqlDataReader reader =
                await cmd.ExecuteReaderAsync();


            table.Load(reader);


            return table;
        }


        // =========================================================
        // SCALAR INT QUERY
        // =========================================================

        private async Task<int>
            QueryScalarIntAsync(
                string sql,
                int? clientId,
                DateOnly? fromDate)
        {
            object? result =
                await QueryScalarAsync(
                    sql,
                    BuildParameters(
                        clientId,
                        fromDate
                    )
                );


            return result == null ||
                   result == DBNull.Value
                ? 0
                : Convert.ToInt32(result);
        }


        // =========================================================
        // SCALAR QUERY
        // =========================================================

        private async Task<object?>
            QueryScalarAsync(
                string sql,
                params SqlParameter[] parameters)
        {
            using SqlConnection con =
                new SqlConnection(
                    _connectionString
                );


            await con.OpenAsync();


            using SqlCommand cmd =
                new SqlCommand(
                    sql,
                    con
                );


            foreach (var parameter in parameters)
            {
                cmd.Parameters.Add(parameter);
            }


            return await cmd.ExecuteScalarAsync();
        }


        // =========================================================
        // BUILD SQL PARAMETERS
        // =========================================================

        private SqlParameter[]
            BuildParameters(
                int? clientId,
                DateOnly? fromDate)
        {
            var list =
                new List<SqlParameter>();


            if (clientId.HasValue)
            {
                list.Add(
                    new SqlParameter(
                        "@ClientId",
                        SqlDbType.Int)
                    {
                        Value =
                            clientId.Value
                    }
                );
            }


            if (fromDate.HasValue)
            {
                list.Add(
                    new SqlParameter(
                        "@FromDate",
                        SqlDbType.Date)
                    {
                        Value =
                            fromDate.Value.ToDateTime(
                                TimeOnly.MinValue
                            )
                    }
                );
            }


            return list.ToArray();
        }


        // =========================================================
        // BUILD PDF
        // =========================================================

        private byte[] BuildReportPdf(
            string reportType,
            string? clientName,
            string dateText,
            DataTable reportData,
            DataTable? extraData,
            DataTable? appointments,
            DataTable? healthMetrics,
            DataTable? workoutPlans,
            DataTable? dietPlans,
            DataTable? workoutLogs,
            DataTable? mealLogs)
        {
            QuestPDF.Settings.License =
                LicenseType.Community;


            string title =
                GetReportTitle(reportType)
                    .Replace(
                        "_",
                        " "
                    );


            var document =
                Document.Create(
                    document =>
                    {
                        document.Page(
                            page =>
                            {
                                page.Size(
                                    PageSizes.A4
                                );

                                page.Margin(28);

                                page.PageColor(
                                    Colors.White
                                );

                                page.DefaultTextStyle(
                                    x => x.FontSize(8)
                                );


                                // =================================================
                                // HEADER
                                // =================================================

                                page.Header()
                                    .Element(
                                        header =>
                                        {
                                            header
                                                .Background(
                                                    "#111827"
                                                )
                                                .Padding(15)
                                                .Column(
                                                    column =>
                                                    {
                                                        column.Item()
                                                            .Text(
                                                                "HEALTHTRACK"
                                                            )
                                                            .FontSize(18)
                                                            .Bold()
                                                            .FontColor(
                                                                "#E5092F"
                                                            );


                                                        column.Item()
                                                            .PaddingTop(3)
                                                            .Text(
                                                                title
                                                            )
                                                            .FontSize(13)
                                                            .Bold()
                                                            .FontColor(
                                                                "#FFFFFF"
                                                            );


                                                        column.Item()
                                                            .PaddingTop(4)
                                                            .Text(
                                                                clientName == null
                                                                    ? $"All Clients  •  {dateText}"
                                                                    : $"{clientName}  •  {dateText}"
                                                            )
                                                            .FontSize(8)
                                                            .FontColor(
                                                                "#D1D5DB"
                                                            );
                                                    }
                                                );
                                        }
                                    );


                                // =================================================
                                // CONTENT
                                // =================================================

                                page.Content()
                                    .PaddingTop(15)
                                    .Element(
                                        content =>
                                        {
                                            content.Column(
                                                column =>
                                                {
                                                    // ---------------------------------
                                                    // OVERALL
                                                    // ---------------------------------

                                                    if (reportType ==
                                                        "overall")
                                                    {
                                                        column.Item()
                                                            .Text(
                                                                "System Summary"
                                                            )
                                                            .FontSize(13)
                                                            .Bold()
                                                            .FontColor(
                                                                "#E5092F"
                                                            );


                                                        AddKpiTable(
                                                            column.Item(),
                                                            extraData
                                                        );


                                                        AddSection(
                                                            column.Item(),
                                                            "Appointments",
                                                            appointments
                                                        );


                                                        AddSection(
                                                            column.Item(),
                                                            "Health Metrics",
                                                            healthMetrics
                                                        );
                                                    }


                                                    // ---------------------------------
                                                    // FULL CLIENT
                                                    // ---------------------------------

                                                    else if (
                                                        reportType ==
                                                        "fullclient")
                                                    {
                                                        column.Item()
                                                            .Text(
                                                                "Client Profile"
                                                            )
                                                            .FontSize(13)
                                                            .Bold()
                                                            .FontColor(
                                                                "#E5092F"
                                                            );


                                                        AddTable(
                                                            column.Item(),
                                                            reportData
                                                        );


                                                        AddSection(
                                                            column.Item(),
                                                            "Appointments",
                                                            appointments
                                                        );


                                                        AddSection(
                                                            column.Item(),
                                                            "Workout Plans",
                                                            workoutPlans
                                                        );


                                                        AddSection(
                                                            column.Item(),
                                                            "Diet Plans",
                                                            dietPlans
                                                        );


                                                        AddSection(
                                                            column.Item(),
                                                            "Health Metrics",
                                                            healthMetrics
                                                        );


                                                        AddSection(
                                                            column.Item(),
                                                            "Workout Logs",
                                                            workoutLogs
                                                        );


                                                        AddSection(
                                                            column.Item(),
                                                            "Meal Logs",
                                                            mealLogs
                                                        );
                                                    }


                                                    // ---------------------------------
                                                    // NORMAL REPORT
                                                    // ---------------------------------

                                                    else
                                                    {
                                                        AddTable(
                                                            column.Item(),
                                                             reportData
                                                        );
                                                    }
                                                }
                                            );
                                        }
                                    );


                                // =================================================
                                // FOOTER
                                // =================================================

                                page.Footer()
                                    .AlignCenter()
                                    .Text(
                                        text =>
                                        {
                                            text.Span(
                                                "HealthTrack • Page "
                                            );

                                            text.CurrentPageNumber();

                                            text.Span(
                                                " / "
                                            );

                                            text.TotalPages();
                                        }
                                    );
                            }
                        );
                    }
                );


            return document.GeneratePdf();
        }

        // =========================================================
        // PDF SECTION
        // =========================================================

        private void AddSection(
            QuestPDF.Infrastructure.IContainer column,
            string title,
            DataTable? data)
        {
            column.Column(section =>
            {
                // Section title
                section.Item()
                    .PaddingTop(12)
                    .Text(title)
                    .FontSize(11)
                    .Bold()
                    .FontColor("#111827");


                // Section content
                section.Item()
                    .Element(content =>
                    {
                        if (data == null ||
                            data.Columns.Count == 0 ||
                            data.Rows.Count == 0)
                        {
                            content
                                .PaddingTop(4)
                                .Text("No records found.")
                                .FontColor("#64748B");

                            return;
                        }

                        AddTable(
                            content,
                            data
                        );
                    });
            });
        }


        // =========================================================
        // PDF KPI TABLE
        // =========================================================

        private void AddKpiTable(
            QuestPDF.Infrastructure.IContainer column,
            DataTable? data)
        {
            if (data == null ||
                data.Rows.Count == 0)
            {
                return;
            }


            column
                .PaddingTop(8)
                .Table(
                    table =>
                    {
                        table.ColumnsDefinition(
                            columns =>
                            {
                                columns.RelativeColumn();

                                columns.RelativeColumn();

                                columns.RelativeColumn();

                                columns.RelativeColumn();
                            }
                        );


                        foreach (DataRow row in
                                 data.Rows)
                        {
                            table.Cell()
                                .Padding(4)
                                .Border(1)
                                .BorderColor(
                                    "#E5E7EB"
                                )
                                .Background(
                                    "#F8FAFC"
                                )
                                .Padding(9)
                                .Column(
                                    c =>
                                    {
                                        c.Item()
                                            .Text(
                                                Convert.ToString(
                                                    row[0]
                                                ) ?? "-"
                                            )
                                            .FontSize(7)
                                            .FontColor(
                                                "#64748B"
                                            );


                                        c.Item()
                                            .PaddingTop(3)
                                            .Text(
                                                Convert.ToString(
                                                    row[1]
                                                ) ?? "0"
                                            )
                                            .FontSize(14)
                                            .Bold()
                                            .FontColor(
                                                "#111827"
                                            );
                                    }
                                );
                        }
                    }
                );
        }


        // =========================================================
        // PDF DATA TABLE
        // =========================================================

        private void AddTable(
            QuestPDF.Infrastructure.IContainer column,
            DataTable data)
        {
            if (data.Rows.Count == 0)
            {
                column
                    .PaddingTop(4)
                    .Text(
                        "No records found."
                    )
                    .FontColor(
                        "#64748B"
                    );

                return;
            }


            int maxColumns =
                Math.Min(
                    data.Columns.Count,
                    8
                );


            column
                .PaddingTop(6)
                .Table(
                    table =>
                    {
                        table.ColumnsDefinition(
                            columns =>
                            {
                                for (
                                    int i = 0;
                                    i < maxColumns;
                                    i++)
                                {
                                    columns.RelativeColumn();
                                }
                            }
                        );


                        // ---------------------------------------------
                        // HEADER
                        // ---------------------------------------------

                        table.Header(
                            header =>
                            {
                                for (
                                    int i = 0;
                                    i < maxColumns;
                                    i++)
                                {
                                    header.Cell()
                                        .Background(
                                            "#111827"
                                        )
                                        .Padding(6)
                                        .Text(
                                            data.Columns[i]
                                                .ColumnName
                                        )
                                        .FontSize(7)
                                        .Bold()
                                        .FontColor(
                                            "#FFFFFF"
                                        );
                                }
                            }
                        );


                        // ---------------------------------------------
                        // ROWS
                        // ---------------------------------------------

                        foreach (DataRow row in
                                 data.Rows)
                        {
                            for (
                                int i = 0;
                                i < maxColumns;
                                i++)
                            {
                                string value =
                                    row[i] == DBNull.Value
                                        ? "-"
                                        : Convert.ToString(
                                            row[i]
                                        ) ?? "-";


                                table.Cell()
                                    .BorderBottom(1)
                                    .BorderColor(
                                        "#E5E7EB"
                                    )
                                    .Padding(5)
                                    .Text(value)
                                    .FontSize(7)
                                    .FontColor(
                                        "#1F2937"
                                    );
                            }
                        }
                    }
                );
        }


        // =========================================================
        // REPORT DATE FILTER
        // =========================================================

        private DateOnly?
            GetReportFromDate(
                string? dateRange)
        {
            return (
                dateRange ?? "all"
            )
            .ToLowerInvariant()
            .Trim()
            switch
            {
                "7" =>
                    DateOnly.FromDateTime(
                        DateTime.Today.AddDays(-6)
                    ),

                "30" =>
                    DateOnly.FromDateTime(
                        DateTime.Today.AddDays(-29)
                    ),

                "90" =>
                    DateOnly.FromDateTime(
                        DateTime.Today.AddDays(-89)
                    ),

                _ =>
                    null
            };
        }


        // =========================================================
        // REPORT DATE TEXT
        // =========================================================

        private string GetReportDateText(
            string? dateRange)
        {
            return (
                dateRange ?? "all"
            )
            .ToLowerInvariant()
            .Trim()
            switch
            {
                "7" =>
                    "Last 7 Days",

                "30" =>
                    "Last 30 Days",

                "90" =>
                    "Last 90 Days",

                _ =>
                    "All Time"
            };
        }


        // =========================================================
        // REPORT TITLE
        // =========================================================

        private string GetReportTitle(
            string reportType)
        {
            return reportType switch
            {
                "overall" =>
                    "Overall_System_Report",

                "clients" =>
                    "Clients_Report",

                "trainers" =>
                    "Trainers_Report",

                "appointments" =>
                    "Appointments_Report",

                "workoutplans" =>
                    "Workout_Plans_Report",

                "workoutlogs" =>
                    "Workout_Logs_Report",

                "dietplans" =>
                    "Diet_Plans_Report",

                "meallogs" =>
                    "Meal_Logs_Report",

                "healthmetrics" =>
                    "Health_Metrics_Report",

                "fullclient" =>
                    "Full_Client_Report",

                _ =>
                    "HealthTrack_Report"
            };
        }


        // =========================================================
        // SAFE FILE NAME
        // =========================================================

        private string SafeFileName(
            string value)
        {
            foreach (
                char c in
                Path.GetInvalidFileNameChars())
            {
                value =
                    value.Replace(
                        c,
                        '_'
                    );
            }


            return value.Replace(
                " ",
                "_"
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
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            // -----------------------------------------------------
            // ADMIN INFORMATION
            // -----------------------------------------------------

            SetAdminViewData(admin);


            // -----------------------------------------------------
            // GET ALL SYSTEM NOTIFICATIONS
            // -----------------------------------------------------
            // Admin notification page displays all notifications
            // from the Notifications table.
            // -----------------------------------------------------

            var notifications = await _context.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();


            // -----------------------------------------------------
            // TOTAL NOTIFICATIONS
            // -----------------------------------------------------

            ViewBag.TotalNotifications =
                notifications.Count;


            // -----------------------------------------------------
            // UNREAD NOTIFICATIONS
            // -----------------------------------------------------

            ViewBag.UnreadNotifications =
                notifications.Count(n => n.IsRead != true);


            // -----------------------------------------------------
            // READ NOTIFICATIONS
            // -----------------------------------------------------

            ViewBag.ReadNotifications =
                notifications.Count(n => n.IsRead == true);


            // -----------------------------------------------------
            // SEND NOTIFICATIONS TO VIEW
            // -----------------------------------------------------

            ViewBag.Notifications =
                notifications;


            return View(
                "~/Views/Admin/Notifications.cshtml"
            );
        }


        // =========================================================
        // ADMIN - MARK NOTIFICATION AS READ
        // URL:
        // /Home/MarkNotificationAsRead
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationAsRead(
            int id
        )
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            // -----------------------------------------------------
            // FIND NOTIFICATION
            // -----------------------------------------------------

            var notification =
                await _context.Notifications
                    .FirstOrDefaultAsync(
                        n =>
                            n.NotificationId == id
                    );


            // -----------------------------------------------------
            // MARK AS READ
            // -----------------------------------------------------

            if (notification != null)
            {
                notification.IsRead = true;

                await _context.SaveChangesAsync();
            }


            return RedirectToAction(
                nameof(Notifications)
            );
        }


        // =========================================================
        // ADMIN - MARK ALL NOTIFICATIONS AS READ
        // URL:
        // /Home/MarkAllNotificationsAsRead
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllNotificationsAsRead()
        {
            var admin = await GetLoggedInAdmin();

            if (admin == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            // -----------------------------------------------------
            // GET ALL UNREAD NOTIFICATIONS
            // -----------------------------------------------------

            var notifications =
                await _context.Notifications
                    .Where(
                        n =>
                            n.IsRead != true
                    )
                    .ToListAsync();


            // -----------------------------------------------------
            // MARK ALL AS READ
            // -----------------------------------------------------

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }


            // -----------------------------------------------------
            // SAVE CHANGES
            // -----------------------------------------------------

            if (notifications.Count > 0)
            {
                await _context.SaveChangesAsync();
            }


            return RedirectToAction(
                nameof(Notifications)
            );
        }



        // =========================================================
        // TRAINER - PROFILE SETTINGS
        // URL: /Home/TrainerSettings
        // =========================================================
        public async Task<IActionResult> TrainerSettings()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users.FirstOrDefaultAsync(u =>
                u.UserId == userId.Value && u.Role == "TRAINER");

            if (trainer == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            ViewBag.UserName = RemoveDrPrefix(trainer.FullName);
            ViewBag.UserEmail = trainer.Email ?? "";
            ViewBag.UserRole = trainer.Role ?? "TRAINER";
            ViewBag.ProfileImage = GetProfileImage(trainer.ProfileImage);
            ViewBag.Phone = trainer.Phone ?? "";

            return View("~/Views/Trainer/TrainerSettings.cshtml");
        }


        // =========================================================
        // TRAINER - SAVE PROFILE SETTINGS
        // POST: /Home/SaveTrainerSettings
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTrainerSettings(
            string FullName,
            string Email,
            string Phone,
            string CurrentPassword,
            string NewPassword,
            string ConfirmPassword,
            Microsoft.AspNetCore.Http.IFormFile? ProfileImage)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users.FirstOrDefaultAsync(u =>
                u.UserId == userId.Value && u.Role == "TRAINER");

            if (trainer == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Email))
            {
                TempData["Error"] = "Name and email are required.";
                return RedirectToAction(nameof(TrainerSettings));
            }

            FullName = FullName.Trim();
            Email = Email.Trim();

            bool duplicateEmail = await _context.Users.AnyAsync(u =>
                u.Email == Email && u.UserId != trainer.UserId);

            if (duplicateEmail)
            {
                TempData["Error"] = "Another account already uses this email.";
                return RedirectToAction(nameof(TrainerSettings));
            }

            trainer.FullName = FullName;
            trainer.Email = Email;
            trainer.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();

            // PROFILE IMAGE
            if (ProfileImage != null && ProfileImage.Length > 0)
            {
                var extension = System.IO.Path.GetExtension(ProfileImage.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

                if (!allowedExtensions.Contains(extension))
                {
                    TempData["Error"] = "Only JPG, JPEG, PNG and WEBP images are allowed.";
                    return RedirectToAction(nameof(TrainerSettings));
                }

                if (ProfileImage.Length > 5 * 1024 * 1024)
                {
                    TempData["Error"] = "Profile image must be smaller than 5 MB.";
                    return RedirectToAction(nameof(TrainerSettings));
                }

                var webRoot = System.IO.Path.Combine(
                    Directory.GetCurrentDirectory(), "wwwroot", "images");

                Directory.CreateDirectory(webRoot);

                var fileName = "trainer_" + trainer.UserId + "_" +
                               DateTime.Now.ToString("yyyyMMddHHmmssfff") + extension;

                var filePath = System.IO.Path.Combine(webRoot, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await ProfileImage.CopyToAsync(stream);
                }

                trainer.ProfileImage = fileName;
            }

            // OPTIONAL PASSWORD CHANGE
            bool wantsPasswordChange =
                !string.IsNullOrWhiteSpace(CurrentPassword) ||
                !string.IsNullOrWhiteSpace(NewPassword) ||
                !string.IsNullOrWhiteSpace(ConfirmPassword);

            if (wantsPasswordChange)
            {
                if (string.IsNullOrWhiteSpace(CurrentPassword) ||
                    string.IsNullOrWhiteSpace(NewPassword) ||
                    string.IsNullOrWhiteSpace(ConfirmPassword))
                {
                    TempData["Error"] = "Please fill in all password fields.";
                    return RedirectToAction(nameof(TrainerSettings));
                }

                if (!VerifySettingsPassword(CurrentPassword, trainer.PasswordHash))
                {
                    TempData["Error"] = "Current password is incorrect.";
                    return RedirectToAction(nameof(TrainerSettings));
                }

                if (NewPassword.Length < 6)
                {
                    TempData["Error"] = "New password must contain at least 6 characters.";
                    return RedirectToAction(nameof(TrainerSettings));
                }

                if (NewPassword != ConfirmPassword)
                {
                    TempData["Error"] = "New passwords do not match.";
                    return RedirectToAction(nameof(TrainerSettings));
                }

                trainer.PasswordHash = HashClientPassword(NewPassword);
            }

            await _context.SaveChangesAsync();

            HttpContext.Session.SetString("FullName", trainer.FullName ?? "");
            HttpContext.Session.SetString("Email", trainer.Email ?? "");
            HttpContext.Session.SetString("Role", trainer.Role ?? "TRAINER");
            HttpContext.Session.SetString("ProfileImage", trainer.ProfileImage ?? "");

            TempData["Success"] = "Profile settings updated successfully.";
            return RedirectToAction(nameof(TrainerSettings));
        }


        // =========================================================
        // LOGOUT
        // =========================================================
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
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
            ViewBag.AdminPhone = admin.Phone ?? "";

            return View(
                "~/Views/Admin/Settings.cshtml"
            );
        }


        // =========================================================
        // ADMIN - SAVE SETTINGS
        // POST:
        // /Home/SaveSettings
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(
            string FullName,
            string Email,
            string Phone,
            string Role,
            string CurrentPassword,
            string NewPassword,
            string ConfirmPassword,
            Microsoft.AspNetCore.Http.IFormFile? ProfileImage)
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var admin =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.UserId == userId.Value
                    );

            if (admin == null || admin.Role != "ADMIN")
            {
                return RedirectToAction("Index", "Home");
            }

            // -----------------------------
            // BASIC VALIDATION
            // -----------------------------

            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Email))
            {
                TempData["Error"] =
                    "Name and email are required.";

                return RedirectToAction(nameof(Settings));
            }

            FullName = FullName.Trim();
            Email = Email.Trim();

            // -----------------------------
            // VALID ROLE
            // -----------------------------

            Role = (Role ?? "ADMIN").Trim().ToUpperInvariant();

            if (Role != "ADMIN" &&
                Role != "TRAINER" &&
                Role != "CLIENT")
            {
                TempData["Error"] =
                    "Invalid role selected.";

                return RedirectToAction(nameof(Settings));
            }

            // -----------------------------
            // EMAIL DUPLICATE CHECK
            // -----------------------------

            bool duplicateEmail =
                await _context.Users.AnyAsync(
                    u =>
                        u.Email == Email &&
                        u.UserId != admin.UserId
                );

            if (duplicateEmail)
            {
                TempData["Error"] =
                    "Another account already uses this email.";

                return RedirectToAction(nameof(Settings));
            }

            // -----------------------------
            // UPDATE BASIC INFORMATION
            // -----------------------------

            admin.FullName = FullName;
            admin.Email = Email;

            admin.Phone =
                string.IsNullOrWhiteSpace(Phone)
                    ? null
                    : Phone.Trim();

            // -----------------------------
            // PROFILE IMAGE
            // -----------------------------

            if (ProfileImage != null &&
                ProfileImage.Length > 0)
            {
                var extension =
                    System.IO.Path
                        .GetExtension(ProfileImage.FileName)
                        .ToLowerInvariant();

                var allowedExtensions =
                    new[] { ".jpg", ".jpeg", ".png", ".webp" };

                if (!allowedExtensions.Contains(extension))
                {
                    TempData["Error"] =
                        "Only JPG, JPEG, PNG and WEBP images are allowed.";

                    return RedirectToAction(nameof(Settings));
                }

                if (ProfileImage.Length > 5 * 1024 * 1024)
                {
                    TempData["Error"] =
                        "Profile image must be smaller than 5 MB.";

                    return RedirectToAction(nameof(Settings));
                }

                var webRoot =
                    System.IO.Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        "images"
                    );

                Directory.CreateDirectory(webRoot);

                var fileName =
                    "admin_" +
                    admin.UserId +
                    "_" +
                    DateTime.Now.ToString("yyyyMMddHHmmssfff") +
                    extension;

                var filePath =
                    System.IO.Path.Combine(
                        webRoot,
                        fileName
                    );

                using (var stream =
                       new FileStream(
                           filePath,
                           FileMode.Create))
                {
                    await ProfileImage.CopyToAsync(stream);
                }

                admin.ProfileImage = fileName;
            }

            // -----------------------------
            // PASSWORD CHANGE
            // -----------------------------

            bool wantsPasswordChange =
                !string.IsNullOrWhiteSpace(CurrentPassword) ||
                !string.IsNullOrWhiteSpace(NewPassword) ||
                !string.IsNullOrWhiteSpace(ConfirmPassword);

            if (wantsPasswordChange)
            {
                if (string.IsNullOrWhiteSpace(CurrentPassword) ||
                    string.IsNullOrWhiteSpace(NewPassword) ||
                    string.IsNullOrWhiteSpace(ConfirmPassword))
                {
                    TempData["Error"] =
                        "Please fill in all password fields.";

                    return RedirectToAction(nameof(Settings));
                }

                if (!VerifySettingsPassword(
                        CurrentPassword,
                        admin.PasswordHash))
                {
                    TempData["Error"] =
                        "Current password is incorrect.";

                    return RedirectToAction(nameof(Settings));
                }

                if (NewPassword.Length < 6)
                {
                    TempData["Error"] =
                        "New password must contain at least 6 characters.";

                    return RedirectToAction(nameof(Settings));
                }

                if (NewPassword != ConfirmPassword)
                {
                    TempData["Error"] =
                        "New passwords do not match.";

                    return RedirectToAction(nameof(Settings));
                }

                admin.PasswordHash =
                    HashClientPassword(NewPassword);
            }

            // -----------------------------
            // ROLE CHANGE
            // -----------------------------

            bool roleChanged =
                !string.Equals(
                    admin.Role,
                    Role,
                    StringComparison.OrdinalIgnoreCase
                );

            admin.Role = Role;

            await _context.SaveChangesAsync();

            // Keep session information synchronized.
            HttpContext.Session.SetString(
                "FullName",
                admin.FullName ?? ""
            );

            HttpContext.Session.SetString(
                "Email",
                admin.Email ?? ""
            );

            HttpContext.Session.SetString(
                "Role",
                admin.Role ?? ""
            );

            HttpContext.Session.SetString(
                "ProfileImage",
                admin.ProfileImage ?? ""
            );

            // If the Admin changes their own role away from ADMIN,
            // end the admin session and send them back to Login.
            if (roleChanged && admin.Role != "ADMIN")
            {
                HttpContext.Session.Clear();

                TempData["Success"] =
                    "Your account was updated successfully. Please login again with your new role.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            TempData["Success"] =
                "Settings updated successfully.";

            return RedirectToAction(nameof(Settings));
        }


        // =========================================================
        // SETTINGS PASSWORD VERIFY
        // SAME FORMAT USED BY ACCOUNTCONTROLLER
        // =========================================================

        private static bool VerifySettingsPassword(
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

                return System.Security.Cryptography
                    .CryptographicOperations
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



        // ============================================================
        // TRAINER DASHBOARD
        // ============================================================
        public async Task<IActionResult> TrainerDashboard()
        {
            // ------------------------------------------------------------
            // 1. Get logged-in trainer
            // ------------------------------------------------------------
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // ------------------------------------------------------------
            // 2. Get trainer details
            // ------------------------------------------------------------
            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // ------------------------------------------------------------
            // 3. Trainer profile information
            // ------------------------------------------------------------
            ViewBag.UserName = RemoveDrPrefix(trainer.FullName);
            ViewBag.UserEmail = trainer.Email;
            ViewBag.UserRole = trainer.Role;
            ViewBag.ProfileImage = GetProfileImage(trainer.ProfileImage);

            // ------------------------------------------------------------
            // 4. Get ALL appointments belonging to this trainer
            // ------------------------------------------------------------
            var trainerAppointments = await _context.Appointments
                .Where(a => a.TrainerId == trainer.UserId)
                .Include(a => a.Client)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.AppointmentTime)
                .ToListAsync();

            // ------------------------------------------------------------
            // 5. Recent appointments
            // ------------------------------------------------------------
            var recentAppointments = trainerAppointments
                .Take(5)
                .ToList();



            // ------------------------------------------------------------
            // 6. Upcoming appointments
            // ------------------------------------------------------------

            var today =
     DateOnly.FromDateTime(
         DateTime.Today
     );

            var currentTime =
                TimeOnly.FromDateTime(
                    DateTime.Now
                );


            // ---------------------------------------------------------
            // Upcoming appointments for this trainer
            // ---------------------------------------------------------

            var upcomingAppointments =
                trainerAppointments
                    .Where(a =>
                        a.Status == null ||
                        a.Status != "Cancelled"
                    )
                    .Where(a =>
                        a.AppointmentDate > today
                        ||
                        (
                            a.AppointmentDate == today
                            &&
                            a.AppointmentTime >= currentTime
                        )
                    )
                    .Take(10)
                    .ToList();


            var upcomingAppointment =
                upcomingAppointments.FirstOrDefault();


            ViewBag.UpcomingAppointments =
                upcomingAppointments.Count;

            ViewBag.UpcomingAppointment =
                upcomingAppointment;

            // ------------------------------------------------------------
            // 7. Get trainer workout plans
            // ------------------------------------------------------------
            var workoutPlans = await _context.WorkoutPlans
                .Where(w => w.TrainerId == trainer.UserId)
                .Include(w => w.Client)
                .OrderByDescending(w => w.WorkoutPlanId)
                .ToListAsync();

            // ------------------------------------------------------------
            // 8. Get trainer diet plans
            // ------------------------------------------------------------
            var dietPlans = await _context.DietPlans
                .Where(d => d.TrainerId == trainer.UserId)
                .Include(d => d.Client)
                .OrderByDescending(d => d.DietPlanId)
                .ToListAsync();

            // ------------------------------------------------------------
            // 9. Find clients connected to this trainer
            //
            // Clients are identified through:
            // - Appointments
            // - Workout Plans
            // - Diet Plans
            // ------------------------------------------------------------
            var appointmentClientIds = trainerAppointments
                .Where(a => a.ClientId > 0)
                .Select(a => a.ClientId)
                .Distinct()
                .ToList();

            var workoutClientIds = workoutPlans
                .Where(w => w.ClientId > 0)
                .Select(w => w.ClientId)
                .Distinct()
                .ToList();

            var dietClientIds = dietPlans
                .Where(d => d.ClientId > 0)
                .Select(d => d.ClientId)
                .Distinct()
                .ToList();

            var clientIds = appointmentClientIds
                .Union(workoutClientIds)
                .Union(dietClientIds)
                .Distinct()
                .ToList();

            // ------------------------------------------------------------
            // 10. Load actual client users
            // ------------------------------------------------------------
            var clients = await _context.Users
                .Where(u =>
                    u.Role == "CLIENT" &&
                    clientIds.Contains(u.UserId))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            // ------------------------------------------------------------
            // 11. Dashboard statistics
            // ------------------------------------------------------------

            // Total clients belonging to this trainer
            ViewBag.TotalClients = clients.Count;

            // Total appointments belonging to this trainer
            ViewBag.TotalAppointments = trainerAppointments.Count;

            // Total workout plans belonging to this trainer
            ViewBag.TotalWorkoutPlans = workoutPlans.Count;

            // Total diet plans belonging to this trainer
            ViewBag.TotalDietPlans = dietPlans.Count;

            // ------------------------------------------------------------
            // 12. Notifications
            // ------------------------------------------------------------
            ViewBag.TotalNotifications = await _context.Notifications
                .CountAsync(n =>
                    n.UserId == trainer.UserId &&
                    n.IsRead != true);

            // ------------------------------------------------------------
            // 13. Additional dashboard counts
            // ------------------------------------------------------------

            ViewBag.TotalWorkoutLogs = await _context.WorkoutLogs
                .CountAsync();

            ViewBag.TotalMealLogs = await _context.MealLogs
                .CountAsync();

            ViewBag.TotalHealthMetrics = await _context.HealthMetrics
                .CountAsync();

            // ------------------------------------------------------------
            // 14. Send lists to Trainer Dashboard View
            // ------------------------------------------------------------

            ViewBag.Clients = clients;

            ViewBag.TrainerAppointments = trainerAppointments;

            ViewBag.RecentAppointments = recentAppointments;

            ViewBag.UpcomingAppointments = upcomingAppointments;

            ViewBag.WorkoutPlansList = workoutPlans;

            ViewBag.DietPlansList = dietPlans;

            // ------------------------------------------------------------
            // 15. Open Trainer Dashboard
            // ------------------------------------------------------------
            return View("~/Views/Trainer/TrainerDashboard.cshtml");
        }

        public async Task<IActionResult> MyClients()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");

            // Trainer information
            ViewBag.UserName = RemoveDrPrefix(trainer.FullName);
            ViewBag.UserEmail = trainer.Email;
            ViewBag.UserRole = trainer.Role;
            ViewBag.ProfileImage = GetProfileImage(trainer.ProfileImage);

            // ---------------------------------------------------------
            // 1. Get trainer's appointments
            // ---------------------------------------------------------
            var trainerAppointments = await _context.Appointments
                .Where(a => a.TrainerId == trainer.UserId)
                .Include(a => a.Client)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.AppointmentTime)
                .ToListAsync();

            // ---------------------------------------------------------
            // 2. Get trainer's workout plans
            // ---------------------------------------------------------
            var workoutPlans = await _context.WorkoutPlans
                .Where(w => w.TrainerId == trainer.UserId)
                .Include(w => w.Client)
                .OrderByDescending(w => w.WorkoutPlanId)
                .ToListAsync();

            // ---------------------------------------------------------
            // 3. Get trainer's diet plans
            // ---------------------------------------------------------
            var dietPlans = await _context.DietPlans
                .Where(d => d.TrainerId == trainer.UserId)
                .Include(d => d.Client)
                .OrderByDescending(d => d.DietPlanId)
                .ToListAsync();

            // ---------------------------------------------------------
            // 4. Find all unique clients belonging to this trainer
            // ---------------------------------------------------------
            var appointmentClientIds = trainerAppointments
                .Where(a => a.ClientId > 0)
                .Select(a => a.ClientId)
                .Distinct()
                .ToList();

            var workoutClientIds = workoutPlans
                .Where(w => w.ClientId > 0)
                .Select(w => w.ClientId)
                .Distinct()
                .ToList();

            var dietClientIds = dietPlans
                .Where(d => d.ClientId > 0)
                .Select(d => d.ClientId)
                .Distinct()
                .ToList();

            var clientIds = appointmentClientIds
                .Union(workoutClientIds)
                .Union(dietClientIds)
                .Distinct()
                .ToList();

            // ---------------------------------------------------------
            // 5. Load clients from Users table
            // ---------------------------------------------------------
            var clients = await _context.Users
                .Where(u =>
                    u.Role == "CLIENT" &&
                    clientIds.Contains(u.UserId))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            // ---------------------------------------------------------
            // 6. Client appointment counts
            // ---------------------------------------------------------
            var clientAppointmentCounts = trainerAppointments
                .GroupBy(a => a.ClientId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count());

            // ---------------------------------------------------------
            // 7. Client workout plan counts
            // ---------------------------------------------------------
            var clientWorkoutCounts = workoutPlans
                .GroupBy(w => w.ClientId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count());

            // ---------------------------------------------------------
            // 8. Client diet plan counts
            // ---------------------------------------------------------
            var clientDietCounts = dietPlans
                .GroupBy(d => d.ClientId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count());

            // ---------------------------------------------------------
            // 9. Upcoming appointments
            // ---------------------------------------------------------
            var today = DateOnly.FromDateTime(DateTime.Today);
            var currentTime = TimeOnly.FromDateTime(DateTime.Now);

            var upcomingAppointments = trainerAppointments
                .Where(a =>
                    !string.Equals(
                        a.Status,
                        "Cancelled",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    (
                        a.AppointmentDate > today
                        ||
                        (
                            a.AppointmentDate == today &&
                            a.AppointmentTime >= currentTime
                        )
                    ))
                .ToList();

            // ---------------------------------------------------------
            // 10. Active clients
            // ---------------------------------------------------------
            var activeClients = clients
                .Count(c =>
                    string.Equals(
                        c.Status,
                        "Active",
                        StringComparison.OrdinalIgnoreCase));

            // ---------------------------------------------------------
            // 11. ViewBag data
            // ---------------------------------------------------------
            ViewBag.Clients = clients;

            ViewBag.TotalClients = clients.Count;

            ViewBag.ActiveClients = activeClients;

            ViewBag.UpcomingAppointments =
                upcomingAppointments.Count;

            ViewBag.TotalWorkoutPlans =
                workoutPlans.Count;

            ViewBag.TotalDietPlans =
                dietPlans.Count;

            ViewBag.ClientAppointmentCounts =
                clientAppointmentCounts;

            ViewBag.ClientWorkoutCounts =
                clientWorkoutCounts;

            ViewBag.ClientDietCounts =
                clientDietCounts;

            // ---------------------------------------------------------
            // 12. Return My Clients page
            // ---------------------------------------------------------
            return View("~/Views/Trainer/MyClients.cshtml");
        }
        // ============================================================
        // TRAINER - APPOINTMENTS
        // ============================================================
        public async Task<IActionResult> TrainerAppointments(string? search)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");

            ViewBag.UserName = RemoveDrPrefix(trainer.FullName);
            ViewBag.UserEmail = trainer.Email;
            ViewBag.UserRole = trainer.Role;
            ViewBag.ProfileImage = GetProfileImage(trainer.ProfileImage);

            var query = _context.Appointments
                .Where(a => a.TrainerId == trainer.UserId)
                .Include(a => a.Client)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(a =>
                    (a.Client != null && a.Client.FullName.Contains(search)) ||
                    (a.Reason != null && a.Reason.Contains(search)) ||
                    (a.Status != null && a.Status.Contains(search)));
            }

            var appointments = await query
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.AppointmentTime)
                .ToListAsync();

            ViewBag.Appointments = appointments;
            ViewBag.Search = search ?? "";
            ViewBag.TotalAppointments = appointments.Count;
            ViewBag.PendingAppointments = appointments.Count(a =>
                string.Equals(a.Status, "Pending", StringComparison.OrdinalIgnoreCase));
            ViewBag.ConfirmedAppointments = appointments.Count(a =>
                string.Equals(a.Status, "Confirmed", StringComparison.OrdinalIgnoreCase));
            ViewBag.CompletedAppointments = appointments.Count(a =>
                string.Equals(a.Status, "Completed", StringComparison.OrdinalIgnoreCase));

            return View("~/Views/Trainer/TrainerAppointments.cshtml");
        }

        // ============================================================
        // TRAINER - UPDATE APPOINTMENT STATUS
        // POST: /Home/UpdateTrainerAppointmentStatus
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTrainerAppointmentStatus(
            int appointmentId,
            string status)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");

            var allowedStatuses = new[]
            {
        "Pending",
        "Confirmed",
        "Approved",
        "Completed",
        "Cancelled"
    };

            if (string.IsNullOrWhiteSpace(status) ||
                !allowedStatuses.Contains(
                    status.Trim(),
                    StringComparer.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Invalid appointment status.";
                return RedirectToAction(nameof(TrainerAppointments));
            }

            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(a =>
                    a.AppointmentId == appointmentId &&
                    a.TrainerId == trainer.UserId);

            if (appointment == null)
            {
                TempData["Error"] = "Appointment was not found.";
                return RedirectToAction(nameof(TrainerAppointments));
            }

            appointment.Status = status.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Appointment status updated successfully.";

            return RedirectToAction(nameof(TrainerAppointments));
        }
        // ============================================================
        // TRAINER - WORKOUT PLANS
        // ============================================================
        public async Task<IActionResult> TrainerWorkoutPlans(string? search)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");

            ViewBag.UserName = RemoveDrPrefix(trainer.FullName);
            ViewBag.UserEmail = trainer.Email;
            ViewBag.UserRole = trainer.Role;
            ViewBag.ProfileImage = GetProfileImage(trainer.ProfileImage);

            // ---------------------------------------------------------
            // CLIENTS
            // ---------------------------------------------------------

            // ---------------------------------------------------------
            // TRAINER'S CLIENTS ONLY
            // ---------------------------------------------------------

            // Trainer's appointments
            var trainerAppointmentClientIds = await _context.Appointments
                .Where(a =>
                    a.TrainerId == trainer.UserId &&
                    a.ClientId > 0)
                .Select(a => a.ClientId)
                .Distinct()
                .ToListAsync();


            // Trainer's workout plan clients
            var trainerWorkoutClientIds = await _context.WorkoutPlans
                .Where(w =>
                    w.TrainerId == trainer.UserId &&
                    w.ClientId > 0)
                .Select(w => w.ClientId)
                .Distinct()
                .ToListAsync();


            // Trainer's diet plan clients
            var trainerDietClientIds = await _context.DietPlans
                .Where(d =>
                    d.TrainerId == trainer.UserId &&
                    d.ClientId > 0)
                .Select(d => d.ClientId)
                .Distinct()
                .ToListAsync();


            // Combine all trainer client IDs
            var trainerClientIds = trainerAppointmentClientIds
                .Union(trainerWorkoutClientIds)
                .Union(trainerDietClientIds)
                .Distinct()
                .ToList();


            // Load ONLY this trainer's clients
            var clients = await _context.Users
                .Where(u =>
                    u.Role == "CLIENT" &&
                    trainerClientIds.Contains(u.UserId))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.Clients = clients;

            // ---------------------------------------------------------
            // WORKOUT PLANS
            // ---------------------------------------------------------

            // ---------------------------------------------------------
            // ALL WORKOUT PLANS
            // ---------------------------------------------------------
            // Show every workout plan so the trainer can choose the
            // correct plan and assign/reassign it to his own client.
            var query = _context.WorkoutPlans
                .AsNoTracking()
                .Include(w => w.Client)
                .Include(w => w.Trainer)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(w =>
                    w.PlanName.Contains(search) ||
                    (w.Description != null &&
                     w.Description.Contains(search)) ||
                    (w.Client != null &&
                     w.Client.FullName.Contains(search)) ||
                    (w.Status != null &&
                     w.Status.Contains(search)));
            }

            var workoutPlans = await query
                .OrderByDescending(w => w.WorkoutPlanId)
                .ToListAsync();

            ViewBag.WorkoutPlans = workoutPlans;
            ViewBag.Search = search ?? "";

            ViewBag.TotalWorkoutPlans =
                workoutPlans.Count;

            ViewBag.ActiveWorkoutPlans =
                workoutPlans.Count(w =>
                    string.Equals(
                        w.Status,
                        "Active",
                        StringComparison.OrdinalIgnoreCase));

            ViewBag.CompletedWorkoutPlans =
                workoutPlans.Count(w =>
                    string.Equals(
                        w.Status,
                        "Completed",
                        StringComparison.OrdinalIgnoreCase));

            return View("~/Views/Trainer/WorkoutPlans.cshtml");
        }

        // ============================================================
        // TRAINER - ASSIGN WORKOUT PLAN TO CLIENT
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTrainerWorkoutPlan(
            int workoutPlanId,
            int clientId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");

            // Any workout plan can be selected.
            // When assigned, ownership is moved to the current trainer.
            var plan = await _context.WorkoutPlans
                .FirstOrDefaultAsync(w =>
                    w.WorkoutPlanId == workoutPlanId);

            if (plan == null)
            {
                TempData["Error"] =
                    "Workout plan was not found.";

                return RedirectToAction(
                    nameof(TrainerWorkoutPlans));
            }

            // Only allow CLIENT users
            // who are already connected to this trainer
            var trainerClientIds = await _context.Appointments
                .Where(a =>
                    a.TrainerId == trainer.UserId &&
                    a.ClientId > 0)
                .Select(a => a.ClientId)
                .Distinct()
                .ToListAsync();

            var workoutClientIds = await _context.WorkoutPlans
                .Where(w =>
                    w.TrainerId == trainer.UserId &&
                    w.ClientId > 0)
                .Select(w => w.ClientId)
                .Distinct()
                .ToListAsync();

            var dietClientIds = await _context.DietPlans
                .Where(d =>
                    d.TrainerId == trainer.UserId &&
                    d.ClientId > 0)
                .Select(d => d.ClientId)
                .Distinct()
                .ToListAsync();

            var allowedClientIds = trainerClientIds
                .Union(workoutClientIds)
                .Union(dietClientIds)
                .Distinct()
                .ToList();

            if (!allowedClientIds.Contains(clientId))
            {
                TempData["Error"] =
                    "This client is not assigned to you.";

                return RedirectToAction(
                    nameof(TrainerWorkoutPlans));
            }

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == clientId &&
                    u.Role == "CLIENT");

            if (client == null)
            {
                TempData["Error"] =
                    "Selected client was not found.";

                return RedirectToAction(
                    nameof(TrainerWorkoutPlans));
            }

            // Assign workout plan to the selected client
            // and make the current trainer its assigned trainer.
            plan.TrainerId = trainer.UserId;
            plan.ClientId = client.UserId;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Workout plan assigned to " +
                client.FullName +
                " successfully.";

            return RedirectToAction(
                nameof(TrainerWorkoutPlans));
        }

        // ============================================================
        // TRAINER - DIET PLANS
        // ============================================================
        public async Task<IActionResult> TrainerDietPlans(string? search)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");

            ViewBag.UserName = RemoveDrPrefix(trainer.FullName);
            ViewBag.UserEmail = trainer.Email;
            ViewBag.UserRole = trainer.Role;
            ViewBag.ProfileImage = GetProfileImage(trainer.ProfileImage);

            // ---------------------------------------------------------
            // TRAINER'S DIET PLANS
            // ---------------------------------------------------------

            var query = _context.DietPlans
                .AsNoTracking()
                .Include(d => d.Client)
                .Include(d => d.Trainer)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(d =>
                    d.PlanName.Contains(search) ||
                    (d.Description != null &&
                     d.Description.Contains(search)) ||
                    (d.Client != null &&
                     d.Client.FullName.Contains(search)) ||
                    (d.Status != null &&
                     d.Status.Contains(search)));
            }

            var dietPlans = await query
                .OrderByDescending(d => d.DietPlanId)
                .ToListAsync();

            // ---------------------------------------------------------
            // TRAINER'S CLIENTS ONLY
            // ---------------------------------------------------------

            var appointmentClientIds = await _context.Appointments
                .Where(a =>
                    a.TrainerId == trainer.UserId &&
                    a.ClientId > 0)
                .Select(a => a.ClientId)
                .Distinct()
                .ToListAsync();

            var workoutClientIds = await _context.WorkoutPlans
                .Where(w =>
                    w.TrainerId == trainer.UserId &&
                    w.ClientId > 0)
                .Select(w => w.ClientId)
                .Distinct()
                .ToListAsync();

            var dietClientIds = await _context.DietPlans
                .Where(d =>
                    d.TrainerId == trainer.UserId &&
                    d.ClientId > 0)
                .Select(d => d.ClientId)
                .Distinct()
                .ToListAsync();

            var trainerClientIds = appointmentClientIds
                .Union(workoutClientIds)
                .Union(dietClientIds)
                .Distinct()
                .ToList();

            var clients = await _context.Users
                .Where(u =>
                    u.Role == "CLIENT" &&
                    trainerClientIds.Contains(u.UserId))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.Clients = clients;

            // ---------------------------------------------------------
            // VIEW DATA
            // ---------------------------------------------------------

            ViewBag.DietPlans = dietPlans;
            ViewBag.Search = search ?? "";

            ViewBag.TotalDietPlans =
                dietPlans.Count;

            ViewBag.ActiveDietPlans =
                dietPlans.Count(d =>
                    string.Equals(
                        d.Status,
                        "Active",
                        StringComparison.OrdinalIgnoreCase));

            ViewBag.CompletedDietPlans =
                dietPlans.Count(d =>
                    string.Equals(
                        d.Status,
                        "Completed",
                        StringComparison.OrdinalIgnoreCase));

            return View("~/Views/Trainer/DietPlans.cshtml");
        }


        // ============================================================
        // TRAINER - ASSIGN ANY DIET PLAN TO THIS TRAINER'S CLIENT
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTrainerDietPlan(
            int dietPlanId,
            int clientId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");

            // All diet plans are visible on the trainer page.
            // Therefore, do NOT restrict the plan by its old TrainerId.
            var plan = await _context.DietPlans
                .FirstOrDefaultAsync(d =>
                    d.DietPlanId == dietPlanId);

            if (plan == null)
            {
                TempData["Error"] =
                    "Diet plan was not found.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            // ---------------------------------------------------------
            // GET THIS TRAINER'S CLIENTS ONLY
            // ---------------------------------------------------------

            var appointmentClientIds = await _context.Appointments
                .Where(a =>
                    a.TrainerId == trainer.UserId &&
                    a.ClientId > 0)
                .Select(a => a.ClientId)
                .Distinct()
                .ToListAsync();

            var workoutClientIds = await _context.WorkoutPlans
                .Where(w =>
                    w.TrainerId == trainer.UserId &&
                    w.ClientId > 0)
                .Select(w => w.ClientId)
                .Distinct()
                .ToListAsync();

            var dietClientIds = await _context.DietPlans
                .Where(d =>
                    d.TrainerId == trainer.UserId &&
                    d.ClientId > 0)
                .Select(d => d.ClientId)
                .Distinct()
                .ToListAsync();

            var allowedClientIds = appointmentClientIds
                .Union(workoutClientIds)
                .Union(dietClientIds)
                .Distinct()
                .ToList();

            if (!allowedClientIds.Contains(clientId))
            {
                TempData["Error"] =
                    "This client is not assigned to you.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == clientId &&
                    u.Role == "CLIENT");

            if (client == null)
            {
                TempData["Error"] =
                    "Selected client was not found.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            // The plan is now assigned to this trainer and client.
            plan.TrainerId = trainer.UserId;
            plan.ClientId = client.UserId;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Diet plan assigned to " +
                client.FullName +
                " successfully.";

            return RedirectToAction(
                nameof(TrainerDietPlans));
        }


        // ============================================================
        // TRAINER - CREATE DIET PLAN
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTrainerDietPlan(
            string planName,
            string? description,
            int clientId,
            DateOnly startDate,
            DateOnly? endDate,
            string? status)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(planName))
            {
                TempData["Error"] =
                    "Diet plan name is required.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            if (planName.Trim().Length > 150)
            {
                TempData["Error"] =
                    "Diet plan name cannot exceed 150 characters.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            if (!string.IsNullOrWhiteSpace(description) &&
                description.Trim().Length > 500)
            {
                TempData["Error"] =
                    "Description cannot exceed 500 characters.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            if (endDate.HasValue &&
                endDate.Value < startDate)
            {
                TempData["Error"] =
                    "End date cannot be earlier than start date.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == clientId &&
                    u.Role == "CLIENT");

            if (client == null)
            {
                TempData["Error"] =
                    "Selected client was not found.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            // Verify that the selected client belongs to this trainer.
            var appointmentClientIds = await _context.Appointments
                .Where(a =>
                    a.TrainerId == trainer.UserId &&
                    a.ClientId > 0)
                .Select(a => a.ClientId)
                .Distinct()
                .ToListAsync();

            var workoutClientIds = await _context.WorkoutPlans
                .Where(w =>
                    w.TrainerId == trainer.UserId &&
                    w.ClientId > 0)
                .Select(w => w.ClientId)
                .Distinct()
                .ToListAsync();

            var dietClientIds = await _context.DietPlans
                .Where(d =>
                    d.TrainerId == trainer.UserId &&
                    d.ClientId > 0)
                .Select(d => d.ClientId)
                .Distinct()
                .ToListAsync();

            var allowedClientIds = appointmentClientIds
                .Union(workoutClientIds)
                .Union(dietClientIds)
                .Distinct()
                .ToList();

            if (!allowedClientIds.Contains(clientId))
            {
                TempData["Error"] =
                    "This client is not assigned to you.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            var dietPlan = new DietPlan
            {
                TrainerId = trainer.UserId,
                ClientId = client.UserId,

                PlanName = planName.Trim(),

                Description =
                    string.IsNullOrWhiteSpace(description)
                        ? null
                        : description.Trim(),

                StartDate = startDate,
                EndDate = endDate,

                Status =
                    string.IsNullOrWhiteSpace(status)
                        ? "Active"
                        : status.Trim(),

                CreatedAt = DateTime.Now
            };

            _context.DietPlans.Add(dietPlan);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Diet plan created and assigned to " +
                client.FullName +
                " successfully.";

            return RedirectToAction(
                nameof(TrainerDietPlans));
        }


        // ============================================================
        // TRAINER - UPDATE DIET PLAN
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTrainerDietPlan(
            int dietPlanId,
            string planName,
            string? description,
            int clientId,
            DateOnly startDate,
            DateOnly? endDate,
            string? status)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");

            var plan = await _context.DietPlans
                .FirstOrDefaultAsync(d =>
                    d.DietPlanId == dietPlanId);

            if (plan == null)
            {
                TempData["Error"] =
                    "Diet plan was not found.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            if (string.IsNullOrWhiteSpace(planName))
            {
                TempData["Error"] =
                    "Diet plan name is required.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            if (planName.Trim().Length > 150)
            {
                TempData["Error"] =
                    "Diet plan name cannot exceed 150 characters.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            if (!string.IsNullOrWhiteSpace(description) &&
                description.Trim().Length > 500)
            {
                TempData["Error"] =
                    "Description cannot exceed 500 characters.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            if (endDate.HasValue &&
                endDate.Value < startDate)
            {
                TempData["Error"] =
                    "End date cannot be earlier than start date.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == clientId &&
                    u.Role == "CLIENT");

            if (client == null)
            {
                TempData["Error"] =
                    "Selected client was not found.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            // ---------------------------------------------------------
            // VERIFY CLIENT BELONGS TO THIS TRAINER
            // ---------------------------------------------------------

            var appointmentClientIds = await _context.Appointments
                .Where(a =>
                    a.TrainerId == trainer.UserId &&
                    a.ClientId > 0)
                .Select(a => a.ClientId)
                .Distinct()
                .ToListAsync();

            var workoutClientIds = await _context.WorkoutPlans
                .Where(w =>
                    w.TrainerId == trainer.UserId &&
                    w.ClientId > 0)
                .Select(w => w.ClientId)
                .Distinct()
                .ToListAsync();

            var dietClientIds = await _context.DietPlans
                .Where(d =>
                    d.TrainerId == trainer.UserId &&
                    d.ClientId > 0)
                .Select(d => d.ClientId)
                .Distinct()
                .ToListAsync();

            var allowedClientIds = appointmentClientIds
                .Union(workoutClientIds)
                .Union(dietClientIds)
                .Distinct()
                .ToList();

            if (!allowedClientIds.Contains(clientId))
            {
                TempData["Error"] =
                    "This client is not assigned to you.";

                return RedirectToAction(
                    nameof(TrainerDietPlans));
            }

            plan.PlanName = planName.Trim();

            plan.Description =
                string.IsNullOrWhiteSpace(description)
                    ? null
                    : description.Trim();

            plan.TrainerId = trainer.UserId;
            plan.ClientId = client.UserId;
            plan.StartDate = startDate;
            plan.EndDate = endDate;

            plan.Status =
                string.IsNullOrWhiteSpace(status)
                    ? "Active"
                    : status.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Diet plan updated successfully.";

            return RedirectToAction(
                nameof(TrainerDietPlans));
        }


        // ============================================================
        // TRAINER - MESSAGES
        // URL: /Home/TrainerMessages
        // VIEW: Views/Trainer/TrainerMessages.cshtml
        // ============================================================

        public async Task<IActionResult> TrainerMessages(int? clientId = null)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");


            // ------------------------------------------------------------
            // TRAINER INFORMATION
            // ------------------------------------------------------------

            ViewBag.UserName = RemoveDrPrefix(trainer.FullName);
            ViewBag.UserEmail = trainer.Email;
            ViewBag.UserRole = trainer.Role;
            ViewBag.ProfileImage = GetProfileImage(trainer.ProfileImage);


            // ------------------------------------------------------------
            // GET TRAINER'S CLIENT IDS
            // ------------------------------------------------------------

            var appointmentClientIds = await _context.Appointments
                .Where(a =>
                    a.TrainerId == trainer.UserId &&
                    a.ClientId > 0)
                .Select(a => a.ClientId)
                .Distinct()
                .ToListAsync();


            var workoutClientIds = await _context.WorkoutPlans
                .Where(w =>
                    w.TrainerId == trainer.UserId &&
                    w.ClientId > 0)
                .Select(w => w.ClientId)
                .Distinct()
                .ToListAsync();


            var dietClientIds = await _context.DietPlans
                .Where(d =>
                    d.TrainerId == trainer.UserId &&
                    d.ClientId > 0)
                .Select(d => d.ClientId)
                .Distinct()
                .ToListAsync();


            var trainerClientIds = appointmentClientIds
                .Concat(workoutClientIds)
                .Concat(dietClientIds)
                .Distinct()
                .ToList();


            // ------------------------------------------------------------
            // GET CLIENTS
            // ------------------------------------------------------------

            var clients = await _context.Users
                .Where(u =>
                    u.Role == "CLIENT" &&
                    trainerClientIds.Contains(u.UserId))
                .OrderBy(u => u.FullName)
                .ToListAsync();


            ViewBag.Clients = clients;
            ViewBag.TotalClients = clients.Count;


            // ------------------------------------------------------------
            // SELECTED CLIENT
            // ------------------------------------------------------------

            int? selectedClientId = null;

            if (clientId.HasValue &&
                trainerClientIds.Contains(clientId.Value))
            {
                selectedClientId = clientId.Value;
            }
            else
            {
                selectedClientId = clients
                    .Select(c => (int?)c.UserId)
                    .FirstOrDefault();
            }


            ViewBag.SelectedClientId = selectedClientId;


            // ------------------------------------------------------------
            // LOAD SELECTED CLIENT
            // ------------------------------------------------------------

            User? selectedClient = null;

            if (selectedClientId.HasValue)
            {
                selectedClient = clients
                    .FirstOrDefault(c =>
                        c.UserId == selectedClientId.Value);
            }


            ViewBag.SelectedClient = selectedClient;


            // ------------------------------------------------------------
            // LOAD CHAT HISTORY
            // ------------------------------------------------------------

            var chatMessages = new List<ChatMessage>();

            if (selectedClientId.HasValue)
            {
                chatMessages = await _context.ChatMessages
                    .Include(m => m.Sender)
                    .Include(m => m.Receiver)
                    .Where(m =>
                        (m.SenderId == trainer.UserId &&
                         m.ReceiverId == selectedClientId.Value)
                        ||
                        (m.SenderId == selectedClientId.Value &&
                         m.ReceiverId == trainer.UserId))
                    .OrderBy(m => m.SentAt)
                    .ToListAsync();


                // --------------------------------------------------------
                // MARK CLIENT -> TRAINER MESSAGES AS READ
                // --------------------------------------------------------

                var unreadMessages = chatMessages
                    .Where(m =>
                        m.SenderId == selectedClientId.Value &&
                        m.ReceiverId == trainer.UserId &&
                        !m.IsRead)
                    .ToList();


                if (unreadMessages.Count > 0)
                {
                    foreach (var message in unreadMessages)
                    {
                        message.IsRead = true;
                    }

                    await _context.SaveChangesAsync();
                }
            }


            ViewBag.ChatMessages = chatMessages;


            return View("~/Views/Trainer/TrainerMessages.cshtml");
        }


        // ============================================================
        // TRAINER - SEND MESSAGE
        // POST: /Home/SendTrainerMessage
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTrainerMessage(
            int receiverId,
            string message)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");


            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");


            // ------------------------------------------------------------
            // VALIDATE MESSAGE
            // ------------------------------------------------------------

            if (string.IsNullOrWhiteSpace(message))
            {
                return RedirectToAction(
                    nameof(TrainerMessages),
                    new { clientId = receiverId });
            }


            // ------------------------------------------------------------
            // VERIFY RECEIVER IS A CLIENT
            // ------------------------------------------------------------

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == receiverId &&
                    u.Role == "CLIENT");

            if (client == null)
            {
                TempData["Error"] = "Invalid client.";

                return RedirectToAction(
                    nameof(TrainerMessages));
            }


            // ------------------------------------------------------------
            // VERIFY CLIENT BELONGS TO TRAINER
            // ------------------------------------------------------------

            var hasAppointment = await _context.Appointments
                .AnyAsync(a =>
                    a.TrainerId == trainer.UserId &&
                    a.ClientId == client.UserId);


            var hasWorkoutPlan = await _context.WorkoutPlans
                .AnyAsync(w =>
                    w.TrainerId == trainer.UserId &&
                    w.ClientId == client.UserId);


            var hasDietPlan = await _context.DietPlans
                .AnyAsync(d =>
                    d.TrainerId == trainer.UserId &&
                    d.ClientId == client.UserId);


            if (!hasAppointment &&
                !hasWorkoutPlan &&
                !hasDietPlan)
            {
                TempData["Error"] =
                    "This client is not assigned to you.";

                return RedirectToAction(
                    nameof(TrainerMessages),
                    new { clientId = receiverId });
            }


            // ------------------------------------------------------------
            // CREATE CHAT MESSAGE
            // ------------------------------------------------------------

            var chatMessage = new ChatMessage
            {
                SenderId = trainer.UserId,
                ReceiverId = client.UserId,
                MessageText = message.Trim(),
                SentAt = DateTime.Now,
                IsRead = false
            };


            _context.ChatMessages.Add(chatMessage);

            await _context.SaveChangesAsync();


            // ------------------------------------------------------------
            // RETURN TO SAME CHAT
            // ------------------------------------------------------------

            return RedirectToAction(
    nameof(TrainerMessages),
    new { clientId = receiverId });
        }


        // ============================================================
        // CHAT - ATTACHMENT UPLOAD
        // POST: /Home/UploadChatAttachment
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(25 * 1024 * 1024)]
        public async Task<IActionResult> UploadChatAttachment(
            int receiverId,
            IFormFile file)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return Unauthorized();

            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please select a file."
                });
            }

            if (file.Length > 25 * 1024 * 1024)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "File size cannot exceed 25 MB."
                });
            }

            var sender = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId.Value);

            var receiver = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == receiverId);

            if (sender == null || receiver == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid chat participant."
                });
            }

            int trainerId;
            int clientId;

            if (sender.Role == "TRAINER" && receiver.Role == "CLIENT")
            {
                trainerId = sender.UserId;
                clientId = receiver.UserId;
            }
            else if (sender.Role == "CLIENT" && receiver.Role == "TRAINER")
            {
                trainerId = receiver.UserId;
                clientId = sender.UserId;
            }
            else
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Only trainer and client chats are supported."
                });
            }

            if (!await AreChatParticipantsConnectedAsync(
                    trainerId,
                    clientId))
            {
                return StatusCode(403, new
                {
                    success = false,
                    message = "This user is not assigned to this chat."
                });
            }

            var allowedExtensions = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".gif", ".webp",
                ".pdf",
                ".doc", ".docx",
                ".xls", ".xlsx",
                ".txt",
                ".mp3", ".wav", ".m4a", ".aac", ".ogg",
                ".mp4", ".webm", ".mov", ".avi", ".mkv"
            };

            var extension =
                Path.GetExtension(file.FileName);

            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "This file type is not supported."
                });
            }

            var originalName =
                Path.GetFileName(file.FileName)
                    .Replace("|", "_")
                    .Trim();

            if (string.IsNullOrWhiteSpace(originalName))
                originalName = "Attachment";

            var storedName =
                $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

            var uploadFolder =
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "uploads",
                    "chat");

            Directory.CreateDirectory(uploadFolder);

            var physicalPath =
                Path.Combine(uploadFolder, storedName);

            try
            {
                await using (var stream =
                    new FileStream(
                        physicalPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None))
                {
                    await file.CopyToAsync(stream);
                }

                var fileUrl =
                    "/uploads/chat/" + storedName;

                var messageText =
                    "__HT_ATTACHMENT__|" +
                    originalName + "|" +
                    fileUrl + "|" +
                    (file.ContentType ?? "application/octet-stream")
                        .Replace("|", "_");

                var chatMessage = new ChatMessage
                {
                    SenderId = sender.UserId,
                    ReceiverId = receiver.UserId,
                    MessageText = messageText,
                    SentAt = DateTime.Now,
                    IsRead = false
                };

                _context.ChatMessages.Add(chatMessage);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Attachment sent successfully.",
                    fileName = originalName,
                    fileUrl = fileUrl
                });
            }
            catch
            {
                if (System.IO.File.Exists(physicalPath))
                {
                    try
                    {
                        System.IO.File.Delete(physicalPath);
                    }
                    catch
                    {
                    }
                }

                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to upload the attachment."
                });
            }
        }


        // ============================================================
        // CHAT - WEBRTC SIGNAL
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendCallSignal(
            int receiverId,
            string roomId,
            string callType,
            string signalType,
            string payload)
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(roomId) ||
                string.IsNullOrWhiteSpace(callType) ||
                string.IsNullOrWhiteSpace(signalType))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid call signal."
                });
            }

            if (string.IsNullOrWhiteSpace(payload))
                payload = "{}";

            if (payload.Length > 2_000_000)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Call signal is too large."
                });
            }

            var sender =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.UserId == userId.Value);

            var receiver =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.UserId == receiverId);

            if (sender == null || receiver == null)
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid call participant."
                });

            if (!(
                (sender.Role == "TRAINER" &&
                 receiver.Role == "CLIENT")
                ||
                (sender.Role == "CLIENT" &&
                 receiver.Role == "TRAINER")
            ))
            {
                return StatusCode(403, new
                {
                    success = false,
                    message = "Calls are only available between trainer and client."
                });
            }

            var trainerId =
                sender.Role == "TRAINER"
                    ? sender.UserId
                    : receiver.UserId;

            var clientId =
                sender.Role == "CLIENT"
                    ? sender.UserId
                    : receiver.UserId;

            if (!await AreChatParticipantsConnectedAsync(
                    trainerId,
                    clientId))
            {
                return StatusCode(403, new
                {
                    success = false,
                    message = "These users are not assigned to each other."
                });
            }

            var packet = new CallSignalPacket
            {
                SenderId = sender.UserId,
                ReceiverId = receiver.UserId,
                RoomId = roomId.Trim(),
                CallType = callType.Trim().ToLowerInvariant(),
                SignalType = signalType.Trim().ToLowerInvariant(),
                Payload = payload,
                CreatedAt = DateTime.Now
            };

            var list =
                _callSignals.GetOrAdd(
                    packet.RoomId,
                    _ => new List<CallSignalPacket>());

            lock (list)
            {
                list.RemoveAll(x =>
                    x.CreatedAt < DateTime.Now.AddMinutes(-10));

                list.Add(packet);
            }

            return Json(new
            {
                success = true
            });
        }


        // ============================================================
        // CHAT - WEBRTC SIGNAL POLLING
        // ============================================================

        [HttpGet]
        public IActionResult GetCallSignals()
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return Unauthorized();

            var result =
                new List<CallSignalPacket>();

            foreach (var pair in _callSignals)
            {
                var list = pair.Value;

                lock (list)
                {
                    list.RemoveAll(x =>
                        x.CreatedAt <
                        DateTime.Now.AddMinutes(-10));

                    var mine =
                        list.Where(x =>
                            x.ReceiverId == userId.Value)
                        .ToList();

                    if (mine.Count > 0)
                    {
                        result.AddRange(mine);

                        list.RemoveAll(x =>
                            x.ReceiverId == userId.Value);
                    }
                }

                if (list.Count == 0)
                {
                    _callSignals.TryRemove(
                        pair.Key,
                        out _);
                }
            }

            return Json(new
            {
                success = true,
                signals = result.Select(x => new
                {
                    senderId = x.SenderId,
                    receiverId = x.ReceiverId,
                    roomId = x.RoomId,
                    callType = x.CallType,
                    signalType = x.SignalType,
                    payload = x.Payload
                })
            });
        }


        // ============================================================
        // CHAT - WEBRTC RELATIONSHIP CHECK
        // ============================================================

        private async Task<bool>
            AreChatParticipantsConnectedAsync(
                int trainerId,
                int clientId)
        {
            if (trainerId <= 0 || clientId <= 0)
                return false;

            var hasAppointment =
                await _context.Appointments.AnyAsync(a =>
                    a.TrainerId == trainerId &&
                    a.ClientId == clientId);

            if (hasAppointment)
                return true;

            var hasWorkoutPlan =
                await _context.WorkoutPlans.AnyAsync(w =>
                    w.TrainerId == trainerId &&
                    w.ClientId == clientId);

            if (hasWorkoutPlan)
                return true;

            return await _context.DietPlans.AnyAsync(d =>
                d.TrainerId == trainerId &&
                d.ClientId == clientId);
        }


        // ============================================================
        // CLIENT - MESSAGES PAGE
        // URL: /Home/Messages
        // VIEW: Views/Client/Messages.cshtml
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Messages()
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
                    .FirstOrDefaultAsync(u =>
                        u.UserId == userId.Value &&
                        u.Role == "CLIENT");

            if (client == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            ViewBag.ClientName = RemoveDrPrefix(client.FullName);
            ViewBag.ProfileImage = GetProfileImage(client.ProfileImage);

            ViewBag.UnreadNotifications =
                await _context.Notifications
                    .CountAsync(n =>
                        n.UserId == client.UserId &&
                        n.IsRead != true);

            return View(
                "~/Views/Client/Messages.cshtml"
            );
        }


        // ============================================================
        // CLIENT - CHAT HISTORY
        // GET: /Home/ClientChatHistory
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> ClientChatHistory()
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return Unauthorized();
            }

            var client =
                await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.UserId == userId.Value &&
                        u.Role == "CLIENT");

            if (client == null)
            {
                return Unauthorized();
            }


            // --------------------------------------------------------
            // FIND CLIENT'S TRAINER
            // Priority:
            // 1. Latest Appointment
            // 2. Latest Workout Plan
            // 3. Latest Diet Plan
            // --------------------------------------------------------

            int? trainerId = null;


            // 1. APPOINTMENT
            trainerId =
                await _context.Appointments
                    .Where(a =>
                        a.ClientId == client.UserId &&
                        a.TrainerId > 0)
                    .OrderByDescending(a =>
                        a.AppointmentDate)
                    .ThenByDescending(a =>
                        a.AppointmentTime)
                    .Select(a =>
                        (int?)a.TrainerId)
                    .FirstOrDefaultAsync();


            // 2. WORKOUT PLAN
            if (!trainerId.HasValue)
            {
                trainerId =
                    await _context.WorkoutPlans
                        .Where(w =>
                            w.ClientId == client.UserId &&
                            w.TrainerId > 0)
                        .OrderByDescending(w =>
                            w.CreatedAt)
                        .Select(w =>
                            (int?)w.TrainerId)
                        .FirstOrDefaultAsync();
            }


            // 3. DIET PLAN
            if (!trainerId.HasValue)
            {
                trainerId =
                    await _context.DietPlans
                        .Where(d =>
                            d.ClientId == client.UserId &&
                            d.TrainerId > 0)
                        .OrderByDescending(d =>
                            d.CreatedAt)
                        .Select(d =>
                            (int?)d.TrainerId)
                        .FirstOrDefaultAsync();
            }


            // --------------------------------------------------------
            // NO TRAINER
            // --------------------------------------------------
            // ------

            if (!trainerId.HasValue)
            {
                return Json(new
                {
                    hasTrainer = false,
                    trainerId = 0,
                    trainerName = "",
                    trainerImage = "",
                    messages = Array.Empty<object>()
                });
            }


            // --------------------------------------------------------
            // GET TRAINER
            // --------------------------------------------------------

            var trainer =
                await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.UserId == trainerId.Value &&
                        u.Role == "TRAINER");

            if (trainer == null)
            {
                return Json(new
                {
                    hasTrainer = false,
                    trainerId = 0,
                    trainerName = "",
                    trainerImage = "",
                    messages = Array.Empty<object>()
                });
            }


            // --------------------------------------------------------
            // LOAD CHAT HISTORY
            // --------------------------------------------------------

            var chatRows =
                await _context.ChatMessages
                    .Where(m =>
                        (
                            m.SenderId == client.UserId &&
                            m.ReceiverId == trainer.UserId
                        )
                        ||
                        (
                            m.SenderId == trainer.UserId &&
                            m.ReceiverId == client.UserId
                        )
                    )
                    .OrderBy(m => m.SentAt)
                    .Select(m => new
                    {
                        messageId = m.MessageId,
                        messageText = m.MessageText,
                        sentAt = m.SentAt,
                        isClient =
                            m.SenderId == client.UserId
                    })
                    .ToListAsync();


            // --------------------------------------------------------
            // FORMAT TIME
            // --------------------------------------------------------

            var messages =
                chatRows
                    .Select(m => new
                    {
                        messageId = m.messageId,

                        messageText =
                            m.messageText,

                        sentAt =
                            m.sentAt.ToString(
                                "hh:mm tt"
                            ),

                        isClient =
                            m.isClient
                    })
                    .ToList();


            // --------------------------------------------------------
            // MARK TRAINER MESSAGES AS READ
            // --------------------------------------------------------

            var unreadMessages =
                await _context.ChatMessages
                    .Where(m =>
                        m.SenderId == trainer.UserId &&
                        m.ReceiverId == client.UserId &&
                        !m.IsRead
                    )
                    .ToListAsync();


            if (unreadMessages.Count > 0)
            {
                foreach (var item in unreadMessages)
                {
                    item.IsRead = true;
                }

                await _context.SaveChangesAsync();
            }


            // --------------------------------------------------------
            // RETURN JSON
            // --------------------------------------------------------

            return Json(new
            {
                success = true,
                hasTrainer = true,
                trainerId = trainer.UserId,
                trainerName = RemoveDrPrefix(trainer.FullName),
                trainerImage = trainer.ProfileImage,
                messages = messages
            });
        }


        // ============================================================
        // CLIENT - SEND MESSAGE TO TRAINER
        // POST: /Home/SendClientMessage
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendClientMessage(
            int receiverId,
            string message)
        {
            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return Unauthorized();
            }


            // --------------------------------------------------------
            // GET CLIENT
            // --------------------------------------------------------

            var client =
                await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.UserId == userId.Value &&
                        u.Role == "CLIENT");

            if (client == null)
            {
                return Unauthorized();
            }


            // --------------------------------------------------------
            // VALIDATE MESSAGE
            // --------------------------------------------------------

            if (string.IsNullOrWhiteSpace(message))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Message is required."
                });
            }


            // --------------------------------------------------------
            // GET TRAINER
            // --------------------------------------------------------

            var trainer =
                await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.UserId == receiverId &&
                        u.Role == "TRAINER");

            if (trainer == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid trainer."
                });
            }


            // --------------------------------------------------------
            // VERIFY CLIENT <-> TRAINER RELATIONSHIP
            // --------------------------------------------------------

            var hasAppointment =
                await _context.Appointments
                    .AnyAsync(a =>
                        a.ClientId == client.UserId &&
                        a.TrainerId == trainer.UserId);


            var hasWorkoutPlan =
                await _context.WorkoutPlans
                    .AnyAsync(w =>
                        w.ClientId == client.UserId &&
                        w.TrainerId == trainer.UserId);


            var hasDietPlan =
                await _context.DietPlans
                    .AnyAsync(d =>
                        d.ClientId == client.UserId &&
                        d.TrainerId == trainer.UserId);


            if (!hasAppointment &&
                !hasWorkoutPlan &&
                !hasDietPlan)
            {
                return Json(new
                {
                    success = false,
                    message = "This trainer is not assigned to you."
                });
            }


            // --------------------------------------------------------
            // CREATE MESSAGE
            // --------------------------------------------------------

            var chatMessage =
                new ChatMessage
                {
                    SenderId =
                        client.UserId,

                    ReceiverId =
                        trainer.UserId,

                    MessageText =
                        message.Trim(),

                    SentAt =
                        DateTime.Now,

                    IsRead =
                        false
                };


            _context.ChatMessages.Add(
                chatMessage
            );


            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Unable to save message: " + ex.Message
                });
            }


            // --------------------------------------------------------
            // SUCCESS
            // --------------------------------------------------------

            return Json(new
            {
                success = true,

                messageId =
                    chatMessage.MessageId,

                messageText =
                    chatMessage.MessageText,

                sentAt =
                    chatMessage.SentAt.ToString("hh:mm tt")
            });
        }
        // ============================================================
        // TRAINER - NOTIFICATIONS
        // URL: /Home/TrainerNotifications
        // VIEW: Views/Trainer/TrainerNotifications.cshtml
        // ============================================================
        public async Task<IActionResult> TrainerNotifications()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");

            ViewBag.UserName = RemoveDrPrefix(trainer.FullName);
            ViewBag.UserEmail = trainer.Email;
            ViewBag.UserRole = trainer.Role;
            ViewBag.ProfileImage = GetProfileImage(trainer.ProfileImage);

            // ------------------------------------------------------------
            // GET ONLY THE LOGGED-IN TRAINER'S NOTIFICATIONS
            // ------------------------------------------------------------
            var notifications = await _context.Notifications
                .Where(n => n.UserId == trainer.UserId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            ViewBag.Notifications = notifications;
            ViewBag.TotalNotifications = notifications.Count;
            ViewBag.UnreadNotifications =
                notifications.Count(n => n.IsRead != true);
            ViewBag.ReadNotifications =
                notifications.Count(n => n.IsRead == true);

            return View("~/Views/Trainer/TrainerNotifications.cshtml");
        }


        // ============================================================
        // TRAINER - MARK NOTIFICATION AS READ
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkTrainerNotificationAsRead(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");

            // IMPORTANT:
            // The notification must belong to the logged-in trainer.
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.NotificationId == id &&
                    n.UserId == trainer.UserId);

            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(TrainerNotifications));
        }


        // ============================================================
        // TRAINER - MARK ALL NOTIFICATIONS AS READ
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllTrainerNotificationsAsRead()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");

            var notifications = await _context.Notifications
                .Where(n =>
                    n.UserId == trainer.UserId &&
                    n.IsRead != true)
                .ToListAsync();

            foreach (var notification in notifications)
                notification.IsRead = true;

            if (notifications.Count > 0)
                await _context.SaveChangesAsync();

            return RedirectToAction(nameof(TrainerNotifications));
        }


        // ============================================================
        // TRAINER - PROGRESS REPORTS / HEALTH RECORDS
        // URL:
        // /Home/TrainerHealthRecords
        //
        // VIEW:
        // Views/Trainer/HealthRecords.cshtml
        // ============================================================
        public async Task<IActionResult> TrainerHealthRecords(
            string? search,
            int? clientId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");

            // ------------------------------------------------------------
            // TRAINER INFORMATION
            // ------------------------------------------------------------

            ViewBag.UserName = RemoveDrPrefix(trainer.FullName);
            ViewBag.UserEmail = trainer.Email;
            ViewBag.UserRole = trainer.Role;
            ViewBag.ProfileImage = GetProfileImage(trainer.ProfileImage);

            // ------------------------------------------------------------
            // CURRENT TRAINER CLIENTS
            // A client is considered the trainer's client when the trainer
            // has an appointment, workout plan, or diet plan for that client.
            // ------------------------------------------------------------

            var appointmentClientIds = await _context.Appointments
                .Where(a => a.TrainerId == trainer.UserId)
                .Select(a => a.ClientId)
                .Distinct()
                .ToListAsync();

            var workoutClientIds = await _context.WorkoutPlans
                .Where(w => w.TrainerId == trainer.UserId)
                .Select(w => w.ClientId)
                .Distinct()
                .ToListAsync();

            var dietClientIds = await _context.DietPlans
                .Where(d => d.TrainerId == trainer.UserId)
                .Select(d => d.ClientId)
                .Distinct()
                .ToListAsync();

            var trainerClientIds = appointmentClientIds
                .Concat(workoutClientIds)
                .Concat(dietClientIds)
                .Distinct()
                .ToList();

            var clients = await _context.Users
                .Where(u =>
                    u.Role == "CLIENT" &&
                    trainerClientIds.Contains(u.UserId))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            // ------------------------------------------------------------
            // SEARCH CLIENTS
            // ------------------------------------------------------------

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                clients = clients
                    .Where(c =>
                        c.FullName.Contains(search) ||
                        c.Email.Contains(search) ||
                        (c.Phone != null && c.Phone.Contains(search)))
                    .ToList();
            }

            ViewBag.Clients = clients;
            ViewBag.Search = search ?? "";
            ViewBag.TotalClients = clients.Count;

            // ------------------------------------------------------------
            // SELECTED CLIENT
            // ------------------------------------------------------------

            int selectedClientId = 0;

            if (clientId.HasValue &&
                trainerClientIds.Contains(clientId.Value) &&
                clients.Any(c => c.UserId == clientId.Value))
            {
                selectedClientId = clientId.Value;
            }

            ViewBag.SelectedClientId = selectedClientId;

            var selectedClient = selectedClientId > 0
                ? clients.FirstOrDefault(c => c.UserId == selectedClientId)
                : null;

            ViewBag.SelectedClientName =
                selectedClient != null
                    ? RemoveDrPrefix(selectedClient.FullName)
                    : "Select a client";

            // ------------------------------------------------------------
            // DEFAULT PROGRESS VALUES
            // ------------------------------------------------------------

            ViewBag.ProgressMetrics = new List<HealthMetric>();

            ViewBag.CurrentWeight = null;
            ViewBag.CurrentBmi = null;
            ViewBag.CurrentBodyFat = null;
            ViewBag.CurrentCalories = null;

            ViewBag.WeightChange = null;
            ViewBag.BmiChange = null;

            ViewBag.TotalWorkoutPlans = 0;
            ViewBag.CompletedWorkoutPlans = 0;
            ViewBag.WorkoutProgress = 0m;

            ViewBag.ProgressDates = new List<string>();
            ViewBag.WeightValues = new List<decimal>();
            ViewBag.BmiValues = new List<decimal>();
            ViewBag.BodyFatValues = new List<decimal>();

            // ------------------------------------------------------------
            // LOAD SELECTED CLIENT PROGRESS
            // ------------------------------------------------------------

            if (selectedClientId > 0)
            {
                var metrics = await _context.HealthMetrics
                    .Where(m => m.ClientId == selectedClientId)
                    .OrderBy(m => m.RecordDate)
                    .ToListAsync();

                ViewBag.ProgressMetrics = metrics;

                var latestMetric = metrics
                    .OrderByDescending(m => m.RecordDate)
                    .FirstOrDefault();

                var previousMetric = metrics
                    .OrderByDescending(m => m.RecordDate)
                    .Skip(1)
                    .FirstOrDefault();

                if (latestMetric != null)
                {
                    ViewBag.CurrentWeight = latestMetric.Weight;
                    ViewBag.CurrentBmi = latestMetric.Bmi;
                    ViewBag.CurrentBodyFat = latestMetric.BodyFat;
                    ViewBag.CurrentCalories = latestMetric.CaloriesBurned;
                }

                if (latestMetric?.Weight.HasValue == true &&
                    previousMetric?.Weight.HasValue == true)
                {
                    ViewBag.WeightChange =
                        latestMetric.Weight.Value -
                        previousMetric.Weight.Value;
                }

                if (latestMetric?.Bmi.HasValue == true &&
                    previousMetric?.Bmi.HasValue == true)
                {
                    ViewBag.BmiChange =
                        latestMetric.Bmi.Value -
                        previousMetric.Bmi.Value;
                }

                // --------------------------------------------------------
                // CHART DATA
                // --------------------------------------------------------

                ViewBag.ProgressDates = metrics
                    .Select(m => m.RecordDate.ToString("dd MMM"))
                    .ToList();

                ViewBag.WeightValues = metrics
                    .Where(m => m.Weight.HasValue)
                    .Select(m => m.Weight!.Value)
                    .ToList();

                ViewBag.BmiValues = metrics
                    .Where(m => m.Bmi.HasValue)
                    .Select(m => m.Bmi!.Value)
                    .ToList();

                ViewBag.BodyFatValues = metrics
                    .Where(m => m.BodyFat.HasValue)
                    .Select(m => m.BodyFat!.Value)
                    .ToList();

                // --------------------------------------------------------
                // WORKOUT PLAN PROGRESS
                // Only plans belonging to the logged-in trainer and the
                // selected client are included.
                // --------------------------------------------------------

                var workoutPlans = await _context.WorkoutPlans
                    .Where(w =>
                        w.TrainerId == trainer.UserId &&
                        w.ClientId == selectedClientId)
                    .ToListAsync();

                ViewBag.TotalWorkoutPlans = workoutPlans.Count;

                int completedWorkoutPlans = workoutPlans.Count(w =>
                    string.Equals(
                        w.Status,
                        "Completed",
                        StringComparison.OrdinalIgnoreCase));

                ViewBag.CompletedWorkoutPlans =
                    completedWorkoutPlans;

                ViewBag.WorkoutProgress =
                    workoutPlans.Count == 0
                        ? 0m
                        : Math.Round(
                            completedWorkoutPlans * 100m /
                            workoutPlans.Count,
                            0);
            }

            return View("~/Views/Trainer/ProgressReports.cshtml");
        }


        // ============================================================
        // TRAINER - PROFILE
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> TrainerProfile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var trainer = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "TRAINER");

            if (trainer == null)
                return RedirectToAction("Login", "Account");

            ViewBag.UserName = RemoveDrPrefix(trainer.FullName);
            ViewBag.UserEmail = trainer.Email;
            ViewBag.UserRole = trainer.Role;
            ViewBag.ProfileImage = GetProfileImage(trainer.ProfileImage);

            return View("~/Views/Trainer/Profile.cshtml", trainer);
        }


        // =========================================================
        // CLIENT DASHBOARD
        // URL:
        // /Home/ClientDashboard
        // =========================================================

        public async Task<IActionResult> ClientDashboard()
        {
            // =========================================================
            // 1. GET LOGGED-IN CLIENT
            // =========================================================

            var userId =
                HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            // =========================================================
            // 2. GET CLIENT
            // =========================================================

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


            // =========================================================
            // 3. CLIENT ROLE CHECK
            // =========================================================

            if (!string.Equals(
                    client.Role,
                    "CLIENT",
                    StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            // =========================================================
            // 4. CLIENT INFORMATION
            // =========================================================

            ViewBag.ClientName =
                RemoveDrPrefix(
                    client.FullName
                );

            ViewBag.ClientEmail =
                client.Email;

            ViewBag.ClientRole =
                client.Role;

            ViewBag.UserName =
                RemoveDrPrefix(
                    client.FullName
                );

            ViewBag.UserEmail =
                client.Email;

            ViewBag.UserRole =
                client.Role;

            ViewBag.ProfileImage =
                GetProfileImage(
                    client.ProfileImage
                );

            ViewBag.ClientProfileImage =
                GetProfileImage(
                    client.ProfileImage
                );


            // =========================================================
            // 5. CLIENT WORKOUT PLANS
            // =========================================================

            var workoutPlans =
                await _context.WorkoutPlans
                    .Include(w => w.Trainer)
                    .Include(w => w.WorkoutPlanDays)
                        .ThenInclude(d => d.WorkoutExercises)
                    .Where(w =>
                        w.ClientId ==
                        client.UserId
                    )
                    .OrderByDescending(
                        w => w.WorkoutPlanId
                    )
                    .ToListAsync();


            var activeWorkoutPlans =
                workoutPlans.Count(
                    w =>
                        string.Equals(
                            w.Status,
                            "Active",
                            StringComparison.OrdinalIgnoreCase
                        )
                );


            ViewBag.WorkoutPlans =
                workoutPlans;

            ViewBag.ActiveWorkoutPlans =
                activeWorkoutPlans;

            ViewBag.TotalWorkoutPlans =
                workoutPlans.Count;


            // =========================================================
            // 6. CLIENT DIET PLANS
            // =========================================================

            var dietPlans =
                await _context.DietPlans
                    .Include(d => d.Trainer)
                    .Include(d => d.DietPlanDays)
                        .ThenInclude(d => d.DietMeals)
                    .Where(d =>
                        d.ClientId ==
                        client.UserId
                    )
                    .OrderByDescending(
                        d => d.DietPlanId
                    )
                    .ToListAsync();


            var activeDietPlans =
                dietPlans.Count(
                    d =>
                        string.Equals(
                            d.Status,
                            "Active",
                            StringComparison.OrdinalIgnoreCase
                        )
                );


            ViewBag.DietPlans =
                dietPlans;

            ViewBag.ActiveDietPlans =
                activeDietPlans;

            ViewBag.TotalDietPlans =
                dietPlans.Count;


            // =========================================================
            // 7. UPCOMING APPOINTMENTS
            // =========================================================

            var today =
                DateOnly.FromDateTime(
                    DateTime.Today
                );

            var currentTime =
                TimeOnly.FromDateTime(
                    DateTime.Now
                );


            var upcomingAppointments =
                await _context.Appointments
                    .Include(a => a.Trainer)
                    .Where(a =>
                        a.ClientId ==
                        client.UserId

                        &&

                        (
                            a.Status == null ||
                            a.Status != "Cancelled"
                        )

                        &&

                        (
                            a.AppointmentDate >
                            today

                            ||

                            (
                                a.AppointmentDate ==
                                today

                                &&

                                a.AppointmentTime >=
                                currentTime
                            )
                        )
                    )
                    .OrderBy(
                        a => a.AppointmentDate
                    )
                    .ThenBy(
                        a => a.AppointmentTime
                    )
                    .Take(10)
                    .ToListAsync();


            var upcomingAppointment =
                upcomingAppointments
                    .FirstOrDefault();


            ViewBag.UpcomingAppointments =
                upcomingAppointments.Count;

            ViewBag.UpcomingAppointment =
                upcomingAppointment;


            // =========================================================
            // 8. FIND CLIENT TRAINER
            //
            // Priority:
            //
            // 1. Upcoming Appointment
            // 2. Active Workout Plan
            // 3. Active Diet Plan
            // 4. Any Workout Plan
            // 5. Any Diet Plan
            // =========================================================

            User? trainer =
                upcomingAppointment?.Trainer;


            if (trainer == null)
            {
                trainer =
                    workoutPlans
                        .Where(
                            w =>
                                w.Trainer != null
                                &&
                                string.Equals(
                                    w.Status,
                                    "Active",
                                    StringComparison.OrdinalIgnoreCase
                                )
                        )
                        .Select(
                            w => w.Trainer
                        )
                        .FirstOrDefault();
            }


            if (trainer == null)
            {
                trainer =
                    dietPlans
                        .Where(
                            d =>
                                d.Trainer != null
                                &&
                                string.Equals(
                                    d.Status,
                                    "Active",
                                    StringComparison.OrdinalIgnoreCase
                                )
                        )
                        .Select(
                            d => d.Trainer
                        )
                        .FirstOrDefault();
            }


            if (trainer == null)
            {
                trainer =
                    workoutPlans
                        .Where(
                            w =>
                                w.Trainer != null
                        )
                        .Select(
                            w => w.Trainer
                        )
                        .FirstOrDefault();
            }


            if (trainer == null)
            {
                trainer =
                    dietPlans
                        .Where(
                            d =>
                                d.Trainer != null
                        )
                        .Select(
                            d => d.Trainer
                        )
                        .FirstOrDefault();
            }


            if (trainer != null)
            {
                ViewBag.TrainerName =
                    RemoveDrPrefix(
                        trainer.FullName
                    );

                ViewBag.TrainerEmail =
                    trainer.Email;

                ViewBag.TrainerId =
                    trainer.UserId;

                ViewBag.TrainerImage =
                    GetProfileImage(
                        trainer.ProfileImage
                    );
            }
            else
            {
                ViewBag.TrainerName =
                    "No Trainer Assigned";

                ViewBag.TrainerEmail =
                    "";

                ViewBag.TrainerId =
                    0;

                ViewBag.TrainerImage =
                    "/images/default-trainer.jpg";
            }


            // =========================================================
            // 9. LATEST HEALTH METRIC
            // =========================================================

            var latestHealthMetric =
                await _context.HealthMetrics
                    .Where(
                        h =>
                            h.ClientId ==
                            client.UserId
                    )
                    .OrderByDescending(
                        h => h.RecordDate
                    )
                    .ThenByDescending(
                        h => h.MetricId
                    )
                    .FirstOrDefaultAsync();


            ViewBag.LatestHealthMetric =
                latestHealthMetric;


            // =========================================================
            // 10. CLIENT NOTIFICATIONS
            // =========================================================

            var unreadNotifications =
                await _context.Notifications
                    .CountAsync(
                        n =>
                            n.UserId ==
                            client.UserId

                            &&

                            n.IsRead != true
                    );


            var totalNotifications =
                await _context.Notifications
                    .CountAsync(
                        n =>
                            n.UserId ==
                            client.UserId
                    );


            ViewBag.TotalNotifications =
                totalNotifications;

            ViewBag.UnreadNotifications =
                unreadNotifications;


            // =========================================================
            // 11. CLIENT WORKOUT LOGS
            // =========================================================

            ViewBag.TotalWorkoutLogs =
                await _context.WorkoutLogs
                    .CountAsync(
                        w =>
                            w.ClientId ==
                            client.UserId
                    );


            // =========================================================
            // 12. CLIENT MEAL LOGS
            // =========================================================

            ViewBag.TotalMealLogs =
                await _context.MealLogs
                    .CountAsync(
                        m =>
                            m.ClientId ==
                            client.UserId
                    );


            // =========================================================
            // 13. CLIENT HEALTH PROFILES
            // =========================================================

            ViewBag.TotalHealthProfiles =
                await _context.HealthProfiles
                    .CountAsync(
                        h =>
                            h.UserId ==
                            client.UserId
                    );


            // =========================================================
            // 14. CLIENT HEALTH METRICS
            // =========================================================

            ViewBag.TotalHealthMetrics =
                await _context.HealthMetrics
                    .CountAsync(
                        h =>
                            h.ClientId ==
                            client.UserId
                    );


            // =========================================================
            // 15. CLIENT APPOINTMENTS
            // =========================================================

            ViewBag.TotalAppointments =
                await _context.Appointments
                    .CountAsync(
                        a =>
                            a.ClientId ==
                            client.UserId
                    );


            // =========================================================
            // 16. OVERALL PROGRESS
            //
            // Current database has no direct OverallProgress field.
            // Therefore calculate from active plans.
            // =========================================================

            int overallProgress = 0;


            if (
                workoutPlans.Count > 0
                ||
                dietPlans.Count > 0
            )
            {
                double workoutScore =
                    workoutPlans.Count == 0
                        ? 0
                        :
                        (
                            (double)
                            activeWorkoutPlans
                            /
                            workoutPlans.Count
                        )
                        *
                        50.0;


                double dietScore =
                    dietPlans.Count == 0
                        ? 0
                        :
                        (
                            (double)
                            activeDietPlans
                            /
                            dietPlans.Count
                        )
                        *
                        50.0;


                overallProgress =
                    (int)Math.Round(
                        Math.Clamp(
                            workoutScore +
                            dietScore,
                            0,
                            100
                        )
                    );
            }


            ViewBag.OverallProgress =
                overallProgress;


            // =========================================================
            // 17. TODAY DATE
            // =========================================================

            ViewBag.TodayDate =
                DateTime.Now;


            // =========================================================
            // 18. RETURN CLIENT DASHBOARD
            // =========================================================

            return View(
                "~/Views/Client/ClientDashboard.cshtml"
            );
        }



        // =========================================================
        // CLIENT - APPOINTMENTS
        // URL: /Home/ClientAppointments
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> ClientAppointments()
        {
            var client = await GetLoggedInClientForPages();

            if (client == null)
                return RedirectToAction("Login", "Account");

            var appointments = await _context.Appointments
                .Include(a => a.Trainer)
                .Where(a => a.ClientId == client.UserId)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.AppointmentTime)
                .ToListAsync();

            SetClientPageViewData(client);

            ViewBag.Appointments = appointments;
            ViewBag.TotalAppointments = appointments.Count;
            ViewBag.UnreadNotifications = await _context.Notifications.CountAsync(
                n => n.UserId == client.UserId && n.IsRead != true);

            return View("~/Views/Client/ClientAppointments.cshtml");
        }


        // =========================================================
        // CLIENT - WORKOUT PLANS
        // URL: /Home/ClientWorkoutPlans
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> ClientWorkoutPlans()
        {
            var client = await GetLoggedInClientForPages();

            if (client == null)
                return RedirectToAction("Login", "Account");

            var plans = await _context.WorkoutPlans
                .Include(w => w.Trainer)
                .Include(w => w.WorkoutPlanDays)
                    .ThenInclude(d => d.WorkoutExercises)
                .Where(w => w.ClientId == client.UserId)
                .OrderByDescending(w => w.WorkoutPlanId)
                .ToListAsync();

            SetClientPageViewData(client);

            ViewBag.IsReadOnly = true;

            ViewBag.WorkoutPlans = plans;
            ViewBag.TotalWorkoutPlans = plans.Count;
            ViewBag.ActiveWorkoutPlans = plans.Count(w =>
                string.Equals(w.Status, "Active", StringComparison.OrdinalIgnoreCase));
            ViewBag.UnreadNotifications = await _context.Notifications.CountAsync(
                n => n.UserId == client.UserId && n.IsRead != true);

            return View("~/Views/Client/ClientWorkoutPlans.cshtml");
        }


        // =========================================================
        // CLIENT - DIET PLANS
        // URL: /Home/ClientDietPlans
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> ClientDietPlans()
        {
            var client = await GetLoggedInClientForPages();

            if (client == null)
                return RedirectToAction("Login", "Account");

            var plans = await _context.DietPlans
                .Include(d => d.Trainer)
                .Include(d => d.DietPlanDays)
                    .ThenInclude(day => day.DietMeals)
                .Where(d => d.ClientId == client.UserId)
                .OrderByDescending(d => d.DietPlanId)
                .ToListAsync();

            SetClientPageViewData(client);

            ViewBag.IsReadOnly = true;

            ViewBag.DietPlans = plans;
            ViewBag.TotalDietPlans = plans.Count;
            ViewBag.ActiveDietPlans = plans.Count(d =>
                string.Equals(d.Status, "Active", StringComparison.OrdinalIgnoreCase));
            ViewBag.UnreadNotifications = await _context.Notifications.CountAsync(
                n => n.UserId == client.UserId && n.IsRead != true);

            return View("~/Views/Client/ClientDietPlans.cshtml");
        }


        // =========================================================
        // CLIENT - PROGRESS
        // URL: /Home/ClientProgress
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> ClientProgress()
        {
            var client = await GetLoggedInClientForPages();

            if (client == null)
                return RedirectToAction("Login", "Account");

            var metrics = await _context.HealthMetrics
                .Where(h => h.ClientId == client.UserId)
                .OrderByDescending(h => h.RecordDate)
                .ThenByDescending(h => h.MetricId)
                .ToListAsync();

            SetClientPageViewData(client);

            ViewBag.HealthMetrics = metrics;
            ViewBag.TotalHealthMetrics = metrics.Count;
            ViewBag.LatestHealthMetric = metrics.FirstOrDefault();
            ViewBag.UnreadNotifications = await _context.Notifications.CountAsync(
                n => n.UserId == client.UserId && n.IsRead != true);

            return View("~/Views/Client/ClientProgress.cshtml");
        }


        // =========================================================
        // CLIENT - NOTIFICATIONS
        // URL: /Home/ClientNotifications
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> ClientNotifications()
        {
            var client = await GetLoggedInClientForPages();

            if (client == null)
                return RedirectToAction("Login", "Account");

            var notifications = await _context.Notifications
                .Where(n => n.UserId == client.UserId)
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.NotificationId)
                .ToListAsync();

            SetClientPageViewData(client);

            ViewBag.Notifications = notifications;
            ViewBag.TotalNotifications = notifications.Count;
            ViewBag.UnreadNotifications = notifications.Count(n => n.IsRead != true);

            return View("~/Views/Client/ClientNotifications.cshtml");
        }


        // =========================================================
        // CLIENT - MARK ONE NOTIFICATION AS READ
        // POST: /Home/MarkClientNotificationRead
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkClientNotificationRead(int id)
        {
            var client = await GetLoggedInClientForPages();

            if (client == null)
                return RedirectToAction("Login", "Account");

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.NotificationId == id &&
                    n.UserId == client.UserId);

            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(ClientNotifications));
        }


        // =========================================================
        // CLIENT - MARK ALL NOTIFICATIONS AS READ
        // POST: /Home/MarkAllClientNotificationsRead
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllClientNotificationsRead()
        {
            var client = await GetLoggedInClientForPages();

            if (client == null)
                return RedirectToAction("Login", "Account");

            var notifications = await _context.Notifications
                .Where(n =>
                    n.UserId == client.UserId &&
                    n.IsRead != true)
                .ToListAsync();

            foreach (var notification in notifications)
                notification.IsRead = true;

            if (notifications.Count > 0)
                await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ClientNotifications));
        }


        // =========================================================
        // CLIENT PAGE HELPERS
        // =========================================================

        private async Task<User?> GetLoggedInClientForPages()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return null;

            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId.Value &&
                    u.Role == "CLIENT");
        }


        private void SetClientPageViewData(User client)
        {
            ViewBag.ClientName = RemoveDrPrefix(client.FullName);
            ViewBag.ClientEmail = client.Email;
            ViewBag.ClientRole = client.Role;
            ViewBag.UserName = RemoveDrPrefix(client.FullName);
            ViewBag.UserEmail = client.Email;
            ViewBag.UserRole = client.Role;
            ViewBag.ProfileImage = GetProfileImage(client.ProfileImage);
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
            // -----------------------------------------------------
            // ADMIN USER INFORMATION
            // -----------------------------------------------------

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


            // -----------------------------------------------------
            // ADMIN INFORMATION
            // -----------------------------------------------------

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


            // -----------------------------------------------------
            // UNREAD NOTIFICATION COUNT
            // -----------------------------------------------------
            // Notifications are system-wide for Admin.
            // Therefore we count all unread notifications.
            // -----------------------------------------------------

            ViewBag.UnreadNotifications =
                _context.Notifications
                    .Count(
                        n =>
                            n.IsRead != true
                    );


            // -----------------------------------------------------
            // TOTAL NOTIFICATION COUNT
            // -----------------------------------------------------

            ViewBag.TotalNotifications =
                _context.Notifications
                    .Count(
                        n =>
                            n.IsRead != true
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
