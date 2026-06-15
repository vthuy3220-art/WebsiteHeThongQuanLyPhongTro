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
            // Nếu đã đăng nhập rồi thì chuyển hướng
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                var role = HttpContext.Session.GetString("Role");
                if (role == "SuperAdmin" || role == "Admin" || role == "ChuTro")
                {
                    return RedirectToAction("Index", "Dashboard");
                }
                else if (role == "Khach")
                {
                    return RedirectToAction("Dashboard", "KhachHang");
                }
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(string tenDangNhap, string matKhau)
        {
            var user = await _context.TaiKhoan
                .FirstOrDefaultAsync(x => x.TenDangNhap == tenDangNhap && x.MatKhau == matKhau);

            if (user == null)
            {
                ViewBag.Error = "Sai tài khoản hoặc mật khẩu";
                return View();
            }

            if (user.TrangThai == "Khóa")
            {
                ViewBag.Error = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên!";
                return View();
            }

            // Lưu session
            HttpContext.Session.SetInt32("UserId", user.MaTaiKhoan);
            HttpContext.Session.SetString("Role", user.VaiTro);
            HttpContext.Session.SetString("Username", user.TenDangNhap);

            // 👇 THÊM: Lưu MaChuTro nếu là Chủ trọ
            if (user.VaiTro == "ChuTro")
            {
                HttpContext.Session.SetInt32("MaChuTro", user.MaTaiKhoan);
            }

            // Phân quyền chuyển hướng
            if (user.VaiTro == "SuperAdmin" || user.VaiTro == "Admin")
            {
                return RedirectToAction("Index", "Dashboard");
            }
            else if (user.VaiTro == "ChuTro")
            {
                // Kiểm tra xem chủ trọ đã có tòa nhà chưa
                var coToaNha = await _context.ToaNha.AnyAsync(t => t.MaChuTro == user.MaTaiKhoan);
                if (!coToaNha)
                {
                    TempData["Warning"] = "Bạn chưa có tòa nhà nào. Vui lòng thêm tòa nhà trước!";
                }
                return RedirectToAction("Index", "Dashboard");
            }
            else // Khach
            {
                return RedirectToAction("Dashboard", "KhachHang");
            }
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult DangXuat()
        {
            // Xóa sạch Session lưu vết đăng nhập của Admin / Chủ trọ
            HttpContext.Session.Clear();

            // Đuổi thẳng về trang chủ của sàn
            return RedirectToAction("Index", "Login");
        }
    }

}