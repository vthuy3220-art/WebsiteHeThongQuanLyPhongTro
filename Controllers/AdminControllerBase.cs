using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using HeThongQuanLyPhongTro.Data;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class AdminControllerBase : BaseController
    {
        public AdminControllerBase(ApplicationDbContext context) : base(context)
        {
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);

            var role = HttpContext.Session.GetString("Role");

            // Phân quyền: Chỉ cho phép Admin và SuperAdmin vào trang quản trị
            if (string.IsNullOrEmpty(role) ||
                (role != "Admin" && role != "SuperAdmin"))
            {
                context.Result = RedirectToAction("Index", "Home");
            }
        }
    }
}