using UnityEngine;
using Hesi.Core;
using Hesi.Player;

namespace Hesi.Ball
{
    /// <summary>
    /// Grab + throw. Custom (not XRGrabInteractable) because basketball needs
    /// things the stock interactable can't do:
    ///   • Grab is proximity + grip based, with a "carry" offset so the ball
    ///     sits in the palm, not at the controller origin.
    ///   • While held, the ball is moved with MovePosition (kinematic follow)
    ///     so it still pushes the defender's hands and rim correctly.
    ///   • On release, velocity comes from the hand tracker's smoothed window
    ///     PLUS a wrist-snap angular contribution — this is what makes throws
    ///     feel 1:1 instead of weak.
    ///   • Two-hand pickup: if both hands grip near the ball, dominant hand wins.
    ///
    /// Attach to the Basketball prefab. Assign both hand trackers.
    /// </summary>
    [RequireComponent(typeof(BasketballPhysics))]
    public class BallGrabHandler : MonoBehaviour
    {
        [SerializeField] private VRHandTracker leftHand;
        [SerializeField] private VRHandTracker rightHand;

        [Tooltip("Hand must be within this distance (m) of the ball surface to grab.")]
        [SerializeField] private float grabRange = 0.10f;
        [Tooltip("Grip amount (0..1) above which a grab engages.")]
        [SerializeField] private float gripThreshold = 0.55f;
        [Tooltip("Grip below which the ball releases.")]
        [SerializeField] private float releaseThreshold = 0.35f;
        [Tooltip("Extra throw velocity from wrist rotation (flick). 0.15 feels right.")]
        [SerializeField] private float wristSnapFactor = 0.15f;

        public bool IsHeld => _holdingHand != null;
        public VRHandTracker HoldingHand => _holdingHand;

        private BasketballPhysics _ball;
        private SphereCollider _collider;
        private VRHandTracker _holdingHand;
        private Vector3 _localGrabOffset;
        private Quaternion _lastHandRot;

        private void Awake()
        {
            _ball = GetComponent<BasketballPhysics>();
            _collider = GetComponent<SphereCollider>();
        }

        private void Update()
        {
            if (_holdingHand == null)
                TryGrab(rightHand) ; // dominant hand gets priority each frame
            if (_holdingHand == null)
                TryGrab(leftHand);

            if (_holdingHand != null && _holdingHand.GripAmount < releaseThreshold)
                Release();
        }

        private void FixedUpdate()
        {
            if (_holdingHand == null) return;

            // Kinematic follow: keep the rigidbody non-kinematic but force its
            // pose, so contacts with the defender's steal-hands still register.
            Vector3 target = _holdingHand.PalmPosition + _holdingHand.PalmRotation * _localGrabOffset;
            _ball.Body.velocity = Vector3.zero;
            _ball.Body.angularVelocity = Vector3.zero;
            _ball.Body.MovePosition(target);
        }

        private void TryGrab(VRHandTracker hand)
        {
            if (hand == null || !hand.IsTracked) return;
            if (hand.GripAmount < gripThreshold) return;

            float distToSurface = Vector3.Distance(hand.PalmPosition, transform.position) - _collider.radius;
            if (distToSurface > grabRange) return;

            _holdingHand = hand;
            _lastHandRot = hand.PalmRotation;
            // Preserve where on the ball the hand grabbed, in hand-local space,
            // so the ball doesn't snap-teleport into the palm.
            _localGrabOffset = Quaternion.Inverse(hand.PalmRotation)
                               * (transform.position - hand.PalmPosition);
            // But clamp the offset so it can't be held by a fingertip.
            _localGrabOffset = Vector3.ClampMagnitude(_localGrabOffset, _collider.radius * 1.1f);

            _ball.IsHeld = true;
            _ball.Body.useGravity = false;
            GameEvents.RaiseBallGrabbed(hand.transform);
        }

        public void Release()
        {
            if (_holdingHand == null) return;
            VRHandTracker hand = _holdingHand;
            _holdingHand = null;

            _ball.IsHeld = false;
            _ball.Body.useGravity = true;

            // Linear throw velocity from the smoothed window...
            Vector3 throwVel = hand.Velocity;

            // ...plus the wrist snap: angular velocity of the hand crossed with
            // the lever arm to the ball center. This is what gives a flick its pop
            // and puts natural backspin on a proper shooting motion.
            Quaternion delta = hand.PalmRotation * Quaternion.Inverse(_lastHandRot);
            delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
            if (angleDeg > 180f) angleDeg -= 360f;
            Vector3 handAngularVel = axis * (angleDeg * Mathf.Deg2Rad / Mathf.Max(Time.deltaTime, 0.001f));
            Vector3 lever = transform.position - hand.PalmPosition;
            throwVel += Vector3.Cross(handAngularVel, lever) * wristSnapFactor;

            _ball.Body.velocity = throwVel;
            // Backspin proportional to the upward flick component.
            Vector3 spinAxis = Vector3.Cross(throwVel.normalized, Vector3.up);
            _ball.Body.angularVelocity = -spinAxis * Mathf.Clamp(throwVel.magnitude * 1.5f, 0f, 25f);

            GameEvents.RaiseBallReleased(throwVel);
        }

        private void LateUpdate()
        {
            if (_holdingHand != null) _lastHandRot = _holdingHand.PalmRotation;
        }
    }
}
