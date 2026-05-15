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

        // Trang chủ
        public async Task<IActionResult> Index(string searchString, int? maCoSo, int? giaTu, int? giaDen)
        {
            // Lấy danh sách cơ sở
            var danhSachCoSo = await _context.CoSo.ToListAsync();
            ViewBag.DanhSachCoSo = danhSachCoSo;
            ViewBag.MaCoSo = maCoSo;
            ViewBag.GiaTu = giaTu;
            ViewBag.GiaDen = giaDen;
            ViewBag.SearchString = searchString;

            // Lấy danh sách phòng trống
            var phongs = _context.Phong
                .Include(p => p.CoSo)
                .Where(p => p.TrangThai == "Trống")
                .AsQueryable();

            if (maCoSo.HasValue && maCoSo.Value > 0)
            {
                phongs = phongs.Where(p => p.MaCoSo == maCoSo.Value);
            }
            if (giaTu.HasValue)
            {
                phongs = phongs.Where(p => p.GiaPhong >= giaTu.Value);
            }
            if (giaDen.HasValue)
            {
                phongs = phongs.Where(p => p.GiaPhong <= giaDen.Value);
            }
            if (!string.IsNullOrEmpty(searchString))
            {
                phongs = phongs.Where(p => p.TenPhong.Contains(searchString) ||
                                           (p.CoSo != null && p.CoSo.TenCoSo.Contains(searchString)));
            }

            var danhSachPhong = await phongs.ToListAsync();

            // THÊM: Lấy ảnh đại diện cho từng phòng
            var phongImages = await _context.PhongImages.ToListAsync();
            var dictAnh = phongImages.GroupBy(i => i.MaPhong)
                .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.ImagePath);

            ViewBag.TongSo = danhSachPhong.Count;
            ViewBag.PhongNoiBat = danhSachPhong;
            ViewBag.DictAnh = dictAnh;

            // Lấy bài đăng
            var baiDangs = await _context.BaiDang
                .Include(b => b.PhongNavigation)
                .ThenInclude(p => p.CoSo)
                .Where(b => b.TrangThai == "Hiển thị")
                .OrderByDescending(b => b.NgayDang)
                .ToListAsync();

            return View(baiDangs);
        }
        // Chi tiết phòng
        public async Task<IActionResult> ChiTietPhong(int? id)
        {
            if (id == null) return NotFound();

            var phong = await _context.Phong
                .Include(p => p.CoSo)
                .FirstOrDefaultAsync(p => p.MaPhong == id);

            if (phong == null) return NotFound();

            var images = await _context.PhongImages
                .Where(i => i.MaPhong == id)
                .OrderByDescending(i => i.IsMain)
                .ThenByDescending(i => i.NgayUpload)
                .ToListAsync();

            var baiDang = await _context.BaiDang
                .FirstOrDefaultAsync(b => b.MaPhong == id && b.TrangThai == "Hiển thị");

            ViewBag.Images = images;
            ViewBag.BaiDang = baiDang;

            return View(phong);
        }

        // Chi tiết bài đăng (chuyển sang chi tiết phòng)
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