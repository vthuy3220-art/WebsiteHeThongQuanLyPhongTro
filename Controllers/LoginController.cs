using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
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
    }
}