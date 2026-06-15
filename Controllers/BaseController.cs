using HeThongQuanLyPhongTro.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongQuanLyPhongTro.Controllers
{
    public abstract class BaseController : Controller
    {
        protected readonly ApplicationDbContext _context;

        protected BaseController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Lấy UserId hiện tại
        protected int GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        // Lấy Role hiện tại
        protected string GetCurrentRole()
        {
            return HttpContext.Session.GetString("Role") ?? "";
        }

        // Kiểm tra có phải SuperAdmin không
        protected bool IsSuperAdmin()
        {
            return GetCurrentRole() == "SuperAdmin";
        }

        // Kiểm tra có phải Chủ trọ không
        protected bool IsChuTro()
        {
            return GetCurrentRole() == "ChuTro";
        }

        // Kiểm tra có phải Khách không
        protected bool IsKhach()
        {
            return GetCurrentRole() == "Khach";
        }
    }
}