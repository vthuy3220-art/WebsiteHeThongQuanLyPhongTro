using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HeThongQuanLyPhongTro.Models;
using HeThongQuanLyPhongTro.Data;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class ChiTietHoaDonsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ChiTietHoaDonsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ChiTietHoaDons
        public async Task<IActionResult> Index()
        {
            var chiTietHoaDons = _context.ChiTietHoaDon.Include(c => c.HoaDonNavigation);
            return View(await chiTietHoaDons.ToListAsync());
        }

        // GET: ChiTietHoaDons/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chiTietHoaDon = await _context.ChiTietHoaDon
                .Include(c => c.HoaDonNavigation)
                .FirstOrDefaultAsync(m => m.MaChiTiet == id);
            if (chiTietHoaDon == null)
            {
                return NotFound();
            }

            return View(chiTietHoaDon);
        }

        // GET: ChiTietHoaDons/Create
        public IActionResult Create()
        {
            ViewData["MaHoaDon"] = new SelectList(_context.HoaDon, "MaHoaDon", "MaHoaDon");
            return View();
        }

        // POST: ChiTietHoaDons/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaChiTiet,MaHoaDon,LoaiKhoanThu,SoLuong,DonGia,ThanhTien,GhiChu")] ChiTietHoaDon chiTietHoaDon)
        {
            if (ModelState.IsValid)
            {
                _context.Add(chiTietHoaDon);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["MaHoaDon"] = new SelectList(_context.HoaDon, "MaHoaDon", "MaHoaDon", chiTietHoaDon.MaHoaDon);
            return View(chiTietHoaDon);
        }

        // GET: ChiTietHoaDons/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chiTietHoaDon = await _context.ChiTietHoaDon.FindAsync(id);
            if (chiTietHoaDon == null)
            {
                return NotFound();
            }
            ViewData["MaHoaDon"] = new SelectList(_context.HoaDon, "MaHoaDon", "MaHoaDon", chiTietHoaDon.MaHoaDon);
            return View(chiTietHoaDon);
        }

        // POST: ChiTietHoaDons/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaChiTiet,MaHoaDon,LoaiKhoanThu,SoLuong,DonGia,ThanhTien,GhiChu")] ChiTietHoaDon chiTietHoaDon)
        {
            if (id != chiTietHoaDon.MaChiTiet)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(chiTietHoaDon);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ChiTietHoaDonExists(chiTietHoaDon.MaChiTiet))
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
            ViewData["MaHoaDon"] = new SelectList(_context.HoaDon, "MaHoaDon", "MaHoaDon", chiTietHoaDon.MaHoaDon);
            return View(chiTietHoaDon);
        }

        // GET: ChiTietHoaDons/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chiTietHoaDon = await _context.ChiTietHoaDon
                .Include(c => c.HoaDonNavigation)
                .FirstOrDefaultAsync(m => m.MaChiTiet == id);
            if (chiTietHoaDon == null)
            {
                return NotFound();
            }

            return View(chiTietHoaDon);
        }

        // POST: ChiTietHoaDons/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var chiTietHoaDon = await _context.ChiTietHoaDon.FindAsync(id);
            if (chiTietHoaDon != null)
            {
                _context.ChiTietHoaDon.Remove(chiTietHoaDon);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ChiTietHoaDonExists(int id)
        {
            return _context.ChiTietHoaDon.Any(e => e.MaChiTiet == id);
        }
    }
}