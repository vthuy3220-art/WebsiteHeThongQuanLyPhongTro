using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class HoaDonController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HoaDonController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==================== DANH SÁCH HÓA ĐƠN ====================
        public async Task<IActionResult> Index(string searchString, int? thang, int? nam, string trangThai)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Index", "Login");

            var hoaDons = _context.HoaDon
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.PhongNavigation)
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.KhachHangNavigation)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                hoaDons = hoaDons.Where(h =>
                    (h.HopDongNavigation != null && h.HopDongNavigation.KhachHangNavigation != null &&
                     h.HopDongNavigation.KhachHangNavigation.HoTen.Contains(searchString)) ||
                    (h.HopDongNavigation != null && h.HopDongNavigation.PhongNavigation != null &&
                     h.HopDongNavigation.PhongNavigation.TenPhong.Contains(searchString)));
            }

            if (thang.HasValue) hoaDons = hoaDons.Where(h => h.Thang == thang.Value);
            if (nam.HasValue) hoaDons = hoaDons.Where(h => h.Nam == nam.Value);
            if (!string.IsNullOrEmpty(trangThai) && trangThai != "Tất cả")
                hoaDons = hoaDons.Where(h => h.TrangThai == trangThai);

            ViewBag.SearchString = searchString;
            ViewBag.Thang = thang;
            ViewBag.Nam = nam;
            ViewBag.TrangThai = trangThai;
            ViewBag.TrangThaiList = new List<string> { "Tất cả", "Chưa thanh toán", "Đã thanh toán" };
            ViewBag.NamList = await _context.HoaDon.Select(h => h.Nam).Distinct().OrderByDescending(n => n).ToListAsync();

            return View(await hoaDons.ToListAsync());
        }

        // ==================== CHI TIẾT HÓA ĐƠN ====================
        public async Task<IActionResult> Details(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Index", "Login");

            if (id == null) return NotFound();

            var hoaDon = await _context.HoaDon
                .FirstOrDefaultAsync(m => m.MaHoaDon == id);
            if (hoaDon == null) return NotFound();

            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Include(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(h => h.MaHopDong == hoaDon.MaHopDong);

            var chiTietHoaDons = await _context.ChiTietHoaDon
                .Where(c => c.MaHoaDon == id)
                .ToListAsync();

            // Lấy số người từ bảng NguoiOHopDong
            int soNguoi = 1;
            if (hopDong != null)
            {
                int soNguoiO = await _context.NguoiOHopDong
                    .CountAsync(n => n.MaHopDong == hopDong.MaHopDong);
                soNguoi = soNguoiO + 1;
            }

            // ========== TỰ ĐỘNG LẤY CHỈ SỐ CŨ TỪ THÁNG TRƯỚC ==========
            decimal chiSoDienCu = 0;
            decimal chiSoNuocCu = 0;

            if (hopDong != null && hoaDon.Thang > 1)
            {
                int thangTruoc = hoaDon.Thang - 1;
                int namTruoc = hoaDon.Nam;
                if (thangTruoc == 0)
                {
                    thangTruoc = 12;
                    namTruoc = hoaDon.Nam - 1;
                }

                // Tìm lịch sử tháng trước
                var lichSuThangTruoc = await _context.LichSuChiSoDienNuoc
                    .FirstOrDefaultAsync(l => l.MaPhong == hopDong.MaPhong && l.Thang == thangTruoc && l.Nam == namTruoc);

                if (lichSuThangTruoc != null)
                {
                    chiSoDienCu = lichSuThangTruoc.ChiSoDienMoi;
                    chiSoNuocCu = lichSuThangTruoc.ChiSoNuocMoi;
                }
            }

            // Lấy chỉ số mới từ chi tiết hóa đơn hiện tại (nếu có)
            var chiSoDienMoi = chiTietHoaDons?.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số điện mới")?.SoLuong ?? chiSoDienCu;
            var chiSoNuocMoi = chiTietHoaDons?.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số nước mới")?.SoLuong ?? chiSoNuocCu;
            var giaDien = chiTietHoaDons?.FirstOrDefault(c => c.LoaiKhoanThu == "Đơn giá điện")?.DonGia ?? 3500;
            var giaNuoc = chiTietHoaDons?.FirstOrDefault(c => c.LoaiKhoanThu == "Đơn giá nước")?.DonGia ?? 15000;
            var tienPhatSinh = chiTietHoaDons?.FirstOrDefault(c => c.LoaiKhoanThu == "Phí phát sinh")?.ThanhTien ?? 0;
            // ========== KẾT THÚC ==========

            ViewBag.HopDong = hopDong;
            ViewBag.ChiTietHoaDons = chiTietHoaDons;
            ViewBag.SoNguoi = soNguoi;

            // Truyền giá trị sang View
            ViewBag.ChiSoDienCu = chiSoDienCu;
            ViewBag.ChiSoDienMoi = chiSoDienMoi;
            ViewBag.ChiSoNuocCu = chiSoNuocCu;
            ViewBag.ChiSoNuocMoi = chiSoNuocMoi;
            ViewBag.GiaDien = giaDien;
            ViewBag.GiaNuoc = giaNuoc;
            ViewBag.TienPhatSinh = tienPhatSinh;

            return View(hoaDon);
        }

        // ==================== CẬP NHẬT CHI TIẾT HÓA ĐƠN ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CapNhatChiTietHoaDon(
     int maHoaDon, int soNguoi,
     decimal chiSoDienMoi, decimal chiSoNuocMoi,
     decimal giaDien, decimal giaNuoc,
     decimal tienPhatSinh)
        {
            var hoaDon = await _context.HoaDon.FindAsync(maHoaDon);
            if (hoaDon == null) return RedirectToAction(nameof(Index));

            if (hoaDon.TrangThai == "Đã thanh toán")
            {
                TempData["Error"] = "Hóa đơn đã thanh toán, không thể cập nhật!";
                return RedirectToAction(nameof(Details), new { id = maHoaDon });
            }

            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .FirstOrDefaultAsync(h => h.MaHopDong == hoaDon.MaHopDong);

            var maPhong = hopDong?.MaPhong ?? 0;
            var giaPhong = hopDong?.PhongNavigation?.GiaPhong ?? 0;
            var thangHienTai = hoaDon.Thang;
            var namHienTai = hoaDon.Nam;

            // Lấy chỉ số cũ từ tháng trước
            decimal chiSoDienCu = 0;
            decimal chiSoNuocCu = 0;

            int thangTruoc = thangHienTai == 1 ? 12 : thangHienTai - 1;
            int namTruoc = thangHienTai == 1 ? namHienTai - 1 : namHienTai;

            var lichSuThangTruoc = await _context.LichSuChiSoDienNuoc
                .FirstOrDefaultAsync(l => l.MaPhong == maPhong && l.Thang == thangTruoc && l.Nam == namTruoc);

            if (lichSuThangTruoc != null)
            {
                chiSoDienCu = lichSuThangTruoc.ChiSoDienMoi;
                chiSoNuocCu = lichSuThangTruoc.ChiSoNuocMoi;
            }

            // Tính toán
            decimal soDien = Math.Max(0, chiSoDienMoi - chiSoDienCu);
            decimal soNuoc = Math.Max(0, chiSoNuocMoi - chiSoNuocCu);
            decimal tienDien = soDien * giaDien;
            decimal tienNuoc = soNuoc * giaNuoc;
            decimal tienDichVu = soNguoi * 200000;
            decimal tongCong = giaPhong + tienDien + tienNuoc + tienDichVu + tienPhatSinh;

            // Lưu lịch sử
            var lichSu = await _context.LichSuChiSoDienNuoc
                .FirstOrDefaultAsync(l => l.MaPhong == maPhong && l.Thang == thangHienTai && l.Nam == namHienTai);

            if (lichSu == null)
            {
                lichSu = new LichSuChiSoDienNuoc
                {
                    MaPhong = maPhong,
                    Thang = thangHienTai,
                    Nam = namHienTai,
                    ChiSoDienCu = chiSoDienCu,
                    ChiSoDienMoi = chiSoDienMoi,
                    ChiSoNuocCu = chiSoNuocCu,
                    ChiSoNuocMoi = chiSoNuocMoi,
                    NgayGhi = DateTime.Now
                };
                _context.LichSuChiSoDienNuoc.Add(lichSu);
            }
            else
            {
                lichSu.ChiSoDienMoi = chiSoDienMoi;
                lichSu.ChiSoNuocMoi = chiSoNuocMoi;
                _context.Update(lichSu);
            }

            // Xóa chi tiết cũ và thêm mới
            var oldDetails = await _context.ChiTietHoaDon
                .Where(c => c.MaHoaDon == maHoaDon)
                .ToListAsync();
            _context.ChiTietHoaDon.RemoveRange(oldDetails);

            var chiTietList = new List<ChiTietHoaDon>
    {
        new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Tiền phòng", SoLuong = 1, DonGia = giaPhong, ThanhTien = giaPhong },
        new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Chỉ số điện cũ", SoLuong = chiSoDienCu },
        new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Chỉ số điện mới", SoLuong = chiSoDienMoi },
        new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Đơn giá điện", DonGia = giaDien },
        new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Chỉ số nước cũ", SoLuong = chiSoNuocCu },
        new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Chỉ số nước mới", SoLuong = chiSoNuocMoi },
        new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Đơn giá nước", DonGia = giaNuoc },
        new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Tiền điện", ThanhTien = tienDien },
        new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Tiền nước", ThanhTien = tienNuoc },
        new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Phí dịch vụ", ThanhTien = tienDichVu, GhiChu = $"{soNguoi} người" }
    };
            if (tienPhatSinh > 0)
            {
                chiTietList.Add(new ChiTietHoaDon { MaHoaDon = maHoaDon, LoaiKhoanThu = "Phí phát sinh", ThanhTien = tienPhatSinh });
            }

            _context.ChiTietHoaDon.AddRange(chiTietList);
            hoaDon.TongTien = tongCong;
            _context.Update(hoaDon);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Cập nhật thành công! Điện cũ: {chiSoDienCu}, Nước cũ: {chiSoNuocCu}";
            return RedirectToAction(nameof(Details), new { id = maHoaDon });
        }

        // ==================== THANH TOÁN HÓA ĐƠN ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThanhToan(int id, decimal soTien, string noiDung)
        {
            var hoaDon = await _context.HoaDon.FindAsync(id);
            if (hoaDon == null) return NotFound();

            if (hoaDon.TrangThai == "Đã thanh toán")
            {
                TempData["Error"] = "Hóa đơn này đã được thanh toán!";
                return RedirectToAction(nameof(Index));
            }

            var thanhToan = new ThanhToan
            {
                MaHoaDon = id,
                SoTien = soTien,
                NgayThanhToan = DateTime.Now,
                NoiDungChuyenKhoan = noiDung,
                TrangThai = "Thành công"
            };
            _context.ThanhToan.Add(thanhToan);

            hoaDon.TrangThai = "Đã thanh toán";
            _context.Update(hoaDon);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Thanh toán thành công {soTien:N0} đ!";
            return RedirectToAction(nameof(Index));
        }

        // ==================== XUẤT PDF HÓA ĐƠN ====================
        public async Task<IActionResult> XuatPdf(int id)
        {
            var hoaDon = await _context.HoaDon
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.PhongNavigation)
                .Include(h => h.HopDongNavigation)
                    .ThenInclude(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(h => h.MaHoaDon == id);

            if (hoaDon == null) return NotFound();

            var chiTietHoaDons = await _context.ChiTietHoaDon
                .Where(c => c.MaHoaDon == id)
                .ToListAsync();

            var phong = hoaDon.HopDongNavigation?.PhongNavigation;
            var khachHang = hoaDon.HopDongNavigation?.KhachHangNavigation;

            // Lấy số người từ bảng NguoiOHopDong
            int soNguoiO = await _context.NguoiOHopDong
                .CountAsync(n => n.MaHopDong == hoaDon.MaHopDong);
            int soNguoi = soNguoiO + 1;

            var chiSoDienCu = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số điện cũ")?.SoLuong ?? 0;
            var chiSoDienMoi = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số điện mới")?.SoLuong ?? 0;
            var chiSoNuocCu = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số nước cũ")?.SoLuong ?? 0;
            var chiSoNuocMoi = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Chỉ số nước mới")?.SoLuong ?? 0;
            var giaDien = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Đơn giá điện")?.DonGia ?? 3500;
            var giaNuoc = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Đơn giá nước")?.DonGia ?? 15000;
            var tienDien = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Tiền điện")?.ThanhTien ?? 0;
            var tienNuoc = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Tiền nước")?.ThanhTien ?? 0;
            var tienPhatSinh = chiTietHoaDons.FirstOrDefault(c => c.LoaiKhoanThu == "Phí phát sinh")?.ThanhTien ?? 0;

            string htmlContent = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='UTF-8'>
                <title>HÓA ĐƠN</title>
                <style>
                    body {{ font-family: Arial, sans-serif; margin: 30px; }}
                    .header {{ text-align: center; margin-bottom: 20px; }}
                    .info {{ margin-bottom: 20px; }}
                    .info table {{ width: 100%; }}
                    .info td {{ padding: 4px 0; }}
                    table {{ width: 100%; border-collapse: collapse; margin-bottom: 20px; }}
                    th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
                    th {{ background: #f8f9fa; }}
                    .text-right {{ text-align: right; }}
                    .footer {{ text-align: center; font-size: 12px; margin-top: 20px; padding-top: 10px; border-top: 1px solid #ddd; }}
                    .total {{ font-weight: bold; font-size: 16px; }}
                </style>
            </head>
            <body>
                <div class='header'>
                    <h1>PHÒNG TRỌ XINH</h1>
                    <p>Quản lý phòng trọ</p>
                    <hr />
                    <h2>HÓA ĐƠN TIỀN PHÒNG</h2>
                    <p><strong>Mã hóa đơn: {hoaDon.MaHoaDon.ToString("00000")}</strong></p>
                </div>
                <div class='info'>
                    <table>
                        <tr><td style='width:120px'><strong>Phòng:</strong></td><td>{phong?.TenPhong}</td></tr>
                        <tr><td><strong>Khách hàng:</strong></td><td>{khachHang?.HoTen}</td></tr>
                        <tr><td><strong>Email:</strong></td><td>{khachHang?.Email}</td></tr>
                        <tr><td><strong>Số điện thoại:</strong></td><td>{khachHang?.SoDienThoai}</td></tr>
                        <tr><td><strong>Ngày lập:</strong></td><td>{hoaDon.NgayTao:dd/MM/yyyy}</td></tr>
                        <tr><td><strong>Hạn thanh toán:</strong></td><td style='color:red'>{hoaDon.NgayTao?.AddDays(7):dd/MM/yyyy}</td></tr>
                    </table>
                </div>
                <table>
                    <thead>
                        <tr><th>Tiêu đề</th><th>Mô tả</th><th class='text-right'>Giá</th><th class='text-right'>Tổng</th></tr>
                    </thead>
                    <tbody>
                        <tr><td>Tiền phòng</td><td>{phong?.TenPhong} - tháng {hoaDon.Thang}/{hoaDon.Nam}</td><td class='text-right'>{phong?.GiaPhong:N0} đ</td><td class='text-right'>{phong?.GiaPhong:N0} đ</td></tr>
                        <tr><td>Tiền điện</td><td>Số điện: {(chiSoDienMoi - chiSoDienCu).ToString("N0")} kWh × {giaDien:N0} đ</td><td class='text-right'>{tienDien:N0} đ</td><td class='text-right'>{tienDien:N0} đ</td></tr>
                        <tr><td>Tiền nước</td><td>Số nước: {(chiSoNuocMoi - chiSoNuocCu).ToString("N0")} m³ × {giaNuoc:N0} đ</td><td class='text-right'>{tienNuoc:N0} đ</td><td class='text-right'>{tienNuoc:N0} đ</td></tr>
                        <tr><td>Phí dịch vụ</td><td>{soNguoi} người × 200.000đ</td><td class='text-right'>{(soNguoi * 200000):N0} đ</td><td class='text-right'>{(soNguoi * 200000):N0} đ</td></tr>
                        {(tienPhatSinh > 0 ? $"<tr><td>Phí phát sinh</td><td>Sửa chữa/phát sinh</td><td class='text-right'>{tienPhatSinh:N0} đ</td><td class='text-right'>{tienPhatSinh:N0} đ</td></tr>" : "")}
                    </tbody>
                    <tfoot>
                        <tr><td colspan='3' class='text-right total'>Tổng cộng:</td><td class='text-right total'>{hoaDon.TongTien:N0} đ</td></tr>
                    </tfoot>
                </table>
                <div class='footer'>
                    <p>Cảm ơn bạn đã hợp tác kinh doanh với chúng tôi!</p>
                    <p><strong>Phòng Trọ Xinh</strong> - Nơi an cư, nghiệp vững</p>
                </div>
            </body>
            </html>";

            await new BrowserFetcher().DownloadAsync();
            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(htmlContent);
            var pdfBytes = await page.PdfDataAsync();

            return File(pdfBytes, "application/pdf", $"HoaDon_{hoaDon.MaHoaDon}.pdf");
        }

        // ==================== TẠO HÓA ĐƠN HÀNG LOẠT ====================
        public async Task<IActionResult> TaoHangLoat()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Index", "Login");

            var thangHienTai = DateTime.Now.Month;
            var namHienTai = DateTime.Now.Year;

            var hopDongs = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .Where(h => h.TrangThai == "Hiệu lực")
                .ToListAsync();

            int dem = 0;
            foreach (var hopDong in hopDongs)
            {
                var exists = await _context.HoaDon
                    .AnyAsync(h => h.MaHopDong == hopDong.MaHopDong && h.Thang == thangHienTai && h.Nam == namHienTai);

                if (!exists && hopDong.PhongNavigation != null)
                {
                    var hoaDon = new HoaDon
                    {
                        MaHopDong = hopDong.MaHopDong,
                        Thang = thangHienTai,
                        Nam = namHienTai,
                        TongTien = hopDong.PhongNavigation.GiaPhong,
                        TrangThai = "Chưa thanh toán",
                        NgayTao = DateTime.Now
                    };
                    _context.HoaDon.Add(hoaDon);
                    dem++;
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã tạo {dem} hóa đơn cho tháng {thangHienTai}/{namHienTai}!";
            return RedirectToAction(nameof(Index));
        }

        // ==================== XÓA HÓA ĐƠN ====================
        public async Task<IActionResult> Delete(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Index", "Login");

            if (id == null) return NotFound();

            var hoaDon = await _context.HoaDon
                .Include(h => h.HopDongNavigation)
                .FirstOrDefaultAsync(m => m.MaHoaDon == id);

            if (hoaDon == null) return NotFound();

            if (hoaDon.TrangThai == "Đã thanh toán")
            {
                TempData["Error"] = "Hóa đơn đã thanh toán không thể xóa!";
                return RedirectToAction(nameof(Index));
            }

            return View(hoaDon);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var hoaDon = await _context.HoaDon.FindAsync(id);
            if (hoaDon != null)
            {
                if (hoaDon.TrangThai == "Đã thanh toán")
                {
                    TempData["Error"] = "Không thể xóa hóa đơn đã thanh toán!";
                    return RedirectToAction(nameof(Index));
                }

                var chiTiets = await _context.ChiTietHoaDon.Where(c => c.MaHoaDon == id).ToListAsync();
                _context.ChiTietHoaDon.RemoveRange(chiTiets);
                _context.HoaDon.Remove(hoaDon);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa hóa đơn thành công!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}