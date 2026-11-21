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

        // Environment Colors (Tropical Beach Resort Theme)
        public static readonly Color LawnColor = new Color32(0xF5, 0xCD, 0x79, 0xFF);          // #f5cd79 Warm sun-baked sand
        public static readonly Color LawnPathwayColor = new Color32(0xED, 0xD3, 0x96, 0x4D);   // #edd396 Sand ripple accent
        public static readonly Color AsphaltColor = new Color32(0x47, 0x52, 0x5E, 0xFF);       // #47525e Dark slate asphalt
        public static readonly Color WhiteCurbColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);     // #ffffff Crisp white curb
        public static readonly Color DirtShoulderColor = new Color32(0xEC, 0xCC, 0x68, 0xFF);  // #eccc68 Golden sand shoulder
        public static readonly Color DepotFloorColor = new Color32(0xDC, 0xDD, 0xE1, 0xFF);    // #dcdde1 Cool studio depot floor
        public static readonly Color ApexKerbRed = new Color32(0xE7, 0x4C, 0x3C, 0xFF);        // #e74c3c
        public static readonly Color ApexKerbWhite = new Color32(0xFF, 0xFF, 0xFF, 0xFF);      // #ffffff
        public static readonly Color AmbientLightColor = new Color32(0xFF, 0xF5, 0xEB, 0xFF);  // #fff5eb Warm sunlit ambient
        public static readonly Color DirectionalLightColor = new Color32(0xFF, 0xFA, 0xF0, 0xFF); // #fffaf0 Bright sunlight

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
