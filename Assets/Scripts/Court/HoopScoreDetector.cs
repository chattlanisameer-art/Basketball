using UnityEngine;
using Hesi.Core;
using Hesi.Ball;

namespace Hesi.Court
{
    /// <summary>
    /// Robust two-trigger scoring. A basket counts only when the ball passes
    /// TriggerTop then TriggerBottom, downward, within a short window —
    /// immune to balls rolling on the rim, bouncing up through the net,
    /// or clipping a trigger sideways.
    ///
    /// Setup on the Hoop prefab:
    ///   TriggerTop    — SphereCollider (isTrigger), radius 0.16, center ~5cm ABOVE rim plane
    ///   TriggerBottom — SphereCollider (isTrigger), radius 0.16, center ~25cm BELOW rim plane
    /// Assign both to this component (it lives on the hoop root).
    /// Rim capsules tagged "Rim", backboard tagged "Backboard" (used for clean-swish detection).
    ///
    /// Points: 2 from beyond the arc, 1 inside (streetball 1s & 2s) — the arc
    /// check uses horizontal distance from the rim at release, captured when
    /// the shot leaves the hand.
    /// </summary>
    public class HoopScoreDetector : MonoBehaviour
    {
        [SerializeField] private Collider triggerTop;
        [SerializeField] private Collider triggerBottom;
        [SerializeField] private Transform rimCenter;
        [Tooltip("Streetball arc distance (m) for a 2-pointer. Regulation 3pt = 6.75.")]
        [SerializeField] private float twoPointDistance = 6.75f;
        [Tooltip("Max seconds between top and bottom trigger for a make.")]
        [SerializeField] private float passThroughWindow = 0.8f;

        private float _topCrossTime = -10f;
        private bool _rimTouchedThisShot;
        private Vector3 _releasePosition;
        private BasketballPhysics _ball;

        private void Awake()
        {
            _ball = FindObjectOfType<BasketballPhysics>();
            // Relay components forward trigger events from the child colliders.
            AddRelay(triggerTop, OnTopEnter);
            AddRelay(triggerBottom, OnBottomEnter);
        }

        private void OnEnable()
        {
            GameEvents.BallReleased += OnBallReleased;
        }

        private void OnDisable()
        {
            GameEvents.BallReleased -= OnBallReleased;
        }

        private void OnBallReleased(Vector3 velocity)
        {
            _releasePosition = _ball != null ? _ball.transform.position : Vector3.zero;
            _rimTouchedThisShot = false;
        }

        private void Update()
        {
            // Track rim contact for the clean-swish flag while a shot is live.
            if (_ball != null && _ball.InShotFlight)
            {
                // BasketballPhysics keeps InShotFlight true through rim hits;
                // detect rim contact via proximity + speed change is overkill —
                // we just listen on the rim's collision relay below.
            }
        }

        /// <summary>Called by RimContactRelay on the rim capsules.</summary>
        public void NotifyRimContact() => _rimTouchedThisShot = true;

        private void OnTopEnter(Collider other)
        {
            if (!IsBall(other)) return;
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null && rb.velocity.y < -0.5f)   // must be heading DOWN
                _topCrossTime = Time.time;
        }

        private void OnBottomEnter(Collider other)
        {
            if (!IsBall(other)) return;
            if (Time.time - _topCrossTime > passThroughWindow) return;
            _topCrossTime = -10f; // consume

            float dist = Vector2.Distance(
                new Vector2(_releasePosition.x, _releasePosition.z),
                new Vector2(rimCenter.position.x, rimCenter.position.z));
            int points = dist >= twoPointDistance ? 2 : 1;
            bool clean = !_rimTouchedThisShot;

            if (_ball != null) _ball.InShotFlight = false;
            GameEvents.RaiseScored(points, clean);
        }

        private static bool IsBall(Collider c) =>
            c.attachedRigidbody != null && c.attachedRigidbody.GetComponent<BasketballPhysics>() != null;

        private void AddRelay(Collider trigger, System.Action<Collider> handler)
        {
            if (trigger == null) { Debug.LogError($"{name}: hoop trigger not assigned"); return; }
            var relay = trigger.gameObject.AddComponent<TriggerRelay>();
            relay.OnEnter = handler;
        }

        /// <summary>Tiny helper so child trigger colliders can report to the detector.</summary>
        private class TriggerRelay : MonoBehaviour
        {
            public System.Action<Collider> OnEnter;
            private void OnTriggerEnter(Collider other) => OnEnter?.Invoke(other);
        }
    }

    /// <summary>
    /// Put this on each rim capsule (tag "Rim") and the backboard, pointing at
    /// the HoopScoreDetector, so swishes can be told apart from rim-rattlers.
    /// </summary>
    public class RimContactRelay : MonoBehaviour
    {
        [SerializeField] private HoopScoreDetector detector;
        private void OnCollisionEnter(Collision c)
        {
            if (c.rigidbody != null && c.rigidbody.GetComponent<BasketballPhysics>() != null)
                detector.NotifyRimContact();
        }
    }
}
