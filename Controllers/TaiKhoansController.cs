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
    public class TaiKhoansController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TaiKhoansController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: TaiKhoans
        public async Task<IActionResult> Index()
        {
            return View(await _context.TaiKhoan.ToListAsync());
        }

        // GET: TaiKhoans/Details/5
        public async Task<IActionResult> Details(int? id)
        {
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

        // GET: TaiKhoans/Create
        public IActionResult Create()
        {
            ViewBag.KhachHangList = _context.KhachHang.ToList();

            // CHỈ LẤY PHÒNG ĐÃ CÓ HỢP ĐỒNG (ĐÃ THUÊ)
            ViewBag.PhongList = _context.Phong
                .Include(p => p.CoSo)
                .Where(p => p.TrangThai == "Đã thuê")  // ✅ Chỉ phòng có hợp đồng
                .ToList();

            return View();
        }

        // POST: TaiKhoans/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TaiKhoan taiKhoan, int? MaKhachHang, int? MaPhong)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra tên đăng nhập đã tồn tại
                var exists = await _context.TaiKhoan.AnyAsync(t => t.TenDangNhap == taiKhoan.TenDangNhap);
                if (exists)
                {
                    ModelState.AddModelError("TenDangNhap", "Tên đăng nhập đã tồn tại!");
                    ViewBag.KhachHangList = await _context.KhachHang.ToListAsync();
                    ViewBag.PhongList = await _context.Phong.Include(p => p.CoSo).Where(p => p.TrangThai == "Trống").ToListAsync();
                    return View(taiKhoan);
                }

                _context.Add(taiKhoan);
                await _context.SaveChangesAsync();

                // Nếu là khách hàng và có chọn khách hàng/phòng
                if (taiKhoan.VaiTro == "Khach" && MaKhachHang.HasValue && MaPhong.HasValue)
                {
                    // Cập nhật MaTaiKhoan cho khách hàng
                    var khachHang = await _context.KhachHang.FindAsync(MaKhachHang.Value);
                    if (khachHang != null)
                    {
                        khachHang.MaTaiKhoan = taiKhoan.MaTaiKhoan;
                        _context.Update(khachHang);
                    }

                    // Tạo hợp đồng cho khách với phòng được chọn
                    var hopDong = new HopDong
                    {
                        MaPhong = MaPhong.Value,
                        MaKhachHang = MaKhachHang.Value,
                        NgayBatDau = DateTime.Now,
                        NgayKetThuc = DateTime.Now.AddMonths(12),
                        TienCoc = 0,
                        TrangThai = "Hiệu lực"
                    };
                    _context.HopDong.Add(hopDong);

                    // Cập nhật trạng thái phòng
                    var phong = await _context.Phong.FindAsync(MaPhong.Value);
                    if (phong != null)
                    {
                        phong.TrangThai = "Đã thuê";
                        _context.Update(phong);
                    }

                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Tạo tài khoản thành công! Đã gắn phòng cho khách {khachHang?.HoTen}";
                }
                else
                {
                    TempData["Success"] = "Tạo tài khoản thành công!";
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.KhachHangList = await _context.KhachHang.ToListAsync();
            ViewBag.PhongList = await _context.Phong.Include(p => p.CoSo).Where(p => p.TrangThai == "Trống").ToListAsync();
            return View(taiKhoan);
        }

        // GET: TaiKhoans/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
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

        // POST: TaiKhoans/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaTaiKhoan,TenDangNhap,MatKhau,VaiTro,TrangThai")] TaiKhoan taiKhoan)
        {
            if (id != taiKhoan.MaTaiKhoan)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(taiKhoan);
                    await _context.SaveChangesAsync();
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

        // GET: TaiKhoans/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
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

        // POST: TaiKhoans/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var taiKhoan = await _context.TaiKhoan.FindAsync(id);
            if (taiKhoan != null)
            {
                _context.TaiKhoan.Remove(taiKhoan);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool TaiKhoanExists(int id)
        {
            return _context.TaiKhoan.Any(e => e.MaTaiKhoan == id);
        }
    }
}