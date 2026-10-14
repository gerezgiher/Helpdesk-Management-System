using HelpdeskSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize]
public class TicketsController : Controller
{
    private readonly HelpDeskContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public TicketsController(
        HelpDeskContext db,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env)
    {
        _db = db;
        _userManager = userManager;
        _env = env;
    }


    // ============================================================
    // INDEX
    // ============================================================

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        IQueryable<Ticket> query = _db.Tickets
            .Include(t => t.Category)
            .Include(t => t.Priority)
            .Include(t => t.Status)
            .Include(t => t.CreatedByEmployee)
            .Include(t => t.AssignedTo)
            .Include(t => t.Approval);

        // ========================================================
        // USER / EMPLOYEE
        // Only own requests
        // ========================================================

        if (User.IsInRole("User") || User.IsInRole("Employee"))
        {
            query = query.Where(t =>
                t.CreatedById == user.Id);
        }

        // ========================================================
        // DEPARTMENT HEAD
        //
        // Can see:
        // - Own requests
        // - Requests created by employees in own department
        //
        // Department controls visibility and approval.
        // ========================================================

        else if (User.IsInRole("DepartmentHead"))
        {
            var departmentHeadEmployee =
                await GetCurrentDepartmentHeadEmployeeAsync();

            if (departmentHeadEmployee == null ||
                !departmentHeadEmployee.DepartmentId.HasValue)
            {
                return Forbid();
            }

            var departmentId =
                departmentHeadEmployee.DepartmentId.Value;

            var departmentUserIds =
                await _db.Employees
                    .Where(e =>
                        e.DepartmentId == departmentId &&
                        e.UserId != null)
                    .Select(e => e.UserId!)
                    .ToListAsync();

            query = query.Where(t =>
                t.CreatedById == user.Id ||
                departmentUserIds.Contains(t.CreatedById));
        }

        // ========================================================
        // AGENT
        //
        // Agent can be from ANY department.
        //
        // Can see:
        // - Own requests
        // - Approved requests assigned to the Agent
        // ========================================================

        else if (User.IsInRole("Agent"))
        {
            query = query.Where(t =>
                t.CreatedById == user.Id ||

                (
                    t.AssignedToId == user.Id &&
                    t.Approval != null &&
                    t.Approval.Status == ApprovalStatus.Approved
                )
            );
        }

        // ========================================================
        // ADMIN
        //
        // Admin sees ALL requests from ALL departments.
        // ========================================================

