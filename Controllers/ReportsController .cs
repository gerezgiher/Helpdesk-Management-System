using HelpdeskSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskSystem.Controllers
{
    [Authorize] // Only authenticated users can access reports
    public class ReportsController : Controller
    {
        private readonly HelpDeskContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportsController(HelpDeskContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET: /Reports/TicketStatus
        [Authorize(Roles = "Admin,Agent")] // Only Admin and Agent can see ticket status reports
        public async Task<IActionResult> TicketStatus()
        {
            // Include related entities for display
            var tickets = await _db.Tickets
                .Include(t => t.CreatedByEmployee)
                .Include(t => t.AssignedTo)
                .Include(t => t.Status)
                .Include(t => t.Priority)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tickets); // Make sure Views/Reports/TicketStatus.cshtml exists
        }

        // Example: Agent Performance report
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AgentPerformance()
        {
            var tickets = await _db.Tickets
                .Include(t => t.AssignedTo)
                .Include(t => t.Status)
                .ToListAsync();

            return View(tickets); // Views/Reports/AgentPerformance.cshtml
        }
    }
}
