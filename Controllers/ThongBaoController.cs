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
                tb.MaThongBao,
                tieuDe = tb.TieuDe,
                noiDung = tb.NoiDung,
                loai = tb.Loai,
                duongDan = tb.DuongDan,
                ngayTao = tb.NgayTao,
                daXem = tb.DaXem
            });

            return Json(result);
        }

        [HttpPost]
        public async Task<IActionResult> DanhDauTatCa()
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