        else if (User.IsInRole("Admin"))
        {
            // No filter
        }

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return View(tickets);
    }


    // ============================================================
    // CREATE - GET
    // ============================================================

    [Authorize(Roles = "Admin,DepartmentHead,Agent,User,Employee")]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();

        var user = await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        var model = new TicketCreateViewModel
        {
            RequesterEmail = user.Email ?? ""
        };

        var employee = await _db.Employees
            .Include(e => e.Department)
            .Include(e => e.Block)
            .FirstOrDefaultAsync(e => e.UserId == user.Id);

        if (employee != null)
        {
            model.RequesterName =
                $"{employee.FirstName} {employee.MiddleName} {employee.LastName}"
                .Trim();

            model.PhoneNumber =
                employee.PhoneNumber ?? "";

            model.DepartmentName =
                employee.Department?.DepartmentName ?? "";

            model.RequesterBlock =
                employee.Block?.BlockCode ?? "";

            model.NumberOfFloors =
                employee.Block?.NumberOfFloors ?? 0;
        }
        else
        {
            model.RequesterName =
                user.FullName ??
                user.UserName ??
                "Unknown User";

            model.PhoneNumber =
                user.PhoneNumber ?? "";

            model.DepartmentName = "N/A";
            model.RequesterBlock = "N/A";
            model.NumberOfFloors = 0;
        }

        return View(model);
    }


    // ============================================================
    // CREATE - POST
    // ============================================================

    [HttpPost]
    [Authorize(Roles = "Admin,DepartmentHead,Agent,User,Employee")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        TicketCreateViewModel model,
        AssignmentType assignmentType = AssignmentType.Manual,
        string? agentId = null)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        // ========================================================
        // FIND REQUESTER EMPLOYEE
        // ========================================================

        var employee = await _db.Employees
            .Include(e => e.Department)
            .Include(e => e.Block)
            .FirstOrDefaultAsync(e => e.UserId == user.Id);

        // ========================================================
        // USER / EMPLOYEE / DEPARTMENT HEAD
        // MUST HAVE EMPLOYEE PROFILE
        // ========================================================

        if ((User.IsInRole("User") ||
             User.IsInRole("Employee") ||
             User.IsInRole("DepartmentHead")) &&
            employee == null)
        {
            ModelState.AddModelError(
                "",
                "Your employee profile was not found. Please contact the administrator.");

            await PopulateDropdownsAsync();
            return View(model);
        }

        // ========================================================
        // USER MUST HAVE DEPARTMENT
        // ========================================================

        if ((User.IsInRole("User") ||
             User.IsInRole("Employee") ||
             User.IsInRole("DepartmentHead")) &&
            employee != null &&
            !employee.DepartmentId.HasValue)
        {
            ModelState.AddModelError(
                "",
                "Your department is not configured. Please contact the administrator.");

            await PopulateDropdownsAsync();
            return View(model);
        }

        // ========================================================
        // GET PENDING STATUS
        //
        // Pending = waiting for Department Head approval
        // ========================================================

        var pendingStatus =
            await _db.Statuses
                .FirstOrDefaultAsync(s => s.Name == "Pending");

        if (pendingStatus == null)
        {
            ModelState.AddModelError(
                "",
                "Pending status is not configured in the system.");

            await PopulateDropdownsAsync();
            return View(model);
        }

        // ========================================================
        // CREATE TICKET
        //
        // IMPORTANT:
        // No Agent assignment before approval.
        // ========================================================

        var ticket = new Ticket
        {
            Title = model.Title,
            Description = model.Description,

            CategoryId = model.CategoryId,
            PriorityId = model.PriorityId,

            StatusId = pendingStatus.Id,

            CreatedById = user.Id,

            CreatedAt = DateTime.UtcNow,

            AssignmentType = AssignmentType.Manual,

            AssignedToId = null,

            Approval = new TicketApproval
            {
                Status = ApprovalStatus.Pending
            }
        };

        _db.Tickets.Add(ticket);

        await _db.SaveChangesAsync();

        // ========================================================
        // SEND REQUEST TO EXACT DEPARTMENT HEAD
        //
        // Requester Department
        //       ↓
        // Department
        //       ↓
        // DepartmentHead
        // ========================================================

        if (employee != null &&
            employee.DepartmentId.HasValue)
        {
            var department =
                await _db.Departments
                    .Include(d => d.DepartmentHead)
                    .FirstOrDefaultAsync(d =>
                        d.Id == employee.DepartmentId.Value);

            if (department != null &&
                department.DepartmentHead != null &&
                !string.IsNullOrWhiteSpace(
                    department.DepartmentHead.UserId))
            {
                var departmentHeadUserId =
                    department.DepartmentHead.UserId;

                _db.Notifications.Add(
                    new Notification
                    {
                        UserId = departmentHeadUserId,

                        Message =
                            $"New Request requires your approval: '{ticket.Title}'.",

                        RelatedTicketId = ticket.Id,

                        IsRead = false,

                        CreatedAt = DateTime.UtcNow
                    });

                await _db.SaveChangesAsync();
            }
            else
            {
                TempData["WarningMessage"] =
                    "Request created, but no Department Head is configured for your department.";
            }
        }

        // ========================================================
        // ATTACHMENTS
        // ========================================================

        if (model.Attachments != null &&
            model.Attachments.Any())
        {
            var uploads =
                Path.Combine(
                    _env.WebRootPath,
                    "uploads");

            Directory.CreateDirectory(uploads);

            foreach (var file in model.Attachments)
            {
                if (file == null || file.Length == 0)
                    continue;

                var originalFileName =
                    Path.GetFileName(file.FileName);

                var storedFileName =
                    $"{Guid.NewGuid()}_{originalFileName}";

                var path =
                    Path.Combine(
                        uploads,
                        storedFileName);

                await using var stream =
                    System.IO.File.Create(path);

                await file.CopyToAsync(stream);

                _db.TicketAttachments.Add(
                    new TicketAttachment
                    {
                        TicketId = ticket.Id,

                        FileName = originalFileName,

                        FilePath =
                            $"/uploads/{storedFileName}",

                        Size = file.Length
                    });
            }

            await _db.SaveChangesAsync();
        }

        TempData["SuccessMessage"] =
            "Request created successfully. Awaiting Department Head approval.";

        return RedirectToAction(nameof(Index));
    }


    // ============================================================
    // DETAILS
    // ============================================================

    public async Task<IActionResult> Details(int id)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Priority)
            .Include(t => t.Status)
            .Include(t => t.Category)
            .Include(t => t.CreatedByEmployee)
            .Include(t => t.AssignedTo)
            .Include(t => t.Comments)
                .ThenInclude(c => c.Author)
            .Include(t => t.Attachments)
            .Include(t => t.Approval)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null)
            return NotFound();

        var user =
            await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        bool canView = false;

        // ========================================================
        // CREATOR
        // ========================================================

        if (ticket.CreatedById == user.Id)
        {
            canView = true;
        }

        // ========================================================
        // ADMIN
        // ========================================================

        else if (User.IsInRole("Admin"))
        {
            canView = true;
        }

        // ========================================================
        // AGENT
        //
        // Agent can be from ANY department.
        // Only assigned approved tickets.
        // ========================================================

        else if (User.IsInRole("Agent"))
        {
            canView =
                ticket.AssignedToId == user.Id &&
                IsApproved(ticket);
        }

        // ========================================================
        // DEPARTMENT HEAD
        //
        // Only tickets belonging to own department.
        // ========================================================

        else if (User.IsInRole("DepartmentHead"))
        {
            canView =
                await IsTicketInDepartmentHeadDepartmentAsync(ticket);
        }

        if (!canView)
            return Forbid();

        ViewBag.CurrentUserId = user.Id;

        // IMPORTANT:
        // Show ALL Agents.
        // Agent department does NOT matter.
        ViewBag.Agents =
            await _userManager.GetUsersInRoleAsync("Agent");

        ViewBag.Statuses =
            await _db.Statuses
                .OrderBy(s => s.Name)
                .ToListAsync();

        return View(ticket);
    }


    // ============================================================
    // EDIT - GET
    // ============================================================

    public async Task<IActionResult> Edit(int id)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Approval)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null)
            return NotFound();

        var user =
            await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        bool allowed = false;

        // ========================================================
        // ADMIN
        // ========================================================

        if (User.IsInRole("Admin"))
        {
            allowed = true;
        }

        // ========================================================
        // USER / EMPLOYEE
        //
        // Can edit own request only while pending approval.
        // ========================================================

        else if (User.IsInRole("User") ||
                 User.IsInRole("Employee"))
        {
            allowed =
                ticket.CreatedById == user.Id &&
                IsPendingApproval(ticket);
        }

        // ========================================================
        // DEPARTMENT HEAD
        //
        // Can edit requests in own department while pending.
        // ========================================================

        else if (User.IsInRole("DepartmentHead"))
        {
            allowed =
                await IsTicketInDepartmentHeadDepartmentAsync(ticket)
                &&
                IsPendingApproval(ticket);
        }

        if (!allowed)
        {
            TempData["ErrorMessage"] =
                "You are not authorized to edit this Request.";

            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync();

        return View(ticket);
    }


    // ============================================================
    // EDIT - POST
    // ============================================================

    [HttpPost]
    [Authorize(Roles =
        "Admin,DepartmentHead,User,Employee")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Ticket model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(model);
        }

        var ticket = await _db.Tickets
            .Include(t => t.Approval)
            .FirstOrDefaultAsync(t => t.Id == model.Id);

        if (ticket == null)
            return NotFound();

        var user =
            await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        bool allowed = false;

        // Admin
        if (User.IsInRole("Admin"))
        {
            allowed = true;
        }

        // User / Employee
        else if (User.IsInRole("User") ||
                 User.IsInRole("Employee"))
        {
            allowed =
                ticket.CreatedById == user.Id &&
                IsPendingApproval(ticket);
        }

        // Department Head
        else if (User.IsInRole("DepartmentHead"))
        {
            allowed =
                await IsTicketInDepartmentHeadDepartmentAsync(ticket)
                &&
                IsPendingApproval(ticket);
        }

        if (!allowed)
        {
            TempData["ErrorMessage"] =
                "You are not authorized to edit this Request.";

            return RedirectToAction(nameof(Index));
        }

        ticket.Title =
            model.Title;

        ticket.Description =
            model.Description;

        ticket.CategoryId =
            model.CategoryId;

        ticket.PriorityId =
            model.PriorityId;

        ticket.UpdatedAt =
            DateTime.UtcNow;

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Request updated successfully.";

        return RedirectToAction(nameof(Index));
    }


    // ============================================================
    // APPROVAL HELPERS
    // ============================================================

    private bool IsApproved(Ticket ticket)
    {
        return ticket.Approval != null &&
               ticket.Approval.Status ==
               ApprovalStatus.Approved;
    }


    private bool IsPendingApproval(Ticket ticket)
    {
        return ticket.Approval == null ||
               ticket.Approval.Status ==
               ApprovalStatus.Pending;
    }


    // ============================================================
    // GET CURRENT DEPARTMENT HEAD EMPLOYEE
    // ============================================================

    private async Task<Employee?>
        GetCurrentDepartmentHeadEmployeeAsync()
    {
        var userId =
            _userManager.GetUserId(User);

        if (string.IsNullOrEmpty(userId))
            return null;

        return await _db.Employees
            .Include(e => e.Department)
            .Include(e => e.Block)
            .FirstOrDefaultAsync(e =>
                e.UserId == userId);
    }


    // ============================================================
    // CHECK TICKET DEPARTMENT
    //
    // Ticket
    //   ↓
    // CreatedById
    //   ↓
    // Employee.UserId
    //   ↓
    // Employee.DepartmentId
    //
    // Compare with Department Head department.
    //
    // IMPORTANT:
    // This method checks REQUESTER department only.
    // It does NOT check Agent department.
    // ============================================================

    private async Task<bool>
        IsTicketInDepartmentHeadDepartmentAsync(
            Ticket ticket)
    {
        var departmentHeadEmployee =
            await GetCurrentDepartmentHeadEmployeeAsync();

        if (departmentHeadEmployee == null ||
            !departmentHeadEmployee.DepartmentId.HasValue)
        {
            return false;
        }

        var requesterEmployee =
            await _db.Employees
                .FirstOrDefaultAsync(e =>
                    e.UserId == ticket.CreatedById);

        if (requesterEmployee == null ||
            !requesterEmployee.DepartmentId.HasValue)
        {
            return false;
        }

        return requesterEmployee.DepartmentId.Value ==
               departmentHeadEmployee.DepartmentId.Value;
    }


    // ============================================================
    // CAN USER ACT
    // ============================================================

    private async Task<bool>
        CanUserActAsync(Ticket ticket)
    {
        var user =
            await _userManager.GetUserAsync(User);

        if (user == null)
            return false;

        // ========================================================
        // ADMIN
        // ========================================================

        if (User.IsInRole("Admin"))
            return true;

        // ========================================================
        // USER / EMPLOYEE
        //
        // Can act on own request while pending approval.
        // ========================================================

        if (User.IsInRole("User") ||
            User.IsInRole("Employee"))
        {
            return ticket.CreatedById == user.Id &&
                   IsPendingApproval(ticket);
        }

        // ========================================================
        // AGENT
        //
        // Agent department is irrelevant.
        // Only assignment matters.
        // ========================================================

        if (User.IsInRole("Agent"))
        {
            return IsApproved(ticket) &&
                   ticket.AssignedToId == user.Id;
        }

        // ========================================================
        // DEPARTMENT HEAD
        //
        // Department controls access.
        // ========================================================

        if (User.IsInRole("DepartmentHead"))
        {
            return await
                IsTicketInDepartmentHeadDepartmentAsync(
                    ticket);
        }

        return false;
    }


    // ============================================================
    // ASSIGN
    //
    // Department Head:
    //   Can assign approved requests from own department.
    //
    // Admin:
    //   Can assign approved requests from ALL departments.
    //
    // Agent:
    //   Can belong to ANY department.
    //
    // Assignment:
    //   Manual OR Automatic.
    // ============================================================

    [HttpPost]
    [Authorize(Roles = "Admin,DepartmentHead")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(
        int ticketId,
        string? agentId,
        AssignmentType assignmentType)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Approval)
            .Include(t => t.Status)
            .FirstOrDefaultAsync(t =>
                t.Id == ticketId);

        if (ticket == null)
            return NotFound();

        // ========================================================
        // MUST BE APPROVED
        // ========================================================

        if (!IsApproved(ticket))
        {
            TempData["ErrorMessage"] =
                "Request must be approved before assignment.";

            return RedirectToAction(
                nameof(Details),
                new { id = ticketId });
        }

        // ========================================================
        // DEPARTMENT HEAD
        //
        // Only own department requests.
        // ========================================================

        if (User.IsInRole("DepartmentHead"))
        {
            var allowed =
                await IsTicketInDepartmentHeadDepartmentAsync(
                    ticket);

            if (!allowed)
            {
                TempData["ErrorMessage"] =
                    "You cannot assign a Request from another department.";

                return Forbid();
            }
        }

        // ========================================================
        // ADMIN
        //
        // No department restriction.
        // ========================================================

        string? newAgentId;

        // ========================================================
        // AUTOMATIC ASSIGNMENT
        //
        // Search ALL Agents.
        // Agent department does NOT matter.
        // ========================================================

        if (assignmentType ==
            AssignmentType.Automatic)
        {
            newAgentId =
                await GetLeastLoadedAgentIdAsync();
        }
        else
        {
            // ====================================================
            // MANUAL ASSIGNMENT
            // ====================================================

            newAgentId = agentId;
        }

        // ========================================================
        // AGENT REQUIRED
        // ========================================================

        if (string.IsNullOrWhiteSpace(newAgentId))
        {
            TempData["ErrorMessage"] =
                "No Agent was selected or no Agent is available.";

            return RedirectToAction(
                nameof(Details),
                new { id = ticketId });
        }

        // ========================================================
        // FIND SELECTED AGENT
        // ========================================================

        var selectedAgent =
            await _userManager.FindByIdAsync(
                newAgentId);

        if (selectedAgent == null)
        {
            TempData["ErrorMessage"] =
                "Selected Agent was not found.";

            return RedirectToAction(
                nameof(Details),
                new { id = ticketId });
        }

        // ========================================================
        // VERIFY AGENT ROLE
        //
        // IMPORTANT:
        // NO DEPARTMENT CHECK HERE.
        // ========================================================

        var isAgent =
            await _userManager.IsInRoleAsync(
                selectedAgent,
                "Agent");

        if (!isAgent)
        {
            TempData["ErrorMessage"] =
                "Selected user is not an Agent.";

            return RedirectToAction(
                nameof(Details),
                new { id = ticketId });
        }

        // ========================================================
        // ASSIGN TICKET
        // ========================================================

        ticket.AssignmentType =
            assignmentType;

        ticket.AssignedToId =
            newAgentId;

        ticket.UpdatedAt =
            DateTime.UtcNow;

        // ========================================================
        // STATUS = ASSIGNED
        // ========================================================

        var assignedStatus =
            await _db.Statuses
                .FirstOrDefaultAsync(
                    s => s.Name == "Assigned");

        if (assignedStatus != null)
        {
            ticket.StatusId =
                assignedStatus.Id;
        }

        // ========================================================
        // NOTIFY AGENT
        // ========================================================

        _db.Notifications.Add(
            new Notification
            {
                UserId = newAgentId,

                Message =
                    $"You have been assigned Request '{ticket.Title}'.",

                RelatedTicketId =
                    ticket.Id,

                IsRead = false,

                CreatedAt =
                    DateTime.UtcNow
            });

        // ========================================================
        // NOTIFY REQUESTER
        // ========================================================

        _db.Notifications.Add(
            new Notification
            {
                UserId =
                    ticket.CreatedById,

                Message =
                    $"Your Request '{ticket.Title}' has been assigned to a Help Desk Agent.",

                RelatedTicketId =
                    ticket.Id,

                IsRead = false,

                CreatedAt =
                    DateTime.UtcNow
            });

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            assignmentType == AssignmentType.Automatic
                ? "Request automatically assigned successfully."
                : "Request manually assigned successfully.";

        return RedirectToAction(
            nameof(Details),
            new { id = ticketId });
    }


    // ============================================================
    // APPROVE / REJECT
    // ============================================================

    [HttpPost]
    [Authorize(Roles = "DepartmentHead,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveStatus(
        int id,
        ApprovalStatus status,
        string? rejectionReason)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Approval)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null)
            return NotFound();

        // ========================================================
        // DEPARTMENT HEAD CAN ONLY PROCESS OWN DEPARTMENT
        // ========================================================

        var belongsToDepartment =
            await IsTicketInDepartmentHeadDepartmentAsync(
                ticket);

        if (!belongsToDepartment)
        {
            TempData["ErrorMessage"] =
                "You are not authorized to approve this Request.";

            return Forbid();
        }

        // ========================================================
        // ONLY PENDING APPROVAL
        // ========================================================

        if (ticket.Approval != null &&
            ticket.Approval.Status !=
            ApprovalStatus.Pending)
        {
            TempData["ErrorMessage"] =
                "This Request has already been processed.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        // ========================================================
        // VALIDATE APPROVAL STATUS
        // ========================================================

        if (status != ApprovalStatus.Approved &&
            status != ApprovalStatus.Rejected)
        {
            TempData["ErrorMessage"] =
                "Invalid approval status.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        // ========================================================
        // REJECTION REASON REQUIRED
        // ========================================================

        if (status == ApprovalStatus.Rejected &&
            string.IsNullOrWhiteSpace(rejectionReason))
        {
            TempData["ErrorMessage"] =
                "Rejection reason is required.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        var currentUser =
            await _userManager.GetUserAsync(User);

        if (currentUser == null)
            return Challenge();

        // ========================================================
        // APPROVAL RECORD
        // ========================================================

        var approval =
            ticket.Approval;

        if (approval == null)
        {
            approval = new TicketApproval
            {
                TicketId = ticket.Id
            };

            ticket.Approval =
                approval;

            _db.TicketApprovals.Add(
                approval);
        }

        approval.Status =
            status;

        approval.ApprovedById =
            currentUser.Id;

        approval.DecisionDate =
            DateTime.UtcNow;

        approval.RejectionReason =
            status == ApprovalStatus.Rejected
                ? rejectionReason?.Trim()
                : null;

        // ========================================================
        // APPROVED
        //
        // Approved request goes into assignment queue.
        // No Agent is assigned automatically here unless the
        // Department Head chooses Automatic Assignment.
        // ========================================================

        string statusName;

        if (status ==
            ApprovalStatus.Approved)
        {
            statusName = "Pending";

            ticket.AssignedToId =
                null;

            ticket.AssignmentType =
                AssignmentType.Manual;
        }
        else
        {
            // ====================================================
            // REJECTED
            // ====================================================

            statusName = "Closed";

            ticket.AssignedToId =
                null;
        }

        // ========================================================
        // GET NEW STATUS
        // ========================================================

        var newStatus =
            await _db.Statuses
                .FirstOrDefaultAsync(
                    s => s.Name == statusName);

        if (newStatus == null)
        {
            TempData["ErrorMessage"] =
                $"Status '{statusName}' is not configured.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        ticket.StatusId =
            newStatus.Id;

        ticket.UpdatedAt =
            DateTime.UtcNow;

        // ========================================================
        // NOTIFY REQUEST CREATOR
        // ========================================================

        var message =
            status == ApprovalStatus.Approved
                ? $"Your Request '{ticket.Title}' has been approved and is waiting for Agent assignment."
                : $"Your Request '{ticket.Title}' has been rejected. Reason: {rejectionReason}";

        _db.Notifications.Add(
            new Notification
            {
                UserId =
                    ticket.CreatedById,

                Message =
                    message,

                RelatedTicketId =
                    ticket.Id,

                IsRead = false,

                CreatedAt =
                    DateTime.UtcNow
            });

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            status == ApprovalStatus.Approved
                ? "Request approved successfully. It is now ready for Agent assignment."
                : "Request rejected successfully.";

        return RedirectToAction(
            nameof(Details),
            new { id });
    }


    // ============================================================
    // CHANGE STATUS
    // ============================================================

    [HttpPost]
    [Authorize(Roles = "Admin,Agent")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(
        int ticketId,
        int statusId)
    {
        var user =
            await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        var ticket = await _db.Tickets
            .Include(t => t.AssignedTo)
            .Include(t => t.Approval)
            .FirstOrDefaultAsync(
                t => t.Id == ticketId);

        if (ticket == null)
            return NotFound();

        // ========================================================
        // MUST BE APPROVED
        // ========================================================

        if (!IsApproved(ticket))
        {
            TempData["ErrorMessage"] =
                "Request must be approved before changing work status.";

            return RedirectToAction(
                nameof(Details),
                new { id = ticketId });
        }

        // ========================================================
        // AGENT CAN CHANGE ONLY OWN ASSIGNED REQUEST
        // ========================================================

        if (User.IsInRole("Agent") &&
            ticket.AssignedToId != user.Id)
        {
            TempData["ErrorMessage"] =
                "You are not allowed to update this Request's status.";

            return RedirectToAction(
                nameof(Details),
                new { id = ticketId });
        }

        // ========================================================
        // GET STATUS
        // ========================================================

        var status =
            await _db.Statuses
                .FirstOrDefaultAsync(
                    s => s.Id == statusId);

        if (status == null)
        {
            TempData["ErrorMessage"] =
                "Invalid status selected.";

            return RedirectToAction(
                nameof(Details),
                new { id = ticketId });
        }

        // ========================================================
        // UPDATE STATUS
        // ========================================================

        ticket.StatusId =
            status.Id;

        ticket.UpdatedAt =
            DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // ========================================================
        // NOTIFY REQUEST CREATOR
        // ========================================================

        _db.Notifications.Add(
            new Notification
            {
                UserId =
                    ticket.CreatedById,

                Message =
                    $"The status of your Request '{ticket.Title}' has been changed to '{status.Name}'.",

                RelatedTicketId =
                    ticket.Id,

                IsRead = false,

                CreatedAt =
                    DateTime.UtcNow
            });

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Request status updated successfully.";

        return RedirectToAction(
            nameof(Details),
            new { id = ticketId });
    }


    // ============================================================
    // ADD COMMENT
    // ============================================================

    [HttpPost]
    [Authorize(Roles =
        "Admin,Agent,User,Employee,DepartmentHead")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(
        int ticketId,
        string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            TempData["ErrorMessage"] =
                "Comment cannot be empty.";

            return RedirectToAction(
                nameof(Details),
                new { id = ticketId });
        }

        var ticket = await _db.Tickets
            .Include(t => t.Approval)
            .FirstOrDefaultAsync(
                t => t.Id == ticketId);

        if (ticket == null)
            return NotFound();

        if (!await CanUserActAsync(ticket))
            return Forbid();

        var user =
            await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        _db.TicketComments.Add(
            new TicketComment
            {
                TicketId =
                    ticketId,

                Body =
                    message.Trim(),

                AuthorId =
                    user.Id,

                CreatedAt =
                    DateTime.UtcNow
            });

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Comment added successfully.";

        return RedirectToAction(
            nameof(Details),
            new { id = ticketId });
    }


    // ============================================================
    // DELETE COMMENT
    // ============================================================

    [HttpPost]
    [Authorize(Roles =
        "Admin,Agent,DepartmentHead")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(
        int commentId,
        int ticketId)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Approval)
            .FirstOrDefaultAsync(
                t => t.Id == ticketId);

        if (ticket == null)
            return NotFound();

        var comment =
            await _db.TicketComments
                .FirstOrDefaultAsync(c =>
                    c.Id == commentId &&
                    c.TicketId == ticketId);

        if (comment == null)
            return NotFound();

        var user =
            await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        bool allowed = false;

        // ========================================================
        // ADMIN
        // ========================================================

        if (User.IsInRole("Admin"))
        {
            allowed = true;
        }

        // ========================================================
        // AGENT
        //
        // Agent department does NOT matter.
        // ========================================================

        else if (User.IsInRole("Agent"))
        {
            allowed =
                IsApproved(ticket) &&
                ticket.AssignedToId == user.Id;
        }

        // ========================================================
        // DEPARTMENT HEAD
        //
        // Own department only.
        // ========================================================

        else if (User.IsInRole("DepartmentHead"))
        {
            allowed =
                await IsTicketInDepartmentHeadDepartmentAsync(
                    ticket);
        }

        if (!allowed)
            return Forbid();

        _db.TicketComments.Remove(comment);

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Comment deleted successfully.";

        return RedirectToAction(
            nameof(Details),
            new { id = ticketId });
    }


    // ============================================================
    // DELETE TICKET
    // ============================================================

    [HttpPost]
    [Authorize(Roles =
        "Admin,DepartmentHead,Agent,User,Employee")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .Include(t => t.Approval)
            .FirstOrDefaultAsync(
                t => t.Id == id);

        if (ticket == null)
        {
            TempData["ErrorMessage"] =
                "Request not found.";

            return RedirectToAction(
                "Index",
                "Home");
        }

        var user =
            await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        bool allowed = false;

        // ========================================================
        // ADMIN
        // ========================================================

        if (User.IsInRole("Admin"))
        {
            allowed = true;
        }

        // ========================================================
        // USER / EMPLOYEE
        //
        // Can delete own pending request.
        // ========================================================

        else if (User.IsInRole("User") ||
                 User.IsInRole("Employee"))
        {
            allowed =
                ticket.CreatedById == user.Id &&
                IsPendingApproval(ticket);
        }

        // ========================================================
        // DEPARTMENT HEAD
        //
        // Can delete pending request in own department.
        // ========================================================

        else if (User.IsInRole("DepartmentHead"))
        {
            allowed =
                await IsTicketInDepartmentHeadDepartmentAsync(
                    ticket)
                &&
                IsPendingApproval(ticket);
        }

        // ========================================================
        // AGENT
        //
        // Agent can delete assigned approved request.
        // Agent department does NOT matter.
        // ========================================================

        else if (User.IsInRole("Agent"))
        {
            allowed =
                IsApproved(ticket) &&
                ticket.AssignedToId == user.Id;
        }

        if (!allowed)
        {
            TempData["ErrorMessage"] =
                "You are not authorized to delete this Request.";

            return RedirectToAction(nameof(Index));
        }

        try
        {
            // ====================================================
            // DELETE PHYSICAL ATTACHMENT FILES
            // ====================================================

            if (ticket.Attachments != null)
            {
                foreach (var attachment in ticket.Attachments)
                {
                    if (!string.IsNullOrWhiteSpace(
                        attachment.FilePath))
                    {
                        var relativePath =
                            attachment.FilePath
                                .TrimStart('/')
                                .Replace(
                                    '/',
                                    Path.DirectorySeparatorChar);

                        var physicalPath =
                            Path.Combine(
                                _env.WebRootPath,
                                relativePath);

                        if (System.IO.File.Exists(
                            physicalPath))
                        {
                            System.IO.File.Delete(
                                physicalPath);
                        }
                    }
                }
            }

            // ====================================================
            // COMMENTS
            // ====================================================

            if (ticket.Comments != null &&
                ticket.Comments.Any())
            {
                _db.TicketComments.RemoveRange(
                    ticket.Comments);
            }

            // ====================================================
            // ATTACHMENTS
            // ====================================================

            if (ticket.Attachments != null &&
                ticket.Attachments.Any())
            {
                _db.TicketAttachments.RemoveRange(
                    ticket.Attachments);
            }

            // ====================================================
            // APPROVAL
            // ====================================================

            if (ticket.Approval != null)
            {
                _db.TicketApprovals.Remove(
                    ticket.Approval);
            }

            // ====================================================
            // TICKET
            // ====================================================

            _db.Tickets.Remove(ticket);

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Request deleted successfully.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] =
                "Error deleting Request: " +
                ex.Message;
        }

        return RedirectToAction(
            "Index",
            "Home");
    }


    // ============================================================
    // ASSIGNED
    //
    // Admin:
    //   All approved assigned requests.
    //
    // Agent:
    //   Only requests assigned to current Agent.
    //
    // Department Head:
    //   Assigned requests created by employees
    //   in Department Head's department.
    //
    // Agent's department is NOT considered.
    // ============================================================

    [Authorize(Roles =
        "Admin,Agent,DepartmentHead")]
    public async Task<IActionResult> Assigned()
    {
        var user =
            await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        IQueryable<Ticket> query =
            _db.Tickets
                .Include(t => t.Category)
                .Include(t => t.CreatedByEmployee)
                .Include(t => t.AssignedTo)
                .Include(t => t.Status)
                .Include(t => t.Priority)
                .Include(t => t.Approval)
                .Where(t =>
                    t.AssignedToId != null &&
                    t.Approval != null &&
                    t.Approval.Status ==
                    ApprovalStatus.Approved);

        // ========================================================
        // AGENT
        // ========================================================

        if (User.IsInRole("Agent"))
        {
            query = query.Where(t =>
                t.AssignedToId == user.Id);
        }

        // ========================================================
        // DEPARTMENT HEAD
        // ========================================================

        else if (User.IsInRole("DepartmentHead"))
        {
            var departmentHeadEmployee =
                await GetCurrentDepartmentHeadEmployeeAsync();

            if (departmentHeadEmployee == null ||
                !departmentHeadEmployee.DepartmentId.HasValue)
            {
                return Forbid();
            }

            var departmentId =
                departmentHeadEmployee.DepartmentId.Value;

            var departmentUserIds =
                await _db.Employees
                    .Where(e =>
                        e.DepartmentId == departmentId &&
                        e.UserId != null)
                    .Select(e => e.UserId!)
                    .ToListAsync();

            query = query.Where(t =>
                departmentUserIds.Contains(
                    t.CreatedById));
        }

        // ========================================================
        // ADMIN
        //
        // No filter.
        // ========================================================

        var tickets =
            await query
                .OrderByDescending(
                    t => t.CreatedAt)
                .ToListAsync();

        return View(tickets);
    }


    // ============================================================
    // UNASSIGNED
    //
    // Admin:
    //   All approved unassigned requests.
    //
    // Department Head:
    //   Approved unassigned requests from own department.
    //
    // This is where Department Heads can reduce Admin workload
    // by assigning requests themselves.
    // ============================================================

    [Authorize(Roles =
        "Admin,DepartmentHead")]
    public async Task<IActionResult> Unassigned()
    {
        IQueryable<Ticket> query =
            _db.Tickets
                .Include(t => t.Category)
                .Include(t => t.CreatedByEmployee)
                .Include(t => t.Status)
                .Include(t => t.Priority)
                .Include(t => t.Approval)
                .Where(t =>
                    t.AssignedToId == null &&
                    t.Approval != null &&
                    t.Approval.Status ==
                    ApprovalStatus.Approved);

        // ========================================================
        // DEPARTMENT HEAD
        //
        // Only own department.
        // ========================================================

        if (User.IsInRole("DepartmentHead"))
        {
            var departmentHeadEmployee =
                await GetCurrentDepartmentHeadEmployeeAsync();

            if (departmentHeadEmployee == null ||
                !departmentHeadEmployee.DepartmentId.HasValue)
            {
                return Forbid();
            }

            var departmentId =
                departmentHeadEmployee.DepartmentId.Value;

            var departmentUserIds =
                await _db.Employees
                    .Where(e =>
                        e.DepartmentId == departmentId &&
                        e.UserId != null)
                    .Select(e => e.UserId!)
                    .ToListAsync();

            query = query.Where(t =>
                departmentUserIds.Contains(
                    t.CreatedById));
        }

        // ========================================================
        // ADMIN
        //
        // No department filter.
        // ========================================================

        var tickets =
            await query
                .OrderByDescending(
                    t => t.CreatedAt)
                .ToListAsync();

        return View(tickets);
    }


    // ============================================================
    // POPULATE DROPDOWNS
    //
    // IMPORTANT:
    // ALL AGENTS are loaded.
    //
    // There is NO department filtering for Agents.
    // ============================================================

    private async Task PopulateDropdownsAsync()
    {
        ViewBag.Categories =
            await _db.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

        ViewBag.Priorities =
            await _db.Priorities
                .OrderBy(p => p.Name)
                .ToListAsync();

        ViewBag.Agents =
            await _userManager
                .GetUsersInRoleAsync("Agent");

        ViewBag.Statuses =
            await _db.Statuses
                .OrderBy(s => s.Name)
                .ToListAsync();
    }


    // ============================================================
    // LEAST LOADED AGENT
    //
    // IMPORTANT:
    //
    // ALL Agents in the organization are considered.
    //
    // Agent Department is NOT checked.
    //
    // Only approved, open, assigned requests count toward
    // workload.
    // ============================================================

    private async Task<string?>
        GetLeastLoadedAgentIdAsync()
    {
        // ========================================================
        // GET ALL AGENTS
        // ========================================================

        var agents =
            await _userManager
                .GetUsersInRoleAsync("Agent");

        if (!agents.Any())
            return null;

        // ========================================================
        // GET APPROVED OPEN TICKETS
        // ========================================================

        var approvedOpenTickets =
            await _db.Tickets
                .Include(t => t.Approval)
                .Include(t => t.Status)
                .Where(t =>
                    t.Approval != null &&
                    t.Approval.Status ==
                    ApprovalStatus.Approved &&

                    t.AssignedToId != null &&

                    t.Status != null &&

                    t.Status.Name != "Closed")
                .Select(t =>
                    t.AssignedToId!)
                .ToListAsync();

        // ========================================================
        // CALCULATE AGENT WORKLOAD
        // ========================================================

        var agentWorkloads =
            agents.Select(agent => new
            {
                AgentId = agent.Id,

                Workload =
                    approvedOpenTickets.Count(
                        assignedId =>
                            assignedId == agent.Id)
            });

        // ========================================================
        // SELECT LEAST LOADED AGENT
        // ========================================================

        return agentWorkloads
            .OrderBy(x => x.Workload)
            .ThenBy(x => x.AgentId)
            .Select(x => x.AgentId)
            .FirstOrDefault();
    }
}