using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace HeThongQuanLyPhongTro.Services
{
    public class EmailService
    {
        private const string SENDER_EMAIL = "phongtroxinhh@gmail.com";
        private const string SENDER_PASSWORD = "xxyhvsajcqeppcma";  // App Password
        private const string SMTP_SERVER = "smtp.gmail.com";
        private const int SMTP_PORT = 587;

        // Hàm gửi email hóa đơn (có file đính kèm PDF)
        public async Task<bool> GuiEmailHoaDon(string toEmail, string toName, string maHoaDon, byte[] pdfBytes)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(SENDER_EMAIL));
                email.To.Add(MailboxAddress.Parse(toEmail));
                email.Subject = $"HÓA ĐƠN THANH TOÁN - {maHoaDon}";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px;'>
                        <h2 style='color: #2563eb;'>🏠 PHÒNG TRỌ XINH</h2>
                        <p>Kính gửi <strong>{toName}</strong>,</p>
                        <p>Cảm ơn bạn đã thanh toán hóa đơn <strong>{maHoaDon}</strong>.</p>
                        <p>Vui lòng xem chi tiết trong file PDF đính kèm.</p>
                        <br/>
                        <p>Trân trọng,<br/>Phòng Trọ Xinh</p>
                    </div>
                ";
                bodyBuilder.Attachments.Add($"HoaDon_{maHoaDon}.pdf", pdfBytes);
                email.Body = bodyBuilder.ToMessageBody();

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(SMTP_SERVER, SMTP_PORT, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(SENDER_EMAIL, SENDER_PASSWORD);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi gửi email hóa đơn: {ex.Message}");
                return false;
            }
        }

        // Hàm gửi mã OTP (Quên mật khẩu)
        public async Task<bool> GuiEmailOTP(string emailNhan, string otp)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(SENDER_EMAIL));
                email.To.Add(MailboxAddress.Parse(emailNhan));
                email.Subject = "Mã xác thực Quên Mật Khẩu - Phòng Trọ Xinh";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = $@"
                    <div style='font-family: Arial, sans-serif; padding: 30px; max-width: 500px; margin: auto; border: 1px solid #e5e7eb; border-radius: 12px; background-color: #ffffff;'>
                        <div style='text-align: center; margin-bottom: 20px;'>
                            <h2 style='color: #2563eb; margin: 0;'>🏠 PHÒNG TRỌ XINH</h2>
                            <p style='color: #6b7280;'>YÊU CẦU ĐẶT LẠI MẬT KHẨU</p>
                        </div>
                        <p style='color: #374151; font-size: 16px;'>Chào bạn,</p>
                        <p style='color: #374151; font-size: 16px;'>Hệ thống nhận được yêu cầu đặt lại mật khẩu. Mã OTP của bạn là:</p>
                        
                        <div style='text-align: center; margin: 30px 0;'>
                            <span style='font-size: 36px; font-weight: bold; color: #dc2626; letter-spacing: 5px; background-color: #fef2f2; padding: 12px 25px; border-radius: 8px; display: inline-block;'>{otp}</span>
                        </div>
                        
                        <p style='color: #6b7280; font-size: 14px; text-align: center;'>Mã có hiệu lực trong 5 phút. Không chia sẻ với bất kỳ ai.</p>
                        <hr style='border: 0; border-top: 1px solid #f3f4f6; margin: 20px 0;' />
                        <p style='font-size: 12px; color: #9ca3af; text-align: center;'>Email tự động từ Phòng Trọ Xinh, vui lòng không trả lời.</p>
                    </div>";
                email.Body = bodyBuilder.ToMessageBody();

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(SMTP_SERVER, SMTP_PORT, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(SENDER_EMAIL, SENDER_PASSWORD);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi gửi OTP: {ex.Message}");
                return false;
            }
        }

        // Hàm gửi thông báo xác nhận thanh toán
        public async Task<bool> GuiEmailXacNhanThanhToan(string toEmail, string toName, string maHoaDon, decimal soTien)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(SENDER_EMAIL));
                email.To.Add(MailboxAddress.Parse(toEmail));
                email.Subject = $"XÁC NHẬN THANH TOÁN - {maHoaDon}";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px;'>
                        <h2 style='color: #2563eb;'>✅ XÁC NHẬN THANH TOÁN</h2>
                        <p>Kính gửi <strong>{toName}</strong>,</p>
                        <p>Hệ thống đã ghi nhận thanh toán của bạn:</p>
                        <ul>
                            <li><strong>Mã hóa đơn:</strong> {maHoaDon}</li>
                            <li><strong>Số tiền:</strong> <span style='color: red; font-size: 18px;'>{soTien:N0} đ</span></li>
                            <li><strong>Trạng thái:</strong> <span style='color: green;'>Đã thanh toán</span></li>
                        </ul>
                        <p>Cảm ơn bạn đã thanh toán đúng hạn!</p>
                        <br/>
                        <p>Trân trọng,<br/>Phòng Trọ Xinh</p>
                    </div>";
                email.Body = bodyBuilder.ToMessageBody();

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(SMTP_SERVER, SMTP_PORT, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(SENDER_EMAIL, SENDER_PASSWORD);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi gửi email xác nhận: {ex.Message}");
                return false;
            }
        }
    }
}