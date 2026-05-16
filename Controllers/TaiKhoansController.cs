using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class TaiKhoansController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TaiKhoansController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Danh sách tài khoản
        public async Task<IActionResult> Index()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Index", "Login");
            }

            var taiKhoans = await _context.TaiKhoan.ToListAsync();
            return View(taiKhoans);
        }
        // GET: Tạo tài khoản mới
        public async Task<IActionResult> Create()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Index", "Login");
            }

            // Lấy danh sách khách hàng CHƯA có tài khoản để chọn liên kết
            var khachHangChuaCoTaiKhoan = await _context.KhachHang
                .Where(k => k.MaTaiKhoan == null || k.MaTaiKhoan == 0)
                .ToListAsync();

            ViewBag.KhachHangList = khachHangChuaCoTaiKhoan;
            return View();
        }

        // POST: Tạo tài khoản mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TaiKhoan taiKhoan, int? MaKhachHang)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Index", "Login");
            }

            // Kiểm tra tên đăng nhập đã tồn tại
            var exists = await _context.TaiKhoan
                .AnyAsync(t => t.TenDangNhap == taiKhoan.TenDangNhap);

            if (exists)
            {
                ModelState.AddModelError("TenDangNhap", "Tên đăng nhập đã tồn tại!");
                ViewBag.KhachHangList = await _context.KhachHang
                    .Where(k => k.MaTaiKhoan == null || k.MaTaiKhoan == 0)
                    .ToListAsync();
                return View(taiKhoan);
            }

            if (ModelState.IsValid)
            {
                // Tạo tài khoản mới
                taiKhoan.TrangThai = "Hoạt động";
                _context.TaiKhoan.Add(taiKhoan);
                await _context.SaveChangesAsync();

                // Liên kết với khách hàng nếu có chọn
                if (MaKhachHang.HasValue && MaKhachHang.Value > 0)
                {
                    var khachHang = await _context.KhachHang.FindAsync(MaKhachHang.Value);
                    if (khachHang != null)
                    {
                        khachHang.MaTaiKhoan = taiKhoan.MaTaiKhoan;
                        _context.Update(khachHang);
                        await _context.SaveChangesAsync();

                        TempData["Success"] = $"Tạo tài khoản thành công! Đã liên kết với khách hàng: {khachHang.HoTen}";
                    }
                    else
                    {
                        TempData["Success"] = $"Tạo tài khoản thành công! (Chưa liên kết với khách hàng)";
                    }
                }
                else
                {
                    TempData["Success"] = $"Tạo tài khoản thành công! (Chưa liên kết với khách hàng)";
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.KhachHangList = await _context.KhachHang
                .Where(k => k.MaTaiKhoan == null || k.MaTaiKhoan == 0)
                .ToListAsync();
            return View(taiKhoan);
        }
        // GET: Chi tiết tài khoản
        public async Task<IActionResult> Details(int? id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            var taiKhoan = await _context.TaiKhoan
                .FirstOrDefaultAsync(m => m.MaTaiKhoan == id);
            if (taiKhoan == null)
            {
                return NotFound();
            }

            return View(taiKhoan);
        }

        // ==================== EDIT TÀI KHOẢN ====================

        // GET: Chỉnh sửa tài khoản
        public async Task<IActionResult> Edit(int? id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            var taiKhoan = await _context.TaiKhoan.FindAsync(id);
            if (taiKhoan == null)
            {
                return NotFound();
            }

            return View(taiKhoan);
        }

        // POST: Chỉnh sửa tài khoản
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TaiKhoan taiKhoan)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Index", "Login");
            }

            if (id != taiKhoan.MaTaiKhoan)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Kiểm tra tên đăng nhập trùng (trừ chính nó)
                    var exists = await _context.TaiKhoan
                        .AnyAsync(t => t.TenDangNhap == taiKhoan.TenDangNhap && t.MaTaiKhoan != id);

                    if (exists)
                    {
                        ModelState.AddModelError("TenDangNhap", "Tên đăng nhập đã tồn tại!");
                        return View(taiKhoan);
                    }

                    // Không cho sửa role Admin nếu chỉ còn 1 Admin
                    var oldTaiKhoan = await _context.TaiKhoan.AsNoTracking().FirstOrDefaultAsync(t => t.MaTaiKhoan == id);
                    if (oldTaiKhoan.VaiTro == "Admin" && taiKhoan.VaiTro != "Admin")
                    {
                        var adminCount = await _context.TaiKhoan.CountAsync(t => t.VaiTro == "Admin");
                        if (adminCount <= 1)
                        {
                            TempData["Error"] = "Phải có ít nhất 1 tài khoản Admin!";
                            return RedirectToAction(nameof(Index));
                        }
                    }

                    _context.Update(taiKhoan);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật tài khoản thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TaiKhoanExists(taiKhoan.MaTaiKhoan))
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
            return View(taiKhoan);
        }

        // GET: Xóa tài khoản
        public async Task<IActionResult> Delete(int? id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            var taiKhoan = await _context.TaiKhoan
                .FirstOrDefaultAsync(m => m.MaTaiKhoan == id);
            if (taiKhoan == null)
            {
                return NotFound();
            }

            // Không cho xóa tài khoản Admin cuối cùng
            var adminCount = await _context.TaiKhoan.CountAsync(t => t.VaiTro == "Admin");
            if (taiKhoan.VaiTro == "Admin" && adminCount <= 1)
            {
                TempData["Error"] = "Không thể xóa tài khoản Admin cuối cùng!";
                return RedirectToAction(nameof(Index));
            }

            return View(taiKhoan);
        }

        // POST: Xóa tài khoản
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
            {
                return RedirectToAction("Index", "Login");
            }

            var taiKhoan = await _context.TaiKhoan.FindAsync(id);
            if (taiKhoan != null)
            {
                // Không cho xóa tài khoản Admin cuối cùng
                var adminCount = await _context.TaiKhoan.CountAsync(t => t.VaiTro == "Admin");
                if (taiKhoan.VaiTro == "Admin" && adminCount <= 1)
                {
                    TempData["Error"] = "Không thể xóa tài khoản Admin cuối cùng!";
                    return RedirectToAction(nameof(Index));
                }

                _context.TaiKhoan.Remove(taiKhoan);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa tài khoản thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool TaiKhoanExists(int id)
        {
            return _context.TaiKhoan.Any(e => e.MaTaiKhoan == id);
        }
    }
}