using UnityEngine;

namespace QuixoUnity.Gameplay
{
    /// <summary>
    /// Choix de cadence persistant entre les sessions Unity via PlayerPrefs.
    /// Format type echecs : initial + increment. 0+0 = sans limite.
    /// </summary>
    public static class TurnTimerSettings
    {
        private const string PlayerPrefsKey = "Quixo.TimeControlKey";
        private const string LegacyPlayerPrefsKey = "Quixo.TurnTimerSeconds";

        public sealed class TimeControlOption
        {
            public readonly string Key;
            public readonly int InitialSeconds;
            public readonly int IncrementSeconds;
            public readonly string Label;

            public TimeControlOption(string key, int initialSeconds, int incrementSeconds, string label)
            {
                Key = key;
                InitialSeconds = initialSeconds;
                IncrementSeconds = incrementSeconds;
                Label = label;
            }
        }

        // Compatibilite avec l'ancien code : 0 represente "sans limite".
        public const int Unlimited = 0;
        public const int DefaultSeconds = 300;
        public const string DefaultKey = "5+3";

        public static readonly TimeControlOption[] AvailableOptions =
        {
            new("none", 0, 0, "Sans limite"),
            new("1+0", 60, 0, "1+0"),
            new("3+0", 180, 0, "3+0"),
            new("5+0", 300, 0, "5+0"),
            new("10+0", 600, 0, "10+0"),
            new("15+0", 900, 0, "15+0"),
            new("30+0", 1800, 0, "30+0"),
            new("1+1", 60, 1, "1+1"),
            new("3+2", 180, 2, "3+2"),
            new("5+3", 300, 3, "5+3"),
            new("10+5", 600, 5, "10+5")
        };

        private static string _cachedKey;

        public static string SelectedKey
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_cachedKey))
                {
                    return _cachedKey;
                }

                string stored = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
                if (string.IsNullOrWhiteSpace(stored) && PlayerPrefs.HasKey(LegacyPlayerPrefsKey))
                {
                    stored = KeyFromLegacySeconds(PlayerPrefs.GetInt(LegacyPlayerPrefsKey, DefaultSeconds));
                }

                _cachedKey = NormalizeKey(stored);
                return _cachedKey;
            }
            set
            {
                string safe = NormalizeKey(value);
                _cachedKey = safe;
                PlayerPrefs.SetString(PlayerPrefsKey, safe);
                PlayerPrefs.Save();
            }
        }

        public static TimeControlOption SelectedOption => OptionForKey(SelectedKey);

        public static int SelectedInitialSeconds => SelectedOption.InitialSeconds;

        public static int SelectedIncrementSeconds => SelectedOption.IncrementSeconds;

        // Ancienne API conservee pour les scripts/scenes existants.
        public static int SelectedSeconds
        {
            get => SelectedInitialSeconds;
            set => SelectedKey = KeyFromLegacySeconds(value);
        }

        public static bool IsUnlimited => SelectedSeconds <= 0;

        public static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DefaultKey;
            }

            string trimmed = value.Trim();
            foreach (var option in AvailableOptions)
            {
                if (string.Equals(option.Key, trimmed, System.StringComparison.OrdinalIgnoreCase))
                {
                    return option.Key;
                }
            }

            return DefaultKey;
        }

        public static TimeControlOption OptionForKey(string key)
        {
            string safeKey = NormalizeKey(key);
            foreach (var option in AvailableOptions)
            {
                if (option.Key == safeKey)
                {
                    return option;
                }
            }

            return AvailableOptions[9];
        }

        public static TimeControlOption OptionForNetwork(string key, int initialSeconds, int incrementSeconds)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                var option = OptionForKey(key);
                if (option.Key == NormalizeKey(key))
                {
                    return option;
                }
            }

            if (initialSeconds <= 0 && incrementSeconds <= 0)
            {
                return OptionForKey("none");
            }

            return new TimeControlOption(
                $"{Mathf.Max(0, initialSeconds / 60)}+{Mathf.Max(0, incrementSeconds)}",
                Mathf.Max(0, initialSeconds),
                Mathf.Max(0, incrementSeconds),
                DisplayName(initialSeconds, incrementSeconds));
        }

        public static string DisplayName(int seconds)
        {
            return DisplayName(seconds, 0);
        }

        public static string DisplayName(string key, int initialSeconds, int incrementSeconds)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                return OptionForKey(key).Label;
            }

            return DisplayName(initialSeconds, incrementSeconds);
        }

        public static string DisplayName(int initialSeconds, int incrementSeconds)
        {
            if (initialSeconds <= 0 && incrementSeconds <= 0)
            {
                return "Sans limite";
            }

            int minutes = Mathf.Max(0, initialSeconds) / 60;
            return $"{minutes}+{Mathf.Max(0, incrementSeconds)}";
        }

        public static string DisplayCurrent()
        {
            return SelectedOption.Label;
        }

        private static string KeyFromLegacySeconds(int value)
        {
            if (value <= 0)
            {
                return "none";
            }

            return value switch
            {
                15 => "1+0",
                30 => "3+0",
                60 => "5+0",
                _ => DefaultKey
            };
        }
    }
}
