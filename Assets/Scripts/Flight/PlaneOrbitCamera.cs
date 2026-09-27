using UnityEngine;

namespace Flight
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class PlaneOrbitCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform _target;
        [SerializeField] private bool _findPlaneAutomatically = true;
        [SerializeField] private Vector3 _focusOffset = new Vector3(0f, 1.5f, 0f);

        [Header("Follow")]
        [SerializeField, Min(0.1f)] private float _distance = 12f;
        [SerializeField, Min(0.1f)] private float _minimumDistance = 4f;
        [SerializeField, Min(0.1f)] private float _maximumDistance = 25f;
        [SerializeField, Min(0f)] private float _positionSmoothness = 10f;
        [SerializeField, Min(0f)] private float _rotationSmoothness = 14f;
        [SerializeField] private bool _followPlaneRoll = true;

        [Header("Mouse Orbit (Legacy Input Manager)")]
        [SerializeField] private int _orbitMouseButton = 1;
        [SerializeField] private string _mouseXAxis = "Mouse X";
        [SerializeField] private string _mouseYAxis = "Mouse Y";
        [SerializeField] private string _zoomAxis = "Mouse ScrollWheel";
        [SerializeField, Min(0f)] private float _orbitSensitivity = 4f;
        [SerializeField, Min(0f)] private float _zoomSensitivity = 5f;
        [SerializeField] private bool _invertOrbitY;
        [SerializeField, Range(-89f, 0f)] private float _minimumPitch = -35f;
        [SerializeField, Range(0f, 89f)] private float _maximumPitch = 75f;
        [SerializeField] private bool _returnBehindPlane = true;
        [SerializeField, Min(0f)] private float _returnSpeed = 2.5f;
        [SerializeField] private bool _lockCursorWhileOrbiting = true;

        [Header("Collision")]
        [SerializeField] private bool _avoidObstacles = true;
        [SerializeField] private LayerMask _collisionLayers = ~0;
        [SerializeField, Min(0.01f)] private float _collisionRadius = 0.3f;
        [SerializeField, Min(0f)] private float _collisionPadding = 0.15f;

        private float _orbitYaw;
        private float _orbitPitch = 12f;
        private bool _orbiting;

        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        /// <summary>
        /// Finds the plane and places the camera behind it.
        /// </summary>
        private void Start()
        {
            FindTargetIfNeeded();
            if (_target != null) SnapToTarget();
        }

        /// <summary>
        /// Reads the camera orbit and zoom input.
        /// </summary>
        private void Update()
        {
            FindTargetIfNeeded();
            if (_target == null) return;

            _orbiting = Input.GetMouseButton(_orbitMouseButton);
            if (_orbiting)
            {
                _orbitYaw += Input.GetAxis(_mouseXAxis) * _orbitSensitivity;
                float vertical = Input.GetAxis(_mouseYAxis) * _orbitSensitivity;
                _orbitPitch += vertical * (_invertOrbitY ? 1f : -1f);
                _orbitPitch = Mathf.Clamp(_orbitPitch, _minimumPitch, _maximumPitch);
            }
            else if (_returnBehindPlane)
            {
                _orbitYaw = Mathf.LerpAngle(_orbitYaw, 0f, 1f - Mathf.Exp(-_returnSpeed * Time.deltaTime));
                _orbitPitch = Mathf.Lerp(_orbitPitch, 12f, 1f - Mathf.Exp(-_returnSpeed * Time.deltaTime));
            }

            _distance = Mathf.Clamp(
                _distance - Input.GetAxis(_zoomAxis) * _zoomSensitivity,
                _minimumDistance,
                _maximumDistance);

            if (_lockCursorWhileOrbiting)
            {
                Cursor.lockState = _orbiting ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !_orbiting;
            }
        }

        /// <summary>
        /// Smoothly follows and looks at the target plane.
        /// </summary>
        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 focus = _target.TransformPoint(_focusOffset);
            Quaternion planeHeading = GetPlaneHeading();
            Quaternion orbit = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);
            Vector3 desiredPosition = focus - planeHeading * orbit * Vector3.forward * _distance;
            desiredPosition = ResolveCollision(focus, desiredPosition);

            float positionBlend = 1f - Mathf.Exp(-_positionSmoothness * Time.deltaTime);
            float rotationBlend = 1f - Mathf.Exp(-_rotationSmoothness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionBlend);

            Vector3 lookDirection = focus - transform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Vector3 up = _followPlaneRoll ? _target.up : Vector3.up;
                Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
            }
        }

        /// <summary>
        /// Gets the plane heading used to position the camera.
        /// </summary>
        private Quaternion GetPlaneHeading()
        {
            if (_followPlaneRoll) return _target.rotation;

            Vector3 heading = Vector3.ProjectOnPlane(_target.forward, Vector3.up);
            return heading.sqrMagnitude < 0.001f
                ? Quaternion.Euler(0f, _target.eulerAngles.y, 0f)
                : Quaternion.LookRotation(heading.normalized, Vector3.up);
        }

        /// <summary>
        /// Moves the camera in front of obstacles between it and the target.
        /// </summary>
        private Vector3 ResolveCollision(Vector3 focus, Vector3 desiredPosition)
        {
            if (!_avoidObstacles) return desiredPosition;

            Vector3 direction = desiredPosition - focus;
            float distance = direction.magnitude;
            if (distance <= 0.001f) return desiredPosition;

            if (Physics.SphereCast(
                    focus,
                    _collisionRadius,
                    direction / distance,
                    out RaycastHit hit,
                    distance,
                    _collisionLayers,
                    QueryTriggerInteraction.Ignore))
                return focus + direction.normalized * Mathf.Max(0f, hit.distance - _collisionPadding);

            return desiredPosition;
        }

        /// <summary>
        /// Finds a plane automatically when no target is assigned.
        /// </summary>
        private void FindTargetIfNeeded()
        {
            if (_target != null || !_findPlaneAutomatically) return;
            ArcadePlaneController plane = FindAnyObjectByType<ArcadePlaneController>();
            if (plane != null) _target = plane.transform;
        }

        /// <summary>
        /// Places the camera at its target position immediately.
        /// </summary>
        private void SnapToTarget()
        {
            Vector3 focus = _target.TransformPoint(_focusOffset);
            transform.position = focus - GetPlaneHeading() * Quaternion.Euler(_orbitPitch, 0f, 0f) * Vector3.forward * _distance;
            transform.rotation = Quaternion.LookRotation(focus - transform.position, _followPlaneRoll ? _target.up : Vector3.up);
        }

        /// <summary>
        /// Restores the cursor when the camera is disabled.
        /// </summary>
        private void OnDisable()
        {
            if (!_lockCursorWhileOrbiting) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Keeps the camera settings within valid ranges.
        /// </summary>
        private void OnValidate()
        {
            _maximumDistance = Mathf.Max(_minimumDistance, _maximumDistance);
            _distance = Mathf.Clamp(_distance, _minimumDistance, _maximumDistance);
            _maximumPitch = Mathf.Max(_minimumPitch, _maximumPitch);
        }
    }
}
