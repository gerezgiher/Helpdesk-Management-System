using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HelpdeskSystem.Models;
using System.Linq;

namespace HelpdeskSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly HelpDeskContext _context;

        public HomeController(
            UserManager<ApplicationUser> userManager,
            HelpDeskContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // ============================================================
        // PUBLIC WELCOME PAGE
        // ============================================================

        [AllowAnonymous]
        public IActionResult WelcomePage()
        {
            // If already logged in, go directly to dashboard
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction(nameof(Index));
            }

            return View();
        }


        // ============================================================
        // DASHBOARD
        // ============================================================

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                // Normally [Authorize] handles this.
                // This is only a safety check.
                return RedirectToAction("Login", "Account");
            }

            var model = new HomeDashboardViewModel();


            // ========================================================
            // ADMIN
            // ========================================================

            if (await _userManager.IsInRoleAsync(currentUser, "Admin"))
            {
                model.Role = "Admin";

                // Active employees
                model.TotalEmployees = await _context.Employees
                    .Where(e =>
                        e.AvailabilityStatus == AvailabilityStatus.Active)
                    .CountAsync();

                // Active departments
                model.TotalDepartments = await _context.Departments
                    .Where(d =>
                        d.AvailabilityStatus == AvailabilityStatus.Active)
                    .CountAsync();

                // Active blocks
                model.TotalBlocks = await _context.Blocks
                    .Where(b =>
                        b.AvailabilityStatus == AvailabilityStatus.Active)
                    .CountAsync();

                // Active agents
                model.Agents = (await _userManager
                        .GetUsersInRoleAsync("Agent"))
                    .Where(a =>
                        a.AvailabilityStatus == AvailabilityStatus.Active)
                    .ToList();

                // Active employees
                model.Employees = await _userManager.Users
                    .Where(u =>
                        u.AvailabilityStatus == AvailabilityStatus.Active)
                    .ToListAsync();
            }


            // ========================================================
            // DEPARTMENT HEAD
            // ========================================================

            else if (await _userManager.IsInRoleAsync(
                currentUser, "DepartmentHead"))
            {
                model.Role = "DepartmentHead";

                var departmentHead = await _context.Employees
                    .FirstOrDefaultAsync(e =>
                        e.UserId == currentUser.Id);

                if (departmentHead == null ||
                    departmentHead.DepartmentId == null)
                {
                    return View(model);
                }

                int departmentId =
                    departmentHead.DepartmentId.Value;

                // Users in the same department
                var departmentUserIds = await _context.Employees
                    .Where(e =>
                        e.DepartmentId == departmentId)
                    .Select(e => e.UserId)
                    .ToListAsync();

                // Tickets created by department users
                // OR assigned directly to Department Head
                var tickets = _context.Tickets
                    .Include(t => t.Category)
                    .Include(t => t.Status)
                    .Include(t => t.CreatedByEmployee)
                    .Include(t => t.Approval)
                    .Where(t =>
                        departmentUserIds.Contains(t.CreatedById)
                        ||
                        t.AssignedToId == currentUser.Id);

                model.TotalRequests =
                    await tickets.CountAsync();

                model.PendingRequests =
                    await tickets.CountAsync(t =>
                        t.Approval == null ||
                        t.Approval.Status ==
                        ApprovalStatus.Pending);

                model.ApprovedRequests =
                    await tickets.CountAsync(t =>
                        t.Approval != null &&
                        t.Approval.Status ==
                        ApprovalStatus.Approved);

                model.RejectedRequests =
                    await tickets.CountAsync(t =>
                        t.Approval != null &&
                        t.Approval.Status ==
                        ApprovalStatus.Rejected);

                model.Tickets = await tickets
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();
            }


            // ========================================================
            // AGENT
            // ========================================================

            else if (await _userManager.IsInRoleAsync(
                currentUser, "Agent"))
            {
                model.Role = "Agent";

                var userId = currentUser.Id;

                var tickets = _context.Tickets
                    .Include(t => t.Status)
                    .Include(t => t.Approval)
                    .Where(t =>
                        t.AssignedToId == userId &&
                        t.Approval != null &&
                        t.Approval.Status ==
                        ApprovalStatus.Approved);

                model.TotalRequests =
                    await tickets.CountAsync();

                model.PendingRequests =
                    await tickets.CountAsync(t =>
                        t.Status.Name == "Pending");

                model.InProgressRequests =
                    await tickets.CountAsync(t =>
                        t.Status.Name == "In Progress");

                model.ResolvedRequests =
                    await tickets.CountAsync(t =>
                        t.Status.Name == "Resolved");

                model.RejectedRequests =
                    await tickets.CountAsync(t =>
                        t.Status.Name == "Rejected");
            }


            // ========================================================
            // EMPLOYEE / USER
            // ========================================================

            else if (
                await _userManager.IsInRoleAsync(
                    currentUser, "User") ||
                await _userManager.IsInRoleAsync(
                    currentUser, "Employee"))
            {
                model.Role = "Employee";

                var userId = currentUser.Id;

                model.TotalRequests =
                    await _context.Tickets
                        .CountAsync(t =>
                            t.CreatedById == userId);

                model.PendingRequests =
                    await _context.Tickets
                        .CountAsync(t =>
                            t.CreatedById == userId &&
                            (
                                t.Status.Name == "Pending" ||
                                t.Status.Name == "Open"
                            ));

                model.InProgressRequests =
                    await _context.Tickets
                        .CountAsync(t =>
                            t.CreatedById == userId &&
                            t.Status.Name == "In Progress");

                model.ResolvedRequests =
                    await _context.Tickets
                        .CountAsync(t =>
                            t.CreatedById == userId &&
                            t.Status.Name == "Resolved");

                model.RejectedRequests =
                    await _context.Tickets
                        .CountAsync(t =>
                            t.CreatedById == userId &&
                            t.Status.Name == "Rejected");
            }

            return View(model);
        }


        // ============================================================
        // AGENT MANAGEMENT
        // ============================================================

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Agents()
        {
            var agents =
                await _userManager.GetUsersInRoleAsync("Agent");

            var model = new HomeDashboardViewModel
            {
                Role = "Admin",

                Agents = agents.ToList(),

                TotalActiveAgents =
                    agents.Count(a =>
                        a.AvailabilityStatus ==
                        AvailabilityStatus.Active)
            };

            return View(model);
        }


        // ============================================================
        // TOGGLE AGENT STATUS
        // ============================================================

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAgentStatus(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            user.AvailabilityStatus =
                user.AvailabilityStatus ==
                AvailabilityStatus.Active
                    ? AvailabilityStatus.Inactive
                    : AvailabilityStatus.Active;

            await _userManager.UpdateAsync(user);

            return RedirectToAction(nameof(Agents));
        }
    }
}