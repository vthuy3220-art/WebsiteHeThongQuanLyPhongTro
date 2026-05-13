using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class NguoiOHopDongsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NguoiOHopDongsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Danh sách
        public async Task<IActionResult> Index()
        {
            var nguoiO = await _context.NguoiOHopDong
                .Include(n => n.HopDongNavigation)
                .ThenInclude(h => h.PhongNavigation)
                .ToListAsync();
            return View(nguoiO);
        }

        // Chi tiết
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var nguoiO = await _context.NguoiOHopDong
                .Include(n => n.HopDongNavigation)
                .FirstOrDefaultAsync(m => m.MaNguoiO == id);

            if (nguoiO == null) return NotFound();
            return View(nguoiO);
        }

        // Thêm mới - GET
        public IActionResult Create()
        {
            ViewBag.MaHopDong = new SelectList(_context.HopDong, "MaHopDong", "MaHopDong");
            return View();
        }

        // Thêm mới - POST
        // Trong Create POST, Bind phải bao gồm LaNguoiDaiDien
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaHopDong,HoTen,CCCD,SoDienThoai,LaNguoiDaiDien")] NguoiOHopDong nguoiO)
        {
            if (ModelState.IsValid)
            {
                _context.Add(nguoiO);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm thành công!";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.MaHopDong = new SelectList(_context.HopDong, "MaHopDong", "MaHopDong", nguoiO.MaHopDong);
            return View(nguoiO);
        }

        // Chỉnh sửa - GET
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var nguoiO = await _context.NguoiOHopDong.FindAsync(id);
            if (nguoiO == null) return NotFound();

            ViewBag.MaHopDong = new SelectList(_context.HopDong, "MaHopDong", "MaHopDong", nguoiO.MaHopDong);
            return View(nguoiO);
        }

        // Chỉnh sửa - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaNguoiO,MaHopDong,HoTen,CCCD,SoDienThoai,LaNguoiDaiDien")] NguoiOHopDong nguoiO)
        {
            if (id != nguoiO.MaNguoiO) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(nguoiO);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.NguoiOHopDong.Any(e => e.MaNguoiO == id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.MaHopDong = new SelectList(_context.HopDong, "MaHopDong", "MaHopDong", nguoiO.MaHopDong);
            return View(nguoiO);
        }

        // Xóa - GET
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var nguoiO = await _context.NguoiOHopDong
                .Include(n => n.HopDongNavigation)
                .FirstOrDefaultAsync(m => m.MaNguoiO == id);

            if (nguoiO == null) return NotFound();
            return View(nguoiO);
        }


        // Xóa - POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var nguoiO = await _context.NguoiOHopDong.FindAsync(id);
            if (nguoiO != null)
            {
                _context.NguoiOHopDong.Remove(nguoiO);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa thành công!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}