using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class TaiKhoanController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public TaiKhoanController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        private int GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        private string GetCurrentRole()
        {
            return HttpContext.Session.GetString("Role") ?? "";
        }

        private bool IsChuTro()
        {
            return GetCurrentRole() == "ChuTro";
        }


        // ==================== LẤY DANH SÁCH KHÁCH HÀNG CỦA CHỦ TRỌ ====================
        private async Task<List<int>> GetKhachHangIdsOfChuTro(int chuTroId)
        {
            var phongIds = await _context.Phong
                .Where(p => p.MaChuTro == chuTroId)
                .Select(p => p.MaPhong)
                .ToListAsync();

            var khachHangIds = await _context.HopDong
                .Where(h => phongIds.Contains(h.MaPhong))
                .Select(h => h.MaKhachHang)
                .Distinct()
                .ToListAsync();

            return khachHangIds;
        }

        // ==================== LẤY DANH SÁCH TÀI KHOẢN CỦA KHÁCH HÀNG THUỘC CHỦ TRỌ ====================
        private async Task<List<int>> GetTaiKhoanIdsOfChuTro(int chuTroId)
        {
            var khachHangIds = await GetKhachHangIdsOfChuTro(chuTroId);

            var taiKhoanIds = await _context.KhachHang
                .Where(k => khachHangIds.Contains(k.MaKhachHang) && k.MaTaiKhoan != null)
                .Select(k => k.MaTaiKhoan ?? 0)
                .Distinct()
                .ToListAsync();

            return taiKhoanIds;
        }

        // ==================== DANH SÁCH KHÁCH HÀNG (CHỦ TRỌ) ====================
        public async Task<IActionResult> DanhSachKhachHang()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");

            if (!IsChuTro())
            {
                TempData["Error"] = "Bạn không có quyền truy cập!";
                return RedirectToAction("Index", "Dashboard");
            }

            var taiKhoanIds = await GetTaiKhoanIdsOfChuTro(userId);

            var taiKhoans = await _context.TaiKhoan
                .Where(t => t.VaiTro == "Khach" && taiKhoanIds.Contains(t.MaTaiKhoan))
                .ToListAsync();

            ViewBag.Title = "Danh sách tài khoản khách hàng";
            return View(taiKhoans);
        }

        // ==================== CHI TIẾT TÀI KHOẢN KHÁCH (CHỦ TRỌ) ====================
        public async Task<IActionResult> Details(int? id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");

            if (!IsChuTro())
            {
                TempData["Error"] = "Bạn không có quyền truy cập!";
                return RedirectToAction("Index", "Dashboard");
            }

            if (id == null) return NotFound();

            var taiKhoan = await _context.TaiKhoan
                .FirstOrDefaultAsync(m => m.MaTaiKhoan == id);
            if (taiKhoan == null || taiKhoan.VaiTro != "Khach") return NotFound();

            var taiKhoanIds = await GetTaiKhoanIdsOfChuTro(userId);
            if (!taiKhoanIds.Contains(taiKhoan.MaTaiKhoan))
            {
                TempData["Error"] = "Bạn không có quyền xem tài khoản này!";
                return RedirectToAction(nameof(DanhSachKhachHang));
            }

            return View(taiKhoan);
        }

        // ==================== TẠO TÀI KHOẢN CHO KHÁCH (CHỦ TRỌ) ====================
        public async Task<IActionResult> TaoTaiKhoanKhach()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");

            if (!IsChuTro())
            {
                TempData["Error"] = "Chỉ Chủ trọ mới có quyền tạo tài khoản Khách hàng!";
                return RedirectToAction("Index", "Dashboard");
            }

            var phongIds = await _context.Phong
                .Where(p => p.MaChuTro == userId)
                .Select(p => p.MaPhong)
                .ToListAsync();

            var khachHangIds = await _context.HopDong
                .Where(h => phongIds.Contains(h.MaPhong))
                .Select(h => h.MaKhachHang)
                .Distinct()
                .ToListAsync();

            var khachHangList = await _context.KhachHang
                .Where(k => khachHangIds.Contains(k.MaKhachHang) && (k.MaTaiKhoan == null || k.MaTaiKhoan == 0))
                .ToListAsync();

            ViewBag.KhachHangList = khachHangList;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TaoTaiKhoanKhach(int maKhachHang, string tenDangNhap, string matKhau)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");

            if (!IsChuTro())
            {
                TempData["Error"] = "Chỉ Chủ trọ mới có quyền tạo tài khoản Khách hàng!";
                return RedirectToAction("Index", "Dashboard");
            }

            var khachHangIds = await GetKhachHangIdsOfChuTro(userId);
            if (!khachHangIds.Contains(maKhachHang))
            {
                TempData["Error"] = "Khách hàng không thuộc quyền quản lý của bạn!";
                return RedirectToAction(nameof(DanhSachKhachHang));
            }

            var khachHang = await _context.KhachHang.FindAsync(maKhachHang);
            if (khachHang == null)
            {
                TempData["Error"] = "Không tìm thấy khách hàng!";
                return RedirectToAction(nameof(DanhSachKhachHang));
            }

            var exists = await _context.TaiKhoan
                .AnyAsync(t => t.TenDangNhap == tenDangNhap);

            if (exists)
            {
                TempData["Error"] = "Tên đăng nhập đã tồn tại!";
                ViewBag.KhachHangList = await GetKhachHangListForView(userId);
                return View();
            }

            var taiKhoan = new TaiKhoan
            {
                TenDangNhap = tenDangNhap,
                MatKhau = matKhau,
                VaiTro = "Khach",
                TrangThai = "Hoạt động",
                Email = khachHang.Email
            };

            _context.TaiKhoan.Add(taiKhoan);
            await _context.SaveChangesAsync();

            khachHang.MaTaiKhoan = taiKhoan.MaTaiKhoan;
            _context.Update(khachHang);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Tạo tài khoản thành công cho khách hàng {khachHang.HoTen}! Tài khoản: {tenDangNhap} / {matKhau}";

            return RedirectToAction(nameof(DanhSachKhachHang));
        }

        private async Task<List<KhachHang>> GetKhachHangListForView(int userId)
        {
            var phongIds = await _context.Phong
                .Where(p => p.MaChuTro == userId)
                .Select(p => p.MaPhong)
                .ToListAsync();

            var khachHangIds = await _context.HopDong
                .Where(h => phongIds.Contains(h.MaPhong))
                .Select(h => h.MaKhachHang)
                .Distinct()
                .ToListAsync();

            return await _context.KhachHang
                .Where(k => khachHangIds.Contains(k.MaKhachHang) && (k.MaTaiKhoan == null || k.MaTaiKhoan == 0))
                .ToListAsync();
        }

        // ==================== SỬA TÀI KHOẢN KHÁCH (CHỦ TRỌ) ====================
        public async Task<IActionResult> Edit(int? id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");

            if (!IsChuTro())
            {
                TempData["Error"] = "Bạn không có quyền sửa tài khoản!";
                return RedirectToAction("Index", "Dashboard");
            }

            if (id == null) return NotFound();

            var taiKhoan = await _context.TaiKhoan.FindAsync(id);
            if (taiKhoan == null || taiKhoan.VaiTro != "Khach") return NotFound();

            var taiKhoanIds = await GetTaiKhoanIdsOfChuTro(userId);
            if (!taiKhoanIds.Contains(taiKhoan.MaTaiKhoan))
            {
                TempData["Error"] = "Bạn không có quyền sửa tài khoản này!";
                return RedirectToAction(nameof(DanhSachKhachHang));
            }

            return View(taiKhoan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TaiKhoan taiKhoan)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");

            if (!IsChuTro())
            {
                TempData["Error"] = "Bạn không có quyền sửa tài khoản!";
                return RedirectToAction("Index", "Dashboard");
            }

            if (id != taiKhoan.MaTaiKhoan) return NotFound();

            var taiKhoanIds = await GetTaiKhoanIdsOfChuTro(userId);
            if (!taiKhoanIds.Contains(taiKhoan.MaTaiKhoan))
            {
                TempData["Error"] = "Bạn không có quyền sửa tài khoản này!";
                return RedirectToAction(nameof(DanhSachKhachHang));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var exists = await _context.TaiKhoan
                        .AnyAsync(t => t.TenDangNhap == taiKhoan.TenDangNhap && t.MaTaiKhoan != id);

                    if (exists)
                    {
                        ModelState.AddModelError("TenDangNhap", "Tên đăng nhập đã tồn tại!");
                        return View(taiKhoan);
                    }

                    _context.Update(taiKhoan);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật tài khoản thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TaiKhoanExists(taiKhoan.MaTaiKhoan)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(DanhSachKhachHang));
            }
            return View(taiKhoan);
        }

        // ==================== XÓA TÀI KHOẢN KHÁCH (CHỦ TRỌ) ====================
        public async Task<IActionResult> Delete(int? id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");

            if (!IsChuTro())
            {
                TempData["Error"] = "Bạn không có quyền xóa tài khoản!";
                return RedirectToAction("Index", "Dashboard");
            }

            if (id == null) return NotFound();

            var taiKhoan = await _context.TaiKhoan
                .FirstOrDefaultAsync(m => m.MaTaiKhoan == id);
            if (taiKhoan == null || taiKhoan.VaiTro != "Khach") return NotFound();

            var taiKhoanIds = await GetTaiKhoanIdsOfChuTro(userId);
            if (!taiKhoanIds.Contains(taiKhoan.MaTaiKhoan))
            {
                TempData["Error"] = "Bạn không có quyền xóa tài khoản này!";
                return RedirectToAction(nameof(DanhSachKhachHang));
            }

            return View(taiKhoan);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");

            if (!IsChuTro())
            {
                TempData["Error"] = "Bạn không có quyền xóa tài khoản!";
                return RedirectToAction("Index", "Dashboard");
            }

            var taiKhoan = await _context.TaiKhoan.FindAsync(id);
            if (taiKhoan != null && taiKhoan.VaiTro == "Khach")
            {
                var khachHang = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.MaTaiKhoan == id);
                if (khachHang != null)
                {
                    khachHang.MaTaiKhoan = null;
                    _context.Update(khachHang);
                    await _context.SaveChangesAsync();
                }

                _context.TaiKhoan.Remove(taiKhoan);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa tài khoản thành công!";
            }

            return RedirectToAction(nameof(DanhSachKhachHang));
        }

        private List<SelectListItem> GetDanhSachNganHang()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Chọn ngân hàng --" },
                new SelectListItem { Value = "mbbank", Text = "MB Bank" },
                new SelectListItem { Value = "techcombank", Text = "Techcombank" },
                new SelectListItem { Value = "vietcombank", Text = "Vietcombank" },
                new SelectListItem { Value = "bidv", Text = "BIDV" },
                new SelectListItem { Value = "vietinbank", Text = "Vietinbank" },
                new SelectListItem { Value = "tpbank", Text = "TPBank" },
                new SelectListItem { Value = "acb", Text = "ACB" },
                new SelectListItem { Value = "sacombank", Text = "Sacombank" },
                new SelectListItem { Value = "vpbank", Text = "VPBank" },
                new SelectListItem { Value = "agribank", Text = "Agribank" },
                new SelectListItem { Value = "ocb", Text = "OCB" },
                new SelectListItem { Value = "hdbank", Text = "HDBank" },
                new SelectListItem { Value = "msb", Text = "MSB" },
                new SelectListItem { Value = "seabank", Text = "SeABank" },
                new SelectListItem { Value = "shb", Text = "SHB" },
            };
        }

        public async Task<IActionResult> CapNhatNganHang()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");
            if (!IsChuTro())
            {
                TempData["Error"] = "Chỉ Chủ trọ mới có quyền cập nhật thông tin ngân hàng!";
                return RedirectToAction("Index", "Dashboard");
            }
            var taiKhoan = await _context.TaiKhoan.FindAsync(userId);
            if (taiKhoan == null) return NotFound();
            ViewBag.DanhSachNganHang = GetDanhSachNganHang();
            return View(taiKhoan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CapNhatNganHang(int MaTaiKhoan, string MaNganHang, string TenNganHang, string SoTaiKhoan, string ChuTaiKhoan)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");
            if (!IsChuTro())
            {
                TempData["Error"] = "Chỉ Chủ trọ mới có quyền cập nhật thông tin ngân hàng!";
                return RedirectToAction("Index", "Dashboard");
            }
            var taiKhoan = await _context.TaiKhoan.FindAsync(userId);
            if (taiKhoan == null) return NotFound();

            taiKhoan.MaNganHang = MaNganHang;
            taiKhoan.TenNganHang = TenNganHang;
            taiKhoan.SoTaiKhoan = SoTaiKhoan;
            taiKhoan.ChuTaiKhoan = ChuTaiKhoan;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Cập nhật thông tin ngân hàng thành công!";
            return RedirectToAction("Index", "Dashboard");
        }

        private bool TaiKhoanExists(int id)
        {
            return _context.TaiKhoan.Any(e => e.MaTaiKhoan == id);
        }
    }
}