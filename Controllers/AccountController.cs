using System.Security.Claims;
using HelpdeskSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly HelpDeskContext _db;
        public AccountController(
      UserManager<ApplicationUser> userManager,
      SignInManager<ApplicationUser> signInManager,
      HelpDeskContext db) // ✅ Add this
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db; // ✅ Save it to the private field
        }

        [AllowAnonymous]
        public IActionResult AccessDenied(string returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }
        // ===============================
        // GET: /Account/Login
        // ===============================
        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // ===============================
        // POST: /Account/Login
        // ===============================
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(model);

            // 1️⃣ Check if user exists
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                TempData["ErrorMessage"] = "Incorrect Username.";
                return RedirectToAction("Login");
            }

            // 2️⃣ Validate password
            var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);

            if (!passwordValid)
            {
                TempData["ErrorMessage"] = "Incorrect Password.";
                return RedirectToAction("Login");
            }

            // 3️⃣ Sign in user
            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false
            );

            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(returnUrl))
                    return Redirect(returnUrl);
                TempData["SuccessMessage"] = "Login successful.";
                return RedirectToAction("Index", "Home");
            }

            TempData["ErrorMessage"] = "Incorrect username or password.";
            return RedirectToAction("Login");
        }

        // ===============================
        // GET: /Account/Register
        // ===============================
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // ===============================
        // POST: /Account/Register
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);

                // Clear the form fields
                ModelState.Clear();
                return View(new RegisterViewModel()); // empty model
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }
        private async Task LoadProfileDropdowns(ProfileViewModel model)
        {
            model.Departments = await _db.Departments
                .Where(d => d.AvailabilityStatus == AvailabilityStatus.Active)
                .ToListAsync();

            model.Blocks = await _db.Blocks
                .Where(b => b.AvailabilityStatus == AvailabilityStatus.Active)
                .ToListAsync();
        }
        // ===============================
        // POST: /Account/Logout
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            //TempData["SuccessMessage"] = "Signed out successfully.";
            return RedirectToAction("Login", "Account");
        }
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return NotFound();


            var employee = await _db.Employees
                .Include(e => e.Department)
                .Include(e => e.Block)
                .FirstOrDefaultAsync(e => e.UserId == user.Id);


            var model = new ProfileViewModel
            {
                Email = user.Email,
                FullName = user.FullName,

                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName,

                PhoneNumber = user.PhoneNumber,

                Gender = employee?.Gender,

                DepartmentId = employee?.DepartmentId,
                BlockId = employee?.BlockId,

                DepartmentName = employee?.Department?.DepartmentName,
                BlockName = employee?.Block?.BlockName,

                NumberOfFloors =
                    employee?.Block?.NumberOfFloors.ToString()
            };


            await LoadProfileDropdowns(model);


            return View(model);
        }

        // ===============================
        // POST: /Account/Profile
        // ===============================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadProfileDropdowns(model);
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound();

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.UserId == user.Id);

            // ============================
            // UPDATE IDENTITY USER
            // ============================
            user.FirstName = model.FirstName;
            user.MiddleName = model.MiddleName;
            user.LastName = model.LastName;
            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;

            if (user.Email != model.Email)
            {
                user.UserName = model.Email;

                var emailResult = await _userManager.SetEmailAsync(user, model.Email);
                if (!emailResult.Succeeded)
                {
                    foreach (var error in emailResult.Errors)
                        ModelState.AddModelError("", error.Description);
                    return View(model);
                }
            }

            var updateUserResult = await _userManager.UpdateAsync(user);
            if (!updateUserResult.Succeeded)
            {
                foreach (var error in updateUserResult.Errors)
                    ModelState.AddModelError("", error.Description);
                await LoadProfileDropdowns(model);
                return View(model);
            }

            // ============================
            // UPDATE EMPLOYEE (IF EXISTS)
            // ============================
            if (employee != null)
            {
                employee.Gender = model.Gender ?? employee.Gender;
                employee.DepartmentId = model.DepartmentId ?? employee.DepartmentId;
                employee.BlockId = model.BlockId ?? employee.BlockId;
                employee.PhoneNumber = model.PhoneNumber;

                _db.Employees.Update(employee);
                await _db.SaveChangesAsync();
            }

            await _signInManager.RefreshSignInAsync(user);

            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction(nameof(Profile));
        }

        //[AllowAnonymous]
        //public IActionResult LoginWithOtp() => View();

        //[HttpPost]
        //[AllowAnonymous]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> LoginWithOtp(OtpLoginViewModel model)
        //{
        //    if (!ModelState.IsValid)
        //        return View(model);

        //    ApplicationUser user =
        //        model.UsernameOrEmail.Contains("@")
        //        ? await _userManager.FindByEmailAsync(model.UsernameOrEmail)
        //        : await _userManager.FindByNameAsync(model.UsernameOrEmail);

        //    if (user == null)
        //    {
        //        ModelState.AddModelError("", "Invalid username or OTP.");
        //        return View(model);
        //    }

        //    var otp = await _db.PasswordResetOtps
        //        .Where(o =>
        //            o.UserId == user.Id &&
        //            !o.IsUsed &&
        //            o.ExpireAt > DateTime.UtcNow)
        //        .OrderByDescending(o => o.ExpireAt)
        //        .FirstOrDefaultAsync();

        //    if (otp == null || otp.Code != model.Otp)
        //    {
        //        ModelState.AddModelError("", "Invalid or expired OTP.");
        //        return View(model);
        //    }

        //    otp.IsUsed = true;
        //    await _db.SaveChangesAsync();

        //    // 🔥 HARD RESET AUTH
        //    await _signInManager.SignOutAsync();
        //    await _userManager.UpdateSecurityStampAsync(user);

        //    var claims = new List<Claim>
        //{
        //    new Claim("ForcePasswordChange", "true")
        //};

        //    await _signInManager.SignInWithClaimsAsync(user, false, claims);

        //    return RedirectToAction(nameof(ChangePasswordAfterOtp));
        //}


        [Authorize]
        public IActionResult ChangePasswordAfterOtp()
        {
            if (!User.HasClaim("ForcePasswordChange", "true"))
                return RedirectToAction("Index", "Home");

            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePasswordAfterOtp(string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match.");
                return View();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                return View();
            }

            // 🔥 INVALIDATE ALL OLD PASSWORDS & SESSIONS
            await _userManager.UpdateSecurityStampAsync(user);
            await _signInManager.SignOutAsync();
            await _signInManager.SignInAsync(user, false);

            return RedirectToAction("Index", "Home");
        }

        // ===============================
        // GET: /Account/ChangePassword
        // ===============================
        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }

        // ===============================
        // POST: /Account/ChangePassword
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                foreach (var error in changePasswordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            await _signInManager.RefreshSignInAsync(user);

            TempData["SuccessMessage"] = "Your password has been changed successfully!";
            return RedirectToAction(nameof(Profile));
        }
    }
}
