using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class NguoiOHopDongsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NguoiOHopDongsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Không dùng Index gốc nữa vì đã chuyển sang Dashboard, đá về Dashboard luôn nếu ai cố tình gõ link
        public IActionResult Index()
        {
            return RedirectToAction("QuanLyNguoiO", "Dashboard");
        }

        // Chi tiết người ở cùng
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var nguoiO = await _context.NguoiOHopDong
                .Include(n => n.HopDongNavigation)
                .ThenInclude(h => h.PhongNavigation)
                .FirstOrDefaultAsync(m => m.MaNguoiO == id);

            if (nguoiO == null) return NotFound();
            return View(nguoiO);
        }

        // Thêm mới - GET (Chỉ hiển thị hợp đồng của chủ trọ đang đăng nhập)
        // ==================== THÊM MỚI - GET ====================
        public async Task<IActionResult> Create()
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");

            if (role != "ChuTro" || userId == null) return RedirectToAction("Index", "Login");

            // ĐỒNG BỘ: Include cả Phong và KhachHang để hiển thị tên người đại diện
            var danhSachHopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Include(h => h.KhachHangNavigation)
                .Where(h => h.MaChuTro == userId.Value && h.TrangThai == "Hiệu lực")
                .Select(h => new
                {
                    MaHopDong = h.MaHopDong,
                    HienThi = $"🏠 {(h.PhongNavigation != null ? h.PhongNavigation.TenPhong : "N/A")} - (Đại diện: {(h.KhachHangNavigation != null ? h.KhachHangNavigation.HoTen : "Chưa rõ")})"
                })
                .ToListAsync();

            ViewBag.MaHopDong = new SelectList(danhSachHopDong, "MaHopDong", "HienThi");
            return View();
        }

        // ==================== THÊM MỚI - POST ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaHopDong,HoTen,CCCD,SoDienThoai")] NguoiOHopDong nguoiO)
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");

            if (ModelState.IsValid)
            {
                _context.Add(nguoiO);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm thành công!";
                return RedirectToAction("QuanLyNguoiO", "Dashboard");
            }

            // FIX TẠI ĐÂY: Nạp lại Dropdown chuẩn định dạng nếu form nhập bị lỗi validation
            var danhSachHopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Include(h => h.KhachHangNavigation)
                .Where(h => h.MaChuTro == userId.Value && h.TrangThai == "Hiệu lực")
                .Select(h => new {
                    MaHopDong = h.MaHopDong,
                    HienThi = $"🏠 {(h.PhongNavigation != null ? h.PhongNavigation.TenPhong : "N/A")} - (Đại diện: {(h.KhachHangNavigation != null ? h.KhachHangNavigation.HoTen : "Chưa rõ")})"
                })
                .ToListAsync();

            ViewBag.MaHopDong = new SelectList(danhSachHopDong, "MaHopDong", "HienThi", nguoiO.MaHopDong);
            return View(nguoiO);
        }

        // Chỉnh sửa - GET
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");

            if (role != "ChuTro" || userId == null) return RedirectToAction("Index", "Login");

            var nguoiO = await _context.NguoiOHopDong.FindAsync(id);
            if (nguoiO == null) return NotFound();

            // Lọc Dropdown hợp đồng đảm bảo chủ trọ không sửa nhầm sang phòng người khác
            var danhSachHopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Include(h => h.KhachHangNavigation)
                .Where(h => h.MaChuTro == userId.Value && h.TrangThai == "Hiệu lực")
                .Select(h => new {
                    MaHopDong = h.MaHopDong,
                    HienThi = $"🏠 {(h.PhongNavigation != null ? h.PhongNavigation.TenPhong : "N/A")} - (Đại diện: {(h.KhachHangNavigation != null ? h.KhachHangNavigation.HoTen : "Chưa rõ")})"
                })
                   .ToListAsync();

            ViewBag.MaHopDong = new SelectList(danhSachHopDong, "MaHopDong", "HienThi", nguoiO.MaHopDong);
            return View(nguoiO);
        }

        // Chỉnh sửa - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaNguoiO,MaHopDong,HoTen,CCCD,SoDienThoai")] NguoiOHopDong nguoiO)
        {
            if (id != nguoiO.MaNguoiO) return NotFound();

            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");

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

                // CHUYỂN HƯỚNG QUAN TRỌNG: Quay trở lại trang Quản lý của Chủ trọ ở Dashboard
                return RedirectToAction("QuanLyNguoiO", "Dashboard");
            }

            var danhSachHopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Where(h => h.MaChuTro == userId.Value && h.TrangThai == "Hiệu lực")
                .Select(h => new {
                    MaHopDong = h.MaHopDong,
                    HienThi = $"HĐ #{h.MaHopDong} - Phòng: {(h.PhongNavigation != null ? h.PhongNavigation.TenPhong : "N/A")}"
                })
                .ToListAsync();

            ViewBag.MaHopDong = new SelectList(danhSachHopDong, "MaHopDong", "HienThi", nguoiO.MaHopDong);
            return View(nguoiO);
        }

        // ==================== SỬA DỨT ĐIỂM HÀM XÓA (GET) DÒNG 165 ====================
        // Xóa - GET: Hiển thị trang xác nhận xóa
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var nguoiO = await _context.NguoiOHopDong
                .Include(n => n.HopDongNavigation)
                .ThenInclude(h => h.PhongNavigation)
                .FirstOrDefaultAsync(m => m.MaNguoiO == id);

            if (nguoiO == null) return NotFound();


            return View(nguoiO);
        }


        // Xóa - POST: Thực hiện xóa khi bấm nút xác nhận
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

            return RedirectToAction("QuanLyNguoiO", "Dashboard");
        }
    }
}