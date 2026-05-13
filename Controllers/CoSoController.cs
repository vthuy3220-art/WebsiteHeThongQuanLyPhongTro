using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class CoSoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CoSoController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Danh sách cơ sở
        public async Task<IActionResult> Index()
        {
            // Kiểm tra đăng nhập
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            var danhSachCoSo = await _context.CoSo.ToListAsync();
            return View(danhSachCoSo);
        }

        // GET: Tạo mới cơ sở
        public IActionResult Create()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }
            return View();
        }

        // POST: Tạo mới cơ sở
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CoSo coSo)
        {
            if (ModelState.IsValid)
            {
                _context.Add(coSo);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm cơ sở thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(coSo);
        }

        // GET: Chỉnh sửa cơ sở
        public async Task<IActionResult> Edit(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            var coSo = await _context.CoSo.FindAsync(id);
            if (coSo == null)
            {
                return NotFound();
            }
            return View(coSo);
        }

        // POST: Chỉnh sửa cơ sở
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CoSo coSo)
        {
            if (id != coSo.MaCoSo)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(coSo);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật cơ sở thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CoSoExists(coSo.MaCoSo))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(coSo);
        }

        // GET: Xóa cơ sở
        public async Task<IActionResult> Delete(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            var coSo = await _context.CoSo
                .FirstOrDefaultAsync(m => m.MaCoSo == id);
            if (coSo == null)
            {
                return NotFound();
            }

            return View(coSo);
        }

        // POST: Xóa cơ sở
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var coSo = await _context.CoSo.FindAsync(id);
            if (coSo != null)
            {
                // Kiểm tra xem cơ sở có đang có phòng không
                var coPhong = await _context.Phong.AnyAsync(p => p.MaCoSo == id);
                if (coPhong)
                {
                    TempData["Error"] = "Không thể xóa vì cơ sở này đang có phòng!";
                    return RedirectToAction(nameof(Index));
                }

                _context.CoSo.Remove(coSo);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa cơ sở thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Chi tiết cơ sở
        public async Task<IActionResult> Details(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            var coSo = await _context.CoSo
                .FirstOrDefaultAsync(m => m.MaCoSo == id);
            if (coSo == null)
            {
                return NotFound();
            }

            // Lấy danh sách phòng thuộc cơ sở này
            var danhSachPhong = await _context.Phong
                .Where(p => p.MaCoSo == id)
                .ToListAsync();

            ViewBag.DanhSachPhong = danhSachPhong;
            ViewBag.SoLuongPhong = danhSachPhong.Count;

            return View(coSo);
        }

        private bool CoSoExists(int id)
        {
            return _context.CoSo.Any(e => e.MaCoSo == id);
        }
    }
}