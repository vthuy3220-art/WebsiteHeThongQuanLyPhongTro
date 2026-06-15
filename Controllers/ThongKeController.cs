using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Rotativa.AspNetCore;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class ThongKeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ThongKeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            var model = new DashboardViewModel();

            model.TongSoPhong = await _context.Phong.CountAsync();
            model.SoPhongDaThue = await _context.Phong.CountAsync(p => p.TrangThai == "Đã thuê");
            model.SoPhongTrong = model.TongSoPhong - model.SoPhongDaThue;

            model.TongSoKhachHang = await _context.KhachHang.CountAsync();
            model.SoHopDongHieuLuc = await _context.HopDong.CountAsync(h => h.TrangThai == "Hiệu lực");
            model.SoHopDongHetHan = await _context.HopDong.CountAsync(h => h.TrangThai == "Đã hủy" || h.TrangThai == "Hết hạn");

            var hienTaiThang = DateTime.Now.Month;
            var hienTaiNam = DateTime.Now.Year;

            var doanhThuThanhToanThangNay = await _context.ThanhToan
                .Where(t => t.NgayThanhToan.HasValue && t.NgayThanhToan.Value.Year == hienTaiNam && t.NgayThanhToan.Value.Month == hienTaiThang)
                .SumAsync(t => t.SoTien ?? 0);

            var doanhThuHoaDonThangNay = await _context.HoaDon
                .Where(h => h.Thang == hienTaiThang && h.Nam == hienTaiNam && h.TrangThai == "Đã thanh toán")
                .SumAsync(h => h.TongTien ?? 0);

            model.DoanhThuThangNay = doanhThuThanhToanThangNay + doanhThuHoaDonThangNay;

            var hoaDonChuaThanhToan = await _context.HoaDon.Where(h => h.TrangThai == "Chưa thanh toán").ToListAsync();
            model.SoHoaDonChuaThanhToan = hoaDonChuaThanhToan.Count;
            model.TongNoHienTai = hoaDonChuaThanhToan.Sum(h => h.TongTien ?? 0);

            var doanhThuTheoThang = new List<DoanhThuTheoThang>();
            for (int i = 5; i >= 0; i--)
            {
                var mThang = DateTime.Now.AddMonths(-i);

                var tienThanhToan = await _context.ThanhToan
                    .Where(t => t.NgayThanhToan.HasValue && t.NgayThanhToan.Value.Year == mThang.Year && t.NgayThanhToan.Value.Month == mThang.Month)
                    .SumAsync(t => t.SoTien ?? 0);

                var tienHoaDon = await _context.HoaDon
                    .Where(h => h.Thang == mThang.Month && h.Nam == mThang.Year && h.TrangThai == "Đã thanh toán")
                    .SumAsync(h => h.TongTien ?? 0);

                doanhThuTheoThang.Add(new DoanhThuTheoThang
                {
                    Thang = mThang.Month,
                    Nam = mThang.Year,
                    DoanhThu = tienThanhToan + tienHoaDon
                });
            }

            model.TopPhongList = await (from h in _context.HoaDon
                                        join hd in _context.HopDong on h.MaHopDong equals hd.MaHopDong
                                        join p in _context.Phong on hd.MaPhong equals p.MaPhong
                                        where h.TrangThai == "Đã thanh toán"
                                        group h by new { p.MaPhong, p.TenPhong } into g
                                        select new TopPhongSuDung
                                        {
                                            MaPhong = g.Key.MaPhong,
                                            TenPhong = g.Key.TenPhong,
                                            TongDoanhThu = g.Sum(x => x.TongTien ?? 0),
                                            SoHoaDon = g.Count()
                                        })
                                        .OrderByDescending(x => x.TongDoanhThu)
                                        .Take(5)
                                        .ToListAsync();

            var tongSoPhong = model.TongSoPhong == 0 ? 1 : model.TongSoPhong;
            var lapDayTheoThang = new List<LapDayTheoThang>();
            for (int i = 5; i >= 0; i--)
            {
                var mThang = DateTime.Now.AddMonths(-i);
                var soPhongCoHopDong = await _context.HopDong
                    .Where(h => h.TrangThai == "Hiệu lực"
                             && h.NgayBatDau.HasValue
                             && h.NgayBatDau.Value <= new DateTime(mThang.Year, mThang.Month, DateTime.DaysInMonth(mThang.Year, mThang.Month))
                             && (!h.NgayKetThuc.HasValue || h.NgayKetThuc.Value >= new DateTime(mThang.Year, mThang.Month, 1)))
                    .Select(h => h.MaPhong)
                    .Distinct()
                    .CountAsync();

                lapDayTheoThang.Add(new LapDayTheoThang
                {
                    Thang = mThang.Month,
                    Nam = mThang.Year,
                    TyLeLapDay = Math.Round((double)soPhongCoHopDong / tongSoPhong * 100, 1)
                });
            }
            model.LapDayTheoThangList = lapDayTheoThang;
            return View(model);
        }

        // ======================= MODULE HÓA ĐƠN =======================
        public async Task<IActionResult> ThongKeHoaDon(int? thang, int? nam)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            int m = thang ?? DateTime.Now.Month;
            int y = nam ?? DateTime.Now.Year;

            var query = from h in _context.HoaDon
                        join hd in _context.HopDong on h.MaHopDong equals hd.MaHopDong
                        join p in _context.Phong on hd.MaPhong equals p.MaPhong
                        join k in _context.KhachHang on hd.MaKhachHang equals k.MaKhachHang
                        where h.Thang == m && h.Nam == y
                        select new ThongKeHoaDonViewModel
                        {
                            MaHoaDon = h.MaHoaDon.ToString(),
                            TenPhong = p.TenPhong,
                            KhachHang = k.HoTen,
                            TongTien = h.TongTien ?? 0,
                            TrangThai = (h.TrangThai != null) ? h.TrangThai.Trim() : "Chưa thanh toán",
                            NgayTao = h.NgayTao
                        };

            var data = await query.ToListAsync();
            data = data.OrderBy(x => x.TrangThai.Equals("Chưa thanh toán", StringComparison.OrdinalIgnoreCase) ? 1 : 2).ToList();

            ViewBag.Thang = m;
            ViewBag.Nam = y;
            ViewBag.TongSoHD = data.Count;
            ViewBag.DaThu = data.Where(x => x.TrangThai.Equals("Đã thanh toán", StringComparison.OrdinalIgnoreCase)).Sum(x => x.TongTien);
            ViewBag.ChuaThu = data.Where(x => x.TrangThai.Equals("Chưa thanh toán", StringComparison.OrdinalIgnoreCase)).Sum(x => x.TongTien);

            return View(data);
        }

        public async Task<IActionResult> XuatPDFThongKeHoaDon(int thang, int nam)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            var query = from h in _context.HoaDon
                        join hd in _context.HopDong on h.MaHopDong equals hd.MaHopDong
                        join p in _context.Phong on hd.MaPhong equals p.MaPhong
                        join k in _context.KhachHang on hd.MaKhachHang equals k.MaKhachHang
                        where h.Thang == thang && h.Nam == nam
                        select new ThongKeHoaDonViewModel
                        {
                            MaHoaDon = h.MaHoaDon.ToString(),
                            TenPhong = p.TenPhong,
                            KhachHang = k.HoTen,
                            TongTien = h.TongTien ?? 0,
                            TrangThai = (h.TrangThai != null) ? h.TrangThai.Trim() : "Chưa thanh toán",
                            NgayTao = h.NgayTao
                        };

            var data = await query.ToListAsync();
            data = data.OrderBy(x => x.TrangThai.Equals("Chưa thanh toán", StringComparison.OrdinalIgnoreCase) ? 1 : 2).ToList();

            ViewBag.Thang = thang;
            ViewBag.Nam = nam;
            ViewBag.TongSoHD = data.Count;
            ViewBag.DaThu = data.Where(x => x.TrangThai.Equals("Đã thanh toán", StringComparison.OrdinalIgnoreCase)).Sum(x => x.TongTien);
            ViewBag.ChuaThu = data.Where(x => x.TrangThai.Equals("Chưa thanh toán", StringComparison.OrdinalIgnoreCase)).Sum(x => x.TongTien);

            var sessionUser = HttpContext.Session.GetString("UserName");
            var sessionName = HttpContext.Session.GetString("Fullname");

            if (!string.IsNullOrWhiteSpace(sessionUser)) { ViewBag.Username = sessionUser.Trim(); }
            else if (!string.IsNullOrWhiteSpace(sessionName)) { ViewBag.Username = sessionName.Trim(); }
            else { ViewBag.Username = "Người quản lý"; }

            return new ViewAsPdf("ThongKeHoaDonPDF", (object)data)
            {
                FileName = $"BaoCao_HoaDon_Thang_{thang}_{nam}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageMargins = { Left = 15, Right = 15, Top = 15, Bottom = 15 }
            };
        }

        // ======================= MODULE HỢP ĐỒNG =======================
        public async Task<IActionResult> ChiTietHopDong()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            var query = from h in _context.HopDong
                        join p in _context.Phong on h.MaPhong equals p.MaPhong
                        join k in _context.KhachHang on h.MaKhachHang equals k.MaKhachHang
                        select new ChiTietHopDongViewModel
                        {
                            MaHopDong = h.MaHopDong.ToString(),
                            TenPhong = p.TenPhong,
                            TenKhachThue = k.HoTen,
                            NgayBatDau = h.NgayBatDau,
                            NgayKetThuc = h.NgayKetThuc,
                            TrangThai = (h.TrangThai == "Hiệu lực") ? "Đang hiệu lực" : h.TrangThai
                        };

            var data = await query.ToListAsync();
            data = data.OrderBy(x => x.TrangThai == "Đang hiệu lực" ? 1 : (x.TrangThai == "Hết hạn" ? 2 : 3)).ToList();

            ViewBag.TongSo = data.Count;
            ViewBag.DangHieuLuc = data.Count(x => x.TrangThai == "Đang hiệu lực");
            ViewBag.HetHan = data.Count(x => x.TrangThai == "Hết hạn");
            ViewBag.DaHuy = data.Count(x => x.TrangThai == "Đã hủy");

            return View(data);
        }

        // ĐÃ SỬA CHUẨN XÁC: So sánh đúng theo chuỗi chữ gốc trong Database tránh lệch pha số 0
        public async Task<IActionResult> XuatPDFChiTietHopDong()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            // Lấy danh sách hợp đồng gốc từ Database để đếm số liệu thật
            var danhSachGoc = await _context.HopDong.ToListAsync();

            // ĐÃ SỬA: Ép điều kiện đếm trúng chữ gốc lưu trong DB của ông ("Hiệu lực", "Hết hạn", "Đã hủy")
            ViewBag.TongSo = danhSachGoc.Count;
            ViewBag.DangHieuLuc = danhSachGoc.Count(x => x.TrangThai == "Hiệu lực");
            ViewBag.HetHan = danhSachGoc.Count(x => x.TrangThai == "Hết hạn");
            ViewBag.DaHuy = danhSachGoc.Count(x => x.TrangThai == "Đã hủy");

            var query = from h in _context.HopDong
                        join p in _context.Phong on h.MaPhong equals p.MaPhong
                        join k in _context.KhachHang on h.MaKhachHang equals k.MaKhachHang
                        select new ChiTietHopDongViewModel
                        {
                            MaHopDong = h.MaHopDong.ToString(),
                            TenPhong = p.TenPhong,
                            TenKhachThue = k.HoTen,
                            NgayBatDau = h.NgayBatDau,
                            NgayKetThuc = h.NgayKetThuc,
                            TrangThai = (h.TrangThai == "Hiệu lực") ? "Đang hiệu lực" : h.TrangThai
                        };

            var data = await query.ToListAsync();
            data = data.OrderBy(x => x.TrangThai == "Đang hiệu lực" ? 1 : (x.TrangThai == "Hết hạn" ? 2 : 3)).ToList();
            ViewBag.Username = HttpContext.Session.GetString("FullName") ?? HttpContext.Session.GetString("Username") ?? "Quản trị viên";

            return new ViewAsPdf("ChiTietHopDongPDF", (object)data)
            {
                FileName = $"BaoCao_TrangThaiHopDong_{DateTime.Now:ddMMyyyy}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageMargins = { Left = 15, Right = 15, Top = 15, Bottom = 15 }
            };
        }

        // ======================= MODULE PHÒNG =======================
        public async Task<IActionResult> ChiTietPhong()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            var data = await _context.Phong
                .Include(p => p.ToaNha)
                .GroupBy(p => p.ToaNha.TenToaNha)
                .Select(g => new ThongKeCoSoViewModel
                {
                    TenCoSo = g.Key ?? "Cơ sở chính",
                    TongSoPhong = g.Count(),
                    SoPhongDaThue = g.Count(x => x.TrangThai == "Đã thuê"),
                    SoPhongTrong = g.Count(x => x.TrangThai != "Đã thuê"),
                    TyLeSuDung = g.Count() == 0 ? 0 : Math.Max(0, Math.Round(((double)g.Count(x => x.TrangThai == "Đã thuê") / g.Count() * 100) - 5.5, 2))
                }).ToListAsync();

            ViewBag.TongPhong = data.Sum(x => x.TongSoPhong);
            ViewBag.TongDaThue = data.Sum(x => x.SoPhongDaThue);
            ViewBag.TongTrong = data.Sum(x => x.SoPhongTrong);

            return View(data);
        }

        public async Task<IActionResult> XuatPDFChiTietPhong()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            var data = await _context.Phong
                .Include(p => p.ToaNha)
                .GroupBy(p => p.ToaNha.TenToaNha)
                .Select(g => new ThongKeCoSoViewModel
                {
                    TenCoSo = g.Key ?? "Cơ sở chính",
                    TongSoPhong = g.Count(),
                    SoPhongDaThue = g.Count(x => x.TrangThai == "Đã thuê"),
                    SoPhongTrong = g.Count(x => x.TrangThai != "Đã thuê"),
                    TyLeSuDung = g.Count() == 0 ? 0 : Math.Max(0, Math.Round(((double)g.Count(x => x.TrangThai == "Đã thuê") / g.Count() * 100) - 5.5, 2))
                }).ToListAsync();

            ViewBag.TongPhong = data.Sum(x => x.TongSoPhong);
            ViewBag.TongDaThue = data.Sum(x => x.SoPhongDaThue);
            ViewBag.TongTrong = data.Sum(x => x.SoPhongTrong);
            ViewBag.Username = HttpContext.Session.GetString("FullName") ?? HttpContext.Session.GetString("Username") ?? "Quản trị viên";

            return new ViewAsPdf("ChiTietPhongPDF", (object)data)
            {
                FileName = $"BaoCao_LapDayPhong_{DateTime.Now:ddMMyyyy}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageMargins = { Left = 15, Right = 15, Top = 15, Bottom = 15 }
            };
        }

        // ======================= MODULE DOANH THU =======================
        public async Task<IActionResult> ChiTietDoanhThu(int? thang, int? nam)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            int m = thang ?? DateTime.Now.Month;
            int y = nam ?? DateTime.Now.Year;

            var query = from hd in _context.HoaDon
                        join hopDong in _context.HopDong on hd.MaHopDong equals hopDong.MaHopDong
                        join p in _context.Phong on hopDong.MaPhong equals p.MaPhong
                        where hd.TrangThai == "Đã thanh toán"
                              && hd.NgayTao.HasValue
                              && hd.NgayTao.Value.Month == m
                              && hd.NgayTao.Value.Year == y
                        select new ChiTietDoanhThuViewModel
                        {
                            MaHoaDon = hd.MaHoaDon.ToString(),
                            TenPhong = p.TenPhong,
                            NgayThanhToan = hd.NgayTao,
                            TongTien = hd.TongTien ?? 0
                        };

            var data = await query.ToListAsync();

            ViewBag.Thang = m;
            ViewBag.Nam = y;
            ViewBag.TongDoanhThu = data.Sum(x => x.TongTien);
            ViewBag.SoGiaoDich = data.Count;
            ViewBag.TrungBinh = data.Count > 0 ? data.Average(x => x.TongTien) : 0;
            ViewBag.Username = HttpContext.Session.GetString("FullName") ?? HttpContext.Session.GetString("Username") ?? "Quản trị viên";

            return View(data);
        }

        public async Task<IActionResult> XuatPDFBaoCao(int thang, int nam)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin") return RedirectToAction("Index", "Login");

            var query = from hd in _context.HoaDon
                        join hopDong in _context.HopDong on hd.MaHopDong equals hopDong.MaHopDong
                        join p in _context.Phong on hopDong.MaPhong equals p.MaPhong
                        where hd.TrangThai == "Đã thanh toán"
                              && hd.NgayTao.HasValue
                              && hd.NgayTao.Value.Month == thang
                              && hd.NgayTao.Value.Year == nam
                        select new ChiTietDoanhThuViewModel
                        {
                            MaHoaDon = hd.MaHoaDon.ToString(),
                            TenPhong = p.TenPhong,
                            NgayThanhToan = hd.NgayTao,
                            TongTien = hd.TongTien ?? 0
                        };

            var data = await query.ToListAsync();

            ViewBag.Thang = thang;
            ViewBag.Nam = nam;
            ViewBag.TongDoanhThu = data.Sum(x => x.TongTien);
            ViewBag.Username = HttpContext.Session.GetString("FullName") ?? HttpContext.Session.GetString("Username") ?? "Quản trị viên";

            return new ViewAsPdf("BaoCaoPDF", (object)data)
            {
                FileName = $"BaoCao_DoanhThu_{thang}_{nam}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageMargins = { Left = 15, Right = 15, Top = 15, Bottom = 15 }
            };
        }
    }
}