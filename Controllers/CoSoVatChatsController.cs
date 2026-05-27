using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
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

        // GET: Danh sách CSVC
        public async Task<IActionResult> Index()
        {
            var csvcList = await _context.CoSoVatChat
                .Include(c => c.PhongNavigation)
                .ToListAsync();
            return View(csvcList);
        }

        // GET: Tạo mới
        public IActionResult Create()
        {
            ViewBag.PhongList = _context.Phong.ToList();
            return View();
        }

        // POST: Tạo mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int MaPhong, string TenThietBi, int SoLuong, string TinhTrang)
        {
            var csvc = new CoSoVatChat
            {
                MaPhong = MaPhong,
                TenThietBi = TenThietBi,
                SoLuong = SoLuong,
                TinhTrang = TinhTrang ?? "Tốt"
            };
            _context.CoSoVatChat.Add(csvc);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã thêm {TenThietBi} vào phòng!";
            return RedirectToAction(nameof(Index));
        }
        // GET: Chi tiết CSVC
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var csvc = await _context.CoSoVatChat
                .Include(c => c.PhongNavigation)
                .FirstOrDefaultAsync(m => m.MaCSVC == id);

            if (csvc == null) return NotFound();

            return View(csvc);
        }
        // GET: Chỉnh sửa CSVC
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var csvc = await _context.CoSoVatChat.FindAsync(id);
            if (csvc == null) return NotFound();

            // Lấy danh sách phòng để hiển thị trong dropdown
            ViewBag.PhongList = await _context.Phong.ToListAsync();
            return View(csvc);
        }

        // POST: Chỉnh sửa CSVC
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, int MaPhong, string TenThietBi, int SoLuong, string TinhTrang)
        {
            var csvc = await _context.CoSoVatChat.FindAsync(id);
            if (csvc == null) return NotFound();

            csvc.MaPhong = MaPhong;
            csvc.TenThietBi = TenThietBi;
            csvc.SoLuong = SoLuong;
            csvc.TinhTrang = TinhTrang;

            _context.Update(csvc);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật thành công!";
            return RedirectToAction(nameof(Index));
        }
        // GET: Xóa
        public async Task<IActionResult> Delete(int id)
        {
            var csvc = await _context.CoSoVatChat.FindAsync(id);
            if (csvc != null)
            {
                _context.CoSoVatChat.Remove(csvc);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa thành công!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}