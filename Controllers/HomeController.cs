using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==================== TRANG CHỦ (Hiển thị phòng trống) ====================
        public async Task<IActionResult> Index(string searchString, int? maKhuVuc, int? giaTu, int? giaDen)
        {
            var danhSachKhuVuc = await _context.CoSo.OrderBy(c => c.TenCoSo).ToListAsync();
            ViewBag.DanhSachKhuVuc = danhSachKhuVuc;
            ViewBag.DanhSachCoSo = danhSachKhuVuc;
            ViewBag.MaKhuVuc = maKhuVuc;
            ViewBag.GiaTu = giaTu;
            ViewBag.GiaDen = giaDen;
            ViewBag.SearchString = searchString;

            var phongs = _context.Phong
                .Include(p => p.ToaNha)
                    .ThenInclude(t => t.CoSo)
                .Include(p => p.ChuTro)
                .Where(p => p.TrangThai == "Trống")
                .AsQueryable();

            if (maKhuVuc.HasValue && maKhuVuc.Value > 0)
                phongs = phongs.Where(p => p.ToaNha.CoSo.MaCoSo == maKhuVuc.Value);

            if (giaTu.HasValue)
                phongs = phongs.Where(p => p.GiaPhong >= giaTu.Value);

            if (giaDen.HasValue)
                phongs = phongs.Where(p => p.GiaPhong <= giaDen.Value);

            if (!string.IsNullOrEmpty(searchString))
            {
                var maPhongCsvc = await _context.CoSoVatChat
                    .Where(c => c.TenThietBi.Contains(searchString))
                    .Select(c => c.MaPhong)
                    .ToListAsync();

                phongs = phongs.Where(p =>
                    p.TenPhong.Contains(searchString) ||
                    (p.ToaNha != null && p.ToaNha.TenToaNha.Contains(searchString)) ||
                    (p.ToaNha != null && p.ToaNha.DiaChi.Contains(searchString)) ||
                    (p.ToaNha != null && p.ToaNha.CoSo != null && p.ToaNha.CoSo.TenCoSo.Contains(searchString)) ||
                    maPhongCsvc.Contains(p.MaPhong)
                );
            }

            var danhSachPhong = await phongs.ToListAsync();

            var phongImages = await _context.PhongImage.ToListAsync();
            var dictAnh = phongImages
                .Where(i => i != null)
                .GroupBy(i => i.MaPhong)
                .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.ImagePath ?? "/images/default-room.jpg");

            ViewBag.TongSo = danhSachPhong.Count;
            ViewBag.PhongNoiBat = danhSachPhong;
            ViewBag.DictAnh = dictAnh;

            return View(danhSachPhong);
        }

        // ==================== CHI TIẾT PHÒNG (Cho khách vãng lai) ====================
        public async Task<IActionResult> ChiTietPhong(int id)
        {

            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";


            if (HttpContext.Session.GetInt32("UserId") == null || HttpContext.Session.GetInt32("UserId") == 0)
            {
                return RedirectToAction("Index", "Login");
            }

            // 1. Lấy thông tin phòng (Dùng AsNoTracking để chống kẹt luồng)
            var phong = await _context.Phong
                .Include(p => p.ToaNha)
                    .ThenInclude(t => t.CoSo)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MaPhong == id);

            if (phong == null) return NotFound();

            // 2. Truy vấn album ảnh đổ vào ViewBag
            var images = await _context.PhongImage
                .Where(i => i.MaPhong == id)
                .AsNoTracking()
                .ToListAsync();
            ViewBag.Images = images;

            // 3. Truy vấn thông tin bài đăng lấy Mô tả chi tiết
            var baiDang = await _context.BaiDang
                .Where(b => b.MaPhong == id)
                .AsNoTracking()
                .FirstOrDefaultAsync();
            ViewBag.BaiDang = baiDang;

            // 4. Truy vấn tiện ích cơ sở vật chất
            var csvcList = await _context.CoSoVatChat
                .Where(c => c.MaPhong == id)
                .AsNoTracking()
                .ToListAsync();
            ViewBag.CSVCNhanh = csvcList;

            return View(phong);
        }

        // ==================== CHI TIẾT BÀI ĐĂNG (Chuyển sang chi tiết phòng) ====================
        public async Task<IActionResult> ChiTietBaiDang(int? id)
        {
            if (id == null) return NotFound();

            var baiDang = await _context.BaiDang
                .FirstOrDefaultAsync(b => b.MaBaiDang == id);

            if (baiDang == null) return NotFound();

            return RedirectToAction("ChiTietPhong", new { id = baiDang.MaPhong });
        }
    }
}