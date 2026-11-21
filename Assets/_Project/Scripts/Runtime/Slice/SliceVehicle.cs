using System;
using System.Collections;
using System.Collections.Generic;
using RoadwaysGame.Core;
using UnityEngine;

namespace RoadwaysGame.Slice
{
    /// <summary>One depot vehicle: visuals built with the shared HD mesh generators, plus its motions.</summary>
    public sealed class SliceVehicle : MonoBehaviour
    {
        private const float GroundY = 0.04f; // sits on the depot floor / apron surface

        public VehicleDef Def { get; private set; }
        public bool Busy { get; private set; }

        private Transform body;
        private GameObject arrow;
        private TextMesh counter;
        private int capacity, aboard;

        public static SliceVehicle Create(VehicleDef def, int index, Transform parent)
        {
            var go = new GameObject("Vehicle" + index);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(def.Position.X, GroundY, def.Position.Y);
            go.transform.localRotation = Quaternion.Euler(0f, def.HeadingDeg, 0f);
            var v = go.AddComponent<SliceVehicle>();
            v.Def = def;
            v.Build();
            return v;
        }

        private void Build()
        {
            float height = Def.Kind == VehicleKind.Coupe ? 0.5f : Def.Kind == VehicleKind.Bus ? 0.64f : 0.58f;
            Color colour = ColorPaletteDatabase.GetPrimaryColor((ChilbiColorType)Def.Colour);

            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            Part("Wheels", SliceMeshes.Wheels(Def.Width, Def.Length), SliceWorld.Lit(new Color32(0x26, 0x2B, 0x33, 0xFF), 0.3f));
            Part("Hull", SliceMeshes.Body(Def.Width, Def.Length, height), SliceWorld.Lit(colour, 0.68f), SliceWorld.Lit(new Color32(0x1E, 0x2A, 0x3C, 0xFF), 0.92f));
            arrow = Part("Arrow", SliceMeshes.Arrow(Def.Width, Def.Length), SliceWorld.Lit(Color.white, 0.5f));
            arrow.transform.localPosition = new Vector3(0f, height + 0.004f, 0f);
        }

        private GameObject Part(string name, Mesh mesh, params Material[] materials)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(body, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        /// <summary>Where boarding passengers aim for: the front of the vehicle.</summary>
        public Vector3 DoorPosition => transform.position + transform.forward * (Def.Length * 0.45f) + Vector3.up * 0.25f;

        /// <summary>Follows a smoothed route with an eased start and a braking finish; heading tracks the path tangent.</summary>
        public IEnumerator Drive(List<Vector3> route, float speed, Quaternion? finalRotation, Action onArrive)
        {
            Busy = true;
            for (int i = 0; i < route.Count; i++) route[i] = new Vector3(route[i].x, GroundY, route[i].z);
            var path = new SlicePath(route, 10);
            const float accel = 22f;
            float s = 0f, v = 0f;
            while (s < path.Length)
            {
                float brakeLimit = Mathf.Sqrt(2f * accel * (path.Length - s));
                v = Mathf.Min(Mathf.MoveTowards(v, speed, accel * Time.deltaTime), Mathf.Max(brakeLimit, 0.8f));
                s = Mathf.Min(s + v * Time.deltaTime, path.Length);
                transform.position = path.PointAt(s);
                Vector3 t = path.TangentAt(s);
                if (t.sqrMagnitude > 1e-6f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(t), 1f - Mathf.Exp(-16f * Time.deltaTime));
                yield return null;
            }
            if (finalRotation.HasValue) transform.rotation = finalRotation.Value;
            Busy = false;
            onArrive?.Invoke();
        }

        /// <summary>Blocked tap: lurch forward to the blocker and bounce back, so the player sees why.</summary>
        public IEnumerator Nudge(float distance)
        {
            Busy = true;
            Vector3 start = transform.position, fwd = transform.forward;
            float d = Mathf.Clamp(distance, 0.06f, 0.5f);
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.3f)
            {
                transform.position = start + fwd * (d * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            transform.position = start;
            Busy = false;
        }

        /// <summary>The vehicle that was bumped into gives a short wobble.</summary>
        public IEnumerator Flinch()
        {
            yield return new WaitForSeconds(0.12f);
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.3f)
            {
                body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 4f) * 6f * (1f - k));
                yield return null;
            }
            body.localRotation = Quaternion.identity;
        }

        public void Dock(int seats)
        {
            capacity = seats;
            aboard = 0;
            arrow.SetActive(false);
            var go = new GameObject("Counter", typeof(TextMesh));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            counter = go.GetComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            counter.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            counter.fontSize = 96;
            counter.characterSize = 0.38f * 10f / counter.fontSize;
            counter.fontStyle = FontStyle.Bold;
            counter.anchor = TextAnchor.MiddleCenter;
            counter.color = Color.white;
            Refresh();
        }

        public void AddPassenger()
        {
            aboard++;
            Refresh();
            StartCoroutine(Punch());
        }

        private void Refresh()
        {
            if (counter != null) counter.text = aboard + "/" + capacity;
        }

        private IEnumerator Punch()
        {
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.15f)
            {
                body.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            body.localScale = Vector3.one;
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (counter != null && cam != null) counter.transform.rotation = cam.transform.rotation; // billboard
        }
    }
}
