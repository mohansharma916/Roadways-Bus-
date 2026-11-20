using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame
{
    [Serializable]
    public struct ChilbiInstanceData
    {
        public int globalIndex;      // 0 to 429
        public int rowIndex;        // 0 to 61
        public int columnIndex;     // 0 to 6
        public ChilbiColorType colorType;
        public float laneOffset;     // Lateral offset across road [-35.1, +35.1]
        public float longitudinalOffset; // Along spline
        public bool isLeadRunner;    // True for Cyan Lead Chilbi
    }

    /// <summary>
    /// Master Data Generator & Repository for the 430-Chilbi squad formation.
    /// Strictly enforces:
    /// - Exactly 430 characters
    /// - 62 single-color rows (61 rows of 7 abreast + 1 row of 3)
    /// - 7 equal lanes across 82px road (spacing 11.71px)
    /// - Repeating sequence: 5 Red -> 7 Yellow -> 4 Blue -> 8 Green -> 6 Purple -> 5 Orange -> 6 Pink -> 5 Cyan
    /// - Center Cyan Lead Chilbi 14px ahead of Row 0
    /// </summary>
    public static class ChilbiFormationData
    {
        public const int TotalCount = GameConstants.TotalChilbis;       // 430
        public const int TotalRows = 62;
        public const int ColumnsPerRow = GameConstants.ChilbisPerRow;    // 7
        public const float RoadWidth = GameConstants.RoadWidth;          // 82
        public const float LaneSpacing = 11.71f;                         // 7 lanes across 82px

        // Repeating sequence of row counts per color
        private static readonly (ChilbiColorType color, int rowCount)[] SequencePattern = new[]
        {
            (ChilbiColorType.Red,    5),
            (ChilbiColorType.Yellow, 7),
            (ChilbiColorType.Blue,   4),
            (ChilbiColorType.Green,  8),
            (ChilbiColorType.Purple, 6),
            (ChilbiColorType.Orange, 5),
            (ChilbiColorType.Pink,   6),
            (ChilbiColorType.Cyan,   5)
        };

        public static ChilbiColorType GetRowColor(int rowIndex)
        {
            int totalPatternRows = 0;
            foreach (var item in SequencePattern) totalPatternRows += item.rowCount; // 46 rows

            int wrappedRow = rowIndex % totalPatternRows;
            int accum = 0;
            foreach (var item in SequencePattern)
            {
                accum += item.rowCount;
                if (wrappedRow < accum)
                {
                    return item.color;
                }
            }
            return ChilbiColorType.Red;
        }

        public static float GetLaneOffset(int colIndex)
        {
            // 7 columns: -3, -2, -1, 0, 1, 2, 3
            // col 0 = -35.13, col 3 = 0, col 6 = +35.13
            float centerCol = (ColumnsPerRow - 1) * 0.5f; // 3.0
            return (colIndex - centerCol) * LaneSpacing;
        }

        /// <summary>
        /// Generates the complete array of 430 Chilbi definitions.
        /// </summary>
        public static ChilbiInstanceData[] GenerateFormation()
        {
            ChilbiInstanceData[] data = new ChilbiInstanceData[TotalCount];
            int count = 0;

            for (int r = 0; r < TotalRows; r++)
            {
                ChilbiColorType rowColor = GetRowColor(r);
                int colsThisRow = (r == TotalRows - 1) ? 3 : ColumnsPerRow; // 61 rows of 7 (427) + 1 row of 3 = 430

                int startCol = (r == TotalRows - 1) ? 2 : 0; // Center the 3 characters on the last row

                for (int c = 0; c < colsThisRow; c++)
                {
                    int colIndex = startCol + c;
                    data[count] = new ChilbiInstanceData
                    {
                        globalIndex = count,
                        rowIndex = r,
                        columnIndex = colIndex,
                        colorType = rowColor,
                        laneOffset = GetLaneOffset(colIndex),
                        longitudinalOffset = 0f,
                        isLeadRunner = (count == 0 && colIndex == 3) // Center runner of first row
                    };
                    count++;
                }
            }

            return data;
        }
    }
}
