using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

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
            .Where(p => p.TrangThai == "Trống" && _context.BaiDang.Any(b => b.MaPhong == p.MaPhong && b.TrangThai == "Hiển thị"))
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

            // SỬA TẠI ĐÂY: Thay vì chỉ lấy trong bảng PhongImage, dictAnh sẽ quét lấy ảnh từ bài đăng của phòng trước
            var danhSachBaiDang = await _context.BaiDang.Where(b => b.TrangThai == "Hiển thị").ToListAsync();
            var phongImages = await _context.PhongImage.ToListAsync();

            var dictAnh = new Dictionary<int, string>();
            foreach (var phong in danhSachPhong)
            {
                // Tìm ảnh của bài đăng tương ứng với phòng trước
                var anhBaiDang = danhSachBaiDang.FirstOrDefault(b => b.MaPhong == phong.MaPhong)?.HinhAnh;
                if (!string.IsNullOrEmpty(anhBaiDang))
                {
                    dictAnh[phong.MaPhong] = anhBaiDang;
                }
                else
                {
                    // Nếu bài đăng không có ảnh thì mới tìm trong PhongImage hoặc dùng ảnh mặc định
                    var anhPhongGoc = phongImages.FirstOrDefault(i => i.MaPhong == phong.MaPhong)?.ImagePath;
                    dictAnh[phong.MaPhong] = !string.IsNullOrEmpty(anhPhongGoc) ? anhPhongGoc : "/images/default-room.jpg";
                }
            }

            ViewBag.TongSo = danhSachPhong.Count;
            ViewBag.PhongNoiBat = danhSachPhong;
            ViewBag.DictAnh = dictAnh;

            return View(danhSachPhong);
        }

        // ==================== CHI TIẾT PHÒNG (Cho khách vãng lai) ====================
        public async Task<IActionResult> ChiTietPhong(int id)
        {
            var phong = await _context.Phong
                .Include(p => p.ToaNha)
                    .ThenInclude(t => t.CoSo)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MaPhong == id);

            if (phong == null) return NotFound();

            var images = await _context.PhongImage
                .Where(i => i.MaPhong == id)
                .AsNoTracking()
                .ToListAsync();
            ViewBag.Images = images;

            var baiDang = await _context.BaiDang
                .Where(b => b.MaPhong == id)
                .AsNoTracking()
                .FirstOrDefaultAsync();
            ViewBag.BaiDang = baiDang;

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