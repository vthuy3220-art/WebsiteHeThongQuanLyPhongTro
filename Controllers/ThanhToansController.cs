using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HeThongQuanLyPhongTro.Models;
using HeThongQuanLyPhongTro.Data;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class ThanhToansController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ThanhToansController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==================== HELPER ====================
        private int GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        private string GetCurrentRole()
        {
            return HttpContext.Session.GetString("Role") ?? "";
        }

        // ==================== DANH SÁCH THANH TOÁN ====================
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            var thanhToans = _context.ThanhToan
                .Include(t => t.HoaDonNavigation)
                .ThenInclude(h => h.HopDongNavigation)
                .ThenInclude(h => h.PhongNavigation)
                .AsQueryable();

            // ✅ PHÂN QUYỀN: Chủ trọ chỉ thấy thanh toán của mình
            if (role == "ChuTro")
            {
                thanhToans = thanhToans.Where(t =>
                    t.HoaDonNavigation != null &&
                    t.HoaDonNavigation.MaChuTro == userId
                );
            }
            // ✅ KHÁCH HÀNG CHỈ THẤY THANH TOÁN CỦA MÌNH
            else if (role == "Khach")
            {
                var khachHang = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);
                if (khachHang != null)
                {
                    var hopDongIds = await _context.HopDong
                        .Where(h => h.MaKhachHang == khachHang.MaKhachHang)
                        .Select(h => h.MaHopDong)
                        .ToListAsync();

                    thanhToans = thanhToans.Where(t =>
                        t.HoaDonNavigation != null &&
                        hopDongIds.Contains(t.HoaDonNavigation.MaHopDong)
                    );
                }
                else
                {
                    thanhToans = thanhToans.Where(t => false);
                }
            }
            // ✅ ADMIN KHÔNG ĐƯỢC XEM
            else if (role == "Admin")
            {
                TempData["Error"] = "Admin không có quyền xem lịch sử thanh toán!";
                return RedirectToAction("Index", "Home");
            }

            return View(await thanhToans.ToListAsync());
        }

        // ==================== CHI TIẾT THANH TOÁN ====================
        public async Task<IActionResult> Details(int? id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            if (id == null)
                return NotFound();

            var thanhToan = await _context.ThanhToan
                .Include(t => t.HoaDonNavigation)
                .ThenInclude(h => h.HopDongNavigation)
                .ThenInclude(h => h.PhongNavigation)
                .FirstOrDefaultAsync(m => m.MaThanhToan == id);

            if (thanhToan == null)
                return NotFound();

            // ✅ PHÂN QUYỀN: Kiểm tra quyền xem
            var hasAccess = false;

            if (role == "ChuTro")
            {
                if (thanhToan.HoaDonNavigation != null &&
                    thanhToan.HoaDonNavigation.MaChuTro == userId)
                {
                    hasAccess = true;
                }
            }
            else if (role == "Khach")
            {
                var khachHang = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

                if (khachHang != null && thanhToan.HoaDonNavigation != null)
                {
                    var hopDong = await _context.HopDong
                        .FirstOrDefaultAsync(h => h.MaHopDong == thanhToan.HoaDonNavigation.MaHopDong);

                    if (hopDong != null && hopDong.MaKhachHang == khachHang.MaKhachHang)
                    {
                        hasAccess = true;
                    }
                }
            }
            else if (role == "Admin")
            {
                TempData["Error"] = "Admin không có quyền xem chi tiết thanh toán!";
                return RedirectToAction("Index", "Home");
            }

            if (!hasAccess)
            {
                TempData["Error"] = "Bạn không có quyền xem thanh toán này!";
                return RedirectToAction("Index");
            }

            return View(thanhToan);
        }

        // ==================== TẠO THANH TOÁN (GET) ====================
        public IActionResult Create()
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            // ✅ CHỈ CHỦ TRỌ MỚI TẠO ĐƯỢC
            if (role != "ChuTro")
            {
                TempData["Error"] = "Bạn không có quyền tạo thanh toán!";
                return RedirectToAction("Index", "Home");
            }

            // ✅ Lọc hóa đơn theo chủ trọ
            var hoaDons = _context.HoaDon
                .Where(h => h.MaChuTro == userId && h.TrangThai == "Chờ thanh toán")
                .ToList();

            ViewData["MaHoaDon"] = new SelectList(hoaDons, "MaHoaDon", "MaHoaDon");
            return View();
        }

        // ==================== TẠO THANH TOÁN (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaThanhToan,MaHoaDon,SoTien,NgayThanhToan,NoiDungChuyenKhoan,TrangThai")] ThanhToan thanhToan)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            // ✅ CHỈ CHỦ TRỌ MỚI TẠO ĐƯỢC
            if (role != "ChuTro")
            {
                TempData["Error"] = "Bạn không có quyền tạo thanh toán!";
                return RedirectToAction("Index", "Home");
            }

            // ✅ Kiểm tra hóa đơn thuộc về chủ trọ
            var hoaDon = await _context.HoaDon
                .FirstOrDefaultAsync(h => h.MaHoaDon == thanhToan.MaHoaDon);

            if (hoaDon == null || hoaDon.MaChuTro != userId)
            {
                TempData["Error"] = "Bạn không có quyền tạo thanh toán cho hóa đơn này!";
                return RedirectToAction("Index");
            }

            if (ModelState.IsValid)
            {
                thanhToan.NgayThanhToan = DateTime.Now;
                _context.Add(thanhToan);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var hoaDons = _context.HoaDon
                .Where(h => h.MaChuTro == userId && h.TrangThai == "Chờ thanh toán")
                .ToList();
            ViewData["MaHoaDon"] = new SelectList(hoaDons, "MaHoaDon", "MaHoaDon", thanhToan.MaHoaDon);
            return View(thanhToan);
        }

        // ==================== SỬA THANH TOÁN (GET) ====================
        public async Task<IActionResult> Edit(int? id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            if (id == null)
                return NotFound();

            var thanhToan = await _context.ThanhToan
                .Include(t => t.HoaDonNavigation)
                .FirstOrDefaultAsync(t => t.MaThanhToan == id);

            if (thanhToan == null)
                return NotFound();

            // ✅ CHỦ TRỌ CHỈ SỬA THANH TOÁN CỦA MÌNH
            if (role != "ChuTro")
            {
                TempData["Error"] = "Bạn không có quyền sửa thanh toán!";
                return RedirectToAction("Index", "Home");
            }

            if (thanhToan.HoaDonNavigation == null ||
                thanhToan.HoaDonNavigation.MaChuTro != userId)
            {
                TempData["Error"] = "Bạn không có quyền sửa thanh toán này!";
                return RedirectToAction("Index");
            }

            var hoaDons = _context.HoaDon
                .Where(h => h.MaChuTro == userId && h.TrangThai == "Chờ thanh toán")
                .ToList();
            ViewData["MaHoaDon"] = new SelectList(hoaDons, "MaHoaDon", "MaHoaDon", thanhToan.MaHoaDon);
            return View(thanhToan);
        }

        // ==================== SỬA THANH TOÁN (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaThanhToan,MaHoaDon,SoTien,NgayThanhToan,NoiDungChuyenKhoan,TrangThai")] ThanhToan thanhToan)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            if (id != thanhToan.MaThanhToan)
                return NotFound();

            // ✅ CHỦ TRỌ CHỈ SỬA THANH TOÁN CỦA MÌNH
            if (role != "ChuTro")
            {
                TempData["Error"] = "Bạn không có quyền sửa thanh toán!";
                return RedirectToAction("Index", "Home");
            }

            var existingThanhToan = await _context.ThanhToan
                .Include(t => t.HoaDonNavigation)
                .FirstOrDefaultAsync(t => t.MaThanhToan == id);

            if (existingThanhToan == null)
                return NotFound();

            if (existingThanhToan.HoaDonNavigation == null ||
                existingThanhToan.HoaDonNavigation.MaChuTro != userId)
            {
                TempData["Error"] = "Bạn không có quyền sửa thanh toán này!";
                return RedirectToAction("Index");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    thanhToan.NgayThanhToan = DateTime.Now;
                    _context.Update(thanhToan);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ThanhToanExists(thanhToan.MaThanhToan))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }

            var hoaDons = _context.HoaDon
                .Where(h => h.MaChuTro == userId && h.TrangThai == "Chờ thanh toán")
                .ToList();
            ViewData["MaHoaDon"] = new SelectList(hoaDons, "MaHoaDon", "MaHoaDon", thanhToan.MaHoaDon);
            return View(thanhToan);
        }

        // ==================== XÓA THANH TOÁN (GET) ====================
        public async Task<IActionResult> Delete(int? id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            if (id == null)
                return NotFound();

            var thanhToan = await _context.ThanhToan
                .Include(t => t.HoaDonNavigation)
                .ThenInclude(h => h.HopDongNavigation)
                .ThenInclude(h => h.PhongNavigation)
                .FirstOrDefaultAsync(m => m.MaThanhToan == id);

            if (thanhToan == null)
                return NotFound();

            // ✅ CHỦ TRỌ CHỈ XÓA THANH TOÁN CỦA MÌNH
            if (role != "ChuTro")
            {
                TempData["Error"] = "Bạn không có quyền xóa thanh toán!";
                return RedirectToAction("Index", "Home");
            }

            if (thanhToan.HoaDonNavigation == null ||
                thanhToan.HoaDonNavigation.MaChuTro != userId)
            {
                TempData["Error"] = "Bạn không có quyền xóa thanh toán này!";
                return RedirectToAction("Index");
            }

            return View(thanhToan);
        }

        // ==================== XÓA THANH TOÁN (POST) ====================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0)
                return RedirectToAction("Index", "Login");

            var thanhToan = await _context.ThanhToan
                .Include(t => t.HoaDonNavigation)
                .FirstOrDefaultAsync(t => t.MaThanhToan == id);

            if (thanhToan != null)
            {
                // ✅ CHỦ TRỌ CHỈ XÓA THANH TOÁN CỦA MÌNH
                if (role != "ChuTro")
                {
                    TempData["Error"] = "Bạn không có quyền xóa thanh toán!";
                    return RedirectToAction("Index", "Home");
                }

                if (thanhToan.HoaDonNavigation == null ||
                    thanhToan.HoaDonNavigation.MaChuTro != userId)
                {
                    TempData["Error"] = "Bạn không có quyền xóa thanh toán này!";
                    return RedirectToAction("Index");
                }

                _context.ThanhToan.Remove(thanhToan);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa thanh toán thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool ThanhToanExists(int id)
        {
            return _context.ThanhToan.Any(e => e.MaThanhToan == id);
        }
    }
}