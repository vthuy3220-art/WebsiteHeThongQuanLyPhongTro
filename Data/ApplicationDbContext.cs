using HeThongQuanLyPhongTro.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongQuanLyPhongTro.Data
{
    // Lớp này quản lý kết nối và tương tác với database
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Các DbSet tương ứng với từng bảng trong database
        public DbSet<TaiKhoan> TaiKhoan { get; set; }
        public DbSet<KhachHang> KhachHang { get; set; }
        public DbSet<CoSo> CoSo { get; set; }
        public DbSet<Phong> Phong { get; set; }
        public DbSet<HopDong> HopDong { get; set; }
        public DbSet<NguoiOHopDong> NguoiOHopDong { get; set; }
        public DbSet<HoaDon> HoaDon { get; set; }
        public DbSet<ChiTietHoaDon> ChiTietHoaDon { get; set; }
        public DbSet<ThanhToan> ThanhToan { get; set; }
        public DbSet<BaiDang> BaiDang { get; set; }
        public DbSet<CoSoVatChat> CoSoVatChat { get; set; }
        public DbSet<PhongImage> PhongImages { get; set; }
        public DbSet<LichSuChiSoDienNuoc> LichSuChiSoDienNuoc { get; set; }
        public DbSet<ThongBao> ThongBao { get; set; }
        public DbSet<YeuCauSuaChua> YeuCauSuaChua { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Đặt tên bảng (khớp với SQL)
            modelBuilder.Entity<TaiKhoan>().ToTable("TaiKhoan");
            modelBuilder.Entity<KhachHang>().ToTable("KhachHang");
            modelBuilder.Entity<CoSo>().ToTable("CoSo");
            modelBuilder.Entity<Phong>().ToTable("Phong");
            modelBuilder.Entity<ChiTietHoaDon>().ToTable("ChiTietHoaDon");
            modelBuilder.Entity<ThongBao>().ToTable("ThongBao");
            modelBuilder.Entity<YeuCauSuaChua>().ToTable("YeuCauSuaChua");
            modelBuilder.Entity<NguoiOHopDong>()
         .ToTable(tb => tb.UseSqlOutputClause(false));

            modelBuilder.Entity<HopDong>()
                .ToTable(tb => tb.UseSqlOutputClause(false));

            modelBuilder.Entity<HoaDon>()
                .ToTable(tb => tb.UseSqlOutputClause(false));

            modelBuilder.Entity<ThanhToan>()
                .ToTable(tb => tb.UseSqlOutputClause(false));
            modelBuilder.Entity<BaiDang>().ToTable("BaiDang");
            modelBuilder.Entity<CoSoVatChat>().ToTable("CoSoVatChat");
            modelBuilder.Entity<PhongImage>().ToTable("PhongImage");
            modelBuilder.Entity<LichSuChiSoDienNuoc>().ToTable("LichSuChiSoDienNuoc");
            // Cấu hình quan hệ: Một hợp đồng có nhiều hóa đơn
            modelBuilder.Entity<HoaDon>()
                .HasOne(h => h.HopDongNavigation)
                .WithMany()
                .HasForeignKey(h => h.MaHopDong)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}