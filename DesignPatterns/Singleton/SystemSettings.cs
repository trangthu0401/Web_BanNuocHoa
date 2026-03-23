using Microsoft.Extensions.Configuration;

namespace PerfumeStore.DesignPatterns.Singleton
{
    public sealed class SystemSettings
    {
        private static SystemSettings _instance = null;
        private static readonly object _lock = new object();

        // Các thuộc tính lấy từ cấu hình appsettings.json của nhóm
        public string ConnectionString { get; private set; }
        public string SenderEmail { get; private set; }
        public string ChatBotApiKey { get; private set; }
        public string OpenRouterKey { get; private set; }

        private SystemSettings()
        {
            // Nạp các giá trị thực tế từ file cấu hình của bạn
            this.ConnectionString = "Server=LAPTOP-GSG2T2VH;Database=PerfumeStore;Trusted_Connection=True;TrustServerCertificate=True;";
            this.SenderEmail = "ngtttrang040105@gmail.com";
            this.ChatBotApiKey = "perfume-bot-2024";
            this.OpenRouterKey = "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";
        }

        public static SystemSettings Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new SystemSettings();
                    }
                    return _instance;
                }
            }
        }
    }
}