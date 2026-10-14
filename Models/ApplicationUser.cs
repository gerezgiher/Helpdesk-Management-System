using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HelpdeskSystem.Models
{
    using Microsoft.AspNetCore.Identity;

    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string MiddleName { get; set; }
        public AvailabilityStatus AvailabilityStatus { get; set; } = AvailabilityStatus.Active;
        // Writable FullName
        [NotMapped]
        public string FullName
        {
            get
            {
                return string.Join(" ",
                    new[] { FirstName, MiddleName, LastName }
                    .Where(n => !string.IsNullOrWhiteSpace(n)));
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;

                var names = value
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);

                FirstName = names.Length > 0 ? names[0] : null;
                MiddleName = names.Length > 2 ? names[1] : null;
                LastName = names.Length > 1
                    ? string.Join(" ", names.Skip(2))
                    : null;
            }
        }
    }
    public class HomeDashboardViewModel
    {
        // Common
        public string Role { get; set; }


        // ================= ADMIN =================
        public int TotalEmployees { get; set; }
        public int TotalDepartments { get; set; }
        public int TotalBlocks { get; set; }


        // ================= REQUEST STATISTICS =================
        public int TotalRequests { get; set; }
        public int PendingRequests { get; set; }
        public int InProgressRequests { get; set; }
        public int CanceledRequests { get; set; }
        public int ResolvedRequests { get; set; }
        public int RejectedRequests { get; set; }
        public int ApprovedRequests { get; set; }


        // ================= AGENTS =================
        public int TotalActiveAgents { get; set; }
        public int TotalInactiveAgents { get; set; }


        // ================= DEPARTMENT HEAD =================
        public List<Ticket> Tickets { get; set; } = new List<Ticket>();


        // ================= APPROVAL =================
        public List<TicketApproval> TicketApproval { get; set; }
            = new List<TicketApproval>();


        // ================= ADMIN USER LIST =================
        public List<ApplicationUser> Employees { get; set; }
            = new List<ApplicationUser>();


        // ================= AGENT LIST =================
        public List<ApplicationUser> Agents { get; set; }
            = new List<ApplicationUser>();
    }
    public class AssignAgentViewModel
    {
        public List<ApplicationUser> Users { get; set; }
        public IList<ApplicationUser> Agents { get; set; }
        public AvailabilityStatus AvailabilityStatus { get; set; }
    }

}