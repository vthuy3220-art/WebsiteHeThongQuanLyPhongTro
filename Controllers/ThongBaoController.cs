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

            List<ThongBao> list = new List<ThongBao>();

            if (role == "Admin")
            {
                // Đã bỏ && tb.DaXem == false để lấy cả thông báo cũ hiển thị kiểu nhạt màu
                list = await _context.ThongBao
                    .Where(tb => tb.NguoiNhan == null)
                    .OrderByDescending(tb => tb.NgayTao)
                    .Take(20)
                    .ToListAsync();
            }
            else if (role == "Khach")
            {
                var khachHang = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);
                if (khachHang != null)
                {
                    // Đã bỏ && tb.DaXem == false để lấy cả thông báo cũ hiển thị kiểu nhạt màu
                    list = await _context.ThongBao
                        .Where(tb => tb.NguoiNhan == khachHang.MaKhachHang)
                        .OrderByDescending(tb => tb.NgayTao)
                        .Take(20)
                        .ToListAsync();
                }
            }

            // Chuyển đổi tên thuộc tính cho đúng với frontend
            var result = list.Select(tb => new
            {
                id = tb.MaThongBao, // Đổi sang 'id' để khớp tuyệt đối với tham số của Javascript
                tieuDe = tb.TieuDe,
                noiDung = tb.NoiDung,
                loai = tb.Loai,
                duongDan = tb.DuongDan,
                ngayTao = tb.NgayTao.ToString("dd/MM/yyyy HH:mm"), // Đã fix lỗi bỏ dấu ?
                daXem = tb.DaXem
            });

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");

            List<ThongBao> list = new List<ThongBao>();

            if (role == "Admin")
            {
                // Lấy tất cả thông báo của Admin
                list = await _context.ThongBao
                    .Where(tb => tb.NguoiNhan == null)
                    .OrderByDescending(tb => tb.NgayTao)
                    .ToListAsync();
            }
            else if (role == "Khach")
            {
                var khachHang = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);
                if (khachHang != null)
                {
                    // Lấy tất cả thông báo của Khách hàng này
                    list = await _context.ThongBao
                        .Where(tb => tb.NguoiNhan == khachHang.MaKhachHang)
                        .OrderByDescending(tb => tb.NgayTao)
                        .ToListAsync();
                }
            }

            return View(list);
        }

        // HÀM MỚI ĐƯỢC THÊM: Đánh dấu 1 thông báo cụ thể là đã đọc khi click vào
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var thongBao = await _context.ThongBao.FindAsync(id);
            if (thongBao != null)
            {
                thongBao.DaXem = true;
                await _context.SaveChangesAsync();
            }
            return Ok();
        }

        // ĐÃ SỬA TÊN: Đổi từ DanhDauTatCa -> MarkAllAsRead để khớp code Frontend
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");

            if (role == "Admin")
            {
                var list = await _context.ThongBao
                    .Where(tb => tb.NguoiNhan == null && tb.DaXem == false)
                    .ToListAsync();
                foreach (var tb in list)
                {
                    tb.DaXem = true;
                }
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
                    foreach (var tb in list)
                    {
                        tb.DaXem = true;
                    }
                    await _context.SaveChangesAsync();
                }
            }

            return Ok();
        }
    }
}