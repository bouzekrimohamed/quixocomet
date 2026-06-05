using UnityEngine;

namespace QuixoUnity.Gameplay
{
    /// <summary>
    /// Choix du temps par tour, persistant entre les sessions Unity via PlayerPrefs.
    /// 0 = sans limite. Les autres valeurs autorisees sont 15, 30 et 60 secondes.
    /// </summary>
    public static class TurnTimerSettings
    {
        private const string PlayerPrefsKey = "Quixo.TurnTimerSeconds";

        // Valeur par defaut : 30 secondes. Permet une partie nerveuse sans etre punitive.
        public const int DefaultSeconds = 30;

        // 0 represente "sans limite" en interne. Ne pas changer cette convention.
        public const int Unlimited = 0;

        public static readonly int[] AvailableOptions = { Unlimited, 15, 30, 60 };

        private static int _cached = -1;

        public static int SelectedSeconds
        {
            get
            {
                if (_cached >= 0)
                {
                    return _cached;
                }

                int stored = PlayerPrefs.GetInt(PlayerPrefsKey, DefaultSeconds);
                _cached = Normalize(stored);
                return _cached;
            }
            set
            {
                int safe = Normalize(value);
                _cached = safe;
                PlayerPrefs.SetInt(PlayerPrefsKey, safe);
                PlayerPrefs.Save();
            }
        }

        public static bool IsUnlimited => SelectedSeconds <= 0;

        public static string DisplayName(int seconds)
        {
            return seconds <= 0 ? "Sans limite" : $"{seconds}s";
        }

        public static string DisplayCurrent()
        {
            return DisplayName(SelectedSeconds);
        }

        private static int Normalize(int value)
        {
            if (value <= 0)
            {
                return Unlimited;
            }

            // On clampe sur les valeurs autorisees pour eviter les valeurs corrompues.
            foreach (var allowed in AvailableOptions)
            {
                if (allowed == value)
                {
                    return value;
                }
            }

            return DefaultSeconds;
        }
    }
}
