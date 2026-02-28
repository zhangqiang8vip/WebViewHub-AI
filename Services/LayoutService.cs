using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WebViewHub.Services
{
    public class LayoutData
    {
        public string ProfileID { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string RoleTag { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class LayoutService
    {
        private readonly string _configPath;

        public LayoutService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WebViewHub"
            );
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            _configPath = Path.Combine(appDataPath, "layout.json");
        }

        public void SaveLayout(List<LayoutData> layout)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(layout, options);
                File.WriteAllText(_configPath, json);
            }
            catch
            {
                // 静默失败
            }
        }

        public List<LayoutData>? LoadLayout()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    return null;
                }
                var json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<List<LayoutData>>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
