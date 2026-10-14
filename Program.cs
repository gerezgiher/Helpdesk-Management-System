using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using HelpdeskSystem.Data;
using HelpdeskSystem.Models;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// DATABASE
// ============================================================

builder.Services.AddDbContext<HelpDeskContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ============================================================
// IDENTITY
// ============================================================

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<HelpDeskContext>()
.AddDefaultTokenProviders();

// ============================================================
// MVC
// ============================================================

builder.Services.AddControllersWithViews();

// ============================================================
// COOKIE / LOGIN
// ============================================================

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
});

// ============================================================
// BUILD
// ============================================================

var app = builder.Build();

// ============================================================
// SEED DATA
// ============================================================

using (var scope = app.Services.CreateScope())
{
    await SeedData.Initialize(scope.ServiceProvider);
}

// ============================================================
// MIDDLEWARE
// ============================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

// ============================================================
// DEFAULT ROUTE
// ============================================================



app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();