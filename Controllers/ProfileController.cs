using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        private bool IsLoggedIn()
        {
            return GetCurrentUserId() != 0;
        }

        // GET: Thông tin cá nhân
        [HttpGet]
        public async Task<IActionResult> Index1()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Index", "Login");

            var userId = GetCurrentUserId();
            var taiKhoan = await _context.TaiKhoan
                .FirstOrDefaultAsync(t => t.MaTaiKhoan == userId);

            if (taiKhoan == null)
                return NotFound();

            return View(taiKhoan);
        }

        // POST: Cập nhật thông tin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CapNhatThongTin(string email, string soDienThoai, string diaChi)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Index", "Login");

            var userId = GetCurrentUserId();
            var taiKhoan = await _context.TaiKhoan.FindAsync(userId);

            if (taiKhoan == null)
                return NotFound();

            taiKhoan.Email = email;
            taiKhoan.SoDienThoai = soDienThoai;
            taiKhoan.DiaChi = diaChi;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Cập nhật thông tin thành công!";
            return RedirectToAction(nameof(Index1));
        }

        // GET: Đổi mật khẩu
        [HttpGet]
        public IActionResult ChangePassword1()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Index", "Login");

            return View();
        }

        // POST: Đổi mật khẩu
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword1(string matKhauCu, string matKhauMoi, string xacNhanMatKhau)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Index", "Login");

            if (string.IsNullOrEmpty(matKhauCu) || string.IsNullOrEmpty(matKhauMoi))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin!";
                return View();
            }

            if (matKhauMoi != xacNhanMatKhau)
            {
                TempData["Error"] = "Mật khẩu xác nhận không khớp!";
                return View();
            }

            if (matKhauMoi.Length < 6)
            {
                TempData["Error"] = "Mật khẩu mới phải có ít nhất 6 ký tự!";
                return View();
            }

            var userId = GetCurrentUserId();
            var taiKhoan = await _context.TaiKhoan.FindAsync(userId);

            if (taiKhoan == null)
                return NotFound();

            if (taiKhoan.MatKhau != matKhauCu)
            {
                TempData["Error"] = "Mật khẩu hiện tại không đúng!";
                return View();
            }

            taiKhoan.MatKhau = matKhauMoi;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đổi mật khẩu thành công!";
            return RedirectToAction(nameof(Index1));
        }
    }
}