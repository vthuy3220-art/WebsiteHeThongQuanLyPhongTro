using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Services;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Session
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
// Trong hàm Configure hoặc cấu hình dịch vụ
// Services
builder.Services.AddScoped<ThongBaoService>();
builder.Services.AddScoped<EmailService>();

var app = builder.Build();

// THÊM ROTATIVA Ở ĐÂY
Rotativa.AspNetCore.RotativaConfiguration.Setup(
    app.Environment.WebRootPath,
    "Rotativa"
);

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
