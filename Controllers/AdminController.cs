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

        // Đã thêm các tham số: searchToaNha, statusToaNha, và pageToaNha
        public async Task<IActionResult> Index(string tab = "Dashboard")
        {
            ViewBag.CurrentTab = tab;

            ViewBag.TotalToaNha = await _context.ToaNha.CountAsync();
            ViewBag.TotalPhong = await _context.Phong.CountAsync();
            ViewBag.PendingBaiDang = await _context.BaiDang.CountAsync(b => b.TrangThai == "Chờ duyệt");

            // ===== BỔ SUNG: THỐNG KÊ SỐ LƯỢNG BÀI ĐĂNG 6 THÁNG QUA =====
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");

            var queryBaiDang = _context.BaiDang.AsQueryable();
            var thongKeBaiDang = new List<object>();
            for (int i = 5; i >= 0; i--)
            {
                var mThang = DateTime.Now.AddMonths(-i);

                // Lấy đúng số lượng bài đăng thực tế theo từng tháng/năm
                int soLuong = await queryBaiDang
                    .Where(b => b.NgayDang.HasValue
                             && b.NgayDang.Value.Month == mThang.Month
                             && b.NgayDang.Value.Year == mThang.Year)
                    .CountAsync();

                thongKeBaiDang.Add(new
                {
                    thang = mThang.Month,
                    nam = mThang.Year,
                    soLuong = soLuong,
                    nhan = $"T{mThang.Month}"
                });
            }
            ViewBag.ThongKeBaiDang = thongKeBaiDang;


            // Khai báo danh sách bài đăng chuẩn
            var danhSachBaiDang = await _context.BaiDang
                .Include(b => b.PhongNavigation)
                .ThenInclude(p => p.ToaNha)
                .Include(b => b.PhongNavigation)
                .ThenInclude(p => p.ChuTro)
                .OrderByDescending(b => b.MaBaiDang)
                .ToListAsync();
            ViewBag.DanhSachBaiDang = danhSachBaiDang;

            // Tạo Dictionary map ảnh theo MaPhong
            var phongImages = await _context.PhongImage.ToListAsync();
            var dictAnhAdmin = phongImages
                .Where(i => i != null)
                .GroupBy(i => i.MaPhong)
                .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.ImagePath ?? "/images/default-room.jpg");
            ViewBag.DictAnhAdmin = dictAnhAdmin;

            // NẠP TOÀN BỘ DANH SÁCH ĐỂ JAVASCRIPT TỰ ĐỘNG LỌC TRÊN GIAO DIỆN
            var danhSachToaNha = await _context.ToaNha
                .Include(t => t.CoSo)
                .OrderByDescending(t => t.MaToaNha)
                .ToListAsync();
            ViewBag.DanhSachToaNha = danhSachToaNha;

            var danhSachCoSo = await _context.CoSo
                .OrderByDescending(c => c.MaCoSo)
                .ToListAsync();
            ViewBag.DanhSachCoSo = danhSachCoSo;

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

            // Sửa lại chữ so sánh ở đây cho khớp 100% với value từ Form gửi lên
            if (trangThai == "Hiển thị" || trangThai == "Hoạt động")
            {
                baiDang.TrangThai = "Hiển thị";
            }
            else if (trangThai == "Chờ duyệt" || trangThai == "Pending")
            {
                baiDang.TrangThai = "Chờ duyệt";
            }
            else
            {
                baiDang.TrangThai = "Từ chối";
            }

            await _context.SaveChangesAsync();
            // Sau khi lưu xong, ép chuyển hướng về lại tab BaiDang để reload lại số liệu trên đầu
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

        // ==================== HÀM POST CHÍNH: XÁC NHẬN XÓA CƠ SỞ (ĐÃ KHỚP FORM RIÊNG) ====================
        [HttpPost]
        public async Task<IActionResult> XacNhanXoaCoSo(int id)  // ← Sửa tham số thành 'id' để khớp chuẩn 'name="id"' ngoài Form
        {
            // 1. Tìm cơ sở kèm theo danh sách tòa nhà dựa trên 'id' nhận được từ form
            var coSo = await _context.CoSo
                .Include(c => c.ToaNhas)
                .FirstOrDefaultAsync(c => c.MaCoSo == id);

            if (coSo == null)
            {
                TempData["Error"] = "❌ Không tìm thấy thông tin cơ sở này trên hệ thống!";
                return RedirectToAction(nameof(Index), new { tab = "CoSo" });
            }

            // 2. TÍCH HỢP Ý CỦA THUẬN: Quét xem có phòng nào thuộc cơ sở này còn người ở ("Đã thuê") hay không
            bool dangCoNguoiO = await _context.Phong
                .Include(p => p.ToaNha)
                .AnyAsync(p => p.ToaNha.MaCoSo == id && p.TrangThai == "Đã thuê");

            if (dangCoNguoiO)
            {
                // Nếu vướng phòng đang thuê -> Chặn cứng, bắn lỗi ra màn hình chính Dashboard
                TempData["Error"] = $"❌ Không thể xóa! Cơ sở '{coSo.TenCoSo}' hiện vẫn còn các phòng đang có người ở (Đã thuê). Vui lòng thanh lý hợp đồng trước.";
                return RedirectToAction(nameof(Index), new { tab = "CoSo" });
            }

            // 3. Nếu kiểm tra đạt điều kiện TRỐNG HẾT -> Tiến hành xóa dọn dẹp bắc cầu (Cascade delete bằng code)
            try
            {
                if (coSo.ToaNhas != null && coSo.ToaNhas.Any())
                {
                    foreach (var toaNha in coSo.ToaNhas)
                    {
                        // Tìm và xóa sạch các phòng trống của từng tòa nhà trước để tránh lỗi Foreign Key
                        var danhSachPhongTrong = await _context.Phong
                            .Where(p => p.MaToaNha == toaNha.MaToaNha)
                            .ToListAsync();

                        _context.Phong.RemoveRange(danhSachPhongTrong);
                    }

                    // Sau khi dọn sạch phòng trống, thực hiện xóa danh sách tòa nhà
                    _context.ToaNha.RemoveRange(coSo.ToaNhas);
                }

                // Cuối cùng là xóa gốc thực thể Cơ sở
                _context.CoSo.Remove(coSo);

                // Thực thi lưu xuống Database SQL Server
                await _context.SaveChangesAsync();

                TempData["Success"] = $"✅ Đã dọn dẹp hạ tầng trống và xóa cơ sở '{coSo.TenCoSo}' thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "❌ Có lỗi hệ thống phát sinh trong quá trình xóa dữ liệu: " + ex.Message;
            }

            // Đẩy Admin quay trở lại đúng tab Cơ sở trên giao diện Dashboard chính
            return RedirectToAction(nameof(Index), new { tab = "CoSo" });
        }


        /* GET: Hiển thị form xác nhận xóa
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
        }*/

        // POST: Xác nhận xóa cơ sở

        /*[HttpPost]
        [ValidateAntiForgeryToken] 
        public async Task<IActionResult> XacNhanXoaCoSo(int id)
        {
            // 1. Tìm cơ sở và nạp kèm danh sách tòa nhà dựa trên id từ Form gửi lên
            var coSo = await _context.CoSo
                .Include(c => c.ToaNhas)
                .FirstOrDefaultAsync(c => c.MaCoSo == id);

            if (coSo == null)
            {
                TempData["Error"] = "❌ Không tìm thấy thông tin cơ sở này trên hệ thống!";
                return RedirectToAction(nameof(Index), new { tab = "CoSo" });
            }

            // 2. LOGIC CỦA THUẬN: Quét xem có phòng nào thuộc cơ sở này còn người ở ("Đã thuê") hay không
            bool dangCoNguoiO = await _context.Phong
                .Include(p => p.ToaNha)
                .AnyAsync(p => p.ToaNha.MaCoSo == id && p.TrangThai == "Đã thuê");

            if (dangCoNguoiO)
            {
                // Nếu vướng phòng đang thuê -> Chặn cứng, trả lỗi về trang Dashboard chính
                TempData["Error"] = $"❌ Không thể xóa! Cơ sở '{coSo.TenCoSo}' hiện vẫn còn các phòng đang có người ở (Đã thuê). Vui lòng thanh lý hợp đồng trước.";
                return RedirectToAction(nameof(Index), new { tab = "CoSo" });
            }

            // 3. Đạt điều kiện TRỐNG HẾT -> Tiến hành xóa dọn dẹp hạ tầng
            try
            {
                if (coSo.ToaNhas != null && coSo.ToaNhas.Any())
                {
                    foreach (var toaNha in coSo.ToaNhas)
                    {
                        // Tìm và xóa sạch dữ liệu phòng trống thuộc tòa nhà đó trước để không bị lỗi Khóa ngoại
                        var danhSachPhongTrong = await _context.Phong
                            .Where(p => p.MaToaNha == toaNha.MaToaNha)
                            .ToListAsync();

                        _context.Phong.RemoveRange(danhSachPhongTrong);
                    }

                    // Xóa danh sách tòa nhà trống trực thuộc cơ sở
                    _context.ToaNha.RemoveRange(coSo.ToaNhas);
                }

                // Cuối cùng xóa thực thể Cơ sở gốc
                _context.CoSo.Remove(coSo);

                // Lưu mọi thay đổi xuống SQL Server
                await _context.SaveChangesAsync();

                TempData["Success"] = $"✅ Đã giải phóng toàn bộ hạ tầng trống và xóa cơ sở '{coSo.TenCoSo}' thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "❌ Có lỗi hệ thống phát sinh khi xóa dữ liệu: " + ex.Message;
            }

            // Điều hướng Admin về lại tab Cơ sở trên Dashboard chính
            return RedirectToAction(nameof(Index), new { tab = "CoSo" });
        }*/

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
        public async Task<IActionResult> MoKhoaChuTro(int id)
        {
            var taiKhoan = await _context.TaiKhoan.FindAsync(id);
            if (taiKhoan == null) return NotFound();
            return View(taiKhoan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // Thêm bảo mật chống giả mạo
        public async Task<IActionResult> XacNhanMoKhoaTaiKhoan(int id)
        {
            var tk = await _context.TaiKhoan.FindAsync(id);
            if (tk != null)
            {
                tk.TrangThai = "Hoạt động";
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã mở khóa tài khoản {tk.TenDangNhap} thành công!";
            }
            // Mở khóa xong đẩy lại về tab Người dùng
            return RedirectToAction(nameof(Index), new { tab = "NguoiDung" });
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