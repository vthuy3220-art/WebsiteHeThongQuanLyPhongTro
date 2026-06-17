using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class CoSoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CoSoController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Helper lấy thông tin user
        private int GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        private string GetCurrentRole()
        {
            return HttpContext.Session.GetString("Role") ?? "";
        }

        private bool IsSuperAdmin()
        {
            var role = GetCurrentRole();
            return role == "SuperAdmin" || role == "Admin";
        }

        // ==================== DANH SÁCH CƠ SỞ ====================
        public async Task<IActionResult> Index(string searchString)
        {
            if (GetCurrentUserId() == 0)
            {
                return RedirectToAction("Index", "Login");
            }

            var coSos = _context.CoSo.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                coSos = coSos.Where(c => c.TenCoSo.Contains(searchString));
            }

            ViewBag.SearchString = searchString;
            ViewBag.Role = GetCurrentRole();
            ViewBag.IsSuperAdmin = IsSuperAdmin();

            return View(await coSos.ToListAsync());
        }

        // ==================== CHI TIẾT CƠ SỞ ====================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var coSo = await _context.CoSo
                .FirstOrDefaultAsync(m => m.MaCoSo == id);

            if (coSo == null) return NotFound();

            // Lấy danh sách tòa nhà thuộc cơ sở này
            var toaNhas = await _context.ToaNha
                .Where(t => t.MaCoSo == id)
                .Include(t => t.ChuTro)
                .ToListAsync();
            ViewBag.ToaNhas = toaNhas;
            ViewBag.IsSuperAdmin = IsSuperAdmin();

            return View(coSo);
        }

        // ==================== THÊM CƠ SỞ (CHỈ SUPERADMIN) ====================
        public IActionResult Create()
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền thêm cơ sở!";
                return RedirectToAction(nameof(Index));
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CoSo coSo)
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền thêm cơ sở!";
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                _context.Add(coSo);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm cơ sở thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(coSo);
        }

        // ==================== CHỈNH SỬA CƠ SỞ (CHỈ SUPERADMIN) ====================
        public async Task<IActionResult> Edit(int? id)
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền sửa cơ sở!";
                return RedirectToAction(nameof(Index));
            }

            if (id == null) return NotFound();

            var coSo = await _context.CoSo.FindAsync(id);
            if (coSo == null) return NotFound();

            return View(coSo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CoSo coSo)
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền sửa cơ sở!";
                return RedirectToAction(nameof(Index));
            }

            if (id != coSo.MaCoSo) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(coSo);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật cơ sở thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CoSoExists(coSo.MaCoSo)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(coSo);
        }

        // ==================== CẤU TRÚC XÓA CƠ SỞ CHUẨN KHỚP FORM LUỒNG CŨ ====================

        // 1. Hàm GET: Bấm thùng rác ở Index sẽ nhảy vào đây để hiển thị trang Delete.cshtml
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int MaCoSo) // <-- SỬA Ở ĐÂY THÀNH MaCoSo
        {
            if (!IsSuperAdmin())
            {
                TempData["Error"] = "Bạn không có quyền xóa cơ sở!";
                return RedirectToAction(nameof(Index));
            }

            // Đổi toàn bộ chữ 'id' bên trong hàm này thành 'MaCoSo'
            var coSo = await _context.CoSo.FindAsync(MaCoSo);
            if (coSo == null) return NotFound();

            // KIỂM TRA PHÒNG ĐANG THUÊ TRÚ
            bool dangCoNguoiO = await _context.Phong
                .Include(p => p.ToaNha)
                .AnyAsync(p => p.ToaNha.MaCoSo == MaCoSo && p.TrangThai == "Đã thuê");

            if (dangCoNguoiO)
            {
                TempData["Error"] = $"Không thể xóa! Cơ sở '{coSo.TenCoSo}' hiện vẫn còn các phòng đang có người ở (Đã thuê). Vui lòng thanh lý hợp đồng trước.";
                return View("Delete", coSo);
            }

            try
            {
                var danhSachToaNha = await _context.ToaNha.Where(t => t.MaCoSo == MaCoSo).ToListAsync();

                foreach (var toaNha in danhSachToaNha)
                {
                    var danhSachPhong = await _context.Phong.Where(p => p.MaToaNha == toaNha.MaToaNha).ToListAsync();
                    _context.Phong.RemoveRange(danhSachPhong);
                }

                _context.ToaNha.RemoveRange(danhSachToaNha);
                _context.CoSo.Remove(coSo);

                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa cơ sở thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi hệ thống phát sinh: " + ex.Message;
                return View("Delete", coSo);
            }

            return RedirectToAction(nameof(Index));
        }

        private bool CoSoExists(int id)
        {
            return _context.CoSo.Any(e => e.MaCoSo == id);
        }
    }
}