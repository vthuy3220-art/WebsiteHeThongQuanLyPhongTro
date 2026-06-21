using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

        // GET: Danh sách phòng
        // GET: Danh sách phòng
        public async Task<IActionResult> Index(string searchString, string trangThai)
        {
            if (GetCurrentUserId() == 0) return RedirectToAction("Index", "Login");

            var userId = GetCurrentUserId();

            var phongs = _context.Phong
                .Include(p => p.ToaNha)
                    .ThenInclude(t => t.CoSo)

                .AsQueryable();

            // Phân quyền: Chủ trọ chỉ thấy phòng của mình
            if (IsChuTro())
            {
                phongs = phongs.Where(p => p.MaChuTro == userId);
            }

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

            return View(await phongs.OrderBy(p => p.MaPhong).ToListAsync());
        }

        // GET: Chi tiết phòng

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || id.Value <= 0) return NotFound();


            var phong = await _context.Phong
                .Include(p => p.ToaNha)
                    .ThenInclude(t => t.CoSo)
                .FirstOrDefaultAsync(m => m.MaPhong == id.Value);

            if (phong == null) return NotFound();


            if (IsChuTro() && phong.MaChuTro != GetCurrentUserId())
            {
                TempData["Error"] = "Bạn không có quyền xem phòng này!";
                return RedirectToAction(nameof(Index));
            }


            var csvcList = await _context.CoSoVatChat
                .Where(c => c.MaPhong == id.Value)
                .ToListAsync();
            ViewBag.CSVCNhanh = csvcList;


            var images = await _context.PhongImage
                .Where(i => i.MaPhong == phong.MaPhong) // Ép chuẩn tìm theo đúng khóa chính MaPhong
                .OrderByDescending(i => i.IsMain)
                .ThenByDescending(i => i.NgayUpload)
                .ToListAsync();


            ViewBag.Images = images;

            return View(phong);
        }

        // GET: Thêm phòng mới
        public async Task<IActionResult> Create()
        {
            if (GetCurrentUserId() == 0) return RedirectToAction("Index", "Login");

            var userId = GetCurrentUserId();

            var toaNhas = _context.ToaNha.Include(t => t.CoSo).AsQueryable();
            if (IsChuTro())
            {
                toaNhas = toaNhas.Where(t => t.MaChuTro == userId);
            }
            ViewBag.ToaNhaList = await toaNhas.ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Phong phong)
        {
            var userId = GetCurrentUserId();

            if (phong.MaToaNha <= 0)
            {
                ModelState.AddModelError("MaToaNha", "Vui lòng chọn tòa nhà");
            }
            else if (IsChuTro())
            {
                var toaNhaHopLe = await _context.ToaNha
                    .AnyAsync(t => t.MaToaNha == phong.MaToaNha && t.MaChuTro == userId);

                if (!toaNhaHopLe)
                {
                    ModelState.AddModelError("MaToaNha", "Tòa nhà không hợp lệ");
                }
            }

            if (ModelState.IsValid)
            {
                if (IsChuTro())
                {
                    phong.MaChuTro = userId;
                }

                phong.TrangThai = string.IsNullOrEmpty(phong.TrangThai) ? "Trống" : phong.TrangThai;
                phong.TenPhong = phong.TenPhong ?? "";
                phong.GiaPhong = phong.GiaPhong > 0 ? phong.GiaPhong : 0;
                phong.DienTich = phong.DienTich > 0 ? phong.DienTich : 0;

                _context.Add(phong);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm phòng thành công!";
                return RedirectToAction(nameof(Index));
            }

            var toaNhas = _context.ToaNha.Include(t => t.CoSo).AsQueryable();
            if (IsChuTro())
            {
                toaNhas = toaNhas.Where(t => t.MaChuTro == userId);
            }
            ViewBag.ToaNhaList = await toaNhas.ToListAsync();
            return View(phong);
        }

        // GET: Chỉnh sửa phòng
        public async Task<IActionResult> Edit(int? id)
        {
            if (GetCurrentUserId() == 0) return RedirectToAction("Index", "Login");
            if (id == null || id.Value <= 0) return NotFound();

            var phong = await _context.Phong
                .Include(p => p.ToaNha)
                .FirstOrDefaultAsync(p => p.MaPhong == id.Value);

            if (phong == null) return NotFound();

            // Kiểm tra quyền
            if (IsChuTro() && phong.MaChuTro != GetCurrentUserId())
            {
                TempData["Error"] = "Bạn không có quyền sửa phòng này!";
                return RedirectToAction(nameof(Index));
            }

            var toaNhas = _context.ToaNha.Include(t => t.CoSo).AsQueryable();
            if (IsChuTro())
            {
                toaNhas = toaNhas.Where(t => t.MaChuTro == GetCurrentUserId());
            }
            ViewBag.ToaNhaList = await toaNhas.ToListAsync();

            return View(phong);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Phong phong)
        {
            if (id != phong.MaPhong) return NotFound();

            var phongCu = await _context.Phong.FindAsync(id);
            if (phongCu == null) return NotFound();

            // Kiểm tra quyền sở hữu phòng
            if (IsChuTro() && phongCu.MaChuTro != GetCurrentUserId())
            {
                TempData["Error"] = "Bạn không có quyền sửa phòng này!";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra tòa nhà được chọn có thuộc chủ trọ hiện tại không
            if (IsChuTro())
            {
                var toaNhaHopLe = await _context.ToaNha
                    .AnyAsync(t => t.MaToaNha == phong.MaToaNha && t.MaChuTro == GetCurrentUserId());

                if (!toaNhaHopLe)
                {
                    ModelState.AddModelError("MaToaNha", "Tòa nhà không hợp lệ");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    phongCu.TenPhong = phong.TenPhong ?? "";
                    phongCu.MaToaNha = phong.MaToaNha;
                    phongCu.GiaPhong = phong.GiaPhong > 0 ? phong.GiaPhong : 0;
                    phongCu.DienTich = phong.DienTich > 0 ? phong.DienTich : 0;
                    phongCu.TrangThai = string.IsNullOrEmpty(phong.TrangThai) ? "Trống" : phong.TrangThai;

                    _context.Update(phongCu);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật phòng thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PhongExists(phong.MaPhong)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            var toaNhas = _context.ToaNha.Include(t => t.CoSo).AsQueryable();
            if (IsChuTro())
            {
                toaNhas = toaNhas.Where(t => t.MaChuTro == GetCurrentUserId());
            }
            ViewBag.ToaNhaList = await toaNhas.ToListAsync();
            return View(phong);
        }

        // GET: Xóa phòng

        public async Task<IActionResult> Delete(int? id)
        {
            if (GetCurrentUserId() == 0) return RedirectToAction("Index", "Login");
            if (id == null || id.Value <= 0) return NotFound();

            var phong = await _context.Phong.FindAsync(id.Value);
            if (phong == null) return NotFound();

            if (IsChuTro() && phong.MaChuTro != GetCurrentUserId())
            {
                TempData["Error"] = "Bạn không có quyền xóa phòng này!";
                return RedirectToAction(nameof(Index));
            }

            var coHopDongHieuLuc = await _context.HopDong.AnyAsync(h => h.MaPhong == id.Value && h.TrangThai == "Hiệu lực");
            if (coHopDongHieuLuc)
            {
                TempData["Error"] = "Không thể xóa phòng trọ này vì đang dính hợp đồng thuê còn hiệu lực!";
                return RedirectToAction(nameof(Index));
            }

            var danhSachCSVC = _context.CoSoVatChat.Where(csvc => csvc.MaPhong == id.Value);
            _context.CoSoVatChat.RemoveRange(danhSachCSVC);

            _context.Phong.Remove(phong);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Xóa phòng trọ ra khỏi danh sách quản lý thành công!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Phong/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var phong = await _context.Phong.FindAsync(id);

            if (phong == null)
            {
                return NotFound();
            }

            // Kiểm tra quyền: chủ trọ chỉ được xóa phòng của chính mình
            if (IsChuTro() && phong.MaChuTro != GetCurrentUserId())
            {
                TempData["Error"] = "Bạn không có quyền xóa phòng này!";
                return RedirectToAction(nameof(Index));
            }

            var danhSachCSVC = _context.CoSoVatChat.Where(csvc => csvc.MaPhong == id);
            _context.CoSoVatChat.RemoveRange(danhSachCSVC);

            _context.Phong.Remove(phong);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Xóa phòng thành công!";

            return RedirectToAction(nameof(Index));
        }

        // ==================== UPLOAD ẢNH ====================

        [HttpPost]
        public async Task<IActionResult> UploadImage(int maPhong, IFormFile file, bool isMain = false) // ← Sửa tham số nhận vào thành maPhong
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file ảnh!";
                return RedirectToAction("Details", new { id = maPhong });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Chỉ chấp nhận file ảnh (jpg, jpeg, png, gif, webp)!";
                return RedirectToAction("Details", new { id = maPhong });
            }

            var uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "phongs");
            if (!Directory.Exists(uploadFolder))
                Directory.CreateDirectory(uploadFolder);

            var fileName = $"{maPhong}_{DateTime.Now.Ticks}{extension}";
            var filePath = Path.Combine(uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            if (isMain)
            {
                var oldMain = await _context.PhongImage
                    .FirstOrDefaultAsync(i => i.MaPhong == maPhong && i.IsMain);
                if (oldMain != null)
                    oldMain.IsMain = false;
            }

            var phongImage = new PhongImage
            {
                MaPhong = maPhong, // ← Lưu chuẩn theo mã phòng
                ImagePath = $"/images/phongs/{fileName}",
                IsMain = isMain,
                NgayUpload = DateTime.Now
            };

            _context.PhongImage.Add(phongImage);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Upload ảnh thành công!";
            return RedirectToAction("Details", new { id = maPhong });
        }

        // GET: Xóa ảnh
        [HttpGet]
        public async Task<IActionResult> DeleteImage(int? id)
        {
            if (GetCurrentUserId() == 0) return RedirectToAction("Index", "Login");
            if (id == null || id.Value <= 0) return NotFound();

            var image = await _context.PhongImage.FindAsync(id.Value);
            if (image == null) return NotFound();

            return View(image);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteImage(int maImage, int maPhong)
        {
            if (maImage <= 0)
            {
                TempData["Error"] = "Không tìm thấy ảnh!";
                return RedirectToAction("Details", new { id = maPhong });
            }

            var image = await _context.PhongImage.FindAsync(maImage);
            if (image != null)
            {
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, image.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                _context.PhongImage.Remove(image);
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
                var userId = GetCurrentUserId();

                var phongsQuery = _context.Phong
                    .Include(p => p.ToaNha)
                        .ThenInclude(t => t.CoSo)
                    .AsQueryable();

                if (IsChuTro())
                {
                    phongsQuery = phongsQuery.Where(p => p.MaChuTro == userId);
                }

                var phongs = await phongsQuery.ToListAsync();

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Danh sách phòng");

                    worksheet.Cells[1, 1].Value = "Mã phòng";
                    worksheet.Cells[1, 2].Value = "Tên phòng";
                    worksheet.Cells[1, 3].Value = "Tòa nhà";
                    worksheet.Cells[1, 4].Value = "Cơ sở";
                    worksheet.Cells[1, 5].Value = "Giá phòng";
                    worksheet.Cells[1, 6].Value = "Diện tích";
                    worksheet.Cells[1, 7].Value = "Trạng thái";

                    using (var range = worksheet.Cells[1, 1, 1, 7])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }

                    int row = 2;
                    foreach (var phong in phongs)
                    {
                        worksheet.Cells[row, 1].Value = phong.MaPhong;
                        worksheet.Cells[row, 2].Value = phong.TenPhong ?? "";
                        worksheet.Cells[row, 3].Value = phong.ToaNha?.TenToaNha ?? "";
                        worksheet.Cells[row, 4].Value = phong.ToaNha?.CoSo?.TenCoSo ?? "";
                        worksheet.Cells[row, 5].Value = phong.GiaPhong;
                        worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0";
                        worksheet.Cells[row, 6].Value = phong.DienTich ?? 0;
                        worksheet.Cells[row, 7].Value = phong.TrangThai ?? "Trống";
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