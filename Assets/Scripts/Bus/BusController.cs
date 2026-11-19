using UnityEngine;

namespace RoadwaysGame
{
    public class BusController : MonoBehaviour
    {
        [Header("Engine & Speed")]
        public float maxSpeed = 22f;
        public float acceleration = 8f;
        public float reverseSpeed = 8f;
        public float brakeForce = 14f;

        [Header("Steering & Handling")]
        public float maxSteerAngle = 32f;
        public float steerSpeed = 4f;

        [Header("Suspension Body Tilt")]
        public float bodyRollIntensity = 2.5f;

        private float currentSpeed = 0f;
        private float currentSteerAngle = 0f;
        private Transform cabinBody;

        private void Start()
        {
            cabinBody = transform.Find("ChibiCarContainer/MainCabin") ?? transform.Find("BusMeshContainer/Cabin");
        }

        private void Update()
        {
            float inputVertical = Input.GetAxis("Vertical");
            float inputHorizontal = Input.GetAxis("Horizontal");

            // Acceleration & Braking
            if (inputVertical > 0)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);
            }
            else if (inputVertical < 0)
            {
                if (currentSpeed > 0.5f)
                {
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0, brakeForce * Time.deltaTime);
                }
                else
                {
                    currentSpeed = Mathf.MoveTowards(currentSpeed, -reverseSpeed, acceleration * Time.deltaTime);
                }
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0, 3f * Time.deltaTime);
            }

            // Steering
            float targetSteer = inputHorizontal * maxSteerAngle;
            currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteer, steerSpeed * 40f * Time.deltaTime);

            // Move & Turn
            if (Mathf.Abs(currentSpeed) > 0.1f)
            {
                float turnDir = currentSpeed >= 0 ? 1f : -1f;
                transform.Rotate(0, currentSteerAngle * (currentSpeed / maxSpeed) * turnDir * 35f * Time.deltaTime, 0);
            }
            transform.Translate(Vector3.forward * (currentSpeed * Time.deltaTime));

            // Body roll tilt
            if (cabinBody != null)
            {
                float targetRoll = -inputHorizontal * bodyRollIntensity * (Mathf.Abs(currentSpeed) / maxSpeed);
                cabinBody.localRotation = Quaternion.Slerp(cabinBody.localRotation, Quaternion.Euler(0, 0, targetRoll), 8f * Time.deltaTime);
            }
        }
    }
}
