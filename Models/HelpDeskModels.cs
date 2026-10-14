using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System.Linq;

namespace HelpdeskSystem.Models
{
    // ===== ENUMS =====
    public enum AssignmentType
    {
        Manual,
        Automatic
    }

    public enum ApprovalStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    public enum AvailabilityStatus
    {
        Active,
        Inactive
    }

    public enum Gender
    {
        Male,
        Female,
        Other
    }

    public enum NotificationType
    {
        RequestCreated,
        RequestApproved,
        RequestRejected,
        RequestAssigned,
        StatusChanged,
        CommentAdded
    }

    // ===== CATEGORY, PRIORITY, STATUS =====
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Category Name")]
        [RegularExpression(@"^[a-zA-Z\u1200-\u137F ]+$", ErrorMessage = "Category name can contain English or Amharic letters only.")]
        public string Name { get; set; }
    }

    public class Priority
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Priority Name")]
        public string Name { get; set; } // Low, Medium, High, Urgent
    }

    public class Status
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Status Name")]
        public string Name { get; set; } // Open, In Progress, Resolved, Closed
    }

    // ===== REQUEST ENTITY (was Ticket) =====
    public class Ticket
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(200)]
        [Display(Name = "Request Title")]
        public string Title { get; set; }
        [Required]
        [Display(Name = "Request Description")]
        [RegularExpression(@"^[a-zA-Z\u1200-\u137F ]+$", ErrorMessage = "Request Description can contain English or Amharic letters only.")]
        public string Description { get; set; }

        [Display(Name = "Request Category")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [Display(Name = "Urgency")]
        public int PriorityId { get; set; }
        public Priority? Priority { get; set; }

        [Display(Name = "Status")]
        public int StatusId { get; set; }
        public Status? Status { get; set; }

        [Required]
        [Display(Name = "Created By")]
        [RegularExpression(@"^[a-zA-Z\u1200-\u137F ]+$", ErrorMessage = "Created By can contain English or Amharic letters only.")]
        public string CreatedById { get; set; }
        public ApplicationUser? CreatedByEmployee { get; set; }
        
        [Display(Name = "Assigned To")]
        [RegularExpression(@"^[a-zA-Z\u1200-\u137F ]+$", ErrorMessage = "Assigned To can contain English or Amharic letters only.")]
        public string? AssignedToId { get; set; }
        public ApplicationUser? AssignedTo { get; set; }

        [Display(Name = "Assignment Type")]
        public AssignmentType AssignmentType { get; set; } = AssignmentType.Manual;

        [Display(Name = "Request Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Request Updated At")]
        public DateTime? UpdatedAt { get; set; }

        public TicketApproval? Approval { get; set; }
        public ICollection<TicketComment>? Comments { get; set; }
        public ICollection<TicketAttachment>? Attachments { get; set; }
    }

    // ===== REQUEST VIEW MODELS =====
    public class TicketCreateViewModel
    {
        public int Id { get; set; }

        // Requester Info
        [Display(Name = "Requester Name")]
        public string RequesterName { get; set; } = string.Empty;

        [Display(Name = "Requester Email")]
        public string RequesterEmail { get; set; } = string.Empty;

        [Display(Name = "Block")]
        public string RequesterBlock { get; set; } = string.Empty;

        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Department")]
        public string DepartmentName { get; set; } = string.Empty;

        [Display(Name = "Number of Floors")]
        public int NumberOfFloors { get; set; }

        // Request Core Data
        [Required, StringLength(200)]
        [Display(Name = "Request Title")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Request Description")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Request Category")]
        public int CategoryId { get; set; }

        [Required]
        [Display(Name = "Urgency")]
        public int PriorityId { get; set; }

        [Display(Name = "Assigned To")]
        public string? AssignedToId { get; set; }

        [Display(Name = "Assignment Type")]
        public AssignmentType AssignmentType { get; set; } = AssignmentType.Manual;

        [Display(Name = "Attachments")]
        public List<IFormFile>? Attachments { get; set; }

        [Display(Name = "Department Head Approval")]
        public int? ApprovedById { get; set; }

        [Display(Name = "Approval Date")]
        public DateTime? ApprovedAt { get; set; }

        [Display(Name = "Rejection Reason")]
        [StringLength(1000)]
        public string? RejectionReason { get; set; }
    }

    public class TicketApproval
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Request Id")]
        public int TicketId { get; set; }
        public Ticket? Ticket { get; set; }

        [Display(Name = "Approved By")]
        public string? ApprovedById { get; set; }
        public ApplicationUser? ApprovedBy { get; set; }

        [Required]
        [Display(Name = "Approval Status")]
        public ApprovalStatus? Status { get; set; } = ApprovalStatus.Pending;

        [Display(Name = "Rejection Reason")]
        [StringLength(1000)]
        public string? RejectionReason { get; set; }

        [Display(Name = "Decision Date")]
        public DateTime? DecisionDate { get; set; }
    }

    public class TicketComment
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Request Id")]
        public int TicketId { get; set; }
        public Ticket Ticket { get; set; }

        [Required]
        [Display(Name = "Comment Author")]
        public string AuthorId { get; set; }
        public ApplicationUser Author { get; set; }

        [Required]
        [Display(Name = "Comment Body")]
        public string Body { get; set; }

        [Display(Name = "Comment Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class TicketAttachment
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Request Id")]
        public int TicketId { get; set; }
        public Ticket Ticket { get; set; }

        [Required]
        [Display(Name = "File Name")]
        public string FileName { get; set; }

        [Required]
        [Display(Name = "File Path")]
        public string FilePath { get; set; }

        [Display(Name = "File Size")]
        public long Size { get; set; }
    }

    // ===== EMPLOYEE =====
    public class Employee
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "First Name")]
        [RegularExpression(@"^[a-zA-Z\u1200-\u137F ]+$", ErrorMessage = "First name can contain English or Amharic letters only.")]
        public string FirstName { get; set; }

        [Display(Name = "Middle Name")]
        [RegularExpression(@"^[a-zA-Z\u1200-\u137F ]*$", ErrorMessage = "Middle name can contain English or Amharic letters only.")]
        public string? MiddleName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        [RegularExpression(@"^[a-zA-Z\u1200-\u137F ]+$", ErrorMessage = "Last name can contain English or Amharic letters only.")]
        public string LastName { get; set; }

        [Required]
        [Display(Name = "Gender")]
        public Gender Gender { get; set; }

        [Required]
        [Display(Name = "Department")]
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Display(Name = "Block")]
        public int? BlockId { get; set; }
        public Block? Block { get; set; }

        [NotMapped]
        [Display(Name = "Full Name")]
        public string FullName => string.Join(" ", new[] { FirstName, MiddleName, LastName }.Where(n => !string.IsNullOrWhiteSpace(n)));

        [Display(Name = "Is Department Head")]
        public bool IsDepartmentHead { get; set; } = false;

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        [Required]
        [Display(Name = "Availability Status")]
        public AvailabilityStatus AvailabilityStatus { get; set; } = AvailabilityStatus.Active;
    }

    // ===== DEPARTMENT =====
    public class Department
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Department Code")]
        [RegularExpression(@"^[a-zA-Z0-9\u1200-\u137F]+$", ErrorMessage = "Department code can contain letters, numbers, and Amharic letters.")]
        public string DepartmentCode { get; set; }

        [Required]
        [Display(Name = "Department Name")]
        [RegularExpression(@"^[a-zA-Z\u1200-\u137F ]+$", ErrorMessage = "Department name can contain English or Amharic letters only.")]
        public string DepartmentName { get; set; }

        [Display(Name = "Location")]
        [RegularExpression(@"^[a-zA-Z0-9\u1200-\u137F ]*$", ErrorMessage = "Location can contain letters, numbers, Amharic letters, and spaces.")]
        public string Location { get; set; }

        [Display(Name = "Department Head")]
        public int? DepartmentHeadId { get; set; }
        public Employee? DepartmentHead { get; set; }

        public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

        [Required]
        [Display(Name = "Availability Status")]
        public AvailabilityStatus AvailabilityStatus { get; set; } = AvailabilityStatus.Active;
    }

    // ===== BLOCK =====
    public class Block
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Block Name")]
        [RegularExpression(@"^[a-zA-Z\u1200-\u137F ]+$", ErrorMessage = "Block name can contain English or Amharic letters only.")]
        public string BlockName { get; set; }

        [Required]
        [Display(Name = "Block Code")]
        [RegularExpression(@"^[a-zA-Z0-9\u1200-\u137F]+$", ErrorMessage = "Block code can contain letters, numbers, and Amharic letters.")]
        public string BlockCode { get; set; }

        [Display(Name = "Number of Floors")]
        public int NumberOfFloors { get; set; }

        [Display(Name = "Location")]
        [RegularExpression(@"^[a-zA-Z0-9\u1200-\u137F ]*$", ErrorMessage = "Location can contain letters, numbers, Amharic letters, and spaces.")]
        public string Location { get; set; }

        [Display(Name = "Description")]
        [RegularExpression(@"^[a-zA-Z0-9\u1200-\u137F,.!? ]*$", ErrorMessage = "Description can contain letters, numbers, Amharic letters, and punctuation.")]
        public string? Description { get; set; }

        [Display(Name = "Date Created")]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [Display(Name = "Date Modified")]
        public DateTime? DateModified { get; set; }

        public virtual ICollection<Employee>? Employees { get; set; }

        [Required]
        [Display(Name = "Availability Status")]
        public AvailabilityStatus AvailabilityStatus { get; set; } = AvailabilityStatus.Active;
    }

    // ===== NOTIFICATION =====
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "User Id")]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        [Required]
        [Display(Name = "Message")]
        public string Message { get; set; }

        [Display(Name = "Notification Type")]
        public NotificationType Type { get; set; }

        [Display(Name = "Related Request Id")]
        public int? RelatedTicketId { get; set; }

        [Display(Name = "Is Read")]
        public bool IsRead { get; set; } = false;

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ===== EMPLOYEE REGISTER VIEWMODEL =====
    public class EmployeeRegisterViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "First Name")]
        [RegularExpression(@"^[a-zA-Z\u1200-\u137F ]+$", ErrorMessage = "First name can contain English or Amharic letters only.")]
        public string FirstName { get; set; }

        [Display(Name = "Middle Name")]
        [RegularExpression(@"^[a-zA-Z\u1200-\u137F ]*$", ErrorMessage = "Middle name can contain English or Amharic letters only.")]
        public string MiddleName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        [RegularExpression(@"^[a-zA-Z\u1200-\u137F ]+$", ErrorMessage = "Last name can contain English or Amharic letters only.")]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required]
        [Display(Name = "Gender")]
        public Gender Gender { get; set; }

        [Required]
        [Display(Name = "Availability Status")]
        public AvailabilityStatus AvailabilityStatus { get; set; }

        [Required]
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }

        [Display(Name = "Block")]
        public int? BlockId { get; set; }

      
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        public List<Department> Departments { get; set; } = new List<Department>();
        public List<Block> Blocks { get; set; } = new List<Block>();
    }
    public class EmployeeEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "First Name")]
        [RegularExpression(@"^[a-zA-Z\u1200-\u137F ]+$", ErrorMessage = "First name can contain English or Amharic letters only.")]
        public string FirstName { get; set; }

        [Display(Name = "Middle Name")]
        [RegularExpression(@"^[a-zA-Z\u1200-\u137F ]*$", ErrorMessage = "Middle name can contain English or Amharic letters only.")]
        public string? MiddleName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        [RegularExpression(@"^[a-zA-Z\u1200-\u137F ]+$", ErrorMessage = "Last name can contain English or Amharic letters only.")]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required]
        [Display(Name = "Gender")]
        public Gender Gender { get; set; }

        [Required]
        [Display(Name = "Availability Status")]
        public AvailabilityStatus AvailabilityStatus { get; set; }

        [Required]
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }

        [Display(Name = "Block")]
        public int? BlockId { get; set; }


        //[DataType(DataType.Password)]
        //[Display(Name = "Password")]
        //public string Password { get; set; }

        public List<Department> Departments { get; set; } = new List<Department>();
        public List<Block> Blocks { get; set; } = new List<Block>();
    }
    

    // ===== LOGIN, REGISTER, DASHBOARD, PROFILE, CHANGE PASSWORD VIEWMODELS =====
    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; }
    }

    public class AdminDashboardViewModel
    {
        [Display(Name = "Total Users")]
        public int UserCount { get; set; }

        [Display(Name = "Total Requests")]
        public int TicketCount { get; set; }

        [Display(Name = "Open Requests")]
        public int OpenTickets { get; set; }

        [Display(Name = "Pending Requests")]
        public int PendingTickets { get; set; }

        [Display(Name = "Closed Requests")]
        public int ClosedTickets { get; set; }

        [Display(Name = "Agents")]
        public IEnumerable<ApplicationUser> Agents { get; set; }
    }

    public class UserWithRoleViewModel
    {
        [Display(Name = "User Id")]
        public string Id { get; set; }

        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Display(Name = "Username")]
        public string UserName { get; set; }

        [Display(Name = "Role")]
        public string Role { get; set; }

        [Display(Name = "Availability Status")]
        public AvailabilityStatus AvailabilityStatus { get; set; }
    }
    public class EditRoleViewModel
    {
        public string Id { get; set; }


        [Required]
        [Display(Name="Role Name")]
        public string RoleName { get; set; }
    }
    public class ProfileViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Full Name")]
        [StringLength(100, MinimumLength = 2)]
        public string FullName { get; set; }

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "First Name")]
        public string? FirstName { get; set; }

        [Display(Name = "Middle Name")]
        public string? MiddleName { get; set; }

        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        [Display(Name = "Gender")]
        public Gender? Gender { get; set; }

        [Display(Name = "Department Id")]
        public int? DepartmentId { get; set; }

        [Display(Name = "Department Name")]
        public string? DepartmentName { get; set; }

        [Display(Name = "Block Id")]
        public int? BlockId { get; set; }

        [Display(Name = "Block Name")]
        public string? BlockName { get; set; }

        public string? NumberOfFloors { get; set; }

        // ✅ Add these for dropdowns
        public IEnumerable<Department>? Departments { get; set; }
        public IEnumerable<Block>? Blocks { get; set; }
    }


    public class OtpLoginViewModel
    {
        [Required]
        public string UsernameOrEmail { get; set; }

        [Required]
        public string Otp { get; set; }
    }
    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string OldPassword { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        [Display(Name = "Confirm New Password")]
        public string ConfirmPassword { get; set; }
    }
}
