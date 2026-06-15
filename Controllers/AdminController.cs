using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string tab = "Dashboard")
        {
            ViewBag.CurrentTab = tab;

            ViewBag.TotalToaNha = await _context.ToaNha.CountAsync();
            ViewBag.TotalPhong = await _context.Phong.CountAsync();
            ViewBag.PendingBaiDang = await _context.BaiDang.CountAsync(b => b.TrangThai == "Pending" || b.TrangThai == "Chờ duyệt");

            var danhSachBaiDang = await _context.BaiDang
                .Include(b => b.PhongNavigation)
                .OrderByDescending(b => b.MaBaiDang)
                .ToListAsync();
            ViewBag.DanhSachBaiDang = danhSachBaiDang;

            var danhSachToaNha = await _context.ToaNha
                .Include(t => t.CoSo)
                .OrderByDescending(t => t.MaToaNha)
                .ToListAsync();
            ViewBag.DanhSachToaNha = danhSachToaNha;

            var danhSachCoSo = await _context.CoSo
                .OrderByDescending(c => c.MaCoSo)
                .ToListAsync();
            ViewBag.DanhSachCoSo = danhSachCoSo;

            // Danh sách chủ trọ cho tab NguoiDung
            var danhSachChuTro = await _context.TaiKhoan
                .Where(t => t.VaiTro == "ChuTro")
                .OrderByDescending(t => t.MaTaiKhoan)
                .ToListAsync();
            ViewBag.DanhSachChuTro = danhSachChuTro;

            return View();
        }

        // ==================== DUYỆT BÀI ĐĂNG ====================
        [HttpPost]
        public async Task<IActionResult> DuyetBaiDang(int id, string trangThai)
        {
            var baiDang = await _context.BaiDang.FindAsync(id);
            if (baiDang == null) return NotFound();

            baiDang.TrangThai = trangThai;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { tab = "BaiDang" });
        }

        // ==================== DUYỆT TÒA NHÀ ====================
        [HttpPost]
        public async Task<IActionResult> DuyetToaNha(int id, string trangThai)
        {
            var toaNha = await _context.ToaNha.FindAsync(id);
            if (toaNha == null) return NotFound();

            toaNha.TrangThai = trangThai;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { tab = "ToaNha" });
        }

        // ==================== THÊM TÒA NHÀ MỚI (ADMIN THÊM HỘ CHỦ TRỌ) ====================
        [HttpPost]
        public async Task<IActionResult> ThemToaNha(string tenToaNha, int maCoSo, int maChuTro, string diaChi)
        {
            if (string.IsNullOrEmpty(tenToaNha) || maChuTro == 0)
            {
                return RedirectToAction(nameof(Index), new { tab = "ToaNha" });
            }

            var toaNha = new ToaNha
            {
                TenToaNha = tenToaNha,
                MaCoSo = maCoSo,
                MaChuTro = maChuTro,
                DiaChi = diaChi,
                TrangThai = "Hoạt động"
            };

            _context.ToaNha.Add(toaNha);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { tab = "ToaNha" });
        }

        // ==================== QUẢN LÝ CƠ SỞ ====================
        [HttpPost]
        public async Task<IActionResult> ThemCoSo(string tenCoSo, string diaChi, string moTa)
        {
            if (string.IsNullOrEmpty(tenCoSo)) return RedirectToAction(nameof(Index), new { tab = "CoSo" });

            var coSo = new CoSo
            {
                TenCoSo = tenCoSo,
                DiaChi = diaChi,
                MoTa = moTa
            };

            _context.CoSo.Add(coSo);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { tab = "CoSo" });
        }

        [HttpPost]
        public async Task<IActionResult> SuaCoSo(int maCoSo, string tenCoSo, string diaChi, string moTa)
        {
            var coSo = await _context.CoSo.FindAsync(maCoSo);
            if (coSo == null) return NotFound();

            coSo.TenCoSo = tenCoSo;
            coSo.DiaChi = diaChi;
            coSo.MoTa = moTa;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { tab = "CoSo" });
        }

        // ==================== XÓA CƠ SỞ (CÓ KIỂM TRA RÀNG BUỘC) ====================
        // Thêm action này vào AdminController
        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> XoaCoSo(int id)
        {
            var coSo = await _context.CoSo
                .Include(c => c.ToaNhas)
                .FirstOrDefaultAsync(c => c.MaCoSo == id);

            if (coSo == null)
            {
                TempData["Error"] = "Không tìm thấy cơ sở này!";
                return RedirectToAction(nameof(Index), new { tab = "CoSo" });
            }

            // KIỂM TRA NẾU CÓ TÒA NHÀ -> BÁO LỖI RÕ RÀNG
            if (coSo.ToaNhas != null && coSo.ToaNhas.Any())
            {
                int soLuongToa = coSo.ToaNhas.Count;
                TempData["Error"] = $"❌ Không thể xóa cơ sở '{coSo.TenCoSo}' vì đang có {soLuongToa} tòa nhà thuộc cơ sở này! Vui lòng xóa hoặc chuyển các tòa nhà trước khi xóa cơ sở.";
                return RedirectToAction(nameof(Index), new { tab = "CoSo" });
            }

            // Nếu không có tòa nhà thì mới xóa
            _context.CoSo.Remove(coSo);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Đã xóa cơ sở '{coSo.TenCoSo}' thành công!";
            return RedirectToAction(nameof(Index), new { tab = "CoSo" });
        }
        // GET: Hiển thị form xác nhận xóa
        [HttpGet]
        public async Task<IActionResult> FormXoaCoSo(int id)
        {
            var coSo = await _context.CoSo
                .Include(c => c.ToaNhas)
                .FirstOrDefaultAsync(c => c.MaCoSo == id);

            if (coSo == null) return NotFound();

            // Kiểm tra nếu có tòa nhà thì thông báo và quay lại
            if (coSo.ToaNhas != null && coSo.ToaNhas.Any())
            {
                TempData["Error"] = $"Không thể xóa cơ sở '{coSo.TenCoSo}' vì vẫn còn {coSo.ToaNhas.Count} tòa nhà thuộc cơ sở này! Vui lòng chuyển hoặc xóa các tòa nhà trước.";
                return RedirectToAction(nameof(Index), new { tab = "CoSo" });
            }

            return View("XoaCoSo", coSo);
        }

        // POST: Xác nhận xóa cơ sở
        [HttpPost]
        public async Task<IActionResult> XacNhanXoaCoSo(int maCoSo)  // ← đổi tên parameter thành maCoSo
        {
            var coSo = await _context.CoSo
                .Include(c => c.ToaNhas)
                .FirstOrDefaultAsync(c => c.MaCoSo == maCoSo);  // ← dùng maCoSo để tìm

            if (coSo == null)
            {
                TempData["Error"] = "Không tìm thấy cơ sở!";
                return RedirectToAction(nameof(Index), new { tab = "CoSo" });
            }

            // Kiểm tra lại lần nữa trước khi xóa
            if (coSo.ToaNhas != null && coSo.ToaNhas.Any())
            {
                TempData["Error"] = $"Không thể xóa cơ sở '{coSo.TenCoSo}' vì vẫn còn {coSo.ToaNhas.Count} tòa nhà liên quan!";
                return RedirectToAction(nameof(Index), new { tab = "CoSo" });
            }

            _context.CoSo.Remove(coSo);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã xóa cơ sở '{coSo.TenCoSo}' thành công!";
            return RedirectToAction(nameof(Index), new { tab = "CoSo" });
        }

        // ==================== QUẢN LÝ THÀNH VIÊN (CHỦ TRỌ) ====================
        [HttpPost]
        public async Task<IActionResult> KhoaTaiKhoan(int id)
        {
            var tk = await _context.TaiKhoan.FindAsync(id);
            if (tk != null)
            {
                tk.TrangThai = "Khóa";
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { tab = "NguoiDung" });
        }

        [HttpPost]
        public async Task<IActionResult> MoKhoaTaiKhoan(int id)
        {
            var tk = await _context.TaiKhoan.FindAsync(id);
            if (tk != null)
            {
                tk.TrangThai = "Hoạt động";
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { tab = "NguoiDung" });
        }

        [HttpGet]
        public async Task<IActionResult> KhoaChuTro(int id)
        {
            var taiKhoan = await _context.TaiKhoan.FindAsync(id);
            if (taiKhoan == null) return NotFound();
            return View(taiKhoan);
        }

        [HttpGet]
        public async Task<IActionResult> XoaChuTro(int id)
        {
            var taiKhoan = await _context.TaiKhoan.FindAsync(id);
            if (taiKhoan == null) return NotFound();
            return View(taiKhoan);
        }

        // ==================== THÊM & SỬA TÀI KHOẢN CHỦ TRỌ ====================
        [HttpPost]
        public async Task<IActionResult> ThemChuTro(string tenDangNhap, string matKhau, string email)
        {
            if (string.IsNullOrEmpty(tenDangNhap) || string.IsNullOrEmpty(matKhau))
            {
                return RedirectToAction(nameof(Index), new { tab = "NguoiDung" });
            }

            var taiKhoan = new TaiKhoan
            {
                TenDangNhap = tenDangNhap,
                MatKhau = matKhau,
                Email = email,
                VaiTro = "ChuTro",
                TrangThai = "Hoạt động"
            };

            _context.TaiKhoan.Add(taiKhoan);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { tab = "NguoiDung" });
        }

        [HttpGet]
        public async Task<IActionResult> SuaChuTro(int id)
        {
            var taiKhoan = await _context.TaiKhoan.FindAsync(id);
            if (taiKhoan == null) return NotFound();

            return View("SuaChuTro", taiKhoan);
        }

        [HttpPost]
        public async Task<IActionResult> SuaChuTro(int maTaiKhoan, string email, string trangThai)
        {
            var taiKhoan = await _context.TaiKhoan.FindAsync(maTaiKhoan);
            if (taiKhoan == null) return NotFound();

            taiKhoan.Email = email;
            taiKhoan.TrangThai = trangThai;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { tab = "NguoiDung" });
        }

        // ==================== NGỪNG HOẠT ĐỘNG TÒA NHÀ ====================
        [HttpGet]
        public async Task<IActionResult> NgungHoatDongToaNha(int id)
        {
            var toaNha = await _context.ToaNha.FindAsync(id);
            if (toaNha == null) return NotFound();

            return View("NgungHoatDongToaNha", toaNha);
        }

        [HttpPost]
        public async Task<IActionResult> XacNhanDuyetToaNha(int id, string trangThai)
        {
            var toaNha = await _context.ToaNha.FindAsync(id);
            if (toaNha == null) return NotFound();

            toaNha.TrangThai = trangThai;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { tab = "ToaNha" });
        }

        // ==================== QUẢN LÝ TÀI KHOẢN CHỦ TRỌ (CHUẨN MVC) ====================
        public async Task<IActionResult> DanhSachChuTro()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && role != "SuperAdmin")
            {
                return RedirectToAction("Index", "Login");
            }

            var danhSachChuTro = await _context.TaiKhoan
                .Where(t => t.VaiTro == "ChuTro")
                .OrderByDescending(t => t.MaTaiKhoan)
                .ToListAsync();

            return View(danhSachChuTro);
        }

        public IActionResult TaoTaiKhoanChuTro()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && role != "SuperAdmin")
            {
                return RedirectToAction("Index", "Login");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TaoTaiKhoanChuTro(TaiKhoan taiKhoan)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && role != "SuperAdmin")
            {
                return RedirectToAction("Index", "Login");
            }

            var exists = await _context.TaiKhoan
                .AnyAsync(t => t.TenDangNhap == taiKhoan.TenDangNhap);

            if (exists)
            {
                ModelState.AddModelError("TenDangNhap", "Tên đăng nhập đã tồn tại!");
                return View(taiKhoan);
            }

            if (ModelState.IsValid)
            {
                taiKhoan.VaiTro = "ChuTro";
                taiKhoan.TrangThai = "Hoạt động";

                _context.TaiKhoan.Add(taiKhoan);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Tạo tài khoản Chủ trọ thành công! Tài khoản: {taiKhoan.TenDangNhap} / Mật khẩu: {taiKhoan.MatKhau}";

                return RedirectToAction(nameof(DanhSachChuTro));
            }

            return View(taiKhoan);
        }

        public async Task<IActionResult> SuaTaiKhoanChuTro(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && role != "SuperAdmin")
            {
                return RedirectToAction("Index", "Login");
            }

            var taiKhoan = await _context.TaiKhoan.FindAsync(id);
            if (taiKhoan == null || taiKhoan.VaiTro != "ChuTro")
            {
                TempData["Error"] = "Không tìm thấy tài khoản chủ trọ!";
                return RedirectToAction(nameof(DanhSachChuTro));
            }

            return View(taiKhoan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuaTaiKhoanChuTro(int id, TaiKhoan taiKhoan)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && role != "SuperAdmin")
            {
                return RedirectToAction("Index", "Login");
            }

            if (id != taiKhoan.MaTaiKhoan) return NotFound();

            var taiKhoanCu = await _context.TaiKhoan.AsNoTracking()
                .FirstOrDefaultAsync(t => t.MaTaiKhoan == id);

            if (taiKhoanCu == null || taiKhoanCu.VaiTro != "ChuTro")
            {
                TempData["Error"] = "Không tìm thấy tài khoản chủ trọ!";
                return RedirectToAction(nameof(DanhSachChuTro));
            }

            var exists = await _context.TaiKhoan
                .AnyAsync(t => t.TenDangNhap == taiKhoan.TenDangNhap && t.MaTaiKhoan != id);

            if (exists)
            {
                ModelState.AddModelError("TenDangNhap", "Tên đăng nhập đã tồn tại!");
                return View(taiKhoan);
            }

            if (ModelState.IsValid)
            {
                taiKhoan.VaiTro = "ChuTro";
                _context.Update(taiKhoan);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Cập nhật tài khoản {taiKhoan.TenDangNhap} thành công!";
                return RedirectToAction(nameof(DanhSachChuTro));
            }

            return View(taiKhoan);
        }

        public async Task<IActionResult> XoaTaiKhoanChuTro(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && role != "SuperAdmin")
            {
                return RedirectToAction("Index", "Login");
            }

            var taiKhoan = await _context.TaiKhoan
                .FirstOrDefaultAsync(t => t.MaTaiKhoan == id && t.VaiTro == "ChuTro");

            if (taiKhoan == null)
            {
                TempData["Error"] = "Không tìm thấy tài khoản chủ trọ!";
                return RedirectToAction(nameof(DanhSachChuTro));
            }

            var coToaNha = await _context.ToaNha.AnyAsync(t => t.MaChuTro == id);
            if (coToaNha)
            {
                TempData["Error"] = "Không thể xóa vì chủ trọ này đã có tòa nhà!";
                return RedirectToAction(nameof(DanhSachChuTro));
            }

            return View(taiKhoan);
        }

        [HttpPost, ActionName("XoaTaiKhoanChuTro")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaTaiKhoanChuTroConfirmed(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && role != "SuperAdmin")
            {
                return RedirectToAction("Index", "Login");
            }

            var taiKhoan = await _context.TaiKhoan.FindAsync(id);
            if (taiKhoan != null && taiKhoan.VaiTro == "ChuTro")
            {
                var coToaNha = await _context.ToaNha.AnyAsync(t => t.MaChuTro == id);
                if (coToaNha)
                {
                    TempData["Error"] = "Không thể xóa vì chủ trọ này đã có tòa nhà!";
                    return RedirectToAction(nameof(DanhSachChuTro));
                }

                _context.TaiKhoan.Remove(taiKhoan);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Xóa tài khoản {taiKhoan.TenDangNhap} thành công!";
            }

            return RedirectToAction(nameof(DanhSachChuTro));
        }

        public async Task<IActionResult> KhoaMoTaiKhoan(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin" && role != "SuperAdmin")
            {
                return RedirectToAction("Index", "Login");
            }

            var taiKhoan = await _context.TaiKhoan.FindAsync(id);
            if (taiKhoan == null)
            {
                TempData["Error"] = "Không tìm thấy tài khoản!";
                return RedirectToAction(nameof(DanhSachChuTro));
            }

            taiKhoan.TrangThai = taiKhoan.TrangThai == "Hoạt động" ? "Khóa" : "Hoạt động";
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã {(taiKhoan.TrangThai == "Hoạt động" ? "mở khóa" : "khóa")} tài khoản {taiKhoan.TenDangNhap}!";

            return RedirectToAction(nameof(DanhSachChuTro));
        }
    }
}