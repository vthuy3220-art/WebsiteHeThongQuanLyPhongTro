using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace HeThongQuanLyPhongTro.Helpers
{
    public class ThongBaoItem
    {
        public string TieuDe { get; set; }
        public string NoiDung { get; set; }
        public string NgayTao { get; set; }
        public string Loai { get; set; } // success, error, warning, info
    }

    public static class ThongBaoHelper
    {
        private const string SessionKey = "ThongBaoList";

        // Thêm thông báo vào Session
        public static void ThemThongBao(this ISession session, string tieuDe, string noiDung, string loai = "info")
        {
            var list = session.GetObject<List<ThongBaoItem>>(SessionKey) ?? new List<ThongBaoItem>();
            list.Add(new ThongBaoItem
            {
                TieuDe = tieuDe,
                NoiDung = noiDung,
                NgayTao = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                Loai = loai
            });
            session.SetObject(SessionKey, list);
        }

        // Lấy danh sách thông báo từ Session
        public static List<ThongBaoItem> LayThongBao(this ISession session)
        {
            return session.GetObject<List<ThongBaoItem>>(SessionKey) ?? new List<ThongBaoItem>();
        }

        // Xóa toàn bộ thông báo trong Session
        public static void XoaThongBao(this ISession session)
        {
            session.Remove(SessionKey);
        }

        // Helper lưu object vào Session
        public static void SetObject(this ISession session, string key, object value)
        {
            session.SetString(key, JsonConvert.SerializeObject(value));
        }

        // Helper lấy object từ Session
        public static T GetObject<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default(T) : JsonConvert.DeserializeObject<T>(value);
        }
    }
}