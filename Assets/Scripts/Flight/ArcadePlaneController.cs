using UnityEngine;

namespace Flight
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ArcadePlaneController : MonoBehaviour
    {
        [Header("Input (Legacy Input Manager)")]
        [SerializeField] private string _pitchAxis = "Vertical";
        [SerializeField] private string _rollAxis = "Horizontal";
        [SerializeField] private string _yawAxis = "";
        [SerializeField] private KeyCode _yawLeftKey = KeyCode.Q;
        [SerializeField] private KeyCode _yawRightKey = KeyCode.E;
        [SerializeField] private KeyCode _throttleUpKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode _throttleDownKey = KeyCode.LeftControl;
        [SerializeField] private KeyCode _boostKey = KeyCode.Space;
        [SerializeField] private KeyCode _airBrakeKey = KeyCode.C;
        [SerializeField] private bool _invertPitch;

        [Header("Engine")]
        [SerializeField, Min(0f)] private float _minimumSpeed = 20f;
        [SerializeField, Min(0f)] private float _maximumSpeed = 80f;
        [SerializeField, Min(0f)] private float _boostSpeed = 115f;
        [SerializeField, Min(0f)] private float _acceleration = 25f;
        [SerializeField, Min(0f)] private float _deceleration = 18f;
        [SerializeField, Range(0f, 1f)] private float _startingThrottle = 0.55f;
        [SerializeField, Min(0f)] private float _throttleChangeRate = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _airBrakeSpeedMultiplier = 0.35f;

        [Header("Steering")]
        [SerializeField, Min(0f)] private float _pitchDegreesPerSecond = 75f;
        [SerializeField, Min(0f)] private float _rollDegreesPerSecond = 115f;
        [SerializeField, Min(0f)] private float _yawDegreesPerSecond = 45f;
        [SerializeField, Range(0f, 1f)] private float _lowSpeedControl = 0.45f;
        [SerializeField, Min(0f)] private float _inputResponse = 7f;

        [Header("Flight Assistance")]
        [SerializeField] private bool _useGravity;
        [SerializeField, Min(0f)] private float _lift = 5f;
        [SerializeField, Min(0f)] private float _autoLevelStrength = 1.5f;
        [SerializeField, Range(0f, 1f)] private float _autoLevelInputDeadZone = 0.08f;
        [SerializeField, Min(0f)] private float _velocityAlignment = 5f;

        private Rigidbody _rigidbody;
        private Vector3 _steeringInput;
        private float _throttle;
        private bool _boosting;
        private bool _braking;

        public float Speed => _rigidbody == null ? 0f : _rigidbody.linearVelocity.magnitude;
        public float Throttle => _throttle;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.useGravity = _useGravity;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _throttle = _startingThrottle;
        }

        private void Start()
        {
            if (_rigidbody.linearVelocity.sqrMagnitude < 0.01f)
                _rigidbody.linearVelocity = transform.forward * Mathf.Lerp(_minimumSpeed, _maximumSpeed, _throttle);
        }

        private void Update()
        {
            float pitch = Input.GetAxisRaw(_pitchAxis) * (_invertPitch ? 1f : -1f);
            float roll = Input.GetAxisRaw(_rollAxis);
            float yaw = ReadOptionalAxis(_yawAxis);
            if (Input.GetKey(_yawLeftKey)) yaw -= 1f;
            if (Input.GetKey(_yawRightKey)) yaw += 1f;

            Vector3 targetInput = Vector3.ClampMagnitude(new Vector3(pitch, yaw, -roll), 1f);
            _steeringInput = Vector3.MoveTowards(
                _steeringInput,
                targetInput,
                Mathf.Max(0.01f, _inputResponse) * Time.deltaTime);

            float throttleInput = 0f;
            if (Input.GetKey(_throttleUpKey)) throttleInput += 1f;
            if (Input.GetKey(_throttleDownKey)) throttleInput -= 1f;
            _throttle = Mathf.Clamp01(_throttle + throttleInput * _throttleChangeRate * Time.deltaTime);

            _boosting = Input.GetKey(_boostKey);
            _braking = Input.GetKey(_airBrakeKey);
        }

        private void FixedUpdate()
        {
            ApplySteering();
            ApplyEngine();
            ApplyLift();
        }

        private void ApplySteering()
        {
            float speedRange = Mathf.Max(0.01f, _maximumSpeed - _minimumSpeed);
            float normalizedSpeed = Mathf.Clamp01((Speed - _minimumSpeed) / speedRange);
            float authority = Mathf.Lerp(_lowSpeedControl, 1f, normalizedSpeed);

            Vector3 localDegrees = new Vector3(
                _steeringInput.x * _pitchDegreesPerSecond,
                _steeringInput.y * _yawDegreesPerSecond,
                _steeringInput.z * _rollDegreesPerSecond) * authority * Time.fixedDeltaTime;

            Quaternion nextRotation = _rigidbody.rotation * Quaternion.Euler(localDegrees);
            bool shouldLevel = Mathf.Abs(_steeringInput.z) <= _autoLevelInputDeadZone && _autoLevelStrength > 0f;
            if (shouldLevel)
            {
                Vector3 leveledForward = Vector3.ProjectOnPlane(nextRotation * Vector3.forward, Vector3.up);
                if (leveledForward.sqrMagnitude > 0.001f)
                {
                    Quaternion levelRotation = Quaternion.LookRotation(leveledForward.normalized, Vector3.up);
                    float levelAmount = 1f - Mathf.Exp(-_autoLevelStrength * Time.fixedDeltaTime);
                    nextRotation = Quaternion.Slerp(nextRotation, levelRotation, levelAmount);
                }
            }

            _rigidbody.MoveRotation(nextRotation);
        }

        private void ApplyEngine()
        {
            float targetSpeed = Mathf.Lerp(_minimumSpeed, _maximumSpeed, _throttle);
            if (_boosting) targetSpeed = Mathf.Max(targetSpeed, _boostSpeed);
            if (_braking) targetSpeed *= _airBrakeSpeedMultiplier;

            float rate = Speed < targetSpeed ? _acceleration : _deceleration;
            float nextSpeed = Mathf.MoveTowards(Speed, targetSpeed, rate * Time.fixedDeltaTime);
            Vector3 desiredVelocity = transform.forward * nextSpeed;
            float alignment = 1f - Mathf.Exp(-_velocityAlignment * Time.fixedDeltaTime);
            _rigidbody.linearVelocity = Vector3.Lerp(_rigidbody.linearVelocity, desiredVelocity, alignment);
        }

        private void ApplyLift()
        {
            if (_lift <= 0f) return;

            float liftScale = Mathf.Clamp01(Speed / Mathf.Max(0.01f, _minimumSpeed));
            _rigidbody.AddForce(transform.up * (_lift * liftScale), ForceMode.Acceleration);
        }

        private static float ReadOptionalAxis(string axisName)
        {
            return string.IsNullOrWhiteSpace(axisName) ? 0f : Input.GetAxisRaw(axisName);
        }

        private void OnValidate()
        {
            _maximumSpeed = Mathf.Max(_minimumSpeed, _maximumSpeed);
            _boostSpeed = Mathf.Max(_maximumSpeed, _boostSpeed);
            if (_rigidbody != null) _rigidbody.useGravity = _useGravity;
        }
    }
}
