using HeThongQuanLyPhongTro.Data;
using HeThongQuanLyPhongTro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using PuppeteerSharp;

namespace HeThongQuanLyPhongTro.Controllers
{
    public class HopDongController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HopDongController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Xuất PDF hợp đồng
        public async Task<IActionResult> XuatPdf(int id)
        {
            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation).ThenInclude(p => p.CoSo)
                .Include(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(h => h.MaHopDong == id);

            if (hopDong == null) return NotFound();

            var nguoiOList = await _context.NguoiOHopDong
                .Where(n => n.MaHopDong == id)
                .ToListAsync();

            string htmlContent = TaoNoiDungHopDongHtml(hopDong, nguoiOList);

            await new BrowserFetcher().DownloadAsync();
            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(htmlContent);
            var pdfBytes = await page.PdfDataAsync();

            return File(pdfBytes, "application/pdf", $"HopDong_{hopDong.MaHopDong}.pdf");
        }

        private string TaoNoiDungHopDongHtml(HopDong hopDong, List<NguoiOHopDong> nguoiOList)
        {
            string danhSachNguoiOBang = "";
            if (nguoiOList != null && nguoiOList.Any())
            {
                int stt = 1;
                foreach (var n in nguoiOList)
                {
                    danhSachNguoiOBang += $@"
    <tr>
        <td class='text-center'>{stt}</td>
        <td>{n.HoTen}</td>
        <td>{n.CCCD}</td>
        <td>{n.SoDienThoai}</td>
        <td></td>
    </tr>";
                    stt++;
                }
            }
            else
            {
                danhSachNguoiOBang = @"
    <tr>
        <td colspan='5' class='text-center'>Không có người ở chung</td>
    </tr>";
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>HỢP ĐỒNG THUÊ PHÒNG</title>
    <style>
        body {{ font-family: 'Times New Roman', Times, serif; font-size: 14px; margin: 40px; line-height: 1.5; }}
        .center {{ text-align: center; }}
        .bold {{ font-weight: bold; }}
        .title {{ font-size: 18px; font-weight: bold; text-transform: uppercase; margin: 20px 0; }}
        .indent {{ text-indent: 30px; }}
        .sign-left {{ float: left; width: 45%; text-align: center; }}
        .sign-right {{ float: right; width: 45%; text-align: center; }}
        .clear {{ clear: both; }}
        .dieu {{ margin: 15px 0; }}
        .dieu-title {{ font-size: 15px; font-weight: bold; margin: 20px 0 10px 0; }}
        .dieu-number {{ font-size: 15px; font-weight: bold; margin: 15px 0 5px 0; }}
        .sign-line {{ border-top: 1px solid #000; width: 80%; margin: 35px auto 8px auto; }}
        table {{ width: 100%; border-collapse: collapse; margin: 15px 0; }}
        th, td {{ border: 1px solid #000; padding: 8px; text-align: left; }}
        th {{ background: #f0f0f0; text-align: center; }}
        .text-center {{ text-align: center; }}
        .header {{ margin-bottom: 20px; }}
        .bold {{ font-weight: bold; }}
        .underline {{ text-decoration: underline; }}
    </style>
</head>
<body>

<div class='header'>
    <div class='center'>
        <div class='bold'>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
        <div>Độc lập - Tự do - Hạnh phúc</div>
        <div>---------------</div>
        <div class='title'>HỢP ĐỒNG CHO THUÊ PHÒNG</div>
    </div>
</div>

<div style='margin: 15px 0;'>
    Hôm nay, ngày <strong>{DateTime.Now:dd} tháng {DateTime.Now:MM} năm {DateTime.Now:yyyy}</strong>
</div>

<div class='dieu-title'>CHÚNG TÔI GỒM:</div>

<div class='dieu'>
    <div class='dieu-title'>I. BÊN CHO THUÊ (BÊN A)</div>
    <div>Ông (Bà): <strong>Vũ Thị Thanh Thúy</strong></div>
    <div>ĐT: <strong>0869189018</strong></div>
    <div>Email: <strong>phongtroxinhh@gmail.com</strong></div>
    <div>CCCD số: <strong>034305004377</strong>. Cấp ngày: <strong>21/07/2021</strong> Tại: <strong>CCS</strong></div>
    <div>HKTT tại: <strong>Ngọc Thụy - Long Biên - Hà Nội</strong></div>
    <div>Là chủ sở hữu ngôi nhà tại địa chỉ: <strong>{hopDong.PhongNavigation?.CoSo?.DiaChi ?? ""}</strong></div>
</div>

<div class='dieu'>
    <div class='dieu-title'>II. BÊN THUÊ (BÊN B)</div>
    <div><strong>Đại diện: Ông (Bà): {hopDong.KhachHangNavigation?.HoTen}</strong></div>
    <div>SN: <strong>{hopDong.KhachHangNavigation?.NgaySinh:dd/MM/yyyy}</strong></div>
    <div>CCCD số: <strong>{hopDong.KhachHangNavigation?.CCCD}</strong>. Cấp ngày: <strong>---</strong> Tại: <strong>---</strong></div>
    <div>HKTT: <strong>{hopDong.KhachHangNavigation?.DiaChi}</strong></div>
    <div>DT: <strong>{hopDong.KhachHangNavigation?.SoDienThoai}</strong></div>
    <div>Email: <strong>{hopDong.KhachHangNavigation?.Email}</strong></div>
    <div>Tổng số người ở: <strong>{((nguoiOList?.Count ?? 0) + 1)} người</strong></div>
</div>

<div class='dieu'>
    <div class='dieu-title'>Người Ở Cùng:</div>
    <table>
        <thead>
            <tr class='bold'>
                <th class='text-center'>STT</th>
                <th>Họ và tên</th>
                <th>CCCD</th>
                <th>Số điện thoại</th>
                <th>Quê quán</th>
            </tr>
        </thead>
        <tbody>
            <tr>
                <td class='text-center'>1</td>
                <td>{hopDong.KhachHangNavigation?.HoTen}</td>
                <td>{hopDong.KhachHangNavigation?.CCCD}</td>
                <td>{hopDong.KhachHangNavigation?.SoDienThoai}</td>
                <td>{hopDong.KhachHangNavigation?.DiaChi}</td>
            </tr>
            {danhSachNguoiOBang}
        </tbody>
    </table>
</div>

<div class='dieu-title'>Sau khi thỏa thuận, hai Bên cùng kí hợp đồng với các điều khoản sau đây:</div>

<div class='dieu'>
    <div class='dieu-number'>ĐIỀU I: NỘI DUNG HỢP ĐỒNG</div>
    <div>- Bên A đồng ý cho Bên B thuê phòng <strong>{hopDong.PhongNavigation?.TenPhong}</strong></div>
    <div>- Trang thiết bị của phòng gồm có: Điều hòa + giường tủ + bình nóng lạnh + thiết bị vệ sinh</div>
    <div>- Trang thiết bị chung của ngôi nhà: Máy giặt + tủ lạnh + bếp từ</div>
    <div>- Mục đích cho thuê: Để ở</div>
    <div>- Hợp đồng có thời hạn: <strong>{(hopDong.NgayKetThuc?.Subtract(hopDong.NgayBatDau.Value).Days / 30)} tháng</strong>, tính từ ngày <strong>{hopDong.NgayBatDau:dd/MM/yyyy}</strong> đến hết ngày <strong>{hopDong.NgayKetThuc:dd/MM/yyyy}</strong>.</div>
</div>

<div class='dieu'>
    <div class='dieu-number'>ĐIỀU II: GIÁ CẢ, ĐẶT CỌC VÀ PHƯƠNG THỨC THANH TOÁN</div>
    <div>1. Giá thuê phòng là: <strong>{(hopDong.PhongNavigation?.GiaPhong ?? 0):N0} đồng/tháng</strong></div>
    <div>2. Giá thuê trên cố định trong suốt thời gian hợp đồng.</div>
    <div>3. Giá dịch vụ:</div>
    <div style='margin-left: 30px;'>
        - Điện: <strong>.....đ/KWh</strong><br>
        - Nước: <strong>.....đ/m3</strong><br>
        - Dịch vụ chung (Điện máy giặt + điện cầu thang + dọn vệ sinh + Internet): <strong>......đ/Người</strong>
    </div>
    <div>4. Phương thức thanh toán:</div>
    <div style='margin-left: 30px;'>
        - Bên B đặt cọc cho Bên A số tiền là: <strong>{(hopDong.TienCoc ?? 0):N0} đồng</strong><br>
        - Tiền cọc sẽ được Bên A hoàn trả sau 07 ngày khi kết thúc HĐ. Số tiền đặt này có thể được Bên A sử dụng để khắc phục sự cố hoặc vi phạm do Bên B gây ra.<br>
        - Bên B thanh toán cho Bên A tiền thuê phòng theo thông báo của Bên A. Thời hạn đóng tiền từ ngày <strong>28 hàng tháng</strong>. Thời gian chậm trả không quá 03 ngày.
    </div>
</div>

<div class='dieu'>
    <div class='dieu-number'>ĐIỀU III: QUYỀN VÀ TRÁCH NHIỆM CỦA BÊN B</div>
    <div>1. Sử dụng phòng đúng mục đích, đóng tiền theo thời hạn quy định trong hợp đồng.</div>
    <div>2. Sau 1 tuần đầu Bên A bàn giao trang thiết bị, trong quá trình sử dụng bị hư hỏng Bên B phải tự sửa chữa, thay thế, khắc phục hoặc bồi thường cho Bên A (Bao gồm cả tắc đường thoát nước, cháy bóng đèn).</div>
    <div>3. Nghiêm cấm mọi hành vi tàng trữ, sử dụng các chất ma túy, chất dễ cháy nổ, mại dâm, cờ bạc. Mọi hành vi vi phạm pháp luật Bên B hoàn toàn chịu trách nhiệm.</div>
    <div>4. Không được đập phá, tháo dỡ, không được thay đổi cấu trúc nhà, không đóng đinh, dán tranh ảnh, vẽ, bôi bẩn lên tường, cửa phòng.</div>
    <div>5. Bên B cam kết thực hiện hợp đồng với thời hạn nêu trên, nếu bên B chuyển trước thời hạn sẽ bị mất toàn bộ số tiền cọc. Nếu muốn kết thúc hợp đồng theo đúng thời hạn hai Bên thỏa thuận thì Bên B phải báo cho bên A trước 30 ngày, nếu không báo sẽ bị phạt 50% tiền đặt cọc.</div>
    <div>6. Các trường hợp thay đổi người ở hoặc chuyển nhượng phòng phải có sự đồng ý của Bên A.</div>
    <div>7. Tuân thủ tuyệt đối nội quy của tòa nhà: không cờ bạc, mại dâm, không sử dụng tàng trữ ma túy, vũ khí trái phép, không cho người lạ ngủ qua đêm, không tổ chức rượu chè, gây rối trật tự, mất vệ sinh, ý thức kém, làm ảnh hưởng tới người xung quanh. Nếu vi phạm sẽ bị phạt theo nội quy của tòa nhà, hoặc Bên A có quyền đơn phương chấm dứt hợp đồng (Bên B sẽ không nhận được tiền đặt cọc).</div>
    <div>8. Sau khi kết thúc Hợp đồng, Bên B có trách nhiệm thu dọn đồ đạc, trả lại phòng theo đúng nguyên trạng ban đầu và chịu chi phí <strong>200.000đ</strong> để bên A thuê người dọn vệ sinh công nghiệp.</div>
    <div>9. Bên B có trách nhiệm đi khai báo với công an khu vực để làm tạm trú, tạm vắng.</div>
    <div>10. Tuyệt đối đảm bảo an toàn PCCC, khóa gas, rút các thiết bị điện khi đi ra ngoài. Chịu hoàn toàn trách nhiệm nếu để xảy ra cháy nổ.</div>
    <div>11. Sau khi hết thời hạn thuê nhà mà hai bên không có thỏa thuận gì khác thì hợp đồng sẽ tự động gia hạn thêm 06 tháng mà không cần ký lại.</div>
</div>

<div class='dieu'>
    <div class='dieu-number'>ĐIỀU IV: CÁC THỎA THUẬN KHÁC</div>
    <div>1. Mọi tài sản của Bên B thì Bên B phải tự bảo quản, tự chịu trách nhiệm nếu xảy ra mất mát. Bên A không chịu trách nhiệm với các vấn đề trộm cắp, cháy nổ, tai nạn liên quan tới tính mạng con người của Bên B trong quá trình thuê phòng.</div>
    <div>2. Bên A có quyền chấm dứt hợp đồng trước hạn nếu Bên B vi phạm các điều khoản trong hợp đồng và Bên B không được nhận lại tiền đặt cọc.</div>
    <div>3. Mọi tranh chấp phát sinh liên quan tới hợp đồng này nếu không thể giải quyết sẽ được đưa ra Tòa án nhân dân có thẩm quyền để giải quyết theo quy định của pháp luật.</div>
    <div>4. Hợp đồng này được lập thành 02 bản, mỗi bên giữ 01 bản có giá trị như nhau. Hợp đồng có hiệu lực kể từ ngày ký.</div>
    <div>5. Sau khi ký hợp đồng, Bên B nộp lại cho bên A bản photo CMND/CCCD của tất cả những người ở phòng mình.</div>
</div>

<div class='dieu'>
    <div class='dieu-number'>THANH TOÁN BAN ĐẦU</div>
    <div>- Tiền cọc: <strong>{(hopDong.TienCoc ?? 0):N0} đồng</strong></div>
    <div>- Tiền phòng tháng đầu: <strong>{(hopDong.PhongNavigation?.GiaPhong ?? 0):N0} đồng</strong></div>
    <div>- Dịch vụ chung: <strong>{((nguoiOList?.Count ?? 0) + 1) * 200000:N0} đồng</strong></div>
</div>

<div class='dieu'>
    <div class='bold'>Kết bạn Zalo chủ nhà: <strong>0869189018</strong>.</div>
</div>

<div class='sign-box' style='margin-top: 40px;'>
    <div class='sign-left'>
        <div class='bold'>ĐẠI DIỆN BÊN A</div>
        <div class='sign-line'></div>
        <div>Vũ Thị Thanh Thúy</div>
    </div>
    <div class='sign-right'>
        <div class='bold'>ĐẠI DIỆN BÊN B</div>
        <div class='sign-line'></div>
        <div>{hopDong.KhachHangNavigation?.HoTen}</div>
    </div>
    <div class='clear'></div>
</div>

</body>
</html>";
        }

        // GET: Danh sách hợp đồng
        // GET: Danh sách hợp đồng (CHỈ ADMIN MỚI ĐƯỢC XEM)
        public async Task<IActionResult> Index(string searchString, string trangThai)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("Role");

            if (userId == null)
            {
                return RedirectToAction("Index", "Login");
            }

            // CHỈ ADMIN MỚI ĐƯỢC XEM DANH SÁCH TẤT CẢ HỢP ĐỒNG
            if (role != "Admin")
            {
                if (role == "Khach")
                {
                    return RedirectToAction("HopDongCuaToi", "KhachHang");
                }
                return RedirectToAction("Index", "Login");
            }

            var hopDongs = _context.HopDong
                .Include(h => h.PhongNavigation)
                .Include(h => h.KhachHangNavigation)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                hopDongs = hopDongs.Where(h =>
                    (h.PhongNavigation != null && h.PhongNavigation.TenPhong.Contains(searchString)) ||
                    (h.KhachHangNavigation != null && h.KhachHangNavigation.HoTen.Contains(searchString)));
            }

            if (!string.IsNullOrEmpty(trangThai) && trangThai != "Tất cả")
            {
                hopDongs = hopDongs.Where(h => h.TrangThai == trangThai);
            }

            ViewBag.SearchString = searchString;
            ViewBag.TrangThai = trangThai;
            ViewBag.TrangThaiList = new List<string> { "Tất cả", "Hiệu lực", "Hết hạn", "Đã hủy" };

            return View(await hopDongs.ToListAsync());
        }

        // GET: Tạo hợp đồng mới
        public async Task<IActionResult> Create()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            ViewBag.PhongList = await _context.Phong
                .Include(p => p.CoSo)
                .Where(p => p.TrangThai == "Trống")
                .ToListAsync();

            ViewBag.KhachHangList = await _context.KhachHang.ToListAsync();

            return View();
        }


        // POST: Tạo hợp đồng mới (CÓ TẠO TÀI KHOẢN VÀ LIÊN KẾT)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            int maPhong,
            DateTime ngayBatDau,
            DateTime ngayKetThuc,
            decimal tienCoc,
            // Thông tin khách hàng
            string HoTen,
            string SoDienThoai,
            string Email,
            string CCCD,
            string DiaChi,
            DateTime? NgaySinh,
            // Thông tin tài khoản
            bool taoTaiKhoan,
            string tenDangNhap,
            string matKhau,
            // Danh sách người ở
            List<string> NguoiOHoTen,
            List<string> NguoiOCCCD,
            List<string> NguoiOSDT)
        {
            // Kiểm tra phòng còn trống không
            var phong = await _context.Phong.FindAsync(maPhong);
            if (phong == null || phong.TrangThai != "Trống")
            {
                TempData["Error"] = "Phòng không còn trống!";
                await LoadViewBags();
                return View();
            }

            int maKhachHangCuoi = 0;

            // ========== 1. TẠO HOẶC LẤY KHÁCH HÀNG ==========
            // Kiểm tra khách hàng đã tồn tại theo số điện thoại
            var khachHangTonTai = await _context.KhachHang
                .FirstOrDefaultAsync(k => k.SoDienThoai == SoDienThoai);

            if (khachHangTonTai != null)
            {
                maKhachHangCuoi = khachHangTonTai.MaKhachHang;
                TempData["Info"] = "Khách hàng đã tồn tại, sử dụng thông tin cũ!";
            }
            else
            {
                // Tạo khách hàng mới
                var khachHangMoi = new KhachHang
                {
                    HoTen = HoTen.Trim(),
                    SoDienThoai = SoDienThoai.Trim(),
                    Email = Email ?? "",
                    CCCD = CCCD ?? "",
                    DiaChi = DiaChi ?? "",
                    NgaySinh = NgaySinh
                };
                _context.KhachHang.Add(khachHangMoi);
                await _context.SaveChangesAsync();
                maKhachHangCuoi = khachHangMoi.MaKhachHang;
            }

            // ========== 2. TẠO TÀI KHOẢN (nếu được chọn) ==========
            if (taoTaiKhoan && !string.IsNullOrEmpty(tenDangNhap) && !string.IsNullOrEmpty(matKhau))
            {
                // Kiểm tra tên đăng nhập đã tồn tại
                var tonTai = await _context.TaiKhoan.AnyAsync(t => t.TenDangNhap == tenDangNhap);
                if (!tonTai)
                {
                    var taiKhoan = new TaiKhoan
                    {
                        TenDangNhap = tenDangNhap,
                        MatKhau = matKhau,
                        VaiTro = "Khach",
                        TrangThai = "Hoạt động"
                    };
                    _context.TaiKhoan.Add(taiKhoan);
                    await _context.SaveChangesAsync();

                    // Liên kết tài khoản với khách hàng
                    var khachHang = await _context.KhachHang.FindAsync(maKhachHangCuoi);
                    if (khachHang != null)
                    {
                        khachHang.MaTaiKhoan = taiKhoan.MaTaiKhoan;
                        _context.Update(khachHang);
                        await _context.SaveChangesAsync();
                    }

                    TempData["TaiKhoan"] = $"Tài khoản đã tạo: {tenDangNhap} / {matKhau}";
                }
                else
                {
                    TempData["Warning"] = "Tên đăng nhập đã tồn tại, vui lòng chọn tên khác!";
                }
            }

            // ========== 3. TẠO HỢP ĐỒNG ==========
            var hopDong = new HopDong
            {
                MaPhong = maPhong,
                MaKhachHang = maKhachHangCuoi,
                NgayBatDau = ngayBatDau,
                NgayKetThuc = ngayKetThuc,
                TienCoc = tienCoc,
                TrangThai = "Hiệu lực"
            };
            _context.HopDong.Add(hopDong);
            await _context.SaveChangesAsync();

            // ========== 4. THÊM DANH SÁCH NGƯỜI Ở ==========
            if (NguoiOHoTen != null && NguoiOHoTen.Any())
            {
                for (int i = 0; i < NguoiOHoTen.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(NguoiOHoTen[i]))
                    {
                        var nguoiO = new NguoiOHopDong
                        {
                            MaHopDong = hopDong.MaHopDong,
                            HoTen = NguoiOHoTen[i].Trim(),
                            CCCD = NguoiOCCCD != null && i < NguoiOCCCD.Count ? NguoiOCCCD[i] ?? "" : "",
                            SoDienThoai = NguoiOSDT != null && i < NguoiOSDT.Count ? NguoiOSDT[i] ?? "" : "",
                        };
                        _context.NguoiOHopDong.Add(nguoiO);
                    }
                }
                await _context.SaveChangesAsync();
            }

            // ========== 5. CẬP NHẬT TRẠNG THÁI PHÒNG ==========
            phong.TrangThai = "Đã thuê";
            _context.Update(phong);
            await _context.SaveChangesAsync();

            // ========== 6. TẠO HÓA ĐƠN TỰ ĐỘNG ==========
            await TaoHoaDonChoHopDong(hopDong.MaHopDong, phong.GiaPhong, ngayBatDau, ngayKetThuc);

            TempData["Success"] = $"Tạo hợp đồng thành công! Mã hợp đồng: {hopDong.MaHopDong}";
            return RedirectToAction(nameof(Index));
        }

        // Load dữ liệu cho View Create
        private async Task LoadViewBags()
        {
            ViewBag.PhongList = await _context.Phong
                .Include(p => p.CoSo)
                .Where(p => p.TrangThai == "Trống")
                .ToListAsync();

            ViewBag.KhachHangList = await _context.KhachHang.ToListAsync();
        }

        // POST: Chấm dứt hợp đồng (CHỈ ADMIN)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChamDut(int id)
        {
            var role = HttpContext.Session.GetString("Role");

            // CHỈ ADMIN MỚI ĐƯỢC CHẤM DỨT HỢP ĐỒNG
            if (role != "Admin")
            {
                TempData["Error"] = "Bạn không có quyền thực hiện chức năng này!";
                return RedirectToAction("Index", "Login");
            }

            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation)
                .FirstOrDefaultAsync(h => h.MaHopDong == id);

            if (hopDong == null) return NotFound();

            hopDong.TrangThai = "Đã hủy";
            _context.Update(hopDong);

            var phong = hopDong.PhongNavigation;
            if (phong != null)
            {
                phong.TrangThai = "Trống";
                _context.Update(phong);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã chấm dứt hợp đồng!";
            return RedirectToAction(nameof(Index));
        }

        // Tạo hóa đơn tự động
        private async Task TaoHoaDonChoHopDong(int maHopDong, decimal giaPhong, DateTime ngayBatDau, DateTime ngayKetThuc)
        {
            int currentMonth = ngayBatDau.Month;
            int currentYear = ngayBatDau.Year;
            int endMonth = ngayKetThuc.Month;
            int endYear = ngayKetThuc.Year;

            while (currentYear < endYear || (currentYear == endYear && currentMonth <= endMonth))
            {
                var existingHoaDon = await _context.HoaDon
                    .AnyAsync(h => h.MaHopDong == maHopDong && h.Thang == currentMonth && h.Nam == currentYear);

                if (!existingHoaDon)
                {
                    var hoaDon = new HoaDon
                    {
                        MaHopDong = maHopDong,
                        Thang = currentMonth,
                        Nam = currentYear,
                        TongTien = giaPhong,
                        TrangThai = "Chưa thanh toán",
                        NgayTao = DateTime.Now
                    };
                    _context.HoaDon.Add(hoaDon);
                }

                currentMonth++;
                if (currentMonth > 12)
                {
                    currentMonth = 1;
                    currentYear++;
                }
            }

            await _context.SaveChangesAsync();
        }

        // GET: Chi tiết hợp đồng
        public async Task<IActionResult> Details(int? id)
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (id == null) return NotFound();

            var hopDong = await _context.HopDong
                .Include(h => h.PhongNavigation).ThenInclude(p => p.CoSo)
                .Include(h => h.KhachHangNavigation)
                .FirstOrDefaultAsync(m => m.MaHopDong == id);

            if (hopDong == null) return NotFound();

            // Kiểm tra nếu là khách hàng thì chỉ cho xem hợp đồng của mình
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");

            if (role == "Khach")
            {
                var khachHang = await _context.KhachHang
                    .FirstOrDefaultAsync(k => k.MaTaiKhoan == userId);

                if (khachHang == null || hopDong.MaKhachHang != khachHang.MaKhachHang)
                {
                    TempData["Error"] = "Bạn không có quyền xem hợp đồng này!";
                    return RedirectToAction("HopDongCuaToi", "KhachHang");
                }
            }

            var nguoiOList = await _context.NguoiOHopDong
                .Where(n => n.MaHopDong == id)
                .ToListAsync();

            var hoaDons = await _context.HoaDon
                .Where(h => h.MaHopDong == id)
                .OrderByDescending(h => h.Nam)
                .ThenByDescending(h => h.Thang)
                .ToListAsync();

            ViewBag.NguoiOList = nguoiOList;
            ViewBag.HoaDons = hoaDons;
            ViewBag.Role = role;  // Truyền role sang View

            return View(hopDong);
        }

        // API: Lấy thông tin phòng
        [HttpGet]
        public async Task<IActionResult> GetPhongInfo(int maPhong)
        {
            var phong = await _context.Phong
                .Include(p => p.CoSo)
                .FirstOrDefaultAsync(p => p.MaPhong == maPhong);

            if (phong == null)
                return Json(new { success = false });

            return Json(new
            {
                success = true,
                giaPhong = phong.GiaPhong,
                dienTich = phong.DienTich,
                tenCoSo = phong.CoSo?.TenCoSo
            });
        }
    }
}