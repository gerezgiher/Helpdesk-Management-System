using HelpdeskSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HelpdeskSystem.Controllers
{
    [Authorize]
    public class CommentsController : Controller
    {
        private readonly HelpDeskContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        public CommentsController(HelpDeskContext db, UserManager<ApplicationUser> um) { _db = db; _userManager = um; }

        [HttpPost]
        public async Task<IActionResult> Create(int ticketId, string body)
        {
            var user = await _userManager.GetUserAsync(User);
            var comment = new TicketComment { TicketId = ticketId, Body = body, AuthorId = user.Id };
            _db.TicketComments.Add(comment);
            await _db.SaveChangesAsync();
            // return partial HTML to append
            return PartialView("_Comment", comment);
        }

    }

}
