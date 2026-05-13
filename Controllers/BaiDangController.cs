using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class BaiDangController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public BaiDangController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Danh sách bài đăng
        public async Task<IActionResult> Index(string searchString, string trangThai)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var baiDangs = _context.BaiDang
                .Include(b => b.PhongNavigation)
                .ThenInclude(p => p.CoSo)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                baiDangs = baiDangs.Where(b =>
                    (b.TieuDe != null && b.TieuDe.Contains(searchString)) ||
                    (b.PhongNavigation != null && b.PhongNavigation.TenPhong.Contains(searchString)));
            }

            if (!string.IsNullOrEmpty(trangThai) && trangThai != "Tất cả")
            {
                baiDangs = baiDangs.Where(b => b.TrangThai == trangThai);
            }

            ViewBag.SearchString = searchString;
            ViewBag.TrangThai = trangThai;
            ViewBag.TrangThaiList = new List<string> { "Tất cả", "Hiển thị", "Ẩn" };

            return View(await baiDangs.OrderByDescending(b => b.NgayDang).ToListAsync());
        }

        // GET: Chi tiết bài đăng
        public async Task<IActionResult> Details(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            var baiDang = await _context.BaiDang
                .Include(b => b.PhongNavigation)
                .ThenInclude(p => p.CoSo)
                .FirstOrDefaultAsync(m => m.MaBaiDang == id);

            if (baiDang == null)
            {
                return NotFound();
            }

            return View(baiDang);
        }

        // GET: Tạo bài đăng mới
        public IActionResult Create()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            ViewBag.PhongList = _context.Phong
                .Include(p => p.CoSo)
                .Where(p => p.TrangThai == "Trống")
                .ToList();
            return View();
        }

        // POST: Tạo bài đăng mới

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BaiDang baiDang, IFormFile? fileAnh)
        {
            if (ModelState.IsValid)
            {
                // Upload ảnh đại diện (nếu có)
                if (fileAnh != null && fileAnh.Length > 0)
                {
                    string uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "baidang");
                    if (!Directory.Exists(uploadFolder))
                        Directory.CreateDirectory(uploadFolder);

                    string fileName = $"bd_{DateTime.Now.Ticks}{Path.GetExtension(fileAnh.FileName)}";
                    string filePath = Path.Combine(uploadFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await fileAnh.CopyToAsync(stream);
                    }

                    baiDang.HinhAnh = $"/images/baidang/{fileName}";
                }

                baiDang.NgayDang = DateTime.Now;
                baiDang.TrangThai = "Hiển thị";
                _context.Add(baiDang);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đăng bài thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(baiDang);
        }
// GET: Chỉnh sửa bài đăng
public async Task<IActionResult> Edit(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Index", "Login");

            if (id == null) return NotFound();

            var baiDang = await _context.BaiDang
                .Include(b => b.PhongNavigation)
                .FirstOrDefaultAsync(m => m.MaBaiDang == id);

            if (baiDang == null) return NotFound();

            ViewBag.PhongList = _context.Phong
                .Include(p => p.CoSo)
                .Where(p => p.TrangThai == "Trống")
                .ToList();

            return View(baiDang);
        }

        // POST: Chỉnh sửa bài đăng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BaiDang baiDang, IFormFile? fileAnh)
        {
            if (id != baiDang.MaBaiDang) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingBaiDang = await _context.BaiDang.AsNoTracking()
                        .FirstOrDefaultAsync(b => b.MaBaiDang == id);

                    // Xử lý upload ảnh mới
                    if (fileAnh != null && fileAnh.Length > 0)
                    {
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                        var extension = Path.GetExtension(fileAnh.FileName).ToLower();

                        if (allowedExtensions.Contains(extension))
                        {
                            // Xóa ảnh cũ nếu có
                            if (!string.IsNullOrEmpty(existingBaiDang?.HinhAnh))
                            {
                                string oldPath = Path.Combine(_webHostEnvironment.WebRootPath, existingBaiDang.HinhAnh.TrimStart('/'));
                                if (System.IO.File.Exists(oldPath))
                                    System.IO.File.Delete(oldPath);
                            }

                            string uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "baidang");
                            if (!Directory.Exists(uploadFolder))
                                Directory.CreateDirectory(uploadFolder);

                            string fileName = $"bd_{DateTime.Now.Ticks}{extension}";
                            string filePath = Path.Combine(uploadFolder, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await fileAnh.CopyToAsync(stream);
                            }

                            baiDang.HinhAnh = $"/images/baidang/{fileName}";
                        }
                    }
                    else
                    {
                        // Giữ ảnh cũ
                        baiDang.HinhAnh = existingBaiDang?.HinhAnh;
                    }

                    // Giữ nguyên ngày đăng
                    baiDang.NgayDang = existingBaiDang?.NgayDang ?? DateTime.Now;

                    _context.Update(baiDang);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật bài đăng thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BaiDangExists(baiDang.MaBaiDang))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.PhongList = _context.Phong.Include(p => p.CoSo).Where(p => p.TrangThai == "Trống").ToList();
            return View(baiDang);
        }

        // GET: Xóa bài đăng
        public async Task<IActionResult> Delete(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            var baiDang = await _context.BaiDang
                .Include(b => b.PhongNavigation)
                .ThenInclude(p => p.CoSo)
                .FirstOrDefaultAsync(m => m.MaBaiDang == id);

            if (baiDang == null)
            {
                return NotFound();
            }

            return View(baiDang);
        }

        // POST: Xóa bài đăng
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var baiDang = await _context.BaiDang.FindAsync(id);
            if (baiDang != null)
            {
                // Xóa file ảnh
                if (!string.IsNullOrEmpty(baiDang.HinhAnh))
                {
                    var filePath = Path.Combine(_webHostEnvironment.WebRootPath, baiDang.HinhAnh.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);
                }

                _context.BaiDang.Remove(baiDang);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa bài đăng thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        // Ẩn/Hiện bài đăng
        [HttpPost]
        public async Task<IActionResult> ToggleTrangThai(int id)
        {
            var baiDang = await _context.BaiDang.FindAsync(id);
            if (baiDang == null)
            {
                return NotFound();
            }

            baiDang.TrangThai = baiDang.TrangThai == "Hiển thị" ? "Ẩn" : "Hiển thị";
            _context.Update(baiDang);
            await _context.SaveChangesAsync();

            return Json(new { success = true, trangThai = baiDang.TrangThai });
        }

        private bool BaiDangExists(int id)
        {
            return _context.BaiDang.Any(e => e.MaBaiDang == id);
        }
    }
}