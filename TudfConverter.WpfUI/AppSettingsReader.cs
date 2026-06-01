using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

namespace TudfConverter.WpfUI
{
    public class AppSettingsReader
    {
        public static HeaderSegmentModel LoadHeaderFromSettings()
        {
            try
            {
                var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (!File.Exists(settingsPath))
                    return new HeaderSegmentModel();

                var json = File.ReadAllText(settingsPath);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("TudfHeader", out var section))
                    return new HeaderSegmentModel();

                var model = new HeaderSegmentModel();

                if (section.TryGetProperty("MemberUserId", out var v1))
                    model.MemberUserId = v1.GetString() ?? "";

                if (section.TryGetProperty("MemberShortName", out var v2))
                    model.ShortName = v2.GetString() ?? "";

                if (section.TryGetProperty("ReportingCycle", out var v3))
                    model.ReportingCycle = v3.GetString() ?? "";

                if (section.TryGetProperty("DateReported", out var v4))
                {
                    var raw = v4.GetString() ?? "";
                    if (DateTime.TryParseExact(raw, "ddMMyyyy", null,
                        System.Globalization.DateTimeStyles.None, out var parsed))
                        model.DateReportedAndCertified = parsed;
                    else
                        model.DateReportedAndCertified = DateTime.Now;
                }

                if (section.TryGetProperty("FutureUse1", out var v5))
                    model.FutureUse1 = v5.GetString() ?? "";

                if (section.TryGetProperty("FutureUse2", out var v6))
                    model.FutureUse2 = v6.GetString() ?? "A";

                if (section.TryGetProperty("FutureUse3", out var v7))
                    model.FutureUse3 = v7.GetString() ?? "00000";

                if (section.TryGetProperty("MemberData", out var v8))
                    model.MemberData = v8.GetString() ?? "";

                return model;
            }
            catch
            {
                return new HeaderSegmentModel();
            }
        }
    }

}
