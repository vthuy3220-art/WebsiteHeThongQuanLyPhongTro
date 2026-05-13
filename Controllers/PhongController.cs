using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using OfficeOpenXml;
using System.IO;
using System.Linq;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class PhongController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public PhongController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // ==================== DANH SÁCH PHÒNG ====================
        public async Task<IActionResult> Index(string searchString, string trangThai)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var phongs = _context.Phong
                .Include(p => p.CoSo)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                phongs = phongs.Where(p => p.TenPhong != null && p.TenPhong.Contains(searchString));
            }

            if (!string.IsNullOrEmpty(trangThai) && trangThai != "Tất cả")
            {
                phongs = phongs.Where(p => p.TrangThai != null && p.TrangThai == trangThai);
            }

            ViewBag.SearchString = searchString ?? "";
            ViewBag.TrangThai = trangThai ?? "Tất cả";
            ViewBag.TrangThaiList = new List<string> { "Tất cả", "Trống", "Đã thuê" };

            return View(await phongs.ToListAsync());
        }
              
        // ==================== CHI TIẾT PHÒNG ====================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var phong = await _context.Phong
                .Include(p => p.CoSo)
                .FirstOrDefaultAsync(m => m.MaPhong == id);

            if (phong == null) return NotFound();

            // Load TẤT CẢ ẢNH của phòng này (không phân biệt người dùng)
            var images = await _context.PhongImages
                .Where(i => i.MaPhong == id)
                .OrderByDescending(i => i.IsMain)      // Ảnh chính lên đầu
                .ThenByDescending(i => i.NgayUpload)   // Ảnh mới nhất sau
                .ToListAsync();

            ViewBag.Images = images;

            return View(phong);
        }
        // ==================== THÊM PHÒNG MỚI ====================
        public IActionResult Create()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            ViewBag.CoSoList = _context.CoSo.ToList() ?? new List<CoSo>();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Phong phong)
        {
            // Kiểm tra ModelState
            if (ModelState.IsValid)
            {
                // Xử lý null cho các trường
                phong.TrangThai = string.IsNullOrEmpty(phong.TrangThai) ? "Trống" : phong.TrangThai;
                phong.SoLuongNguoiO = (phong.SoLuongNguoiO.HasValue && phong.SoLuongNguoiO.Value > 0) ? phong.SoLuongNguoiO.Value : 1;
                phong.TenPhong = phong.TenPhong ?? "";
                phong.GiaPhong = phong.GiaPhong > 0 ? phong.GiaPhong : 0;
                phong.DienTich = phong.DienTich > 0 ? phong.DienTich : 0;

                _context.Add(phong);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm phòng thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CoSoList = _context.CoSo.ToList() ?? new List<CoSo>();
            return View(phong);
        }

        // ==================== CHỈNH SỬA PHÒNG ====================
        public async Task<IActionResult> Edit(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null || id.Value <= 0)
            {
                return NotFound();
            }

            var phong = await _context.Phong.FindAsync(id.Value);
            if (phong == null)
            {
                return NotFound();
            }

            ViewBag.CoSoList = _context.CoSo.ToList() ?? new List<CoSo>();
            return View(phong);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Phong phong)
        {
            if (id != phong.MaPhong)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Xử lý null
                    phong.TrangThai = string.IsNullOrEmpty(phong.TrangThai) ? "Trống" : phong.TrangThai;
                    phong.TenPhong = phong.TenPhong ?? "";

                    _context.Update(phong);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật phòng thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PhongExists(phong.MaPhong))
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

            ViewBag.CoSoList = _context.CoSo.ToList() ?? new List<CoSo>();
            return View(phong);
        }

        // ==================== XÓA PHÒNG ====================
        public async Task<IActionResult> Delete(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null || id.Value <= 0)
            {
                return NotFound();
            }

            var phong = await _context.Phong
                .Include(p => p.CoSo)
                .FirstOrDefaultAsync(m => m.MaPhong == id.Value);

            if (phong == null)
            {
                return NotFound();
            }

            return View(phong);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var phong = await _context.Phong.FindAsync(id);
            if (phong != null)
            {
                // Kiểm tra xem phòng có đang có hợp đồng không
                var coHopDong = await _context.HopDong
                    .AnyAsync(h => h.MaPhong == id && h.TrangThai == "Hiệu lực");

                if (coHopDong)
                {
                    TempData["Error"] = "Không thể xóa vì phòng này đang có hợp đồng hiệu lực!";
                    return RedirectToAction(nameof(Index));
                }

                _context.Phong.Remove(phong);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa phòng thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==================== UPLOAD ẢNH ====================
        [HttpPost]
        public async Task<IActionResult> UploadImage(int id, IFormFile file, bool isMain = false)
        {
            // Kiểm tra file
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file ảnh!";
                return RedirectToAction("Details", new { id });
            }

            // Kiểm tra định dạng
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Chỉ chấp nhận file ảnh (jpg, jpeg, png, gif, webp)!";
                return RedirectToAction("Details", new { id });
            }

            // Tạo thư mục
            var uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "phongs");
            if (!Directory.Exists(uploadFolder))
                Directory.CreateDirectory(uploadFolder);

            // Tạo tên file duy nhất
            var fileName = $"{id}_{DateTime.Now.Ticks}{extension}";
            var filePath = Path.Combine(uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Xóa ảnh chính cũ nếu có
            if (isMain)
            {
                var oldMain = await _context.PhongImages
                    .FirstOrDefaultAsync(i => i.MaPhong == id && i.IsMain);
                if (oldMain != null)
                    oldMain.IsMain = false;
            }

            // Lưu vào database
            var phongImage = new PhongImage
            {
                MaPhong = id,
                ImagePath = $"/images/phongs/{fileName}",
                IsMain = isMain,
                NgayUpload = DateTime.Now
            };

            _context.PhongImages.Add(phongImage);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Upload ảnh thành công!";
            return RedirectToAction("Details", new { id });
        }

        // ==================== XÓA ẢNH ====================
        [HttpPost]
        public async Task<IActionResult> DeleteImage(int maImage, int maPhong)
        {
            if (maImage <= 0)
            {
                TempData["Error"] = "Không tìm thấy ảnh!";
                return RedirectToAction("Details", new { id = maPhong });
            }

            var image = await _context.PhongImages.FindAsync(maImage);
            if (image != null)
            {
                // Xóa file vật lý
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, image.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                _context.PhongImages.Remove(image);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa ảnh thành công!";
            }
            else
            {
                TempData["Error"] = "Không tìm thấy ảnh!";
            }

            return RedirectToAction("Details", new { id = maPhong });
        }

        // ==================== EXPORT EXCEL ====================
        public async Task<IActionResult> ExportExcel()
        {
            try
            {
                // Cài đặt license (bỏ comment nếu cần)
                // ExcelPackage.License.SetNonCommercialLicense();

                var phongs = await _context.Phong
                    .Include(p => p.CoSo)
                    .ToListAsync();

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Danh sách phòng");

                    // Header
                    worksheet.Cells[1, 1].Value = "Mã phòng";
                    worksheet.Cells[1, 2].Value = "Tên phòng";
                    worksheet.Cells[1, 3].Value = "Cơ sở";
                    worksheet.Cells[1, 4].Value = "Giá phòng";
                    worksheet.Cells[1, 5].Value = "Diện tích";
                    worksheet.Cells[1, 6].Value = "Trạng thái";
                    worksheet.Cells[1, 7].Value = "Số người ở";

                    // Style header
                    using (var range = worksheet.Cells[1, 1, 1, 7])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }

                    // Data
                    int row = 2;
                    foreach (var phong in phongs)
                    {
                        worksheet.Cells[row, 1].Value = phong.MaPhong;
                        worksheet.Cells[row, 2].Value = phong.TenPhong ?? "";
                        worksheet.Cells[row, 3].Value = phong.CoSo?.TenCoSo ?? "";
                        worksheet.Cells[row, 4].Value = phong.GiaPhong;
                        worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0";
                        worksheet.Cells[row, 5].Value = phong.DienTich ?? 0;
                        worksheet.Cells[row, 6].Value = phong.TrangThai ?? "Trống";
                        worksheet.Cells[row, 7].Value = phong.SoLuongNguoiO ?? 1;
                        row++;
                    }

                    worksheet.Cells.AutoFitColumns();

                    var stream = new MemoryStream();
                    package.SaveAs(stream);
                    stream.Position = 0;

                    var fileName = $"DanhSachPhong_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                    return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Xuất Excel thất bại: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        private bool PhongExists(int id)
        {
            return _context.Phong.Any(e => e.MaPhong == id);
        }
    }
}