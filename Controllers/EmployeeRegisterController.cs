using HelpdeskSystem.Data;
using HelpdeskSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class EmployeeRegisterController : Controller
    {
        private readonly HelpDeskContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public EmployeeRegisterController(
            HelpDeskContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // ========================= LIST =========================
        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Block)
                .ToListAsync();

            return View(employees);
        }

        // ========================= CREATE (GET) =========================
        public async Task<IActionResult> Create()
        {
            var model = new EmployeeRegisterViewModel
            {
                Departments = await _context.Departments
                    .Where(d => d.AvailabilityStatus == AvailabilityStatus.Active)
                    .ToListAsync(),

                Blocks = await _context.Blocks
                    .Where(b => b.AvailabilityStatus == AvailabilityStatus.Active)
                    .ToListAsync(),

                AvailabilityStatus = AvailabilityStatus.Active
            };

            return View(model);
        }

        // ========================= CREATE (POST) =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeRegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Departments = await _context.Departments.ToListAsync();
                model.Blocks = await _context.Blocks.ToListAsync();

                return View(model);
            }


            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {

                // ============================
                // 1. CREATE IDENTITY USER
                // ============================

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,

                    FirstName = model.FirstName,
                    MiddleName = model.MiddleName,
                    LastName = model.LastName,

                    FullName =
                    $"{model.FirstName} {model.MiddleName} {model.LastName}",

                    PhoneNumber = model.PhoneNumber
                };


                var result = await _userManager.CreateAsync(
                    user,
                    model.Password
                );


                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }


                    model.Departments = await _context.Departments.ToListAsync();
                    model.Blocks = await _context.Blocks.ToListAsync();

                    return View(model);
                }



                // ============================
                // 2. DEFAULT ROLE USER
                // ============================

                await _userManager.AddToRoleAsync(
                    user,
                    "User"
                );



                // ============================
                // 3. CREATE EMPLOYEE PROFILE
                // ============================

                var employee = new Employee
                {
                    UserId = user.Id,

                    FirstName = model.FirstName,
                    MiddleName = model.MiddleName,
                    LastName = model.LastName,

                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,

                    Gender = model.Gender,

                    DepartmentId = model.DepartmentId,
                    BlockId = model.BlockId,

                    AvailabilityStatus =
                        AvailabilityStatus.Active
                };


                _context.Employees.Add(employee);


                await _context.SaveChangesAsync();


                await transaction.CommitAsync();


                TempData["SuccessMessage"] =
                    "Employee registered successfully.";


                return RedirectToAction(nameof(Index));

            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    "",
                    "Error creating employee: " + ex.Message
                );


                model.Departments = await _context.Departments.ToListAsync();
                model.Blocks = await _context.Blocks.ToListAsync();

                return View(model);
            }
        }

        // ========================= EDIT (GET) =========================
        // ========================= EDIT (GET) =========================
        public async Task<IActionResult> Edit(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            var model = new EmployeeEditViewModel
            {
                Id = employee.Id,
                FirstName = employee.FirstName,
                MiddleName = employee.MiddleName,
                LastName = employee.LastName,
                Email = employee.Email,
                PhoneNumber = employee.PhoneNumber,
                Gender = employee.Gender,
                DepartmentId = employee.DepartmentId ?? 0, // Handle nullable properly
                BlockId = employee.BlockId,
                AvailabilityStatus = employee.AvailabilityStatus,
                Departments = await _context.Departments.ToListAsync(), // Use async
                Blocks = await _context.Blocks.ToListAsync() // Use async
            };

            return View(model);
        }

        // ========================= EDIT (POST) =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EmployeeEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Departments = await _context.Departments.ToListAsync();
                model.Blocks = await _context.Blocks.ToListAsync();
                return View(model);
            }

            var employee = await _context.Employees
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == model.Id);

            if (employee == null) return NotFound();

            // Check if email is being changed
            bool emailChanged = employee.Email != model.Email;

            // Update employee properties
            employee.FirstName = model.FirstName;
            employee.MiddleName = model.MiddleName;
            employee.LastName = model.LastName;
            employee.Email = model.Email; // Update email in Employee table
            employee.PhoneNumber = model.PhoneNumber;
            employee.Gender = model.Gender;
            employee.DepartmentId = model.DepartmentId;
            employee.BlockId = model.BlockId;
            employee.AvailabilityStatus = model.AvailabilityStatus;

            // Update Identity User if exists
            if (employee.User != null)
            {
                employee.User.FirstName = model.FirstName;
                employee.User.MiddleName = model.MiddleName;
                employee.User.LastName = model.LastName;
                employee.User.PhoneNumber = model.PhoneNumber;

                // Update email and username if email changed
                if (emailChanged)
                {
                    employee.User.Email = model.Email;
                    employee.User.UserName = model.Email;
                    employee.User.NormalizedEmail = model.Email.ToUpper();
                    employee.User.NormalizedUserName = model.Email.ToUpper();
                }
            }

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Employee updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(model.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while updating the employee: " + ex.Message);
                model.Departments = await _context.Departments.ToListAsync();
                model.Blocks = await _context.Blocks.ToListAsync();
                return View(model);
            }
        }

        private bool EmployeeExists(int id)
        {
            return _context.Employees.Any(e => e.Id == id);
        }

        // ========================= DETAILS (GET) =========================
        [HttpGet]
        public async Task<IActionResult> Details(int id)

        {
            var employee = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Block)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            return View(employee);
        }
        // ========================= TOGGLE ACTIVE / INACTIVE =========================
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
            {
                TempData["ErrorMessage"] = "Employee not found.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (employee.AvailabilityStatus == AvailabilityStatus.Active)
                {
                    employee.AvailabilityStatus = AvailabilityStatus.Inactive;
                    TempData["SuccessMessage"] = $"Employee '{employee.FirstName} {employee.LastName}' has been set to Inactive.";
                }
                else
                {
                    employee.AvailabilityStatus = AvailabilityStatus.Active;
                    TempData["SuccessMessage"] = $"Employee '{employee.FirstName} {employee.LastName}' has been set to Active.";
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error updating employee status: " + ex.Message;
            }

            // ⚡ Redirect to Index so TotalEmployees recalculates
            return RedirectToAction(nameof(Index));
        }

        private string GenerateRandomPassword(int length = 12)
        {
            const string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*?_~-";
            var random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // ========================= DELETE (GET) =========================
        // ========================= DELETE (GET) =========================
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Block)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            return View(employee);
        }


        // ========================= DELETE (POST) =========================
        [HttpPost("EmployeeRegister/DeleteConfirmed/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            if (employee.User != null)
                await _userManager.DeleteAsync(employee.User);

            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Employee deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}