using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class ToaNhaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ToaNhaController(ApplicationDbContext context)
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

        private bool IsAdmin()
        {
            var role = GetCurrentRole();
            return role == "Admin" || role == "SuperAdmin";
        }

        private bool IsChuTro()
        {
            return GetCurrentRole() == "ChuTro";
        }

        // ==================== DANH SÁCH TÒA NHÀ ====================
        public async Task<IActionResult> Index(string searchString)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");

            var role = GetCurrentRole();

            var toaNhas = _context.ToaNha
                .Include(t => t.CoSo)
                .Include(t => t.ChuTro)
                .AsQueryable();

            // PHÂN QUYỀN: Chủ trọ chỉ thấy tòa nhà của mình
            if (IsChuTro())
            {
                toaNhas = toaNhas.Where(t => t.MaChuTro == userId);
            }
            // Admin/SuperAdmin thấy TẤT CẢ (không cần lọc)

            if (!string.IsNullOrEmpty(searchString))
            {
                toaNhas = toaNhas.Where(t => t.TenToaNha.Contains(searchString));
            }

            ViewBag.SearchString = searchString;
            ViewBag.Role = role;

            return View(await toaNhas.OrderBy(t => t.MaToaNha).ToListAsync());
        }

        // ==================== CHI TIẾT TÒA NHÀ ====================
        public async Task<IActionResult> Details(int? id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");
            if (id == null) return NotFound();

            var toaNha = await _context.ToaNha
                .Include(t => t.CoSo)
                .Include(t => t.ChuTro)
                .Include(t => t.Phongs)
                .FirstOrDefaultAsync(m => m.MaToaNha == id);

            if (toaNha == null) return NotFound();

            // KIỂM TRA QUYỀN XEM
            if (IsChuTro() && toaNha.MaChuTro != userId)
            {
                TempData["Error"] = "Bạn không có quyền xem tòa nhà này!";
                return RedirectToAction(nameof(Index));
            }

            // Lấy danh sách phòng thuộc tòa nhà
            var phongs = await _context.Phong
                .Where(p => p.MaToaNha == id)
                .ToListAsync();
            ViewBag.Phongs = phongs;
            ViewBag.SoLuongPhong = phongs.Count;

            return View(toaNha);
        }

        // ==================== THÊM TÒA NHÀ MỚI ====================
        public async Task<IActionResult> Create()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");

            // CHỈ CHỦ TRỌ MỚI ĐƯỢC THÊM TÒA NHÀ
            if (!IsChuTro())
            {
                TempData["Error"] = "Chỉ Chủ trọ mới có quyền thêm tòa nhà!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CoSoList = await _context.CoSo.ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ToaNha toaNha)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");

            // CHỈ CHỦ TRỌ MỚI ĐƯỢC THÊM
            if (!IsChuTro())
            {
                TempData["Error"] = "Chỉ Chủ trọ mới có quyền thêm tòa nhà!";
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                // Tự động gán MaChuTro cho Chủ trọ
                toaNha.MaChuTro = userId;
                toaNha.TrangThai = "Approved"; // Mặc định duyệt

                _context.Add(toaNha);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm tòa nhà thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CoSoList = await _context.CoSo.ToListAsync();
            return View(toaNha);
        }

        // ==================== CHỈNH SỬA TÒA NHÀ ====================
        public async Task<IActionResult> Edit(int? id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");
            if (id == null) return NotFound();

            var toaNha = await _context.ToaNha
                .Include(t => t.CoSo)
                .FirstOrDefaultAsync(t => t.MaToaNha == id);

            if (toaNha == null) return NotFound();

            // KIỂM TRA QUYỀN SỬA
            if (IsChuTro() && toaNha.MaChuTro != userId)
            {
                TempData["Error"] = "Bạn không có quyền sửa tòa nhà này!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CoSoList = await _context.CoSo.ToListAsync();
            return View(toaNha);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ToaNha toaNha)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");
            if (id != toaNha.MaToaNha) return NotFound();

            var toaNhaCu = await _context.ToaNha.AsNoTracking()
                .FirstOrDefaultAsync(t => t.MaToaNha == id);

            if (toaNhaCu == null) return NotFound();

            // KIỂM TRA QUYỀN SỬA
            if (IsChuTro() && toaNhaCu.MaChuTro != userId)
            {
                TempData["Error"] = "Bạn không có quyền sửa tòa nhà này!";
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Giữ nguyên MaChuTro
                    toaNha.MaChuTro = toaNhaCu.MaChuTro;
                    _context.Update(toaNha);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật tòa nhà thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ToaNhaExists(toaNha.MaToaNha))
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

            ViewBag.CoSoList = await _context.CoSo.ToListAsync();
            return View(toaNha);
        }

        // ==================== XÓA TÒA NHÀ ====================
        public async Task<IActionResult> Delete(int? id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");
            if (id == null) return NotFound();

            var toaNha = await _context.ToaNha
                .Include(t => t.Phongs)
                .Include(t => t.CoSo)
                .FirstOrDefaultAsync(m => m.MaToaNha == id);

            if (toaNha == null) return NotFound();

            // KIỂM TRA QUYỀN XÓA
            if (IsChuTro() && toaNha.MaChuTro != userId)
            {
                TempData["Error"] = "Bạn không có quyền xóa tòa nhà này!";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra có phòng không
            if (toaNha.Phongs != null && toaNha.Phongs.Any())
            {
                TempData["Error"] = "Không thể xóa vì tòa nhà này đang có phòng!";
                return RedirectToAction(nameof(Index));
            }

            return View(toaNha);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");

            var toaNha = await _context.ToaNha
                .Include(t => t.Phongs)
                .FirstOrDefaultAsync(t => t.MaToaNha == id);

            if (toaNha != null)
            {
                // KIỂM TRA QUYỀN XÓA
                if (IsChuTro() && toaNha.MaChuTro != userId)
                {
                    TempData["Error"] = "Bạn không có quyền xóa tòa nhà này!";
                    return RedirectToAction(nameof(Index));
                }

                if (toaNha.Phongs != null && toaNha.Phongs.Any())
                {
                    TempData["Error"] = "Không thể xóa vì tòa nhà này đang có phòng!";
                    return RedirectToAction(nameof(Index));
                }

                _context.ToaNha.Remove(toaNha);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa tòa nhà thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ToaNhaExists(int id)
        {
            return _context.ToaNha.Any(e => e.MaToaNha == id);
        }
    }
}