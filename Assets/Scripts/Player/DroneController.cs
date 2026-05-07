using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class DroneController : MonoBehaviour
    {
        [SerializeField] float moveAccel = 180f;
        [SerializeField] float maxSpeed = 42f;
        [SerializeField] float boostAccelMul = 1.8f;
        [SerializeField] float boostSpeedMul = 1.7f;

        Rigidbody rb;
        bool boostActive;
        Vector3 lastInputDir = Vector3.forward;

        public bool IsBoosting => boostActive;
        public Vector3 LastInputDir => lastInputDir;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearDamping = 2f;
            rb.angularDamping = 5f;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void FixedUpdate()
        {
            var kb = Keyboard.current;
            boostActive = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);

            Vector2 axis = ReadMoveAxis(kb);
            Vector3 input = new Vector3(axis.x, 0f, axis.y);
            if (input.sqrMagnitude > 1f) input.Normalize();
            if (input.sqrMagnitude > 0.01f) lastInputDir = input.normalized;

            float accel = boostActive ? moveAccel * boostAccelMul : moveAccel;
            float topSpeed = boostActive ? maxSpeed * boostSpeedMul : maxSpeed;

            rb.AddForce(input * accel, ForceMode.Acceleration);

            Vector3 horiz = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (horiz.magnitude > topSpeed)
            {
                horiz = horiz.normalized * topSpeed;
                rb.linearVelocity = new Vector3(horiz.x, rb.linearVelocity.y, horiz.z);
            }
        }

        static Vector2 ReadMoveAxis(Keyboard kb)
        {
            if (kb == null) return Vector2.zero;
            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                   - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                   - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
        }
    }
}
