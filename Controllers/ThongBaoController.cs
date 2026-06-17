using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
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
            else if (role == "ChuTro")
            {
                // FIX: Lấy thông báo gửi riêng cho Chủ trọ này hoặc thông báo chung hệ thống (NguoiNhan == null)
                list = await _context.ThongBao
                    .Where(tb => tb.NguoiNhan == userId || tb.NguoiNhan == null)
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

            List<ThongBao> list = new List<ThongBao>();

            if (role == "Admin")
            {
                list = await _context.ThongBao
                    .Where(tb => tb.NguoiNhan == null)
                    .OrderByDescending(tb => tb.NgayTao)
                    .ToListAsync();
            }
            else if (role == "ChuTro")
            {
                // FIX: Lấy tất cả thông báo thuộc quyền của Chủ trọ này
                list = await _context.ThongBao
                    .Where(tb => tb.NguoiNhan == userId || tb.NguoiNhan == null)
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
            var thongBao = await _context.ThongBao.FindAsync(id);
            if (thongBao != null)
            {
                thongBao.DaXem = true;
                await _context.SaveChangesAsync();
            }
            return Ok();
        }

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
                foreach (var tb in list) tb.DaXem = true;
                await _context.SaveChangesAsync();
            }
            else if (role == "ChuTro")
            {
                // FIX: Đánh dấu tất cả thông báo của Chủ trọ là đã đọc
                var list = await _context.ThongBao
                    .Where(tb => (tb.NguoiNhan == userId || tb.NguoiNhan == null) && tb.DaXem == false)
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