using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongQuanLyPhongTro.Services
{
    public class ThongBaoService
    {
        private readonly ApplicationDbContext _context;

        public ThongBaoService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Gửi thông báo cho Admin
        public async Task GuiAdmin(string tieuDe, string noiDung, string loai = "info", string duongDan = "")
        {
            var thongBao = new ThongBao
            {
                TieuDe = tieuDe,
                NoiDung = noiDung,
                Loai = loai,
                DuongDan = duongDan,
                NguoiNhan = null,  // NULL = gửi Admin
                NgayTao = DateTime.Now,
                DaXem = false
            };
            _context.ThongBao.Add(thongBao);
            await _context.SaveChangesAsync();
        }

        // Gửi thông báo cho Khách hàng cụ thể
        public async Task GuiKhach(int maKhachHang, string tieuDe, string noiDung, string loai = "info", string duongDan = "")
        {
            var thongBao = new ThongBao
            {
                TieuDe = tieuDe,
                NoiDung = noiDung,
                Loai = loai,
                DuongDan = duongDan,
                NguoiNhan = maKhachHang,
                NgayTao = DateTime.Now,
                DaXem = false
            };
            _context.ThongBao.Add(thongBao);
            await _context.SaveChangesAsync();
        }

        // Lấy thông báo cho Admin
        public async Task<List<ThongBao>> GetThongBaoAdmin()
        {
            return await _context.ThongBao
                .Where(tb => tb.NguoiNhan == null)
                .OrderByDescending(tb => tb.NgayTao)
                .Take(20)
                .ToListAsync();
        }

        // Lấy thông báo cho Khách hàng
        public async Task<List<ThongBao>> GetThongBaoKhach(int maKhachHang)
        {
            return await _context.ThongBao
                .Where(tb => tb.NguoiNhan == maKhachHang)
                .OrderByDescending(tb => tb.NgayTao)
                .Take(20)
                .ToListAsync();
        }

        // Đánh dấu đã xem
        public async Task DanhDauDaXem(int maThongBao)
        {
            var tb = await _context.ThongBao.FindAsync(maThongBao);
            if (tb != null)
            {
                tb.DaXem = true;
                await _context.SaveChangesAsync();
            }
        }

        // Đánh dấu tất cả đã xem cho Admin
        public async Task DanhDauTatCaDaXemAdmin()
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

        // Đánh dấu tất cả đã xem cho Khách
        public async Task DanhDauTatCaDaXemKhach(int maKhachHang)
        {
            var list = await _context.ThongBao
                .Where(tb => tb.NguoiNhan == maKhachHang && tb.DaXem == false)
                .ToListAsync();
            foreach (var tb in list)
            {
                tb.DaXem = true;
            }
            await _context.SaveChangesAsync();
        }
    }
}