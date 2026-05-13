using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class CoSoVatChatsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CoSoVatChatsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==================== DANH SÁCH ====================
        public async Task<IActionResult> Index()
        {
            var danhSach = await _context.CoSoVatChat
                .Include(c => c.PhongNavigation)
                .ThenInclude(p => p.CoSo)
                .ToListAsync();
            return View(danhSach);
        }

        // ==================== THÊM MỚI ====================
        public IActionResult Create()
        {
            ViewBag.MaPhong = new SelectList(_context.Phong.Include(p => p.CoSo), "MaPhong", "TenPhong");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaPhong,TenThietBi,SoLuong,TinhTrang")] CoSoVatChat coSoVatChat)
        {
            if (ModelState.IsValid)
            {
                _context.Add(coSoVatChat);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm thiết bị thành công!";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.MaPhong = new SelectList(_context.Phong, "MaPhong", "TenPhong", coSoVatChat.MaPhong);
            return View(coSoVatChat);
        }

        // ==================== CHỈNH SỬA ====================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var coSoVatChat = await _context.CoSoVatChat.FindAsync(id);
            if (coSoVatChat == null) return NotFound();

            ViewBag.MaPhong = new SelectList(_context.Phong, "MaPhong", "TenPhong", coSoVatChat.MaPhong);
            return View(coSoVatChat);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaCSVC,MaPhong,TenThietBi,SoLuong,TinhTrang")] CoSoVatChat coSoVatChat)
        {
            if (id != coSoVatChat.MaCSVC) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(coSoVatChat);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.CoSoVatChat.Any(e => e.MaCSVC == id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.MaPhong = new SelectList(_context.Phong, "MaPhong", "TenPhong", coSoVatChat.MaPhong);
            return View(coSoVatChat);
        }

        // ==================== XÓA ====================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var coSoVatChat = await _context.CoSoVatChat
                .Include(c => c.PhongNavigation)
                .FirstOrDefaultAsync(m => m.MaCSVC == id);
            if (coSoVatChat == null) return NotFound();

            return View(coSoVatChat);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var coSoVatChat = await _context.CoSoVatChat.FindAsync(id);
            if (coSoVatChat != null)
            {
                _context.CoSoVatChat.Remove(coSoVatChat);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa thiết bị thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        // ==================== CHI TIẾT ====================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var coSoVatChat = await _context.CoSoVatChat
                .Include(c => c.PhongNavigation)
                .ThenInclude(p => p.CoSo)
                .FirstOrDefaultAsync(m => m.MaCSVC == id);
            if (coSoVatChat == null) return NotFound();

            return View(coSoVatChat);
        }
    }
}