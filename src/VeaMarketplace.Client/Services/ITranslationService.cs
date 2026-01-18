using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VeaMarketplace.Client.Services;

/// <summary>
/// Service for translating text between languages.
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Translates text to the specified target language.
    /// </summary>
    /// <param name="text">The text to translate</param>
    /// <param name="targetLanguage">Target language code (e.g., "es", "fr", "de")</param>
    /// <param name="sourceLanguage">Source language code (optional, auto-detect if not specified)</param>
    /// <returns>Translated text</returns>
    Task<TranslationResult> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = null);

    /// <summary>
    /// Gets the list of supported languages.
    /// </summary>
    List<LanguageInfo> GetSupportedLanguages();

    /// <summary>
    /// Detects the language of the given text.
    /// </summary>
    Task<string?> DetectLanguageAsync(string text);
}

public class TranslationResult
{
    public bool Success { get; set; }
    public string OriginalText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public string? DetectedLanguage { get; set; }
    public string TargetLanguage { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class LanguageInfo
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
}

/// <summary>
/// Translation service using MyMemory API (free, no API key required).
/// Falls back to LibreTranslate if MyMemory fails.
/// </summary>
public class TranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly List<LanguageInfo> _supportedLanguages;

    // MyMemory API endpoint (free, no API key needed for basic usage)
    private const string MyMemoryApiUrl = "https://api.mymemory.translated.net/get";

    // LibreTranslate public instances (fallback)
    private static readonly string[] LibreTranslateUrls = new[]
    {
        "https://libretranslate.com/translate",
        "https://translate.argosopentech.com/translate",
        "https://translate.terraprint.co/translate"
    };

