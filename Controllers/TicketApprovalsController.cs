using System.Security.Claims;
using HelpdeskSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskSystem.Controllers
{
    [Authorize(Roles = "Admin,Agent,DepartmentHead")]
    public class TicketApprovalsController : Controller
    {
        private readonly HelpDeskContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TicketApprovalsController(
            HelpDeskContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ======================== INDEX ========================
        public async Task<IActionResult> Index()
        {
            // Include related Ticket, its CreatedByEmployee, and ApprovedBy
            var approvals = await _context.TicketApprovals
                .Include(ta => ta.Ticket)                     // include the ticket
                .ThenInclude(t => t.CreatedByEmployee)  // include the requester info
                .Include(ta => ta.ApprovedBy)                // include the approving user
                .ToListAsync();

            return View(approvals);
        }

        // ======================== DETAILS ========================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var approval = await _context.TicketApprovals
                .Include(a => a.Ticket)
                    .ThenInclude(t => t.CreatedByEmployee)
                .Include(a => a.ApprovedBy)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (approval == null) return NotFound();

            return View(approval);
        }

        // ======================== CREATE ========================
        [Authorize(Roles = "DepartmentHead")]
        public async Task<IActionResult> Create()
        {
            await LoadDropdowns(null);
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "DepartmentHead")]
        public async Task<IActionResult> Create(TicketApproval model)
        {
            if (ModelState.IsValid)
            {
                // Find existing approval or create new
                var ticket = await _context.Tickets
                    .Include(t => t.Approval)
                    .FirstOrDefaultAsync(t => t.Id == model.TicketId);

                if (ticket == null) return NotFound();

                if (ticket.Approval == null)
                {
                    ticket.Approval = new TicketApproval
                    {
                        TicketId = ticket.Id,
                        ApprovedById = User.FindFirstValue(ClaimTypes.NameIdentifier),
                        Status = model.Status,
                        RejectionReason = model.Status == ApprovalStatus.Rejected ? model.RejectionReason : null,
                        DecisionDate = DateTime.Now
                    };
                    _context.TicketApprovals.Add(ticket.Approval);
                }
                else
                {
                    ticket.Approval.Status = model.Status;
                    ticket.Approval.RejectionReason = model.Status == ApprovalStatus.Rejected ? model.RejectionReason : null;
                    ticket.Approval.DecisionDate = DateTime.Now;
                    ticket.Approval.ApprovedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    _context.TicketApprovals.Update(ticket.Approval);
                }

                await _context.SaveChangesAsync();
                return RedirectToAction("Details", "Tickets", new { id = ticket.Id });
            }

            TempData["Error"] = "Invalid submission.";
            return RedirectToAction("Details", "Tickets", new { id = model.TicketId });
        }


        // ======================== APPROVE STATUS ========================
        [Authorize(Roles = "DepartmentHead")]
        public async Task<IActionResult> ApproveStatus(int? id)
        {
            if (id == null) return NotFound();

            var ticket = await _context.Tickets
                .Include(t => t.CreatedByEmployee)
                .Include(t => t.Status)
                .Include(t => t.Approval)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

            if (ticket.Approval != null && ticket.Approval.Status != ApprovalStatus.Pending)
            {
                TempData["Error"] = "This ticket has already been processed.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Status = new SelectList(Enum.GetValues(typeof(ApprovalStatus)));
            return View(ticket);
        }

        [HttpPost]
        [Authorize(Roles = "DepartmentHead")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveStatus(int id, ApprovalStatus status, string? rejectionReason)
        {
            var ticket = await _context.Tickets
                .Include(t => t.Approval)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

            // Rejected must have a reason
            if (status == ApprovalStatus.Rejected && string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["Error"] = "Rejection reason is required.";
                return RedirectToAction(nameof(ApproveStatus), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);

            // Create TicketApproval if it doesn't exist
            if (ticket.Approval == null)
            {
                ticket.Approval = new TicketApproval
                {
                    TicketId = ticket.Id
                };
                _context.TicketApprovals.Add(ticket.Approval);
            }

            // Save approval decision
            ticket.Approval.Status = status;
            ticket.Approval.RejectionReason = rejectionReason;
            ticket.Approval.ApprovedById = currentUser.Id;
            ticket.Approval.DecisionDate = DateTime.UtcNow;

            // Update the ticket status
            string newStatusName = status switch
            {
                ApprovalStatus.Approved => "Approved",
                ApprovalStatus.Rejected => "Rejected",
                _ => "Pending"
            };

            var statusEntity = await _context.Statuses.FirstOrDefaultAsync(s => s.Name == newStatusName);

            if (statusEntity == null)
            {
                TempData["Error"] = $"Status '{newStatusName}' does not exist. Please contact admin.";
                return RedirectToAction(nameof(Index));
            }

            ticket.StatusId = statusEntity.Id;
            ticket.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Ticket status updated successfully.";
            return RedirectToAction(nameof(Index));
        }



        // ======================== EDIT ========================
        [Authorize(Roles = "DepartmentHead")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var approval = await _context.TicketApprovals
                .Include(a => a.Ticket)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (approval == null) return NotFound();

            ViewBag.Status = new SelectList(Enum.GetValues(typeof(ApprovalStatus)), approval.Status);
            return View(approval);
        }

        [HttpPost]
        [Authorize(Roles = "DepartmentHead")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,TicketId,ApprovedById,Status,RejectionReason")] TicketApproval approval)
        {
            if (id != approval.Id) return NotFound();

            if (approval.Status == ApprovalStatus.Rejected && string.IsNullOrWhiteSpace(approval.RejectionReason))
                ModelState.AddModelError(nameof(approval.RejectionReason), "Rejection reason is required.");

            if (!ModelState.IsValid)
            {
                ViewBag.Status = new SelectList(Enum.GetValues(typeof(ApprovalStatus)), approval.Status);
                return View(approval);
            }

            approval.DecisionDate = DateTime.UtcNow;
            _context.Update(approval);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Ticket approval updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ======================== DELETE ========================
        [Authorize(Roles = "DepartmentHead")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var approval = await _context.TicketApprovals
                .Include(a => a.Ticket)
                .Include(a => a.ApprovedBy)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (approval == null) return NotFound();

            return View(approval);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "DepartmentHead")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var approval = await _context.TicketApprovals.FindAsync(id);
            if (approval != null)
            {
                _context.TicketApprovals.Remove(approval);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Ticket approval deleted.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ======================== HELPERS ========================
        private async Task LoadDropdowns(TicketApproval? model)
        {
            var tickets = await _context.Tickets
                .Include(t => t.Approval)
                .Where(t => t.Approval == null || t.Approval.Status == ApprovalStatus.Pending)
                .ToListAsync();

            ViewBag.TicketId = new SelectList(tickets, "Id", "Title", model?.TicketId);

            var heads = await _userManager.GetUsersInRoleAsync("DepartmentHead");
            ViewBag.ApprovedById = new SelectList(heads, "Id", "FullName", model?.ApprovedById);

            ViewBag.Status = new SelectList(Enum.GetValues(typeof(ApprovalStatus)), model?.Status);
        }
    }
}
