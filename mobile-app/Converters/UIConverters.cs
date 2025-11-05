using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace VisualVoicemailPro.Converters
{
    /// <summary>
    /// Converts language codes to display-friendly names
    /// </summary>
    public class LanguageDisplayConverter : IValueConverter
    {
        private static readonly Dictionary<string, string> LanguageNames = new()
        {
            // English variants
            ["en-US"] = "🇺🇸 English (United States)",
            ["en-GB"] = "🇬🇧 English (United Kingdom)",
            ["en-AU"] = "🇦🇺 English (Australia)",
            ["en-CA"] = "🇨🇦 English (Canada)",
            ["en"] = "🇺🇸 English",

            // Spanish variants
            ["es-ES"] = "🇪🇸 Spanish (Spain)",
            ["es-MX"] = "🇲🇽 Spanish (Mexico)",
            ["es-AR"] = "🇦🇷 Spanish (Argentina)",
            ["es-US"] = "🇺🇸 Spanish (United States)",
            ["es"] = "🇪🇸 Spanish",

            // French variants
            ["fr-FR"] = "🇫🇷 French (France)",
            ["fr-CA"] = "🇨🇦 French (Canada)",
            ["fr"] = "🇫🇷 French",

            // German variants
            ["de-DE"] = "🇩🇪 German (Germany)",
            ["de-AT"] = "🇦🇹 German (Austria)",
            ["de-CH"] = "🇨🇭 German (Switzerland)",
            ["de"] = "🇩🇪 German",

            // Other major languages
            ["it-IT"] = "🇮🇹 Italian (Italy)",
            ["it"] = "🇮🇹 Italian",
            ["pt-BR"] = "🇧🇷 Portuguese (Brazil)",
            ["pt-PT"] = "🇵🇹 Portuguese (Portugal)",
            ["pt"] = "🇧🇷 Portuguese",
            ["zh-CN"] = "🇨🇳 Chinese (Simplified)",
            ["zh-TW"] = "🇹🇼 Chinese (Traditional)",
            ["zh"] = "🇨🇳 Chinese",
            ["ja-JP"] = "🇯🇵 Japanese",
            ["ja"] = "🇯🇵 Japanese",
            ["ko-KR"] = "🇰🇷 Korean",
            ["ko"] = "🇰🇷 Korean",
            ["ar-SA"] = "🇸🇦 Arabic (Saudi Arabia)",
            ["ar-EG"] = "🇪🇬 Arabic (Egypt)",
            ["ar"] = "🇸🇦 Arabic",
            ["ru-RU"] = "🇷🇺 Russian",
            ["ru"] = "🇷🇺 Russian",
            ["hi-IN"] = "🇮🇳 Hindi",
            ["hi"] = "🇮🇳 Hindi",
            ["nl-NL"] = "🇳🇱 Dutch",
            ["nl"] = "🇳🇱 Dutch",
            ["sv-SE"] = "🇸🇪 Swedish",
            ["sv"] = "🇸🇪 Swedish",
            ["no-NO"] = "🇳🇴 Norwegian",
            ["no"] = "🇳🇴 Norwegian",
            ["da-DK"] = "🇩🇰 Danish",
            ["da"] = "🇩🇰 Danish",
            ["fi-FI"] = "🇫🇮 Finnish",
            ["fi"] = "🇫🇮 Finnish",
            ["pl-PL"] = "🇵🇱 Polish",
            ["pl"] = "🇵🇱 Polish",
            ["tr-TR"] = "🇹🇷 Turkish",
            ["tr"] = "🇹🇷 Turkish",
            ["th"] = "🇹🇭 Thai",
            ["vi"] = "🇻🇳 Vietnamese",
            ["id"] = "🇮🇩 Indonesian",
            ["ms"] = "🇲🇾 Malay",
            ["tl"] = "🇵🇭 Filipino",
            ["sw"] = "🇰🇪 Swahili",
            ["he"] = "🇮🇱 Hebrew",
            ["fa"] = "🇮🇷 Persian",
            ["ur"] = "🇵🇰 Urdu",
            ["bn"] = "🇧🇩 Bengali"
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string languageCode)
            {
                return LanguageNames.TryGetValue(languageCode, out var displayName) 
                    ? displayName 
                    : languageCode;
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Inverts boolean values for UI binding
    /// </summary>
    public class InvertedBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool boolValue ? !boolValue : false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool boolValue ? !boolValue : false;
        }
    }

    /// <summary>
    /// Converts string to boolean (true if not null/empty)
    /// </summary>
    public class StringToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrEmpty(value?.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts spam status to border color
    /// </summary>
    public class SpamBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool isSpam && isSpam 
                ? Colors.Red 
                : Colors.LightGray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Shows high priority indicator
    /// </summary>
    public class HighPriorityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString()?.ToLower() == "high";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}