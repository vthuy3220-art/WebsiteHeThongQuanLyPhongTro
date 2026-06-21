using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class ThongBaoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ThongBaoController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Hàm xử lý nội bộ lọc thông báo chính xác theo phân quyền tòa nhà/phòng
        private async Task<List<ThongBao>> LocThongBaoChoChuTro(int userId, int? maChuTro)
        {
            int currentChuTroId = maChuTro ?? userId;

            // 1. Lấy toàn bộ danh sách mã phòng thuộc quản lý của chủ trọ này
            var phongIds = await _context.Phong
                .Where(p => p.MaChuTro == currentChuTroId)
                .Select(p => p.MaPhong)
                .ToListAsync();

            // 2. Lấy danh sách Mã hóa đơn thuộc các phòng của chủ trọ này
            var hopDongIds = await _context.HopDong.Where(h => phongIds.Contains(h.MaPhong)).Select(h => h.MaHopDong).ToListAsync();
            var hoaDonIds = await _context.HoaDon.Where(hd => hopDongIds.Contains(hd.MaHopDong)).Select(hd => hd.MaHoaDon).ToListAsync();

            // 3. Lấy danh sách Mã yêu cầu sửa chữa thuộc các phòng của chủ trọ này
            var yeuCauIds = await _context.YeuCauSuaChua.Where(y => phongIds.Contains(y.MaPhong)).Select(y => y.MaYeuCau).ToListAsync();

            // 4. Lấy tất cả thông báo đích danh hoặc thông báo từ hệ thống (NguoiNhan == null)
            var tatCaThongBao = await _context.ThongBao
                .Where(tb => tb.NguoiNhan == userId || tb.NguoiNhan == null)
                .OrderByDescending(tb => tb.NgayTao)
                .ToListAsync();

            var listKetQua = new List<ThongBao>();

            foreach (var tb in tatCaThongBao)
            {
                // Nếu gửi đích danh cho tài khoản chủ trọ này thì hiển thị luôn
                if (tb.NguoiNhan == userId)
                {
                    listKetQua.Add(tb);
                    continue;
                }

                // Nếu NguoiNhan == null, dùng Regex tìm con số ID ở cuối đường dẫn (ví dụ: 25, 1)
                if (!string.IsNullOrEmpty(tb.DuongDan))
                {
                    var match = Regex.Match(tb.DuongDan, @"\d+$");
                    if (match.Success && int.TryParse(match.Value, out int idCuoi))
                    {
                        string pathLower = tb.DuongDan.ToLower();

                        // Kiểm tra từ khóa trong đường dẫn và check quyền sở hữu tương ứng
                        if (pathLower.Contains("hoadon") && hoaDonIds.Contains(idCuoi))
                        {
                            listKetQua.Add(tb);
                        }
                        else if ((pathLower.Contains("suachua") || pathLower.Contains("yeucau")) && yeuCauIds.Contains(idCuoi))
                        {
                            listKetQua.Add(tb);
                        }
                    }
                }
            }

            return listKetQua;
        }

        [HttpGet]
        public async Task<IActionResult> GetList()
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            var maChuTro = HttpContext.Session.GetInt32("MaChuTro");

            if (string.IsNullOrEmpty(role) || userId == null) return Json(new List<object>());

            List<ThongBao> list = new List<ThongBao>();

            if (role == "Admin" || role == "SuperAdmin")
            {
                list = await _context.ThongBao
                    .Where(tb => tb.NguoiNhan == null)
                    .OrderByDescending(tb => tb.NgayTao)
                    .Take(20)
                    .ToListAsync();
            }
            else if (role == "ChuTro")
            {
                list = await LocThongBaoChoChuTro(userId.Value, maChuTro);
                list = list.Take(30).ToList();
            }
            else if (role == "Khach")
            {
                var khachHang = await _context.KhachHang.FirstOrDefaultAsync(k => k.MaTaiKhoan == userId.Value);
                if (khachHang != null)
                {
                    list = await _context.ThongBao
                        .Where(tb => tb.NguoiNhan == khachHang.MaKhachHang)
                        .OrderByDescending(tb => tb.NgayTao)
                        .Take(20)
                        .ToListAsync();
                }
            }

            var result = list.Select(tb => new {
                id = tb.MaThongBao,
                tieuDe = tb.TieuDe,
                noiDung = tb.NoiDung,
                loai = tb.Loai,
                duongDan = tb.DuongDan,
                ngayTao = tb.NgayTao.ToString("dd/MM/yyyy HH:mm"),
                daXem = tb.DaXem
            });

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            var maChuTro = HttpContext.Session.GetInt32("MaChuTro");

            if (string.IsNullOrEmpty(role) || userId == null) return RedirectToAction("Index", "Login");

            List<ThongBao> list = new List<ThongBao>();

            if (role == "Admin" || role == "SuperAdmin")
            {
                list = await _context.ThongBao
                    .Where(tb => tb.NguoiNhan == null)
                    .OrderByDescending(tb => tb.NgayTao)
                    .ToListAsync();
            }
            else if (role == "ChuTro")
            {
                list = await LocThongBaoChoChuTro(userId.Value, maChuTro);
            }
            else if (role == "Khach")
            {
                var khachHang = await _context.KhachHang.FirstOrDefaultAsync(k => k.MaTaiKhoan == userId.Value);
                if (khachHang != null)
                {
                    list = await _context.ThongBao
                        .Where(tb => tb.NguoiNhan == khachHang.MaKhachHang)
                        .OrderByDescending(tb => tb.NgayTao)
                        .ToListAsync();
                }
            }

            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            var maChuTro = HttpContext.Session.GetInt32("MaChuTro");

            var thongBao = await _context.ThongBao.FindAsync(id);
            if (thongBao == null || userId == null) return Json(new { success = false, message = "Không tìm thấy thông báo!" });

            bool coQuyen = false;
            if (role == "Admin" || role == "SuperAdmin") coQuyen = thongBao.NguoiNhan == null;
            else if (role == "ChuTro")
            {
                var hopLeList = await LocThongBaoChoChuTro(userId.Value, maChuTro);
                coQuyen = hopLeList.Any(tb => tb.MaThongBao == id);
            }
            else if (role == "Khach")
            {
                var khachHang = await _context.KhachHang.FirstOrDefaultAsync(k => k.MaTaiKhoan == userId.Value);
                coQuyen = khachHang != null && thongBao.NguoiNhan == khachHang.MaKhachHang;
            }

            if (!coQuyen) return Json(new { success = false, message = "Bạn không có quyền!" });

            thongBao.DaXem = true;
            _context.ThongBao.Update(thongBao);
            await _context.SaveChangesAsync();

            return Json(new { success = true, url = thongBao.DuongDan });
        }

        [HttpPost]
        public async Task<IActionResult> DanhDauTatCa()
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            var maChuTro = HttpContext.Session.GetInt32("MaChuTro");

            if (string.IsNullOrEmpty(role) || userId == null) return BadRequest();

            if (role == "Admin" || role == "SuperAdmin")
            {
                var list = await _context.ThongBao.Where(tb => tb.NguoiNhan == null && tb.DaXem == false).ToListAsync();
                foreach (var tb in list) tb.DaXem = true;
                await _context.SaveChangesAsync();
            }
            else if (role == "ChuTro")
            {
                var hopLeList = await LocThongBaoChoChuTro(userId.Value, maChuTro);
                var chuaXemIds = hopLeList.Where(tb => tb.DaXem == false).Select(tb => tb.MaThongBao).ToList();

                var dbList = await _context.ThongBao.Where(tb => chuaXemIds.Contains(tb.MaThongBao)).ToListAsync();
                foreach (var tb in dbList) tb.DaXem = true;
                await _context.SaveChangesAsync();
            }
            else if (role == "Khach")
            {
                var khachHang = await _context.KhachHang.FirstOrDefaultAsync(k => k.MaTaiKhoan == userId.Value);
                if (khachHang != null)
                {
                    var list = await _context.ThongBao.Where(tb => tb.NguoiNhan == khachHang.MaKhachHang && tb.DaXem == false).ToListAsync();
                    foreach (var tb in list) tb.DaXem = true;
                    await _context.SaveChangesAsync();
                }
            }

            return Ok();
        }
    }
}