    public TranslationService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);

        _supportedLanguages = InitializeSupportedLanguages();
    }

    public async Task<TranslationResult> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationResult
            {
                Success = false,
                ErrorMessage = "Text cannot be empty"
            };
        }

        // Try MyMemory first
        var result = await TryMyMemoryTranslateAsync(text, targetLanguage, sourceLanguage);
        if (result.Success)
            return result;

        // Fallback to LibreTranslate
        result = await TryLibreTranslateAsync(text, targetLanguage, sourceLanguage);
        return result;
    }

    private async Task<TranslationResult> TryMyMemoryTranslateAsync(string text, string targetLanguage, string? sourceLanguage)
    {
        try
        {
            var langPair = $"{sourceLanguage ?? "auto"}|{targetLanguage}";
            var url = $"{MyMemoryApiUrl}?q={Uri.EscapeDataString(text)}&langpair={langPair}";

            var response = await _httpClient.GetAsync(url).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var myMemoryResponse = JsonSerializer.Deserialize<MyMemoryResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (myMemoryResponse?.ResponseStatus == 200 && myMemoryResponse.ResponseData != null)
                {
                    return new TranslationResult
                    {
                        Success = true,
                        OriginalText = text,
                        TranslatedText = myMemoryResponse.ResponseData.TranslatedText ?? text,
                        DetectedLanguage = myMemoryResponse.ResponseData.DetectedLanguage,
                        TargetLanguage = targetLanguage
                    };
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MyMemory translation failed: {ex.Message}");
        }

        return new TranslationResult
        {
            Success = false,
            OriginalText = text,
            ErrorMessage = "MyMemory translation failed"
        };
    }

    private async Task<TranslationResult> TryLibreTranslateAsync(string text, string targetLanguage, string? sourceLanguage)
    {
        foreach (var baseUrl in LibreTranslateUrls)
        {
            try
            {
                var request = new
                {
                    q = text,
                    source = sourceLanguage ?? "auto",
                    target = targetLanguage,
                    format = "text"
                };

                var response = await _httpClient.PostAsJsonAsync(baseUrl, request).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var libreResponse = JsonSerializer.Deserialize<LibreTranslateResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (libreResponse != null && !string.IsNullOrEmpty(libreResponse.TranslatedText))
                    {
                        return new TranslationResult
                        {
                            Success = true,
                            OriginalText = text,
                            TranslatedText = libreResponse.TranslatedText,
                            DetectedLanguage = libreResponse.DetectedLanguage?.Language,
                            TargetLanguage = targetLanguage
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LibreTranslate ({baseUrl}) failed: {ex.Message}");
            }
        }

        return new TranslationResult
        {
            Success = false,
            OriginalText = text,
            ErrorMessage = "Translation service unavailable. Please try again later."
        };
    }

    public async Task<string?> DetectLanguageAsync(string text)
    {
        try
        {
            // Use MyMemory's auto-detect by translating to English
            var result = await TryMyMemoryTranslateAsync(text, "en", null);
            return result.DetectedLanguage;
        }
        catch
        {
            return null;
        }
    }

    public List<LanguageInfo> GetSupportedLanguages()
    {
        return _supportedLanguages;
    }

    private static List<LanguageInfo> InitializeSupportedLanguages()
    {
        return new List<LanguageInfo>
        {
            new() { Code = "en", Name = "English", NativeName = "English" },
            new() { Code = "es", Name = "Spanish", NativeName = "Espanol" },
            new() { Code = "fr", Name = "French", NativeName = "Francais" },
            new() { Code = "de", Name = "German", NativeName = "Deutsch" },
            new() { Code = "it", Name = "Italian", NativeName = "Italiano" },
            new() { Code = "pt", Name = "Portuguese", NativeName = "Portugues" },
            new() { Code = "ru", Name = "Russian", NativeName = "Русский" },
            new() { Code = "zh", Name = "Chinese", NativeName = "中文" },
            new() { Code = "ja", Name = "Japanese", NativeName = "日本語" },
            new() { Code = "ko", Name = "Korean", NativeName = "한국어" },
            new() { Code = "ar", Name = "Arabic", NativeName = "العربية" },
            new() { Code = "hi", Name = "Hindi", NativeName = "हिन्दी" },
            new() { Code = "nl", Name = "Dutch", NativeName = "Nederlands" },
            new() { Code = "pl", Name = "Polish", NativeName = "Polski" },
            new() { Code = "tr", Name = "Turkish", NativeName = "Turkce" },
            new() { Code = "vi", Name = "Vietnamese", NativeName = "Tieng Viet" },
            new() { Code = "th", Name = "Thai", NativeName = "ไทย" },
            new() { Code = "sv", Name = "Swedish", NativeName = "Svenska" },
            new() { Code = "da", Name = "Danish", NativeName = "Dansk" },
            new() { Code = "fi", Name = "Finnish", NativeName = "Suomi" },
            new() { Code = "no", Name = "Norwegian", NativeName = "Norsk" },
            new() { Code = "cs", Name = "Czech", NativeName = "Cestina" },
            new() { Code = "el", Name = "Greek", NativeName = "Ελληνικά" },
            new() { Code = "he", Name = "Hebrew", NativeName = "עברית" },
            new() { Code = "hu", Name = "Hungarian", NativeName = "Magyar" },
            new() { Code = "id", Name = "Indonesian", NativeName = "Bahasa Indonesia" },
            new() { Code = "ro", Name = "Romanian", NativeName = "Romana" },
            new() { Code = "uk", Name = "Ukrainian", NativeName = "Українська" },
            new() { Code = "bg", Name = "Bulgarian", NativeName = "Български" },
            new() { Code = "hr", Name = "Croatian", NativeName = "Hrvatski" },
            new() { Code = "sk", Name = "Slovak", NativeName = "Slovencina" },
            new() { Code = "sl", Name = "Slovenian", NativeName = "Slovenscina" },
            new() { Code = "sr", Name = "Serbian", NativeName = "Srpski" },
            new() { Code = "lt", Name = "Lithuanian", NativeName = "Lietuviu" },
            new() { Code = "lv", Name = "Latvian", NativeName = "Latviesu" },
            new() { Code = "et", Name = "Estonian", NativeName = "Eesti" }
        };
    }
}

#region API Response Models

internal class MyMemoryResponse
{
    public int ResponseStatus { get; set; }
    public MyMemoryResponseData? ResponseData { get; set; }
}

internal class MyMemoryResponseData
{
    public string? TranslatedText { get; set; }
    public string? DetectedLanguage { get; set; }
}

internal class LibreTranslateResponse
{
    public string? TranslatedText { get; set; }
    public LibreDetectedLanguage? DetectedLanguage { get; set; }
}

internal class LibreDetectedLanguage
{
    public string? Language { get; set; }
    public double Confidence { get; set; }
}

#endregion
