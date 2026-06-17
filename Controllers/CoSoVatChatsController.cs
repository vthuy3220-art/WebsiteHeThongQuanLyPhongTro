using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class CoSoVatChatsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CoSoVatChatsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        private string GetCurrentRole()
        {
            return HttpContext.Session.GetString("Role") ?? "";
        }

        private int GetCurrentMaChuTro()
        {
            return HttpContext.Session.GetInt32("MaChuTro") ?? 0;
        }

        private bool IsAdmin()
        {
            var role = GetCurrentRole();
            return role == "Admin" || role == "SuperAdmin";
        }

        // GET: Danh sách CSVC (Admin xem hết, Chủ trọ xem của mình)
        public async Task<IActionResult> Index(string searchString, int? maPhong)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");

            var csvcList = _context.CoSoVatChat
                .Include(c => c.PhongNavigation)
                    .ThenInclude(p => p.ToaNha)
                .AsQueryable();

            // Chủ trọ: chỉ thấy CSVC của phòng mình
            List<Phong> phongListForFilter;
            if (!IsAdmin())
            {
                var maChuTro = GetCurrentMaChuTro();
                csvcList = csvcList.Where(c => c.PhongNavigation.ToaNha.MaChuTro == maChuTro);

                phongListForFilter = await _context.Phong
                    .Include(p => p.ToaNha)
                    .Where(p => p.ToaNha.MaChuTro == maChuTro)
                    .OrderBy(p => p.TenPhong)
                    .ToListAsync();
            }
            else
            {
                // Admin: thấy tất cả phòng để lọc
                phongListForFilter = await _context.Phong
                    .Include(p => p.ToaNha)
                    .OrderBy(p => p.TenPhong)
                    .ToListAsync();
            }

            // Lọc theo phòng
            if (maPhong.HasValue && maPhong.Value > 0)
                csvcList = csvcList.Where(c => c.MaPhong == maPhong.Value);

            // Tìm kiếm theo tên thiết bị
            if (!string.IsNullOrEmpty(searchString))
                csvcList = csvcList.Where(c => c.TenThietBi != null && c.TenThietBi.Contains(searchString));

            ViewBag.PhongListForFilter = phongListForFilter;
            ViewBag.MaPhong = maPhong;
            ViewBag.SearchString = searchString;

            return View(await csvcList.ToListAsync());
        }

        // GET: Chi tiết CSVC
        public async Task<IActionResult> Details(int? id)
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return RedirectToAction("Index", "Login");
            if (id == null) return NotFound();

            var csvc = await _context.CoSoVatChat
                .Include(c => c.PhongNavigation)
                    .ThenInclude(p => p.ToaNha)
                .FirstOrDefaultAsync(m => m.MaCSVC == id);

            if (csvc == null) return NotFound();

            if (!IsAdmin())
            {
                var maChuTro = GetCurrentMaChuTro();
                if (csvc.PhongNavigation?.ToaNha?.MaChuTro != maChuTro)
                {
                    TempData["Error"] = "Bạn không có quyền xem thiết bị này!";
                    return RedirectToAction(nameof(Index));
                }
            }

            return View(csvc);
        }

        // GET: Tạo mới (CHỈ CHỦ TRỌ)
        public async Task<IActionResult> Create()
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");

            if (IsAdmin())
            {
                TempData["Error"] = "Admin không có quyền thêm cơ sở vật chất!";
                return RedirectToAction(nameof(Index));
            }

            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                ViewBag.PhongList = await _context.Phong
                    .Include(p => p.ToaNha)
                    .Where(p => p.ToaNha.MaChuTro == maChuTro)
                    .ToListAsync();
            }

            return View();
        }

        // POST: Tạo mới (CHỈ CHỦ TRỌ)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int MaPhong, string TenThietBi, int SoLuong, string TinhTrang)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");

            if (IsAdmin())
            {
                TempData["Error"] = "Admin không có quyền thêm cơ sở vật chất!";
                return RedirectToAction(nameof(Index));
            }

            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                var phong = await _context.Phong
                    .Include(p => p.ToaNha)
                    .FirstOrDefaultAsync(p => p.MaPhong == MaPhong);

                if (phong == null || phong.ToaNha?.MaChuTro != maChuTro)
                {
                    TempData["Error"] = "Bạn không có quyền thêm thiết bị cho phòng này!";
                    return RedirectToAction(nameof(Index));
                }
            }

            var csvc = new CoSoVatChat
            {
                MaPhong = MaPhong,
                TenThietBi = TenThietBi,
                SoLuong = SoLuong,
                TinhTrang = TinhTrang ?? "Tốt"
            };
            _context.CoSoVatChat.Add(csvc);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã thêm {TenThietBi} vào phòng!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Chỉnh sửa (CHỈ CHỦ TRỌ)
        public async Task<IActionResult> Edit(int? id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");
            if (id == null) return NotFound();

            if (IsAdmin())
            {
                TempData["Error"] = "Admin không có quyền sửa cơ sở vật chất!";
                return RedirectToAction(nameof(Index));
            }

            var csvc = await _context.CoSoVatChat
                .Include(c => c.PhongNavigation)
                    .ThenInclude(p => p.ToaNha)
                .FirstOrDefaultAsync(m => m.MaCSVC == id);

            if (csvc == null) return NotFound();

            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                if (csvc.PhongNavigation?.ToaNha?.MaChuTro != maChuTro)
                {
                    TempData["Error"] = "Bạn không có quyền sửa thiết bị này!";
                    return RedirectToAction(nameof(Index));
                }
            }

            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                ViewBag.PhongList = await _context.Phong
                    .Include(p => p.ToaNha)
                    .Where(p => p.ToaNha.MaChuTro == maChuTro)
                    .ToListAsync();
            }

            return View(csvc);
        }

        // POST: Chỉnh sửa (CHỈ CHỦ TRỌ)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, int MaPhong, string TenThietBi, int SoLuong, string TinhTrang)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");

            if (IsAdmin())
            {
                TempData["Error"] = "Admin không có quyền sửa cơ sở vật chất!";
                return RedirectToAction(nameof(Index));
            }

            var csvc = await _context.CoSoVatChat
                .Include(c => c.PhongNavigation)
                    .ThenInclude(p => p.ToaNha)
                .FirstOrDefaultAsync(c => c.MaCSVC == id);

            if (csvc == null) return NotFound();

            if (role == "ChuTro")
            {
                var maChuTro = GetCurrentMaChuTro();
                if (csvc.PhongNavigation?.ToaNha?.MaChuTro != maChuTro)
                {
                    TempData["Error"] = "Bạn không có quyền sửa thiết bị này!";
                    return RedirectToAction(nameof(Index));
                }
            }

            csvc.MaPhong = MaPhong;
            csvc.TenThietBi = TenThietBi;
            csvc.SoLuong = SoLuong;
            csvc.TinhTrang = TinhTrang;

            _context.Update(csvc);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Xóa (CHỈ CHỦ TRỌ)
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();

            if (userId == 0) return RedirectToAction("Index", "Login");

            if (IsAdmin())
            {
                TempData["Error"] = "Admin không có quyền xóa cơ sở vật chất!";
                return RedirectToAction(nameof(Index));
            }

            var csvc = await _context.CoSoVatChat
                .Include(c => c.PhongNavigation)
                    .ThenInclude(p => p.ToaNha)
                .FirstOrDefaultAsync(c => c.MaCSVC == id);

            if (csvc != null)
            {
                if (role == "ChuTro")
                {
                    var maChuTro = GetCurrentMaChuTro();
                    if (csvc.PhongNavigation?.ToaNha?.MaChuTro != maChuTro)
                    {
                        TempData["Error"] = "Bạn không có quyền xóa thiết bị này!";
                        return RedirectToAction(nameof(Index));
                    }
                }

                _context.CoSoVatChat.Remove(csvc);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa thành công!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
