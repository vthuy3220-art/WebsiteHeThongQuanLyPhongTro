using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace HeThongQuanLyPhongTro.Services
{
    public class ThongBaoService
    {
        private readonly ApplicationDbContext _context;

        public ThongBaoService(ApplicationDbContext context)
        {
            _context = context;
        }

        // 🚀 Hàm xử lý gửi Email chạy ngầm không gây nghẽn luồng ứng dụng
        private async Task GuiEmailNgamChoKhach(string emailNguoiNhan, string tieuDe, string noiDung)
        {
            try
            {
                if (string.IsNullOrEmpty(emailNguoiNhan)) return;

                using (var message = new MailMessage())
                {
                    // Đổi thông tin hòm thư hệ thống của bạn tại đây
                    message.From = new MailAddress("phongtroxinh.system@gmail.com", "Hệ Thống Phòng Trọ Xinh");
                    message.To.Add(new MailAddress(emailNguoiNhan));
                    message.Subject = "🔔 Thông báo mới từ hệ thống: " + tieuDe;
                    message.Body = $"<h3>Bạn có thông báo mới từ Chủ trọ</h3><p>{noiDung}</p><hr/><p><i>Vui lòng đăng nhập hệ thống để xử lý công việc.</i></p>";
                    message.IsBodyHtml = true;

                    using (var client = new SmtpClient("smtp.gmail.com", 587))
                    {
                        client.EnableSsl = true;
                        client.Credentials = new NetworkCredential("phongtroxinh.system@gmail.com", "clwtxuifyvmbpxxx"); // Mật khẩu ứng dụng Gmail
                        await client.SendMailAsync(message);
                    }
                }
            }
            catch (Exception ex)
            {
                // Ghi log lỗi ra console nếu cấu hình sai SMTP để không bị crash ứng dụng
                Console.WriteLine("Lỗi gửi Email thông báo: " + ex.Message);
            }
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
                NguoiNhan = null,
                NgayTao = DateTime.Now,
                DaXem = false
            };
            _context.ThongBao.Add(thongBao);
            await _context.SaveChangesAsync();
        }

        // 🔥 CẬP NHẬT: Gửi thông báo cho Khách hàng cụ thể (Vừa bắn Chuông vừa gửi Email)
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

            // 🎯 Lấy thông tin Email khách hàng để tự động bắn mail đi kèm
            var khachHang = await _context.KhachHang.FindAsync(maKhachHang);
            if (khachHang != null && !string.IsNullOrEmpty(khachHang.Email))
            {
                // Sử dụng discard _ để đẩy tác vụ gửi mail chạy ngầm, không bắt client đợi load trang lâu
                _ = GuiEmailNgamChoKhach(khachHang.Email, tieuDe, noiDung);
            }
        }

        // Gửi thông báo cho Chủ trọ cụ thể
        public async Task GuiChuTro(int maTaiKhoanChuTro, string tieuDe, string noiDung, string loai = "info", string duongDan = "")
        {
            var thongBao = new ThongBao
            {
                TieuDe = tieuDe,
                NoiDung = noiDung,
                Loai = loai,
                DuongDan = duongDan,
                NguoiNhan = maTaiKhoanChuTro,
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

        // Lấy thông báo cho Chủ trọ
        public async Task<List<ThongBao>> GetThongBaoChuTro(int maTaiKhoanChuTro)
        {
            return await _context.ThongBao
                .Where(tb => tb.NguoiNhan == maTaiKhoanChuTro)
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
            foreach (var tb in list) tb.DaXem = true;
            await _context.SaveChangesAsync();
        }

        // Đánh dấu tất cả đã xem cho Khách
        public async Task DanhDauTatCaDaXemKhach(int maKhachHang)
        {
            var list = await _context.ThongBao
                .Where(tb => tb.NguoiNhan == maKhachHang && tb.DaXem == false)
                .ToListAsync();
            foreach (var tb in list) tb.DaXem = true;
            await _context.SaveChangesAsync();
        }

        // Đánh dấu tất cả đã xem cho Chủ trọ
        public async Task DanhDauTatCaDaXemChuTro(int maTaiKhoanChuTro)
        {
            var list = await _context.ThongBao
                .Where(tb => tb.NguoiNhan == maTaiKhoanChuTro && tb.DaXem == false)
                .ToListAsync();
            foreach (var tb in list) tb.DaXem = true;
            await _context.SaveChangesAsync();
        }
    }
}