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

        // Hàm helper lấy danh sách ID phòng thuộc quản lý của Chủ trọ
        private async Task<List<int>> GetPhongIdsByChuTro(int userId, bool isAdmin)
        {
            if (isAdmin) return await _context.Phong.Select(p => p.MaPhong).ToListAsync();

            var toaNhaIds = await _context.ToaNha.Where(t => t.MaChuTro == userId).Select(t => t.MaToaNha).ToListAsync();
            return await _context.Phong.Where(p => toaNhaIds.Contains(p.MaToaNha)).Select(p => p.MaPhong).ToListAsync();
        }

        // ======================= INDEX THỐNG KÊ CHÍNH =======================
        public async Task<IActionResult> Index(int? maToaNha, int? maPhong)
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");

            if (role != "Admin" && role != "ChuTro") return RedirectToAction("Index", "Login");

            bool isAdmin = (role == "Admin");
            var model = new DashboardViewModel();

            // 1. LẤY DANH SÁCH TÒA NHÀ (ĐỂ HIỂN THỊ ĐỊA CHỈ TRÊN DROPDOWN BỘ LỌC)
            var queryToaNha = _context.ToaNha.AsQueryable();
            if (!isAdmin)
            {
                queryToaNha = queryToaNha.Where(t => t.MaChuTro == userId);
            }
            var danhSachToaNha = await queryToaNha.ToListAsync();
            ViewBag.DanhSachToaNha = danhSachToaNha;

            if (maToaNha.HasValue && !isAdmin && !danhSachToaNha.Any(t => t.MaToaNha == maToaNha.Value))
            {
                maToaNha = null;
            }

            // 2. LẤY DANH SÁCH PHÒNG - CHỈ LẤY PHÒNG "ĐÃ THUÊ" ĐỂ ĐỠ RỐI
            var queryDropdownPhong = _context.Phong.Where(p => p.TrangThai == "Đã thuê");
            if (maToaNha.HasValue)
            {
                queryDropdownPhong = queryDropdownPhong.Where(p => p.MaToaNha == maToaNha.Value);
            }
            else
            {
                if (!isAdmin)
                {
                    var loggedInToaNhaIds = danhSachToaNha.Select(t => t.MaToaNha).ToList();
                    queryDropdownPhong = queryDropdownPhong.Where(p => loggedInToaNhaIds.Contains(p.MaToaNha));
                }
            }
            ViewBag.DanhSachPhong = await queryDropdownPhong.ToListAsync();

            // 3. LỌC DANH SÁCH PHÒNG ĐỂ TÍNH TOÁN CÁC TIÊU CHÍ THỐNG KÊ TRÊN DASHBOARD
            var queryPhong = _context.Phong.AsQueryable();
            if (maPhong.HasValue)
            {
                if (!isAdmin)
                {
                    var loggedInToaNhaIds = danhSachToaNha.Select(t => t.MaToaNha).ToList();
                    queryPhong = queryPhong.Where(p => p.MaPhong == maPhong.Value && loggedInToaNhaIds.Contains(p.MaToaNha));
                }
                else
                {
                    queryPhong = queryPhong.Where(p => p.MaPhong == maPhong.Value);
                }
            }
            else if (maToaNha.HasValue)
            {
                queryPhong = queryPhong.Where(p => p.MaToaNha == maToaNha.Value);
            }
            else if (!isAdmin)
            {
                var loggedInToaNhaIds = danhSachToaNha.Select(t => t.MaToaNha).ToList();
                queryPhong = queryPhong.Where(p => loggedInToaNhaIds.Contains(p.MaToaNha));
            }

            var danhSachPhong = await queryPhong.ToListAsync();
            var phongIds = danhSachPhong.Select(p => p.MaPhong).ToList();

            model.TongSoPhong = danhSachPhong.Count;
            model.SoPhongDaThue = danhSachPhong.Count(p => p.TrangThai == "Đã thuê");
            model.SoPhongTrong = model.TongSoPhong - model.SoPhongDaThue;

            // Lọc hợp đồng và khách hàng theo tập phòng đã lọc ở trên
            var queryHopDong = _context.HopDong.Where(h => phongIds.Contains(h.MaPhong));
            model.SoHopDongHieuLuc = await queryHopDong.CountAsync(h => h.TrangThai == "Hiệu lực");
            model.SoHopDongHetHan = await queryHopDong.CountAsync(h => h.TrangThai == "Đã hủy" || h.TrangThai == "Hết hạn");
            model.TongSoKhachHang = await queryHopDong.Select(h => h.MaKhachHang).Distinct().CountAsync();

            var hienTaiThang = DateTime.Now.Month;
            var hienTaiNam = DateTime.Now.Year;
            var hopDongIds = await queryHopDong.Select(h => h.MaHopDong).ToListAsync();

            // Tính doanh thu hóa đơn tháng này
            model.DoanhThuThangNay = await _context.HoaDon
                .Where(h => hopDongIds.Contains(h.MaHopDong) && h.Thang == hienTaiThang && h.Nam == hienTaiNam && h.TrangThai == "Đã thanh toán")
                .SumAsync(h => h.TongTien ?? 0);

            var hoaDonChuaThanhToan = await _context.HoaDon.Where(h => hopDongIds.Contains(h.MaHopDong) && h.TrangThai == "Chưa thanh toán").ToListAsync();
            model.SoHoaDonChuaThanhToan = hoaDonChuaThanhToan.Count;
            model.TongNoHienTai = hoaDonChuaThanhToan.Sum(h => h.TongTien ?? 0);

            // Tính lưu lượng doanh thu 6 tháng gần đây
            var doanhThuTheoThang = new List<HeThongQuanLyPhongTro.Models.DoanhThuTheoThang>();
            for (int i = 5; i >= 0; i--)
            {
                var mThang = DateTime.Now.AddMonths(-i);
                var tienHoaDon = await _context.HoaDon
                    .Where(h => hopDongIds.Contains(h.MaHopDong) && h.Thang == mThang.Month && h.Nam == mThang.Year && h.TrangThai == "Đã thanh toán")
                    .SumAsync(h => h.TongTien ?? 0);

                doanhThuTheoThang.Add(new HeThongQuanLyPhongTro.Models.DoanhThuTheoThang
                {
                    Thang = mThang.Month,
                    Nam = mThang.Year,
                    DoanhThu = tienHoaDon
                });
            }
            model.DoanhThuTheoThangList = doanhThuTheoThang;

            // Top phòng doanh thu cao nhất
            model.TopPhongList = await (from h in _context.HoaDon
                                        join hd in _context.HopDong on h.MaHopDong equals hd.MaHopDong
                                        join p in _context.Phong on hd.MaPhong equals p.MaPhong
                                        where h.TrangThai == "Đã thanh toán" && hopDongIds.Contains(h.MaHopDong)
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

            // Tính tỷ lệ lấp đầy
            var tongSoPhong = model.TongSoPhong == 0 ? 1 : model.TongSoPhong;
            var lapDayTheoThang = new List<LapDayTheoThang>();
            for (int i = 5; i >= 0; i--)
            {
                var mThang = DateTime.Now.AddMonths(-i);
                var soPhongCoHopDong = await _context.HopDong
                    .Where(h => phongIds.Contains(h.MaPhong) && h.TrangThai == "Hiệu lực" && h.NgayBatDau.HasValue
                             && h.NgayBatDau.Value <= new DateTime(mThang.Year, mThang.Month, DateTime.DaysInMonth(mThang.Year, mThang.Month))
                             && (!h.NgayKetThuc.HasValue || h.NgayKetThuc.Value >= new DateTime(mThang.Year, mThang.Month, 1)))
                    .Select(h => h.MaPhong).Distinct().CountAsync();

                lapDayTheoThang.Add(new LapDayTheoThang
                {
                    Thang = mThang.Month,
                    Nam = mThang.Year,
                    TyLeLapDay = Math.Round((double)soPhongCoHopDong / tongSoPhong * 100, 1)
                });
            }
            model.LapDayTheoThangList = lapDayTheoThang;

            ViewBag.SelectedMaToaNha = maToaNha;
            ViewBag.SelectedMaPhong = maPhong;

            return View(model);
        }

        // ======================= MODULE HÓA ĐƠN =======================
        public async Task<IActionResult> ThongKeHoaDon(int? thang, int? nam, int? maToaNha) // <-- Thêm tham số lọc
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            if (role != "Admin" && role != "ChuTro") return RedirectToAction("Index", "Login");

            int m = thang ?? DateTime.Now.Month;
            int y = nam ?? DateTime.Now.Year;
            bool isAdmin = (role == "Admin");

            // 1. LẤY DANH SÁCH TÒA NHÀ LÀM DROPDOWN
            var queryToaNha = _context.ToaNha.AsQueryable();
            if (!isAdmin)
            {
                queryToaNha = queryToaNha.Where(t => t.MaChuTro == userId);
            }
            ViewBag.DanhSachToaNha = await queryToaNha.ToListAsync();
            ViewBag.SelectedMaToaNha = maToaNha;

            // 2. LỌC DANH SÁCH PHÒNG THEO TÒA NHÀ
            var queryPhongIds = _context.Phong.AsQueryable();
            if (maToaNha.HasValue)
            {
                queryPhongIds = queryPhongIds.Where(p => p.MaToaNha == maToaNha.Value);
                if (!isAdmin) queryPhongIds = queryPhongIds.Where(p => p.ToaNha.MaChuTro == userId);
            }
            else if (!isAdmin)
            {
                var toaNhaIds = await queryToaNha.Select(t => t.MaToaNha).ToListAsync();
                queryPhongIds = queryPhongIds.Where(p => toaNhaIds.Contains(p.MaToaNha));
            }
            var pIds = await queryPhongIds.Select(p => p.MaPhong).ToListAsync();

            // 3. TRUY VẤN HÓA ĐƠN
            var query = from h in _context.HoaDon
                        join hd in _context.HopDong on h.MaHopDong equals hd.MaHopDong
                        join p in _context.Phong on hd.MaPhong equals p.MaPhong
                        join k in _context.KhachHang on hd.MaKhachHang equals k.MaKhachHang
                        where h.Thang == m && h.Nam == y && pIds.Contains(p.MaPhong)
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

        public async Task<IActionResult> XuatPDFThongKeHoaDon(int thang, int nam, int? maToaNha) // <-- Thêm tham số lọc
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            if (role != "Admin" && role != "ChuTro") return RedirectToAction("Index", "Login");

            bool isAdmin = (role == "Admin");

            // LỌC DANH SÁCH PHÒNG TƯƠNG TỰ
            var queryPhongIds = _context.Phong.AsQueryable();
            if (maToaNha.HasValue)
            {
                queryPhongIds = queryPhongIds.Where(p => p.MaToaNha == maToaNha.Value);
                if (!isAdmin) queryPhongIds = queryPhongIds.Where(p => p.ToaNha.MaChuTro == userId);
            }
            else if (!isAdmin)
            {
                var toaNhaIds = await _context.ToaNha.Where(t => t.MaChuTro == userId).Select(t => t.MaToaNha).ToListAsync();
                queryPhongIds = queryPhongIds.Where(p => toaNhaIds.Contains(p.MaToaNha));
            }
            var pIds = await queryPhongIds.Select(p => p.MaPhong).ToListAsync();

            var query = from h in _context.HoaDon
                        join hd in _context.HopDong on h.MaHopDong equals hd.MaHopDong
                        join p in _context.Phong on hd.MaPhong equals p.MaPhong
                        join k in _context.KhachHang on hd.MaKhachHang equals k.MaKhachHang
                        where h.Thang == thang && h.Nam == nam && pIds.Contains(p.MaPhong)
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
            ViewBag.Username = HttpContext.Session.GetString("FullName") ?? HttpContext.Session.GetString("Username") ?? "Người quản lý";

            return new ViewAsPdf("ThongKeHoaDonPDF", (object)data)
            {
                FileName = $"BaoCao_HoaDon_Thang_{thang}_{nam}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageMargins = { Left = 15, Right = 15, Top = 15, Bottom = 15 }
            };
        }

        // ======================= MODULE HỢP ĐỒNG =======================
        public async Task<IActionResult> ChiTietHopDong(int? maToaNha) // <-- Thêm tham số lọc
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            if (role != "Admin" && role != "ChuTro") return RedirectToAction("Index", "Login");

            bool isAdmin = (role == "Admin");

            // 1. LẤY DANH SÁCH TÒA NHÀ ĐỂ LÀM DROPDOWN
            var queryToaNha = _context.ToaNha.AsQueryable();
            if (!isAdmin)
            {
                queryToaNha = queryToaNha.Where(t => t.MaChuTro == userId);
            }
            ViewBag.DanhSachToaNha = await queryToaNha.ToListAsync();
            ViewBag.SelectedMaToaNha = maToaNha;

            // 2. LỌC DANH SÁCH PHÒNG THEO TÒA NHÀ ĐƯỢC CHỌN
            var queryPhongIds = _context.Phong.AsQueryable();
            if (maToaNha.HasValue)
            {
                queryPhongIds = queryPhongIds.Where(p => p.MaToaNha == maToaNha.Value);
                if (!isAdmin) queryPhongIds = queryPhongIds.Where(p => p.ToaNha.MaChuTro == userId);
            }
            else if (!isAdmin)
            {
                var toaNhaIds = await queryToaNha.Select(t => t.MaToaNha).ToListAsync();
                queryPhongIds = queryPhongIds.Where(p => toaNhaIds.Contains(p.MaToaNha));
            }
            var pIds = await queryPhongIds.Select(p => p.MaPhong).ToListAsync();

            // 3. TRUY VẤN HỢP ĐỒNG
            var query = from h in _context.HopDong
                        join p in _context.Phong on h.MaPhong equals p.MaPhong
                        join k in _context.KhachHang on h.MaKhachHang equals k.MaKhachHang
                        where pIds.Contains(p.MaPhong)
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

        public async Task<IActionResult> XuatPDFChiTietHopDong(int? maToaNha) // <-- Thêm tham số lọc
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            if (role != "Admin" && role != "ChuTro") return RedirectToAction("Index", "Login");

            bool isAdmin = (role == "Admin");

            // LỌC DANH SÁCH PHÒNG TƯƠNG TỰ
            var queryPhongIds = _context.Phong.AsQueryable();
            if (maToaNha.HasValue)
            {
                queryPhongIds = queryPhongIds.Where(p => p.MaToaNha == maToaNha.Value);
                if (!isAdmin) queryPhongIds = queryPhongIds.Where(p => p.ToaNha.MaChuTro == userId);
            }
            else if (!isAdmin)
            {
                var toaNhaIds = await _context.ToaNha.Where(t => t.MaChuTro == userId).Select(t => t.MaToaNha).ToListAsync();
                queryPhongIds = queryPhongIds.Where(p => toaNhaIds.Contains(p.MaToaNha));
            }
            var pIds = await queryPhongIds.Select(p => p.MaPhong).ToListAsync();

            var query = from h in _context.HopDong
                        join p in _context.Phong on h.MaPhong equals p.MaPhong
                        join k in _context.KhachHang on h.MaKhachHang equals k.MaKhachHang
                        where pIds.Contains(p.MaPhong)
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
            ViewBag.Username = HttpContext.Session.GetString("FullName") ?? HttpContext.Session.GetString("Username") ?? "Người quản lý";

            return new ViewAsPdf("ChiTietHopDongPDF", (object)data)
            {
                FileName = $"BaoCao_TrangThaiHopDong_{DateTime.Now:ddMMyyyy}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                PageMargins = { Left = 15, Right = 15, Top = 15, Bottom = 15 }
            };
        }

        // ======================= MODULE PHÒNG =======================
        public async Task<IActionResult> ChiTietPhong(int? maToaNha) // <-- Thêm tham số lọc
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            if (role != "Admin" && role != "ChuTro") return RedirectToAction("Index", "Login");

            bool isAdmin = (role == "Admin");

            // 1. LẤY DANH SÁCH TÒA NHÀ ĐỂ LÀM DROPDOWN
            var queryToaNha = _context.ToaNha.AsQueryable();
            if (!isAdmin)
            {
                queryToaNha = queryToaNha.Where(t => t.MaChuTro == userId);
            }
            ViewBag.DanhSachToaNha = await queryToaNha.ToListAsync();
            ViewBag.SelectedMaToaNha = maToaNha;

            // 2. TRUY VẤN VÀ LỌC PHÒNG THEO TÒA NHÀ
            var queryPhong = _context.Phong.Include(p => p.ToaNha).AsQueryable();
            if (!isAdmin)
            {
                queryPhong = queryPhong.Where(p => p.ToaNha.MaChuTro == userId);
            }

            if (maToaNha.HasValue)
            {
                queryPhong = queryPhong.Where(p => p.MaToaNha == maToaNha.Value);
            }

            var data = await queryPhong
                .GroupBy(p => p.ToaNha.TenToaNha)
                .Select(g => new ThongKeCoSoViewModel
                {
                    TenCoSo = g.Key ?? "Tòa nhà chính",
                    TongSoPhong = g.Count(),
                    SoPhongDaThue = g.Count(x => x.TrangThai == "Đã thuê"),
                    SoPhongTrong = g.Count(x => x.TrangThai != "Đã thuê"),
                    TyLeSuDung = g.Count() == 0 ? 0 : Math.Round((double)g.Count(x => x.TrangThai == "Đã thuê") / g.Count() * 100, 2)
                }).ToListAsync();

            ViewBag.TongPhong = data.Sum(x => x.TongSoPhong);
            ViewBag.TongDaThue = data.Sum(x => x.SoPhongDaThue);
            ViewBag.TongTrong = data.Sum(x => x.SoPhongTrong);

            return View(data);
        }

        public async Task<IActionResult> XuatPDFChiTietPhong(int? maToaNha) // <-- Thêm tham số lọc
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            if (role != "Admin" && role != "ChuTro") return RedirectToAction("Index", "Login");

            bool isAdmin = (role == "Admin");

            var queryPhong = _context.Phong.Include(p => p.ToaNha).AsQueryable();
            if (!isAdmin)
            {
                queryPhong = queryPhong.Where(p => p.ToaNha.MaChuTro == userId);
            }

            if (maToaNha.HasValue)
            {
                queryPhong = queryPhong.Where(p => p.MaToaNha == maToaNha.Value);
            }

            var data = await queryPhong
                .GroupBy(p => p.ToaNha.TenToaNha)
                .Select(g => new ThongKeCoSoViewModel
                {
                    TenCoSo = g.Key ?? "Tòa nhà chính",
                    TongSoPhong = g.Count(),
                    SoPhongDaThue = g.Count(x => x.TrangThai == "Đã thuê"),
                    SoPhongTrong = g.Count(x => x.TrangThai != "Đã thuê"),
                    TyLeSuDung = g.Count() == 0 ? 0 : Math.Round((double)g.Count(x => x.TrangThai == "Đã thuê") / g.Count() * 100, 2)
                }).ToListAsync();

            ViewBag.TongPhong = data.Sum(x => x.TongSoPhong);
            ViewBag.TongDaThue = data.Sum(x => x.SoPhongDaThue);
            ViewBag.TongTrong = data.Sum(x => x.SoPhongTrong);
            ViewBag.Username = HttpContext.Session.GetString("FullName") ?? HttpContext.Session.GetString("Username") ?? "Người quản lý";

            return new ViewAsPdf("ChiTietPhongPDF", (object)data)
            {
                FileName = $"BaoCao_LapDayPhong_{DateTime.Now:ddMMyyyy}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape, // Đổi sang khổ ngang cho bảng nhiều cột
                PageMargins = { Left = 15, Right = 15, Top = 15, Bottom = 15 }
            };
        }

        // ======================= MODULE DOANH THU =======================
        public async Task<IActionResult> ChiTietDoanhThu(int? thang, int? nam, int? maToaNha) // <--- Thêm tham số maToaNha
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            if (role != "Admin" && role != "ChuTro") return RedirectToAction("Index", "Login");

            int m = thang ?? DateTime.Now.Month;
            int y = nam ?? DateTime.Now.Year;
            bool isAdmin = (role == "Admin");

            // 1. LẤY DANH SÁCH TÒA NHÀ ĐỂ HIỂN THỊ LÊN GIAO DIỆN DROPDOWN
            var queryToaNha = _context.ToaNha.AsQueryable();
            if (!isAdmin)
            {
                queryToaNha = queryToaNha.Where(t => t.MaChuTro == userId);
            }
            ViewBag.DanhSachToaNha = await queryToaNha.ToListAsync();
            ViewBag.SelectedMaToaNha = maToaNha; // Lưu lại trạng thái vừa chọn

            // 2. LẤY DANH SÁCH PHÒNG THEO CHỦ TRỌ HOẶC THEO TÒA NHÀ ĐƯỢC CHỌN
            var queryPhongIds = _context.Phong.AsQueryable();

            if (maToaNha.HasValue)
            {
                // Nếu người dùng chọn Tòa nhà cụ thể
                queryPhongIds = queryPhongIds.Where(p => p.MaToaNha == maToaNha.Value);
                // Bảo mật: Nếu là chủ trọ, đảm bảo tòa nhà đó đúng là của họ
                if (!isAdmin)
                {
                    queryPhongIds = queryPhongIds.Where(p => p.ToaNha.MaChuTro == userId);
                }
            }
            else
            {
                // Nếu lấy tất cả
                if (!isAdmin)
                {
                    var toaNhaIds = await queryToaNha.Select(t => t.MaToaNha).ToListAsync();
                    queryPhongIds = queryPhongIds.Where(p => toaNhaIds.Contains(p.MaToaNha));
                }
            }
            var pIds = await queryPhongIds.Select(p => p.MaPhong).ToListAsync();

            // 3. TRUY VẤN DOANH THU
            var query = from hd in _context.HoaDon
                        join hopDong in _context.HopDong on hd.MaHopDong equals hopDong.MaHopDong
                        join p in _context.Phong on hopDong.MaPhong equals p.MaPhong
                        where hd.TrangThai == "Đã thanh toán"
                              && hd.NgayTao.HasValue
                              && hd.NgayTao.Value.Month == m
                              && hd.NgayTao.Value.Year == y
                              && pIds.Contains(p.MaPhong)
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
            ViewBag.Username = HttpContext.Session.GetString("FullName") ?? HttpContext.Session.GetString("Username") ?? "Người quản lý";

            return View(data);
        }

        public async Task<IActionResult> XuatPDFBaoCao(int thang, int nam, int? maToaNha) // <--- Thêm tham số maToaNha
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            if (role != "Admin" && role != "ChuTro") return RedirectToAction("Index", "Login");

            bool isAdmin = (role == "Admin");

            // XỬ LÝ LỌC ID PHÒNG TƯƠNG TỰ NHƯ HÀM CHITIETDOANHTHU
            var queryPhongIds = _context.Phong.AsQueryable();
            if (maToaNha.HasValue)
            {
                queryPhongIds = queryPhongIds.Where(p => p.MaToaNha == maToaNha.Value);
                if (!isAdmin) queryPhongIds = queryPhongIds.Where(p => p.ToaNha.MaChuTro == userId);
            }
            else if (!isAdmin)
            {
                var toaNhaIds = await _context.ToaNha.Where(t => t.MaChuTro == userId).Select(t => t.MaToaNha).ToListAsync();
                queryPhongIds = queryPhongIds.Where(p => toaNhaIds.Contains(p.MaToaNha));
            }
            var pIds = await queryPhongIds.Select(p => p.MaPhong).ToListAsync();

            var query = from hd in _context.HoaDon
                        join hopDong in _context.HopDong on hd.MaHopDong equals hopDong.MaHopDong
                        join p in _context.Phong on hopDong.MaPhong equals p.MaPhong
                        where hd.TrangThai == "Đã thanh toán"
                              && hd.NgayTao.HasValue
                              && hd.NgayTao.Value.Month == thang
                              && hd.NgayTao.Value.Year == nam
                              && pIds.Contains(p.MaPhong)
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
            ViewBag.Username = HttpContext.Session.GetString("FullName") ?? HttpContext.Session.GetString("Username") ?? "Người quản lý";

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