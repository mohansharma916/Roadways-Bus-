using System;
using UnityEngine;

namespace RoadwaysGame
{
    public enum ChilbiColorType
    {
        Red,
        Yellow,
        Blue,
        Green,
        Purple,
        Orange,
        Pink,
        Cyan
    }

    [Serializable]
    public struct ColorTheme
    {
        public ChilbiColorType colorType;
        public string displayName;
        public Color primaryColor;
        public Color accentColor;
        public float hornFrequencyHz;

        public ColorTheme(ChilbiColorType type, string name, string primaryHex, string accentHex, float freq)
        {
            colorType = type;
            displayName = name;
            ColorUtility.TryParseHtmlString(primaryHex, out primaryColor);
            ColorUtility.TryParseHtmlString(accentHex, out accentColor);
            hornFrequencyHz = freq;
        }
    }

    /// <summary>
    /// Master single-source-of-truth color palette database for Chilbis and Buses.
    /// </summary>
    public static class ColorPaletteDatabase
    {
        public static readonly ColorTheme[] Themes = new ColorTheme[]
        {
            new ColorTheme(ChilbiColorType.Red,    "Crimson Express",    "#c0392b", "#a82c1f", 440f),
            new ColorTheme(ChilbiColorType.Yellow, "Amber School Buggy", "#f39c12", "#d4880f", 392f),
            new ColorTheme(ChilbiColorType.Blue,   "Cobalt Commuter",    "#2548ad", "#1c3987", 494f),
            new ColorTheme(ChilbiColorType.Green,  "Emerald Eco",        "#20bf6b", "#168d4f", 523f),
            new ColorTheme(ChilbiColorType.Purple, "Royal Violet",       "#8854d0", "#6c3db5", 587f),
            new ColorTheme(ChilbiColorType.Orange, "Tangerine Transit",  "#e67e22", "#cf6717", 622f),
            new ColorTheme(ChilbiColorType.Pink,   "Bubblegum Shuttle",  "#e84393", "#cf2d7c", 659f),
            new ColorTheme(ChilbiColorType.Cyan,   "Cyan Deck-Master",   "#0fbcf9", "#0998cc", 740f)
        };

        public static ColorTheme GetTheme(ChilbiColorType type)
        {
            int index = (int)type;
            if (index >= 0 && index < Themes.Length)
            {
                return Themes[index];
            }
            return Themes[0];
        }

        public static Color GetPrimaryColor(ChilbiColorType type)
        {
            return GetTheme(type).primaryColor;
        }

        public static Color GetAccentColor(ChilbiColorType type)
        {
            return GetTheme(type).accentColor;
        }

        public static float GetHornFrequency(ChilbiColorType type)
        {
            return GetTheme(type).hornFrequencyHz;
        }
    }
}
