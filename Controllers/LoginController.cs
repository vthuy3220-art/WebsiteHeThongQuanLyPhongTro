using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using HeThongQuanLyPhongTro.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class LoginController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LoginController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Index(string tenDangNhap, string matKhau)
        {
            var user = _context.TaiKhoan
                .FirstOrDefault(x => x.TenDangNhap == tenDangNhap && x.MatKhau == matKhau);

            if (user == null)
            {
                ViewBag.Error = "Sai tài khoản hoặc mật khẩu";
                return View();
            }

            // Kiểm tra tài khoản có bị khóa không
            if (user.TrangThai == "Khóa")
            {
                ViewBag.Error = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên!";
                return View();
            }

            HttpContext.Session.SetInt32("UserId", user.MaTaiKhoan);
            HttpContext.Session.SetString("Role", user.VaiTro);
            HttpContext.Session.SetString("Username", user.TenDangNhap);

            // Xử lý riêng cho Khách hàng
            if (user.VaiTro == "Khach")
            {
                // Tìm hoặc tạo thông tin khách hàng
                var khachHang = _context.KhachHang.FirstOrDefault(k => k.MaTaiKhoan == user.MaTaiKhoan);
                if (khachHang == null)
                {
                    khachHang = new KhachHang
                    {
                        MaTaiKhoan = user.MaTaiKhoan,
                        HoTen = user.TenDangNhap,
                        SoDienThoai = "",
                        DiaChi = "",
                    };
                    _context.KhachHang.Add(khachHang);
                    _context.SaveChanges();
                }

                // Kiểm tra hợp đồng
                var hopDong = _context.HopDong
                    .Include(h => h.PhongNavigation)
                    .FirstOrDefault(h => h.MaKhachHang == khachHang.MaKhachHang && h.TrangThai == "Hiệu lực");

                if (hopDong == null)
                {
                    TempData["Warning"] = "Bạn chưa có hợp đồng thuê phòng. Vui lòng liên hệ chủ trọ!";
                }
                else
                {
                    TempData["Success"] = $"Chào mừng bạn! Phòng: {hopDong.PhongNavigation?.TenPhong}";
                }
            }

            return RedirectToAction("Index", "Dashboard");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }
        // =========================================
        // LUỒNG QUÊN MẬT KHẨU
        // =========================================

        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            // Tìm trong bảng TaiKhoan xem có ai dùng email này không
            // (Đảm bảo bảng TaiKhoan của cậu đã được thêm cột Email nhé)
            var user = _context.TaiKhoan.FirstOrDefault(x => x.Email == email);
            if (user == null)
            {
                ViewBag.Error = "Email không tồn tại trong hệ thống!";
                return View();
            }

            // Sinh mã OTP 6 số
            Random rd = new Random();
            string otp = rd.Next(100000, 999999).ToString();

            // Lưu mã vào bộ nhớ tạm Session để sang trang sau kiểm tra
            HttpContext.Session.SetString("ResetOTP", otp);
            HttpContext.Session.SetString("ResetEmail", email);

            // Gọi hàm gửi mail vừa tạo ở Bước 1
            var emailService = HttpContext.RequestServices.GetRequiredService<EmailService>();
            bool isSent = await emailService.GuiEmailOTP(email, otp);

            if (isSent)
            {
                return RedirectToAction("VerifyOTP");
            }
            else
            {
                ViewBag.Error = "Lỗi hệ thống khi gửi email, vui lòng thử lại!";
                return View();
            }
        }

        public IActionResult VerifyOTP()
        {
            // Nếu vô tình vào trang này mà chưa nhập email thì đuổi về
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("ResetEmail")))
                return RedirectToAction("ForgotPassword");

            return View();
        }

        [HttpPost]
        public IActionResult VerifyOTP(string otp)
        {
            string? sessionOtp = HttpContext.Session.GetString("ResetOTP");

            // So sánh mã người dùng nhập với mã đã sinh ra
            if (string.IsNullOrEmpty(sessionOtp) || sessionOtp != otp)
            {
                ViewBag.Error = "Mã xác thực không đúng hoặc đã hết hạn!";
                return View();
            }

            return RedirectToAction("ResetPassword");
        }

        public IActionResult ResetPassword()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("ResetOTP")))
                return RedirectToAction("ForgotPassword");

            return View();
        }

        [HttpPost]
        public IActionResult ResetPassword(string matKhauMoi, string xacNhanMatKhau)
        {
            if (matKhauMoi != xacNhanMatKhau)
            {
                ViewBag.Error = "Mật khẩu xác nhận không trùng khớp!";
                return View();
            }

            string? email = HttpContext.Session.GetString("ResetEmail");
            var user = _context.TaiKhoan.FirstOrDefault(x => x.Email == email);

            if (user != null)
            {
                user.MatKhau = matKhauMoi; // Lưu mật khẩu mới
                _context.SaveChanges();

                // Xóa thông tin tạm
                HttpContext.Session.Remove("ResetOTP");
                HttpContext.Session.Remove("ResetEmail");

                TempData["Success"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại!";
                return RedirectToAction("Index");
            }

            ViewBag.Error = "Có lỗi xảy ra, vui lòng thử lại!";
            return View();
        }
    }
}