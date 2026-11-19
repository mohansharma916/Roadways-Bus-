using UnityEngine;

namespace RoadwaysGame
{
    public class ShowcaseManager : MonoBehaviour
    {
        [Header("Showcase Configuration")]
        public float busSpacing = 5.0f;
        public float humanSpacing = 5.0f;
        public float laneOffset = 8.5f;

        private void Start()
        {
            BuildShowcaseScene();
        }

        [ContextMenu("Build Showcase Scene")]
        public void BuildShowcaseScene()
        {
            // Clear existing spawned items
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            BuildGroundPlazas();
            SpawnBuses();
            SpawnCharacters();
        }

        private void BuildGroundPlazas()
        {
            // Main Stage Floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "ShowcaseFloor";
            floor.transform.SetParent(transform, false);
            floor.transform.localPosition = new Vector3(0, -0.25f, 0);
            floor.transform.localScale = new Vector3(56f, 0.5f, 56f);

            // Left Runway (Chibi Candy Car Road)
            GameObject busRoad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            busRoad.name = "BusRunway";
            busRoad.transform.SetParent(transform, false);
            busRoad.transform.localPosition = new Vector3(-laneOffset, 0.04f, 0);
            busRoad.transform.localScale = new Vector3(11f, 0.08f, 32f);

            // Right Runway (Jelly Bean Crowd Plaza)
            GameObject humanPlaza = GameObject.CreatePrimitive(PrimitiveType.Cube);
            humanPlaza.name = "HumanPlaza";
            humanPlaza.transform.SetParent(transform, false);
            humanPlaza.transform.localPosition = new Vector3(laneOffset, 0.04f, 0);
            humanPlaza.transform.localScale = new Vector3(11f, 0.08f, 32f);

            // Center Median
            GameObject median = GameObject.CreatePrimitive(PrimitiveType.Cube);
            median.name = "CenterDivider";
            median.transform.SetParent(transform, false);
            median.transform.localPosition = new Vector3(0, 0.1f, 0);
            median.transform.localScale = new Vector3(2.5f, 0.2f, 32f);
        }

        private void SpawnBuses()
        {
            var busConfigs = new (string name, ChibiVehicleType type, Color primary, Color roof, Color accent, float length, float height)[]
            {
                ("Actual Red Cruiser", ChibiVehicleType.CandyCruiser, new Color(0.93f, 0.07f, 0.07f), Color.white, new Color(0.7f, 0f, 0f), 3.8f, 1.85f),
                ("Light Green Eco Shuttle", ChibiVehicleType.BubbleVan, new Color(0.32f, 0.89f, 0.45f), new Color(0.64f, 0.95f, 0.72f), Color.white, 3.4f, 1.8f),
                ("Pure Cyan Bubble Van", ChibiVehicleType.EcoPod, new Color(0f, 0.9f, 1f), new Color(0.88f, 0.98f, 1f), new Color(0f, 0.7f, 0.85f), 3.5f, 1.85f),
                ("Royal Violet Micro-Bus", ChibiVehicleType.MicroBus, new Color(0.5f, 0.08f, 0.9f), new Color(0.72f, 0.43f, 1f), Color.white, 3.5f, 1.9f),
                ("Rich Brown Chibi Buggy", ChibiVehicleType.SchoolBuggy, new Color(0.51f, 0.27f, 0.14f), new Color(0.91f, 0.79f, 0.68f), new Color(0.29f, 0.14f, 0.06f), 3.9f, 1.9f),
                ("Caramel Brown Deck-Master", ChibiVehicleType.DoubleDeckMaster, new Color(0.72f, 0.42f, 0.2f), new Color(0.99f, 0.91f, 0.81f), new Color(0.51f, 0.27f, 0.14f), 4.0f, 2.6f)
            };

            float zStart = -(busConfigs.Length - 1) * busSpacing / 2f;

            for (int i = 0; i < busConfigs.Length; i++)
            {
                var cfg = busConfigs[i];
                GameObject busObj = new GameObject($"Bus_{i + 1}_{cfg.name}");
                busObj.transform.SetParent(transform, false);
                busObj.transform.localPosition = new Vector3(-laneOffset, 0, zStart + i * busSpacing);
                busObj.transform.localRotation = Quaternion.Euler(0, 30f, 0);

                BusModelGenerator gen = busObj.AddComponent<BusModelGenerator>();
                gen.selectedType = cfg.type;
                gen.primaryColor = cfg.primary;
                gen.roofColor = cfg.roof;
                gen.accentColor = cfg.accent;
                gen.length = cfg.length;
                gen.height = cfg.height;
                gen.GenerateBus();
            }
        }

        private void SpawnCharacters()
        {
            var charConfigs = new (string name, Color candyColor)[]
            {
                ("Actual Red Bean", new Color(0.93f, 0.07f, 0.07f)),
                ("Light Green Bean", new Color(0.32f, 0.89f, 0.45f)),
                ("Cyan Bean", new Color(0f, 0.9f, 1f)),
                ("Violet Bean", new Color(0.5f, 0.08f, 0.9f)),
                ("Rich Brown Bean", new Color(0.51f, 0.27f, 0.14f)),
                ("Caramel Brown Bean", new Color(0.72f, 0.42f, 0.2f))
            };

            float zStart = -(charConfigs.Length - 1) * humanSpacing / 2f;

            for (int i = 0; i < charConfigs.Length; i++)
            {
                var cfg = charConfigs[i];
                GameObject charObj = new GameObject($"JellyBean_{i + 1}_{cfg.name}");
                charObj.transform.SetParent(transform, false);
                charObj.transform.localPosition = new Vector3(laneOffset, 0, zStart + i * humanSpacing);
                charObj.transform.localRotation = Quaternion.Euler(0, -25f, 0);

                RoundCharacterGenerator gen = charObj.AddComponent<RoundCharacterGenerator>();
                gen.characterName = cfg.name;
                gen.candyColor = cfg.candyColor;
                gen.GenerateCharacter();
            }
        }
    }
}
