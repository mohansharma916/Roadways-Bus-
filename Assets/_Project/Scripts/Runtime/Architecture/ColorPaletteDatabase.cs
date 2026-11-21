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
    /// Candy values per FIX_ROADMAP 14.7/18.5: red/yellow/blue brightened to match the Chilbi bodies;
    /// green moved from teal toward pure green so it no longer reads as a near-miss for cyan.
    /// </summary>
    public static class ColorPaletteDatabase
    {
        public static readonly ColorTheme[] Themes = new ColorTheme[]
        {
            new ColorTheme(ChilbiColorType.Red,    "Crimson Express",    "#ee5253", "#c93d3e", 440f),
            new ColorTheme(ChilbiColorType.Yellow, "Amber School Buggy", "#fed330", "#d9ae14", 392f),
            new ColorTheme(ChilbiColorType.Blue,   "Cobalt Commuter",    "#1e90ff", "#1670c9", 494f),
            new ColorTheme(ChilbiColorType.Green,  "Emerald Eco",        "#2bd97c", "#1fae60", 523f),
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
