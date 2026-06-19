using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class ThongBaoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ThongBaoController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetList()
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            var maChuTro = HttpContext.Session.GetInt32("MaChuTro");

            List<ThongBao> list = new List<ThongBao>();

            if (role == "Admin" || role == "SuperAdmin")
            {
                list = await _context.ThongBao
                    .Where(tb => tb.NguoiNhan == null)
                    .OrderByDescending(tb => tb.NgayTao)
                    .Take(30)
                    .ToListAsync();
            }
            else if (role == "ChuTro")
            {
                // Danh sách phòng thuộc chủ trọ này
                var phongIds = await _context.Phong
                    .Where(p => p.MaChuTro == (maChuTro ?? userId))
                    .Select(p => p.MaPhong)
                    .ToListAsync();

                // Danh sách khách hàng thuộc chủ trọ này
                var khachHangIds = await _context.HopDong
                    .Where(h => phongIds.Contains(h.MaPhong))
                    .Select(h => h.MaKhachHang)
                    .Distinct()
                    .ToListAsync();

                // 🎯 LỌC CHUẨN: Chỉ lấy thông báo gửi đích danh cho Chủ trọ (userId) 
                // HOẶC thông báo từ Khách gửi lên (Có nội dung chứa chữ "yêu cầu", "sửa chữa"...) 
                // Chặn hoàn toàn các thông báo về "Hóa đơn", "Thanh toán" do Chủ trọ tự tạo gửi đi.
                list = await _context.ThongBao
                    .Where(tb => tb.NguoiNhan == userId ||
                                (tb.NguoiNhan != null && khachHangIds.Contains(tb.NguoiNhan.Value) &&
                                (tb.TieuDe.Contains("Yêu cầu") || tb.NoiDung.Contains("gửi yêu cầu") || tb.NoiDung.Contains("sửa chữa"))))
                    .OrderByDescending(tb => tb.NgayTao)
                    .Take(30)
                    .ToListAsync();
            }
            else if (role == "Khach")
            {
                var khachHang = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);
                if (khachHang != null)
                {
                    list = await _context.ThongBao
                        .Where(tb => tb.NguoiNhan == khachHang.MaKhachHang)
                        .OrderByDescending(tb => tb.NgayTao)
                        .Take(30)
                        .ToListAsync();
                }
            }

            var result = list.Select(tb => new
            {
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
                var phongIds = await _context.Phong
                    .Where(p => p.MaChuTro == (maChuTro ?? userId))
                    .Select(p => p.MaPhong)
                    .ToListAsync();

                var khachHangIds = await _context.HopDong
                    .Where(h => phongIds.Contains(h.MaPhong))
                    .Select(h => h.MaKhachHang)
                    .Distinct()
                    .ToListAsync();

                // 🎯 ĐỒNG BỘ LỌC CHO TRANG INDEX
                list = await _context.ThongBao
                    .Where(tb => tb.NguoiNhan == userId ||
                                (tb.NguoiNhan != null && khachHangIds.Contains(tb.NguoiNhan.Value) &&
                                (tb.TieuDe.Contains("Yêu cầu") || tb.NoiDung.Contains("gửi yêu cầu") || tb.NoiDung.Contains("sửa chữa"))))
                    .OrderByDescending(tb => tb.NgayTao)
                    .ToListAsync();
            }
            else if (role == "Khach")
            {
                var khachHang = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);
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
            if (thongBao == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông báo!" });
            }

            bool coQuyen = false;
            if (role == "Admin" || role == "SuperAdmin")
            {
                coQuyen = thongBao.NguoiNhan == null;
            }
            else if (role == "ChuTro")
            {
                var phongIds = await _context.Phong
                    .Where(p => p.MaChuTro == (maChuTro ?? userId))
                    .Select(p => p.MaPhong)
                    .ToListAsync();

                var khachHangIds = await _context.HopDong
                    .Where(h => phongIds.Contains(h.MaPhong))
                    .Select(h => h.MaKhachHang)
                    .Distinct()
                    .ToListAsync();

                // Kiểm tra quyền sở hữu thông báo trước khi cho phép click đọc
                coQuyen = thongBao.NguoiNhan == userId || (thongBao.NguoiNhan != null && khachHangIds.Contains(thongBao.NguoiNhan.Value));
            }
            else if (role == "Khach")
            {
                var khachHang = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);
                coQuyen = khachHang != null && thongBao.NguoiNhan == khachHang.MaKhachHang;
            }

            if (!coQuyen)
            {
                return Json(new { success = false, message = "Bạn không có quyền với thông báo này!" });
            }

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

            if (role == "Admin" || role == "SuperAdmin")
            {
                var list = await _context.ThongBao
                    .Where(tb => tb.NguoiNhan == null && tb.DaXem == false)
                    .ToListAsync();
                foreach (var tb in list) tb.DaXem = true;
                await _context.SaveChangesAsync();
            }
            else if (role == "ChuTro")
            {
                var phongIds = await _context.Phong
                    .Where(p => p.MaChuTro == (maChuTro ?? userId))
                    .Select(p => p.MaPhong)
                    .ToListAsync();

                var khachHangIds = await _context.HopDong
                    .Where(h => phongIds.Contains(h.MaPhong))
                    .Select(h => h.MaKhachHang)
                    .Distinct()
                    .ToListAsync();

                var list = await _context.ThongBao
                    .Where(tb => (tb.NguoiNhan == userId || (tb.NguoiNhan != null && khachHangIds.Contains(tb.NguoiNhan.Value))) && tb.DaXem == false)
                    .ToListAsync();
                foreach (var tb in list) tb.DaXem = true;
                await _context.SaveChangesAsync();
            }
            else if (role == "Khach")
            {
                var khachHang = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);
                if (khachHang != null)
                {
                    var list = await _context.ThongBao
                        .Where(tb => tb.NguoiNhan == khachHang.MaKhachHang && tb.DaXem == false)
                        .ToListAsync();
                    foreach (var tb in list) tb.DaXem = true;
                    await _context.SaveChangesAsync();
                }
            }

            return Ok();
        }
    }
}