using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HelpdeskSystem.Models;

namespace HelpdeskSystem.Controllers
{
    public class BlocksController : Controller
    {
        private readonly HelpDeskContext _context;

        public BlocksController(HelpDeskContext context)
        {
            _context = context;
        }

        // GET: Blocks
        public async Task<IActionResult> Index()
        {
            var blocks = await _context.Blocks.ToListAsync();
            return View(blocks ?? new List<Block>());
        }

        // GET: Blocks/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Block ID not provided.";
                return RedirectToAction(nameof(Index));
            }

            var block = await _context.Blocks.FirstOrDefaultAsync(b => b.Id == id);
            if (block == null)
            {
                TempData["ErrorMessage"] = "Block not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(block);
        }

        // GET: Blocks/Create
        public IActionResult Create()
        {
            ViewBag.AvailabilityStatus = new SelectList(Enum.GetValues(typeof(AvailabilityStatus)));
            return View();
        }

        // POST: Blocks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BlockName,BlockCode,NumberOfFloors,Location,Description,AvailabilityStatus")] Block block)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    block.DateCreated = DateTime.UtcNow;
                    _context.Add(block);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Block '{block.BlockName}' created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "An error occurred while creating the block.";
                }
            }

            ViewBag.AvailabilityStatus = new SelectList(Enum.GetValues(typeof(AvailabilityStatus)), block.AvailabilityStatus);
            return View(block);
        }

        // GET: Blocks/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Block ID not provided.";
                return RedirectToAction(nameof(Index));
            }

            var block = await _context.Blocks.FindAsync(id);
            if (block == null)
            {
                TempData["ErrorMessage"] = "Block not found.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.AvailabilityStatus = new SelectList(Enum.GetValues(typeof(AvailabilityStatus)), block.AvailabilityStatus);
            return View(block);
        }

        // POST: Blocks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,BlockName,BlockCode,NumberOfFloors,Location,Description,AvailabilityStatus")] Block block)
        {
            if (id != block.Id)
            {
                TempData["ErrorMessage"] = "Block ID mismatch.";
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    block.DateModified = DateTime.UtcNow;
                    _context.Update(block);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Block '{block.BlockName}' updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BlockExists(block.Id))
                        TempData["ErrorMessage"] = "Block not found.";
                    else
                        throw;
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "An error occurred while updating the block.";
                }
            }

            ViewBag.AvailabilityStatus = new SelectList(Enum.GetValues(typeof(AvailabilityStatus)), block.AvailabilityStatus);
            return View(block);
        }

        // POST: Blocks/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var block = await _context.Blocks.FindAsync(id);
            if (block == null)
            {
                TempData["ErrorMessage"] = "Block not found.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                block.AvailabilityStatus =
                    block.AvailabilityStatus == AvailabilityStatus.Active
                    ? AvailabilityStatus.Inactive
                    : AvailabilityStatus.Active;

                block.DateModified = DateTime.UtcNow;

                _context.Update(block);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    $"Block '{block.BlockName}' is now {block.AvailabilityStatus}.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the block status.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Blocks/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Block ID not provided.";
                return RedirectToAction(nameof(Index));
            }

            var block = await _context.Blocks.FirstOrDefaultAsync(b => b.Id == id);
            if (block == null)
            {
                TempData["ErrorMessage"] = "Block not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(block);
        }

        // POST: Blocks/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var block = await _context.Blocks.FindAsync(id);
            if (block != null)
            {
                try
                {
                    _context.Blocks.Remove(block);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Block '{block.BlockName}' deleted successfully.";
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "An error occurred while deleting the block.";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Block not found.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool BlockExists(int id)
        {
            return _context.Blocks.Any(e => e.Id == id);
        }
    }
}
