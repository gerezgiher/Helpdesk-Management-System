using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HelpdeskSystem.Models;
using HelpdeskSystem.Data;

namespace HelpdeskSystem.Controllers
{
    public class DepartmentsController : Controller
    {
        private readonly HelpDeskContext _context;

        public DepartmentsController(HelpDeskContext context)
        {
            _context = context;
        }

        // GET: Departments
        public async Task<IActionResult> Index()
        {
            var departments = await _context.Departments.ToListAsync();
            return View(departments ?? new List<Department>());
        }

        // GET: Departments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Department ID not provided.";
                return RedirectToAction(nameof(Index));
            }

            var department = await _context.Departments
                .Include(d => d.Employees)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
            {
                TempData["ErrorMessage"] = "Department not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(department);
        }

        // GET: Departments/Create
        public IActionResult Create()
        {
            ViewBag.AvailabilityStatus = new SelectList(Enum.GetValues(typeof(AvailabilityStatus)));
            return View();
        }

        // POST: Departments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("DepartmentCode,DepartmentName,Location,AvailabilityStatus")] Department department)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(department);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Department '{department.DepartmentName}' created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "An error occurred while creating the department.";
                }
            }

            ViewBag.AvailabilityStatus = new SelectList(Enum.GetValues(typeof(AvailabilityStatus)), department.AvailabilityStatus);
            return View(department);
        }

        // GET: Departments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Department ID not provided.";
                return RedirectToAction(nameof(Index));
            }

            var department = await _context.Departments.FindAsync(id);
            if (department == null)
            {
                TempData["ErrorMessage"] = "Department not found.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.AvailabilityStatus = new SelectList(Enum.GetValues(typeof(AvailabilityStatus)), department.AvailabilityStatus);
            return View(department);
        }

        // POST: Departments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,DepartmentCode,DepartmentName,Location,AvailabilityStatus")] Department department)
        {
            if (id != department.Id)
            {
                TempData["ErrorMessage"] = "Department ID mismatch.";
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(department);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Department '{department.DepartmentName}' updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DepartmentExists(department.Id))
                        TempData["ErrorMessage"] = "Department not found.";
                    else
                        throw;
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "An error occurred while updating the department.";
                }
            }

            ViewBag.AvailabilityStatus = new SelectList(Enum.GetValues(typeof(AvailabilityStatus)), department.AvailabilityStatus);
            return View(department);
        }

        // POST: Departments/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var department = await _context.Departments
                .Include(d => d.Employees)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
            {
                TempData["ErrorMessage"] = "Department not found.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                department.AvailabilityStatus =
                    department.AvailabilityStatus == AvailabilityStatus.Active
                    ? AvailabilityStatus.Inactive
                    : AvailabilityStatus.Active;

                _context.Update(department);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    $"Department '{department.DepartmentName}' is now {department.AvailabilityStatus}.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the department status.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Departments/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Department ID not provided.";
                return RedirectToAction(nameof(Index));
            }

            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null)
            {
                TempData["ErrorMessage"] = "Department not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(department);
        }

        // POST: Departments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department != null)
            {
                try
                {
                    _context.Departments.Remove(department);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Department '{department.DepartmentName}' deleted successfully.";
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the department.";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Department not found.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool DepartmentExists(int id)
        {
            return _context.Departments.Any(e => e.Id == id);
        }
    }
}
