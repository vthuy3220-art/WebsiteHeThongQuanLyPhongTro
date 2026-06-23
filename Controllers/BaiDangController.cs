using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;

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

        private int GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        private string GetCurrentRole()
        {
            return HttpContext.Session.GetString("Role") ?? "";
        }

        private int GetCurrentMaChuTro()
        {
            return HttpContext.Session.GetInt32("MaChuTro") ?? 0;
        }

        // GET: BaiDang/Index
        public async Task<IActionResult> Index(string searchString, string trangThai)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");

            var query = _context.BaiDang
                .Include(b => b.PhongNavigation)
                    .ThenInclude(p => p.ToaNha)
                .AsQueryable();

            // PHÂN QUYỀN TUYỆT ĐỐI: Chủ trọ nào chỉ thấy bài đăng của chủ trọ đó
            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                query = query.Where(b => b.MaChuTro == maChuTro);
            }

            // Bộ lọc tìm kiếm theo tiêu đề hoặc tên phòng
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(b => b.TieuDe.Contains(searchString) || b.PhongNavigation.TenPhong.Contains(searchString));
            }

            // Bộ lọc trạng thái bài đăng
            if (!string.IsNullOrEmpty(trangThai) && trangThai != "Tất cả")
            {
                query = query.Where(b => b.TrangThai == trangThai);
            }

            // Cập nhật lại danh sách trạng thái hiển thị ở bộ lọc của Chủ trọ bao gồm cả "Chờ duyệt"
            ViewBag.TrangThaiList = new List<string> { "Tất cả", "Chờ duyệt", "Hiển thị", "Ẩn", "Từ chối" };
            ViewBag.SearchString = searchString;
            ViewBag.TrangThai = trangThai;
            ViewBag.Role = role;

            return View(await query.OrderByDescending(b => b.NgayDang).ToListAsync());
        }

        // GET: BaiDang/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");
            if (id == null) return NotFound();

            var baiDang = await _context.BaiDang
                .Include(b => b.PhongNavigation)
                    .ThenInclude(p => p.ToaNha)
                        .ThenInclude(t => t.CoSo)
                .FirstOrDefaultAsync(b => b.MaBaiDang == id);

            if (baiDang == null) return NotFound();

            // Kiểm tra quyền
            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                if (baiDang.MaChuTro != maChuTro)
                {
                    TempData["Error"] = "Bạn không có quyền xem bài đăng này!";
                    return RedirectToAction(nameof(Index));
                }
            }

            return View(baiDang);
        }

        // GET: BaiDang/Create
        public async Task<IActionResult> Create()
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");

            // Lấy danh sách phòng trống thuộc chủ trọ hiện tại
            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                ViewBag.PhongList = await _context.Phong
                    .Include(p => p.ToaNha)
                    .Where(p => p.ToaNha.MaChuTro == maChuTro && p.TrangThai == "Trống")
                    .ToListAsync();
            }
            else if (role == "SuperAdmin" || role == "Admin")
            {
                ViewBag.PhongList = await _context.Phong
                    .Include(p => p.ToaNha)
                    .Where(p => p.TrangThai == "Trống")
                    .ToListAsync();
            }
            else
            {
                return RedirectToAction("Index", "Login");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BaiDang baiDang, IFormFile? fileAnh)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");

            // Tự động gán MaChuTro
            if (role == "ChuTro")
            {
                baiDang.MaChuTro = GetCurrentMaChuTro();
            }
            else if (role == "SuperAdmin" || role == "Admin")
            {
                var phong = await _context.Phong
                    .Include(p => p.ToaNha)
                    .FirstOrDefaultAsync(p => p.MaPhong == baiDang.MaPhong);
                baiDang.MaChuTro = phong?.ToaNha?.MaChuTro ?? 0;
            }

            if (ModelState.IsValid)
            {
                if (fileAnh != null && fileAnh.Length > 0)
                {
                    try
                    {
                        string uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "baidang");
                        if (!Directory.Exists(uploadFolder))
                            Directory.CreateDirectory(uploadFolder);

                        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(fileAnh.FileName);
                        string filePath = Path.Combine(uploadFolder, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await fileAnh.CopyToAsync(stream);
                        }

                        baiDang.HinhAnh = "/images/baidang/" + fileName;
                    }
                    catch (Exception ex)
                    {
                        TempData["Error"] = $"Lỗi upload ảnh: {ex.Message}";
                    }
                }

                baiDang.NgayDang = DateTime.Now;

                // SỬA TẠI ĐÂY: Gán trạng thái Tiếng Việt chuẩn để đẩy vào hàng đợi của Admin kiểm duyệt
                baiDang.TrangThai = "Chờ duyệt";

                _context.Add(baiDang);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Gửi bài đăng thành công! Vui lòng chờ Ban quản trị phê duyệt.";
                return RedirectToAction(nameof(Index));
            }

            // Load lại danh sách phòng nếu có lỗi
            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                ViewBag.PhongList = await _context.Phong
                    .Include(p => p.ToaNha)
                    .Where(p => p.ToaNha.MaChuTro == maChuTro && p.TrangThai == "Trống")
                    .ToListAsync();
            }
            else
            {
                ViewBag.PhongList = await _context.Phong
                    .Include(p => p.ToaNha)
                    .Where(p => p.TrangThai == "Trống")
                    .ToListAsync();
            }

            return View(baiDang);
        }

        // GET: BaiDang/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");
            if (id == null) return NotFound();

            var baiDang = await _context.BaiDang
                .Include(b => b.PhongNavigation)
                .FirstOrDefaultAsync(b => b.MaBaiDang == id);

            if (baiDang == null) return NotFound();

            // Kiểm tra quyền
            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                if (baiDang.MaChuTro != maChuTro)
                {
                    TempData["Error"] = "Bạn không có quyền sửa bài đăng này!";
                    return RedirectToAction(nameof(Index));
                }
            }

            // Load danh sách phòng
            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                ViewBag.PhongList = await _context.Phong
                    .Include(p => p.ToaNha)
                    .Where(p => p.ToaNha.MaChuTro == maChuTro)
                    .ToListAsync();
            }
            else
            {
                ViewBag.PhongList = await _context.Phong
                    .Include(p => p.ToaNha)
                    .ToListAsync();
            }

            return View(baiDang);
        }

        // POST: BaiDang/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BaiDang baiDang, IFormFile? fileAnh)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");
            if (id != baiDang.MaBaiDang) return NotFound();

            var baiDangCu = await _context.BaiDang.FindAsync(id);
            if (baiDangCu == null) return NotFound();

            // Kiểm tra quyền
            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                if (baiDangCu.MaChuTro != maChuTro)
                {
                    TempData["Error"] = "Bạn không có quyền sửa bài đăng này!";
                    return RedirectToAction(nameof(Index));
                }
            }

            // Cập nhật thông tin
            baiDangCu.MaPhong = baiDang.MaPhong;
            baiDangCu.TieuDe = baiDang.TieuDe;
            baiDangCu.MoTa = baiDang.MoTa;
            baiDangCu.TrangThai = baiDang.TrangThai;

            // Xử lý upload ảnh mới
            if (fileAnh != null && fileAnh.Length > 0)
            {
                try
                {
                    // Xóa ảnh cũ
                    if (!string.IsNullOrEmpty(baiDangCu.HinhAnh))
                    {
                        string oldPath = Path.Combine(_webHostEnvironment.WebRootPath, baiDangCu.HinhAnh.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath))
                            System.IO.File.Delete(oldPath);
                    }

                    string uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "baidang");
                    if (!Directory.Exists(uploadFolder))
                        Directory.CreateDirectory(uploadFolder);

                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(fileAnh.FileName);
                    string filePath = Path.Combine(uploadFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await fileAnh.CopyToAsync(stream);
                    }

                    baiDangCu.HinhAnh = "/images/baidang/" + fileName;
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Lỗi upload ảnh: {ex.Message}";
                }
            }

            _context.Update(baiDangCu);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Cập nhật bài đăng thành công!";

            return RedirectToAction(nameof(Index));
        }

        // GET: BaiDang/Delete/5

        [HttpPost]
        public async Task<IActionResult> XoaBaiDangApi(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return Json(new { success = false, message = "Chưa đăng nhập" });

            var baiDang = await _context.BaiDang.FindAsync(id);
            if (baiDang == null) return Json(new { success = false, message = "Không tìm thấy bài đăng" });

            // Kiểm tra quyền
            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                if (baiDang.MaChuTro != maChuTro)
                {
                    return Json(new { success = false, message = "Bạn không có quyền xóa bài đăng này!" });
                }
            }

            // Xóa file ảnh vật lý trong thư mục (nếu có)
            if (!string.IsNullOrEmpty(baiDang.HinhAnh))
            {
                string filePath = Path.Combine(_webHostEnvironment.WebRootPath, baiDang.HinhAnh.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }

            // Xóa dữ liệu trong DB
            _context.BaiDang.Remove(baiDang);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // POST: BaiDang/ToggleTrangThai
        [HttpPost]
        public async Task<IActionResult> ToggleTrangThai(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return Json(new { success = false, message = "Chưa đăng nhập" });

            var baiDang = await _context.BaiDang.FindAsync(id);
            if (baiDang == null) return Json(new { success = false, message = "Không tìm thấy bài đăng" });

            // Kiểm tra quyền
            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                if (baiDang.MaChuTro != maChuTro)
                {
                    return Json(new { success = false, message = "Bạn không có quyền!" });
                }
            }

            baiDang.TrangThai = baiDang.TrangThai == "Hiển thị" ? "Ẩn" : "Hiển thị";
            await _context.SaveChangesAsync();

            return Json(new { success = true, trangThai = baiDang.TrangThai });
        }
        // POST: BaiDang/DuyetBaiDang (Cho Admin duyệt bài)
        [HttpPost]
        public async Task<IActionResult> DuyetBaiDang(int id, string actionType)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return Json(new { success = false, message = "Chưa đăng nhập" });
            if (role != "Admin" && role != "SuperAdmin")
                return Json(new { success = false, message = "Bạn không có quyền!" });

            var baiDang = await _context.BaiDang.FindAsync(id);
            if (baiDang == null) return Json(new { success = false, message = "Không tìm thấy bài đăng" });

            // ĐỒNG BỘ CHUỖI TRẠNG THÁI TIẾNG VIỆT ĐỂ NGOÀI TRANG CHỦ HIỂN THỊ ĐƯỢC NGAY
            if (actionType == "Approve")
            {
                baiDang.TrangThai = "Hiển thị"; // Sửa từ "Approved" -> "Hiển thị"
            }
            else if (actionType == "Reject")
            {
                baiDang.TrangThai = "Từ chối"; // Sửa từ "Rejected" -> "Từ chối"
            }
            else
            {
                return Json(new { success = false, message = "Hành động không hợp lệ" });
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, trangThai = baiDang.TrangThai });
        }
        private bool BaiDangExists(int id)
        {
            return _context.BaiDang.Any(e => e.MaBaiDang == id);
        }
    }
}