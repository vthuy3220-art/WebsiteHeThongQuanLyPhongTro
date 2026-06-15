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
        public async Task<IActionResult> ChiTietPhong(int? id)
        {
            if (id == null) return NotFound();

            var phong = await _context.Phong
                .Include(p => p.ToaNha)
                    .ThenInclude(t => t.CoSo)
                .Include(p => p.ChuTro)
                .FirstOrDefaultAsync(p => p.MaPhong == id);

            if (phong == null) return NotFound();

            // Lấy danh sách CSVC của phòng
            var csvcList = await _context.CoSoVatChat
                .Where(c => c.MaPhong == id)
                .ToListAsync();
            ViewBag.CSVCNhanh = csvcList;

            // Sửa trong action Details
            var images = await _context.PhongImage
                .Where(i => i.MaPhong == id)
                .OrderByDescending(i => i.IsMain)
                .ThenByDescending(i => i.NgayUpload)
                .ToListAsync();

            // Sửa trong action UploadImage
            var oldMain = await _context.PhongImage
                .FirstOrDefaultAsync(i => i.MaPhong == id && i.IsMain);



            // Lấy bài đăng (nếu có)
            var baiDang = await _context.BaiDang
                .FirstOrDefaultAsync(b => b.MaPhong == id && b.TrangThai == "Hiển thị");
            ViewBag.BaiDang = baiDang;

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