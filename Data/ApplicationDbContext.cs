using HeThongQuanLyPhongTro.Models;
using Microsoft.EntityFrameworkCore;

namespace HeThongQuanLyPhongTro.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSet cho các bảng
        public DbSet<TaiKhoan> TaiKhoan { get; set; }
        public DbSet<KhachHang> KhachHang { get; set; }
        public DbSet<CoSo> CoSo { get; set; }
        public DbSet<ToaNha> ToaNha { get; set; }
        public DbSet<Phong> Phong { get; set; }
        public DbSet<BaiDang> BaiDang { get; set; }
        public DbSet<CoSoVatChat> CoSoVatChat { get; set; }
        public DbSet<HopDong> HopDong { get; set; }
        public DbSet<NguoiOHopDong> NguoiOHopDong { get; set; }
        public DbSet<HoaDon> HoaDon { get; set; }
        public DbSet<ChiTietHoaDon> ChiTietHoaDon { get; set; }
        public DbSet<ThanhToan> ThanhToan { get; set; }
        public DbSet<ThongBao> ThongBao { get; set; }
        public DbSet<YeuCauSuaChua> YeuCauSuaChua { get; set; }
        public DbSet<PhongImage> PhongImage { get; set; }
        public DbSet<LichSuChiSoDienNuoc> LichSuChiSoDienNuoc { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ==================== CẤU HÌNH QUAN HỆ ====================

            // 1. ToaNha - CoSo (Một cơ sở có nhiều tòa nhà)
            modelBuilder.Entity<ToaNha>()
                .HasOne(t => t.CoSo)
                .WithMany(c => c.ToaNhas)
                .HasForeignKey(t => t.MaCoSo)
                .OnDelete(DeleteBehavior.Restrict);

            // 2. ToaNha - TaiKhoan (Một chủ trọ có nhiều tòa nhà)
            modelBuilder.Entity<ToaNha>()
                .HasOne(t => t.ChuTro)
                .WithMany(tk => tk.ToaNhas)
                .HasForeignKey(t => t.MaChuTro)
                .OnDelete(DeleteBehavior.Restrict);

            // 3. Phong - ToaNha (Một tòa nhà có nhiều phòng)
            modelBuilder.Entity<Phong>()
                .HasOne(p => p.ToaNha)
                .WithMany(t => t.Phongs)
                .HasForeignKey(p => p.MaToaNha)
                .OnDelete(DeleteBehavior.Restrict);

            // 4. Phong - TaiKhoan (Chủ trọ sở hữu phòng)
            modelBuilder.Entity<Phong>()
                .HasOne(p => p.ChuTro)
                .WithMany()
                .HasForeignKey(p => p.MaChuTro)
                .OnDelete(DeleteBehavior.Restrict);

            // 5. HopDong - Phong
            modelBuilder.Entity<HopDong>()
                .HasOne(h => h.PhongNavigation)
                .WithMany()
                .HasForeignKey(h => h.MaPhong)
                .OnDelete(DeleteBehavior.Restrict);

            // 6. HopDong - KhachHang
            modelBuilder.Entity<HopDong>()
                .HasOne(h => h.KhachHangNavigation)
                .WithMany()
                .HasForeignKey(h => h.MaKhachHang)
                .OnDelete(DeleteBehavior.Restrict);

            // 7. HopDong - TaiKhoan (Chủ trọ)
            modelBuilder.Entity<HopDong>()
                .HasOne(h => h.ChuTroNavigation)
                .WithMany()
                .HasForeignKey(h => h.MaChuTro)
                .OnDelete(DeleteBehavior.Restrict);

            // 8. HoaDon - HopDong
            modelBuilder.Entity<HoaDon>()
                .HasOne(hd => hd.HopDongNavigation)
                .WithMany()
                .HasForeignKey(hd => hd.MaHopDong)
                .OnDelete(DeleteBehavior.Restrict);

            // 9. HoaDon - TaiKhoan (Chủ trọ)
            modelBuilder.Entity<HoaDon>()
                .HasOne(hd => hd.ChuTroNavigation)
                .WithMany()
                .HasForeignKey(hd => hd.MaChuTro)
                .OnDelete(DeleteBehavior.Restrict);

            // 10. BaiDang - Phong
            modelBuilder.Entity<BaiDang>()
                .HasOne(b => b.PhongNavigation)
                .WithMany()
                .HasForeignKey(b => b.MaPhong)
                .OnDelete(DeleteBehavior.Restrict);

            // 11. BaiDang - TaiKhoan (Chủ trọ)
            modelBuilder.Entity<BaiDang>()
                .HasOne(b => b.ChuTroNavigation)
                .WithMany()
                .HasForeignKey(b => b.MaChuTro)
                .OnDelete(DeleteBehavior.Restrict);

            // 12. YeuCauSuaChua - Phong
            modelBuilder.Entity<YeuCauSuaChua>()
                .HasOne(y => y.PhongNavigation)
                .WithMany()
                .HasForeignKey(y => y.MaPhong)
                .OnDelete(DeleteBehavior.Restrict);

            // 13. YeuCauSuaChua - KhachHang
            modelBuilder.Entity<YeuCauSuaChua>()
                .HasOne(y => y.KhachHangNavigation)
                .WithMany()
                .HasForeignKey(y => y.MaKhachHang)
                .OnDelete(DeleteBehavior.Restrict);

            /*14. YeuCauSuaChua - TaiKhoan (Chủ trọ)
            modelBuilder.Entity<YeuCauSuaChua>()
                .HasOne(y => y.ChuTroNavigation)
                .WithMany()
                .HasForeignKey(y => y.MaChuTro)
                .OnDelete(DeleteBehavior.Restrict); */

            // 15. Cấu hình decimal để tránh lỗi precision
            modelBuilder.Entity<Phong>()
                .Property(p => p.GiaPhong)
                .HasPrecision(18, 2);

            modelBuilder.Entity<HopDong>()
                .Property(h => h.TienCoc)
                .HasPrecision(18, 2);

            modelBuilder.Entity<HoaDon>()
                .Property(h => h.TongTien)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ThanhToan>()
                .Property(t => t.SoTien)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ChiTietHoaDon>()
                .Property(c => c.DonGia)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ChiTietHoaDon>()
                .Property(c => c.ThanhTien)
                .HasPrecision(18, 2);

            modelBuilder.Entity<YeuCauSuaChua>()
                .Property(y => y.ChiPhiPhatSinh)
                .HasPrecision(18, 2);

            modelBuilder.Entity<LichSuChiSoDienNuoc>()
                .Property(l => l.ChiSoDienCu)
                .HasPrecision(18, 2);

            modelBuilder.Entity<LichSuChiSoDienNuoc>()
                .Property(l => l.ChiSoDienMoi)
                .HasPrecision(18, 2);

            modelBuilder.Entity<LichSuChiSoDienNuoc>()
                .Property(l => l.ChiSoNuocCu)
                .HasPrecision(18, 2);

            modelBuilder.Entity<LichSuChiSoDienNuoc>()
                .Property(l => l.ChiSoNuocMoi)
                .HasPrecision(18, 2);

            // 16. Giải quyết lỗi trigger (nếu có)
            modelBuilder.Entity<NguoiOHopDong>()
                .ToTable(tb => tb.UseSqlOutputClause(false));

            modelBuilder.Entity<HopDong>()
                .ToTable(tb => tb.UseSqlOutputClause(false));

            modelBuilder.Entity<HoaDon>()
                .ToTable(tb => tb.UseSqlOutputClause(false));

            modelBuilder.Entity<ThanhToan>()
                .ToTable(tb => tb.UseSqlOutputClause(false));
        }
    }
}