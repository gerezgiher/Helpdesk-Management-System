using HelpdeskSystem.Data;
using HelpdeskSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskSystem.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly HelpDeskContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsController(
            HelpDeskContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ============================================================
        // LIST
        // ============================================================
        // Shows only notifications belonging to the logged-in user.
        // ============================================================
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(notifications);
        }


        // ============================================================
        // MARK ONE NOTIFICATION AS READ
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            // User can modify only their own notification.
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.Id == id &&
                    n.UserId == user.Id);

            if (notification == null)
                return NotFound();

            notification.IsRead = true;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                var innerMsg =
                    ex.InnerException?.Message ?? ex.Message;

                return BadRequest(
                    "Error marking notification as read: " +
                    innerMsg);
            }

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // MARK ALL NOTIFICATIONS AS READ
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var notifications = await _context.Notifications
                .Where(n =>
                    n.UserId == user.Id &&
                    !n.IsRead)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                var innerMsg =
                    ex.InnerException?.Message ?? ex.Message;

                return BadRequest(
                    "Error marking all notifications as read: " +
                    innerMsg);
            }

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // GET LATEST NOTIFICATIONS - AJAX
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetLatest()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Unauthorized();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .Select(n => new
                {
                    n.Id,
                    n.Message,
                    n.IsRead,
                    TicketId = n.RelatedTicketId,
                    Type = n.Type.ToString(),
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            return Json(notifications);
        }


        // ============================================================
        // DELETE NOTIFICATION
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            // User can delete only their own notification.
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.Id == id &&
                    n.UserId == user.Id);

            if (notification == null)
                return NotFound();

            _context.Notifications.Remove(notification);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                var innerMsg =
                    ex.InnerException?.Message ?? ex.Message;

                return BadRequest(
                    "Error deleting notification: " +
                    innerMsg);
            }

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // CREATE NOTIFICATION
        // ============================================================
        // General notification helper.
        // ============================================================
        public async Task CreateNotificationAsync(
            string userId,
            string message,
            NotificationType type,
            int? ticketId = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException(
                    "UserId is required.",
                    nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException(
                    "Message is required.",
                    nameof(message));
            }

            // Verify that the user exists.
            var user =
                await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                throw new Exception(
                    "Cannot create notification. User does not exist.");
            }

            var notification = new Notification
            {
                UserId = userId,
                Message = message.Trim(),
                Type = type,
                RelatedTicketId = ticketId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                var innerMsg =
                    ex.InnerException?.Message ?? ex.Message;

                throw new Exception(
                    "Database error while creating notification: " +
                    innerMsg);
            }
        }


        // ============================================================
        // NOTIFY DEPARTMENT HEAD
        // ============================================================
        // Finds the requester's department and then finds the
        // Department Head assigned to that department.
        //
        // Requester
        //    ↓
        // Employee.DepartmentId
        //    ↓
        // Department
        //    ↓
        // DepartmentHead
        //    ↓
        // DepartmentHead.UserId
        //
        // NotificationType = RequestCreated
        // ============================================================
        public async Task<bool> NotifyDepartmentHeadAsync(
            string requesterUserId,
            int ticketId,
            string ticketTitle)
        {
            if (string.IsNullOrWhiteSpace(requesterUserId))
                return false;

            // --------------------------------------------------------
            // Find requester employee
            // --------------------------------------------------------
            var requesterEmployee =
                await _context.Employees
                    .FirstOrDefaultAsync(e =>
                        e.UserId == requesterUserId);

            if (requesterEmployee == null)
                return false;

            // Requester must have a department.
            if (!requesterEmployee.DepartmentId.HasValue)
                return false;

            var departmentId =
                requesterEmployee.DepartmentId.Value;

            // --------------------------------------------------------
            // Find department and Department Head
            // --------------------------------------------------------
            var department =
                await _context.Departments
                    .Include(d => d.DepartmentHead)
                    .FirstOrDefaultAsync(d =>
                        d.Id == departmentId);

            if (department == null)
                return false;

            if (department.DepartmentHead == null)
                return false;

            var departmentHeadUserId =
                department.DepartmentHead.UserId;

            if (string.IsNullOrWhiteSpace(departmentHeadUserId))
                return false;

            // Verify Department Head's Identity account.
            var departmentHead =
                await _userManager.FindByIdAsync(
                    departmentHeadUserId);

            if (departmentHead == null)
                return false;

            // --------------------------------------------------------
            // Create notification
            // --------------------------------------------------------
            await CreateNotificationAsync(
                departmentHeadUserId,

                $"New Request requires your approval: '{ticketTitle}'.",

                NotificationType.RequestCreated,

                ticketId);

            return true;
        }


        // ============================================================
        // NOTIFY REQUESTER
        // ============================================================
        public async Task NotifyRequesterAsync(
            string requesterUserId,
            int ticketId,
            string message,
            NotificationType type)
        {
            if (string.IsNullOrWhiteSpace(requesterUserId))
                return;

            await CreateNotificationAsync(
                requesterUserId,
                message,
                type,
                ticketId);
        }


        // ============================================================
        // NOTIFY ASSIGNED AGENT
        // ============================================================
        // IMPORTANT:
        //
        // There is NO department restriction on the Agent.
        //
        // An Agent from ANY department can receive a ticket.
        //
        // The only requirement is:
        //
        // User has "Agent" role.
        // ============================================================
        public async Task<bool> NotifyAgentAsync(
            string agentUserId,
            int ticketId,
            string ticketTitle)
        {
            if (string.IsNullOrWhiteSpace(agentUserId))
                return false;

            var agent =
                await _userManager.FindByIdAsync(agentUserId);

            if (agent == null)
                return false;

            // Verify Agent role.
            var isAgent =
                await _userManager.IsInRoleAsync(
                    agent,
                    "Agent");

            if (!isAgent)
                return false;

            await CreateNotificationAsync(
                agentUserId,

                $"You have been assigned Request '{ticketTitle}'.",

                NotificationType.RequestAssigned,

                ticketId);

            return true;
        }


        // ============================================================
        // NOTIFY REQUESTER THAT REQUEST WAS ASSIGNED
        // ============================================================
        public async Task NotifyRequesterAssignmentAsync(
            string requesterUserId,
            int ticketId,
            string ticketTitle)
        {
            if (string.IsNullOrWhiteSpace(requesterUserId))
                return;

            await CreateNotificationAsync(
                requesterUserId,

                $"Your Request '{ticketTitle}' has been assigned to a Help Desk Agent.",

                NotificationType.RequestAssigned,

                ticketId);
        }


        // ============================================================
        // NOTIFY REQUESTER OF APPROVAL RESULT
        // ============================================================
        public async Task NotifyApprovalResultAsync(
            string requesterUserId,
            int ticketId,
            string ticketTitle,
            bool approved,
            string? rejectionReason = null)
        {
            if (string.IsNullOrWhiteSpace(requesterUserId))
                return;

            string message;

            NotificationType type;

            // --------------------------------------------------------
            // APPROVED
            // --------------------------------------------------------
            if (approved)
            {
                message =
                    $"Your Request '{ticketTitle}' has been approved and is waiting for Agent assignment.";

                type =
                    NotificationType.RequestApproved;
            }

            // --------------------------------------------------------
            // REJECTED
            // --------------------------------------------------------
            else
            {
                message =
                    $"Your Request '{ticketTitle}' has been rejected.";

                if (!string.IsNullOrWhiteSpace(rejectionReason))
                {
                    message +=
                        $" Reason: {rejectionReason.Trim()}";
                }

                type =
                    NotificationType.RequestRejected;
            }

            await CreateNotificationAsync(
                requesterUserId,
                message,
                type,
                ticketId);
        }


        // ============================================================
        // NOTIFY REQUESTER OF STATUS CHANGE
        // ============================================================
        public async Task NotifyStatusChangeAsync(
            string requesterUserId,
            int ticketId,
            string ticketTitle,
            string statusName)
        {
            if (string.IsNullOrWhiteSpace(requesterUserId))
                return;

            if (string.IsNullOrWhiteSpace(statusName))
                statusName = "Unknown";

            await CreateNotificationAsync(
                requesterUserId,

                $"The status of your Request '{ticketTitle}' has been changed to '{statusName}'.",

                NotificationType.StatusChanged,

                ticketId);
        }


        // ============================================================
        // NOTIFY COMMENT ADDED
        // ============================================================
        public async Task NotifyCommentAddedAsync(
            string userId,
            int ticketId,
            string ticketTitle)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return;

            await CreateNotificationAsync(
                userId,

                $"A new comment was added to Request '{ticketTitle}'.",

                NotificationType.CommentAdded,

                ticketId);
        }


        // ============================================================
        // NOTIFY MULTIPLE USERS
        // ============================================================
        // Useful when several users need the same notification.
        // ============================================================
        public async Task NotifyUsersAsync(
            IEnumerable<string> userIds,
            string message,
            NotificationType type,
            int? ticketId = null)
        {
            if (userIds == null)
                return;

            if (string.IsNullOrWhiteSpace(message))
                return;

            var distinctUserIds =
                userIds
                    .Where(id =>
                        !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .ToList();

            if (!distinctUserIds.Any())
                return;

            // Only use users that actually exist.
            var existingUsers =
                await _userManager.Users
                    .Where(u =>
                        distinctUserIds.Contains(u.Id))
                    .Select(u => u.Id)
                    .ToListAsync();

            foreach (var userId in existingUsers)
            {
                _context.Notifications.Add(
                    new Notification
                    {
                        UserId = userId,

                        Message = message.Trim(),

                        Type = type,

                        RelatedTicketId = ticketId,

                        IsRead = false,

                        CreatedAt = DateTime.UtcNow
                    });
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                var innerMsg =
                    ex.InnerException?.Message ?? ex.Message;

                throw new Exception(
                    "Database error while creating notifications: " +
                    innerMsg);
            }
        }


        // ============================================================
        // GET UNREAD COUNT
        // ============================================================
        // Used by the notification bell in the layout.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Unauthorized();

            var count =
                await _context.Notifications
                    .CountAsync(n =>
                        n.UserId == user.Id &&
                        !n.IsRead);

            return Json(new
            {
                count
            });
        }
    }
}