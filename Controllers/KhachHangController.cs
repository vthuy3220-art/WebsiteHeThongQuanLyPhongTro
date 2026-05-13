using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class KhachHangController : Controller
    {
        private readonly ApplicationDbContext _context;

        public KhachHangController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Danh sách khách hàng
        public async Task<IActionResult> Index(string searchString)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var khachHangs = _context.KhachHang.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                khachHangs = khachHangs.Where(k =>
                    k.HoTen.Contains(searchString) ||
                    (k.SoDienThoai != null && k.SoDienThoai.Contains(searchString)) ||
                    (k.CCCD != null && k.CCCD.Contains(searchString)));
            }

            ViewBag.SearchString = searchString;
            return View(await khachHangs.ToListAsync());
        }

        // GET: Chi tiết khách hàng
        public async Task<IActionResult> Details(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(m => m.MaKhachHang == id);

            if (khachHang == null)
            {
                return NotFound();
            }

            // Lấy danh sách hợp đồng của khách
            var hopDongs = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Where(h => h.MaKhachHang == id)
                .OrderByDescending(h => h.NgayBatDau)
                .ToListAsync();

            ViewBag.HopDongs = hopDongs;
            return View(khachHang);
        }

        // GET: Thêm khách hàng mới
        public IActionResult Create()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }
            return View();
        }

        // POST: Thêm khách hàng mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(KhachHang khachHang)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra CCCD trùng
                if (!string.IsNullOrEmpty(khachHang.CCCD))
                {
                    var exists = await _context.KhachHang
                        .AnyAsync(k => k.CCCD == khachHang.CCCD);
                    if (exists)
                    {
                        ModelState.AddModelError("CCCD", "Số CCCD đã tồn tại!");
                        return View(khachHang);
                    }
                }

                // Kiểm tra SĐT trùng
                if (!string.IsNullOrEmpty(khachHang.SoDienThoai))
                {
                    var exists = await _context.KhachHang
                        .AnyAsync(k => k.SoDienThoai == khachHang.SoDienThoai);
                    if (exists)
                    {
                        ModelState.AddModelError("SoDienThoai", "Số điện thoại đã tồn tại!");
                        return View(khachHang);
                    }
                }

                _context.Add(khachHang);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm khách hàng thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(khachHang);
        }

        // GET: Chỉnh sửa khách hàng
        public async Task<IActionResult> Edit(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            var khachHang = await _context.KhachHang.FindAsync(id);
            if (khachHang == null)
            {
                return NotFound();
            }
            return View(khachHang);
        }

        // POST: Chỉnh sửa khách hàng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, KhachHang khachHang)
        {
            if (id != khachHang.MaKhachHang)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Kiểm tra CCCD trùng (trừ chính nó)
                    if (!string.IsNullOrEmpty(khachHang.CCCD))
                    {
                        var exists = await _context.KhachHang
                            .AnyAsync(k => k.CCCD == khachHang.CCCD && k.MaKhachHang != id);
                        if (exists)
                        {
                            ModelState.AddModelError("CCCD", "Số CCCD đã tồn tại!");
                            return View(khachHang);
                        }
                    }

                    // Kiểm tra SĐT trùng (trừ chính nó)
                    if (!string.IsNullOrEmpty(khachHang.SoDienThoai))
                    {
                        var exists = await _context.KhachHang
                            .AnyAsync(k => k.SoDienThoai == khachHang.SoDienThoai && k.MaKhachHang != id);
                        if (exists)
                        {
                            ModelState.AddModelError("SoDienThoai", "Số điện thoại đã tồn tại!");
                            return View(khachHang);
                        }
                    }

                    _context.Update(khachHang);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật khách hàng thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!KhachHangExists(khachHang.MaKhachHang))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(khachHang);
        }

        // GET: Xóa khách hàng
        public async Task<IActionResult> Delete(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            var khachHang = await _context.KhachHang
                .FirstOrDefaultAsync(m => m.MaKhachHang == id);
            if (khachHang == null)
            {
                return NotFound();
            }

            // Kiểm tra xem có hợp đồng không
            var coHopDong = await _context.HopDong.AnyAsync(h => h.MaKhachHang == id);
            if (coHopDong)
            {
                ViewBag.HasContract = true;
                ViewBag.Error = "Khách hàng đang có hợp đồng, không thể xóa!";
            }

            return View(khachHang);
        }

        // POST: Xóa khách hàng
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var khachHang = await _context.KhachHang.FindAsync(id);
            if (khachHang != null)
            {
                var coHopDong = await _context.HopDong.AnyAsync(h => h.MaKhachHang == id);
                if (coHopDong)
                {
                    TempData["Error"] = "Không thể xóa vì khách hàng đang có hợp đồng!";
                    return RedirectToAction(nameof(Index));
                }

                _context.KhachHang.Remove(khachHang);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa khách hàng thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool KhachHangExists(int id)
        {
            return _context.KhachHang.Any(e => e.MaKhachHang == id);
        }
    }
}