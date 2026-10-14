using HelpdeskSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskSystem.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly HelpDeskContext _db;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SignInManager<ApplicationUser> signInManager,
            HelpDeskContext db)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _db = db;
        }

        // ================= DASHBOARD =================
        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> Index()
        {
            var dashboard = new AdminDashboardViewModel
            {
                UserCount = _userManager.Users.Count(),
                TicketCount = _db.Tickets.Count(),
                OpenTickets = _db.Tickets.Count(t => t.Status.Name == "Open"),
                ClosedTickets = _db.Tickets.Count(t => t.Status.Name == "Closed"),
                Agents = await _userManager.GetUsersInRoleAsync("Agent")
            };
            return View(dashboard);
        }

        // ================= USERS =================
     
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Users()
        {
            // Get all users
            var users = await _userManager.Users.ToListAsync();

            // Map each ApplicationUser to UserWithRoleViewModel
            var list = new List<UserWithRoleViewModel>();

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u); // get roles for user

                list.Add(new UserWithRoleViewModel
                {
                    Id = u.Id,
                    FullName = string.IsNullOrWhiteSpace(u.FullName)
                        ? $"{u.FirstName} {u.LastName}"
                        : u.FullName,
                    Email = u.Email,
                    UserName = u.UserName,
                    Role = roles.FirstOrDefault() ?? "No Role"
                });
            }

            // Pass the list to the view — now the types match
            return View(list);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult CreateUser()
        {
            return View();
        }


        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(
            ApplicationUser model,
            string password,
            string? role)
        {
            if (!ModelState.IsValid)
                return View(model);


            // ======================================
            // DEFAULT ROLE
            // If no role selected => User
            // ======================================

            if (string.IsNullOrWhiteSpace(role))
            {
                role = "User";
            }


            // Check role exists
            if (!await _roleManager.RoleExistsAsync(role))
            {
                ModelState.AddModelError(
                    "",
                    "Selected role does not exist."
                );

                return View(model);
            }



            // ======================================
            // CREATE IDENTITY USER
            // ======================================

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,

                FirstName = model.FirstName,
                MiddleName = model.MiddleName,
                LastName = model.LastName,

                FullName =
                    string.IsNullOrWhiteSpace(model.FullName)
                    ?
                    $"{model.FirstName} {model.MiddleName} {model.LastName}"
                    :
                    model.FullName
            };



            var result =
                await _userManager.CreateAsync(
                    user,
                    password
                );



            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        "",
                        error.Description
                    );
                }

                return View(model);
            }



            // ======================================
            // ASSIGN ROLE
            // Default = User
            // ======================================

            await _userManager.AddToRoleAsync(
                user,
                role
            );



            // ======================================
            // CREATE EMPLOYEE PROFILE
            // ======================================

            var employee = new Employee
            {
                UserId = user.Id,

                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName,

                Email = user.Email,

                AvailabilityStatus =
                    AvailabilityStatus.Active
            };


            _db.Employees.Add(employee);

            await _db.SaveChangesAsync();



            TempData["SuccessMessage"] =
                $"Employee created successfully with role {role}.";


            return RedirectToAction(nameof(Users));
        }

        // ================= EDIT USER =================
        [Authorize(Roles = "Admin,Agent")]
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (User.IsInRole("Agent") && user.Id != _userManager.GetUserId(User))
                return Forbid();

            return View(user);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Agent")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(ApplicationUser model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            if (User.IsInRole("Agent") && user.Id != _userManager.GetUserId(User))
                return Forbid();

            user.FirstName = model.FirstName;
            user.MiddleName = model.MiddleName;
            user.LastName = model.LastName;
            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.Email;
            user.PhoneNumber = model.PhoneNumber;
            await _userManager.UpdateAsync(user);

            // 🔄 SYNC EMPLOYEE
            var emp = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == user.Id);
            if (emp != null)
            {
                emp.FirstName = user.FirstName;
                emp.MiddleName = user.MiddleName;
                emp.LastName = user.LastName;
                emp.Email = user.Email;
                user.PhoneNumber = model.PhoneNumber;
                _db.Update(emp);
                await _db.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "User updated successfully.";
            return RedirectToAction(nameof(Users));
        }

        // ================= DELETE USER =================
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound();

            return View(user); // Renders DeleteUser.cshtml with @model ApplicationUser
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUserConfirmed(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // 🔴 Step 1: Delete related Employee
                var employee = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == id);
                if (employee != null)
                {
                    _db.Employees.Remove(employee);
                    await _db.SaveChangesAsync();
                }

                // 🔴 Step 2: Delete Identity User
                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                        ModelState.AddModelError("", error.Description);

                    await transaction.RollbackAsync();
                    return View("DeleteUser", user);
                }

                await transaction.CommitAsync();
                TempData["SuccessMessage"] = "User and related employee deleted successfully.";
                return RedirectToAction("Users");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", $"Error deleting user: {ex.Message}");
                return View("DeleteUser", user);
            }
        }


        // ================= ASSIGN ROLE =================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignRole(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            ViewBag.Roles = _roleManager.Roles.ToList();
            ViewBag.CurrentRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
            return View(user);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(string id, string role)
        {
            var user = await _userManager.FindByIdAsync(id);
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, role);

            TempData["SuccessMessage"] = "Role updated.";
            return RedirectToAction(nameof(Users));
        }

        // ================= ROLE MANAGEMENT =================
        [Authorize(Roles = "Admin")]
        public IActionResult Roles() => View(_roleManager.Roles.ToList());

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateRole(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
                await _roleManager.CreateAsync(new IdentityRole(roleName));

            TempData["SuccessMessage"] = "Role created.";
            return RedirectToAction(nameof(Roles));
        }
        // ================= EDIT ROLE =================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditRole(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var role = await _roleManager.FindByIdAsync(id);

            if (role == null)
                return NotFound();

            return View(role);
        }


        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(IdentityRole model)
        {
            if (!ModelState.IsValid)
                return View(model);


            var role = await _roleManager.FindByIdAsync(model.Id);

            if (role == null)
                return NotFound();


            // Check duplicate role name
            var existingRole = await _roleManager.FindByNameAsync(model.Name);

            if (existingRole != null && existingRole.Id != role.Id)
            {
                ModelState.AddModelError(
                    "",
                    "Role name already exists."
                );

                return View(model);
            }


            role.Name = model.Name;
            role.NormalizedName = model.Name.ToUpper();


            var result = await _roleManager.UpdateAsync(role);


            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        "",
                        error.Description
                    );
                }

                return View(model);
            }


            TempData["SuccessMessage"] =
                "Role updated successfully.";


            return RedirectToAction(nameof(Roles));
        }
        // List roles

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteRole(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role != null)
                await _roleManager.DeleteAsync(role);

            TempData["SuccessMessage"] = "Role deleted.";
            return RedirectToAction(nameof(Roles));
        }
    }
}
