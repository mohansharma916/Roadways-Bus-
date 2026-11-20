using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// Master constants for the Roadways Bus Game.
    /// Strictly guarantees cross-system parity across agents and systems.
    /// </summary>
    public static class GameConstants
    {
        // Reference Screen Specifications
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;
        public const float ReferenceAspectRatio = 9f / 16f; // 0.5625
        public const float BaseVerticalFOV = 44.0f;

        // Camera Rig Coordinates
        public static readonly Vector3 CameraPosition = new Vector3(0f, 30f, 42f);
        public static readonly Vector3 CameraLookTarget = new Vector3(0f, 0f, 8f);
        public const float CameraPitch = 35.0f;

        // Environment Colors (Cherry Blossom Theme)
        public static readonly Color LawnColor = new Color32(0xFF, 0xF1, 0xF2, 0xFF);          // #fff1f2 Soft warm pastel
        public static readonly Color LawnPathwayColor = new Color32(0xF2, 0xD5, 0xDB, 0x59);   // #f2d5db alpha 0.35
        public static readonly Color AsphaltColor = new Color32(0x52, 0x57, 0x69, 0xFF);       // #525769 Dark slate asphalt
        public static readonly Color WhiteCurbColor = new Color32(0xE6, 0xE9, 0xEE, 0xFF);     // #e6e9ee Crisp white
        public static readonly Color DirtShoulderColor = new Color32(0xCB, 0xB2, 0x7A, 0xFF);  // #cbb27a Tan dirt border
        public static readonly Color DepotFloorColor = new Color32(0xD3, 0xDA, 0xED, 0xFF);    // #d3daed Soft blue-grey
        public static readonly Color ApexKerbRed = new Color32(0xE7, 0x4C, 0x3C, 0xFF);        // #e74c3c
        public static readonly Color ApexKerbWhite = new Color32(0xFF, 0xFF, 0xFF, 0xFF);      // #ffffff
        public static readonly Color AmbientLightColor = new Color32(0xF7, 0xE8, 0xEA, 0xFF);  // #f7e8ea
        public static readonly Color DirectionalLightColor = new Color32(0xFF, 0xF9, 0xF5, 0xFF); // #fff9f5

        // Road Geometry Constants
        public const float RoadWidth = 82f;
        public const float WhiteCurbWidth = 2f;
        public const float DirtShoulderWidth = 11f;
        public const float StopBarPositionX = 30f;

        // Vehicle Dimensions & Kinematics
        public const float BusWidth = 25f;
        public const float BusLength = 50f;
        public const float BusCornerRadius = 5.5f;
        public const float ParkingBayAngleDeg = -30.0f;
        public const int CapacityPerBus = 7;
        public const int TotalParkingBays = 9;

        // Chilbi Formation & Kinematics
        public const int ChilbisPerRow = 7;
        public const int CircuitMaxRows = 60;
        public const int CircuitMaxChilbis = 420;
        public const int TotalChilbis = 430;
        public const float CircuitCruiseSpeed = 46.0f;
        public const float FeederEntranceSpeed = 150.0f;
    }
}
