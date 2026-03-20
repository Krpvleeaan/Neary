using System.Text.Json;

namespace Neary.Mobile.Services;

/// <summary>Базовый URL API из Resources/Raw/server.json (без секретов в репозитории — подставьте свой хост перед сборкой).</summary>
internal static class ApiConfig
{
    private static string? _baseUrl;

    public static string BaseUrl
    {
        get
        {
            if (_baseUrl is not null)
                return _baseUrl;

            try
            {
                using var stream = FileSystem.OpenAppPackageFileAsync("server.json").GetAwaiter().GetResult();
                using var reader = new StreamReader(stream);
                using var doc = JsonDocument.Parse(reader.ReadToEnd());
                if (doc.RootElement.TryGetProperty("baseUrl", out var el))
                {
                    var s = el.GetString()?.Trim().TrimEnd('/');
                    if (!string.IsNullOrEmpty(s))
                        _baseUrl = s;
                }
            }
            catch
            {
                /* fallback */
            }

            _baseUrl ??= "http://localhost:5000";
            return _baseUrl;
        }
    }

    public static string LocationPostUrl => $"{BaseUrl}/api/location";
}
