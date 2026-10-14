using HelpdeskSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

public class HelpDeskContext : IdentityDbContext<ApplicationUser>
{
    public HelpDeskContext(DbContextOptions<HelpDeskContext> options) : base(options) { }

    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<TicketComment> TicketComments { get; set; }
    public DbSet<TicketAttachment> TicketAttachments { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<TicketApproval> TicketApprovals { get; set; }
    public DbSet<Priority> Priorities { get; set; }
    public DbSet<Status> Statuses { get; set; }
    public DbSet<Employee> Employees { get; set; }  // << Add this
    public DbSet<Block> Blocks { get; set; }
    public DbSet<Department> Departments { get; set; }  // << Add this
    public DbSet<Notification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Ticket → CreatedBy
        builder.Entity<Ticket>()
            .HasOne(t => t.CreatedByEmployee)
            .WithMany()
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Ticket → AssignedTo
        builder.Entity<Ticket>()
            .HasOne(t => t.AssignedTo)
            .WithMany()
            .HasForeignKey(t => t.AssignedToId)
            .OnDelete(DeleteBehavior.Restrict);

        // Employee → Block
        builder.Entity<Employee>()
            .HasOne(e => e.Block)
            .WithMany(b => b.Employees)
            .HasForeignKey(e => e.BlockId)
            .OnDelete(DeleteBehavior.Restrict);

        // 🔴 ADD THIS: Employee → Department (Many Employees, One Department)
        builder.Entity<Employee>()
            .HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
         builder.Entity<Ticket>()
         .HasOne(t => t.Approval)
       .WithOne(a => a.Ticket)
        .HasForeignKey<TicketApproval>(a => a.TicketId)
         .OnDelete(DeleteBehavior.Cascade);
        // 🔴 ADD THIS: Department → DepartmentHead (One Department, One Head)
        builder.Entity<Department>()
            .HasOne(d => d.DepartmentHead)
            .WithMany() // no navigation on Employee
            .HasForeignKey(d => d.DepartmentHeadId)
            .OnDelete(DeleteBehavior.Restrict);
    }

}
