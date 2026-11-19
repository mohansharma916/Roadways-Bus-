using UnityEngine;

namespace RoadwaysGame
{
    public class RoundCharacterAnimator : MonoBehaviour
    {
        [Header("Jelly Bean Crowd Animation Settings")]
        public float waddleSpeed = 4.2f;
        public float bounceHeight = 0.12f;
        public float waddleTiltAngle = 8f;
        public float armSwingAngle = 35f;

        private Transform bodyTrans;
        private Transform headTrans;
        private Transform rightArmTrans;
        private Transform leftArmTrans;
        private Transform rightLegTrans;
        private Transform leftLegTrans;
        private float randomOffset;

        private void Start()
        {
            randomOffset = Random.Range(0f, 10f);
            FindTransforms();
        }

        private void FindTransforms()
        {
            Transform container = transform.Find("CharacterContainer");
            if (container != null)
            {
                bodyTrans = container.Find("RoundBody");
                headTrans = container.Find("RoundHead");
                rightArmTrans = container.Find("RoundHand_R");
                leftArmTrans = container.Find("RoundHand_L");
                rightLegTrans = container.Find("RoundLeg_R");
                leftLegTrans = container.Find("RoundLeg_L");
            }
        }

        private void Update()
        {
            if (bodyTrans == null)
            {
                FindTransforms();
                if (bodyTrans == null) return;
            }

            float t = Time.time * waddleSpeed + randomOffset;
            float bounce = Mathf.Abs(Mathf.Sin(t)) * bounceHeight;

            // 1. Bean Body Waddle & Squish Bounce
            float tilt = Mathf.Sin(t) * waddleTiltAngle;
            bodyTrans.localPosition = new Vector3(0, 1.05f + bounce, 0);
            bodyTrans.localRotation = Quaternion.Euler(0, 0, tilt);

            float squish = 1.0f + Mathf.Sin(t * 2f) * 0.05f;
            float stretch = 1.0f - Mathf.Sin(t * 2f) * 0.025f;
            bodyTrans.localScale = new Vector3(0.85f * stretch, 0.45f * squish, 0.85f * stretch);

            // 2. Round Head Sway
            if (headTrans != null)
            {
                headTrans.localPosition = new Vector3(0, 1.72f + bounce * 1.15f, 0);
                headTrans.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 2f + randomOffset) * 6f, tilt * 0.6f);
            }

            // 3. Stubby Arms Swing (Runner Rhythm)
            if (rightArmTrans != null && leftArmTrans != null)
            {
                float armSwing = Mathf.Sin(t) * armSwingAngle;
                rightArmTrans.localRotation = Quaternion.Euler(armSwing, 0, -15f);
                leftArmTrans.localRotation = Quaternion.Euler(-armSwing, 0, 15f);
                rightArmTrans.localPosition = new Vector3(0.55f, 1.15f + bounce, 0);
                leftArmTrans.localPosition = new Vector3(-0.55f, 1.15f + bounce, 0);
            }

            // 4. Stubby Legs Running
            if (rightLegTrans != null && leftLegTrans != null)
            {
                float legSwing = Mathf.Sin(t) * 30f;
                rightLegTrans.localRotation = Quaternion.Euler(legSwing, 0, 0);
                leftLegTrans.localRotation = Quaternion.Euler(-legSwing, 0, 0);
            }
        }
    }
}
