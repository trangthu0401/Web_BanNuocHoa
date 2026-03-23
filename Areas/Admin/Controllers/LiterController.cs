using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using PerfumeStore.Areas.Admin.Filters;
using PerfumeStore.Areas.Admin.Models;
using PerfumeStore.Areas.Admin.Services;
using System.Threading.Tasks;

namespace PerfumeStore.Areas.Admin.Controllers
{
    [Area("Admin")]
    [AdminAuthorize]

    public class LiterController : Controller
    {
        public readonly PerfumeStoreContext _context;
        private readonly IPaginationService _paginationService;

        public LiterController(PerfumeStoreContext context, IPaginationService paginationService)
        {
            _context = context;
            _paginationService = paginationService;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            var litersQuery = _context.Liters.OrderBy(l => l.LiterId).AsQueryable();
            var pagedResult = await _paginationService.PaginateAsync(litersQuery, page, 10);
            return View(pagedResult);
        }

        public async Task<IActionResult> Create()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Create(Liter lt)
        {
            if (!ModelState.IsValid)
            {

                return View(lt);
            }

            var literNumberExists = await _context.Liters
                .AnyAsync(x => x.LiterNumber == lt.LiterNumber);
            var exists = await _context.Liters.AnyAsync(x => x.LiterNumber == lt.LiterNumber);
            if (exists)
            {
                ModelState.AddModelError("LiterNumber", "Dung tích này đã tồn tại!");
                return View(lt);
            }
            await _context.Liters.AddAsync(lt);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");

        }

        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var liter = await _context.Liters.FirstOrDefaultAsync(x => x.LiterId == id);
            if (liter == null)
                return NotFound();

            return View(liter);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Liter lt)
        {
            if (id != lt.LiterId)
                return NotFound();

            if (!ModelState.IsValid)
                return View(lt);

            var exists = await _context.Liters
                .AnyAsync(x => x.LiterNumber == lt.LiterNumber && x.LiterId != lt.LiterId);

            if (exists)
            {
                ModelState.AddModelError("LiterNumber", "Dung tích này đã tồn tại!");
                return View(lt);
            }

            try
            {
                _context.Liters.Update(lt);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Liters.AnyAsync(e => e.LiterId == id))
                    return NotFound();
                else
                    throw;
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var liter = await _context.Liters.FindAsync(id);
            if (liter == null)
                return NotFound();

            return View(liter);
        }

        // POST: Admin/Liter/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var liter = await _context.Liters.FindAsync(id);
            if (liter == null)
                return NotFound();

            // Prevent delete if any product is linked to this liter (EqualLiter join table)
            var isInUse = await _context.Products.AnyAsync(p => p.Liters.Any(l => l.LiterId == id));
            if (isInUse)
            {
                TempData["Error"] = "Không thể xóa vì dung tích đang được sử dụng bởi sản phẩm. Hãy gỡ liên kết trước.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            _context.Liters.Remove(liter);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
