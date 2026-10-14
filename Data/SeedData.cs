namespace HelpdeskSystem.Data
{
    using HelpdeskSystem.Models;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;

    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<HelpDeskContext>();

            // Apply migrations
            await db.Database.MigrateAsync();

            // =====================
            // Roles
            // =====================
            string[] roles = new[]
            {
                "Admin",
                "Agent",
                "DepartmentHead",
                "User"
            };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // =====================
            // Admin User
            // =====================
            var adminEmail = "admin@helpdesk.local";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,

                    FirstName = "System",
                    MiddleName = "",
                    LastName = "Admin",

                    FullName = "System Admin"
                };

                await userManager.CreateAsync(admin, "Admin123!");
                await userManager.AddToRoleAsync(admin, "Admin");
            }

            // =====================
            // Department Head User (Agent type)
            // =====================
            var deptHeadEmail = "depthead@helpdesk.local";
            if (await userManager.FindByEmailAsync(deptHeadEmail) == null)
            {
                var deptHead = new ApplicationUser
                {
                    UserName = deptHeadEmail,
                    Email = deptHeadEmail,

                    FirstName = "Department",
                    MiddleName = "",
                    LastName = "Head",

                    FullName = "Department Head"
                };

                await userManager.CreateAsync(deptHead, "DeptHead123!");
                await userManager.AddToRoleAsync(deptHead, "DepartmentHead");
            }

            // =====================
            // Lookup Data
            // =====================
            if (!db.Categories.Any())
            {
                db.Categories.AddRange(
                    new Category { Name = "Software" },
                    new Category { Name = "Hardware" },
                    new Category { Name = "Network" }
                );
            }

            if (!db.Priorities.Any())
            {
                db.Priorities.AddRange(
                    new Priority { Name = "Low" },
                    new Priority { Name = "Medium" },
                    new Priority { Name = "High" },
                    new Priority { Name = "Urgent" }
                );
            }

            if (!db.Statuses.Any())
            {
                db.Statuses.AddRange(
                    new Status { Name = "Pending" },
                    new Status { Name = "In Progress" },
                    new Status { Name = "Resolved" },
                    new Status { Name = "Rejected" }
                );
            }

            await db.SaveChangesAsync();
        }
    }
}
