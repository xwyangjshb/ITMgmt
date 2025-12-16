using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITDeviceManager.API.Utils
{
    /// <summary>
    /// 自定义 DateTime JSON 转换器，将 UTC 时间转换为中国标准时间 (UTC+8)
    /// </summary>
    public class ChinaDateTimeConverter : JsonConverter<DateTime>
    {
        private static readonly TimeZoneInfo? ChinaTimeZone;
        private static readonly TimeSpan ChinaOffset = TimeSpan.FromHours(8);

        static ChinaDateTimeConverter()
        {
            // 尝试获取中国标准时区，如果失败则使用固定偏移量
            try
            {
                ChinaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
            }
            catch
            {
                // 在非 Windows 系统上，可能使用不同的时区 ID
                try
                {
                    ChinaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
                }
                catch
                {
                    // 如果都找不到，将使用固定偏移量
                    ChinaTimeZone = null;
                }
            }
        }

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // 读取时假定输入是 UTC 时间或 ISO 8601 格式
            var dateTimeString = reader.GetString();
            if (DateTime.TryParse(dateTimeString, out var dateTime))
            {
                // 如果是 UTC 时间，转换为中国时间
                if (dateTime.Kind == DateTimeKind.Utc)
                {
                    return ConvertToChineseTime(dateTime);
                }
                return dateTime;
            }
            throw new JsonException($"Unable to parse datetime: {dateTimeString}");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            // 将时间转换为中国标准时间并格式化输出
            DateTime chinaTime;

            if (value.Kind == DateTimeKind.Utc)
            {
                // UTC 时间转换为中国时间
                chinaTime = ConvertToChineseTime(value);
            }
            else if (value.Kind == DateTimeKind.Local)
            {
                // 本地时间转换为中国时间
                var utc = value.ToUniversalTime();
                chinaTime = ConvertToChineseTime(utc);
            }
            else
            {
                // 未指定类型，假定为 UTC
                chinaTime = ConvertToChineseTime(DateTime.SpecifyKind(value, DateTimeKind.Utc));
            }

            // 输出格式：yyyy-MM-dd HH:mm:ss
            writer.WriteStringValue(chinaTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        private static DateTime ConvertToChineseTime(DateTime utcDateTime)
        {
            if (ChinaTimeZone != null)
            {
                // 使用系统时区转换
                return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, ChinaTimeZone);
            }
            else
            {
                // 使用固定偏移量 UTC+8
                return utcDateTime.Add(ChinaOffset);
            }
        }
    }
}
