using UnityEngine;
using Hesi.Core;

namespace Hesi.Ball
{
    /// <summary>
    /// Makes the sphere behave like a real basketball.
    ///
    /// Real ball reference numbers (size 7):
    ///   radius 0.121 m, mass 0.62 kg, inflated to bounce ~1.05 m when
    ///   dropped from 1.80 m  →  restitution ≈ 0.76 on hardwood, ~0.72 asphalt.
    ///
    /// Unity's PhysicMaterial restitution alone isn't enough because:
    ///   1. Spin: a real ball converts spin↔horizontal velocity at every bounce
    ///      (why backspin makes a ball check back toward you).
    ///   2. Magnus effect: backspin in flight adds lift, flattening the arc
    ///      slightly and softening rim hits — the "shooter's touch".
    /// This script adds both, plus velocity-scaled bounce audio.
    ///
    /// Rigidbody settings (set in Awake to be safe):
    ///   mass 0.62, drag 0.05, angularDrag 0.4,
    ///   interpolation Interpolate, collision Continuous Dynamic.
    /// PhysicMaterial on the SphereCollider:
    ///   bounciness 0.72, bounce combine Maximum, friction 0.6.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class BasketballPhysics : MonoBehaviour
    {
        [Header("Spin")]
        [Tooltip("Magnus lift coefficient. 0.00012 gives a subtle, realistic float on backspin shots.")]
        [SerializeField] private float magnusCoefficient = 0.00012f;
        [Tooltip("How much surface spin converts to horizontal velocity on bounce (0..1).")]
        [SerializeField, Range(0f, 1f)] private float spinBounceTransfer = 0.35f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] bounceClips;     // asphalt thumps
        [SerializeField] private AudioClip[] rimClips;        // metallic clangs
        [SerializeField] private float minImpactForSound = 0.8f;

        public Rigidbody Body { get; private set; }
        /// <summary>True while held in a hand (BallGrabHandler drives the flag).</summary>
        public bool IsHeld { get; set; }
        /// <summary>Set true by ShootingSystem on release so HoopScoreDetector can attribute shots.</summary>
        public bool InShotFlight { get; set; }

        private SphereCollider _collider;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            _collider = GetComponent<SphereCollider>();

            Body.mass = 0.62f;
            Body.drag = 0.05f;
            Body.angularDrag = 0.4f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxAngularVelocity = 50f;
        }

        private void FixedUpdate()
        {
            if (IsHeld) return;

            // Magnus: F = k * (ω × v). Backspin (ω opposing travel around the
            // horizontal axis) produces an upward component — lift.
            Vector3 magnus = magnusCoefficient * Vector3.Cross(Body.angularVelocity, Body.velocity)
                             * Body.velocity.magnitude;
            Body.AddForce(magnus, ForceMode.Force);
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impact = collision.relativeVelocity.magnitude;

            // Spin → horizontal kick. Surface velocity of the contact point due
            // to spin, projected onto the contact plane, partially becomes
            // linear velocity (rolling friction impulse, simplified).
            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                Vector3 r = contact.point - Body.worldCenterOfMass;
                Vector3 surfaceVel = Vector3.Cross(Body.angularVelocity, r);
                Vector3 tangential = Vector3.ProjectOnPlane(surfaceVel, contact.normal);
                Body.AddForce(-tangential * spinBounceTransfer, ForceMode.VelocityChange);
                Body.angularVelocity *= (1f - spinBounceTransfer * 0.5f);
            }

            PlayBounceSound(collision, impact);

            // Any solid contact ends "shot flight" except rim/backboard —
            // those still count as part of the shot for scoring attribution.
            int layer = collision.gameObject.layer;
            bool rimOrBoard = collision.gameObject.CompareTag("Rim") ||
                              collision.gameObject.CompareTag("Backboard");
            if (InShotFlight && !rimOrBoard)
                InShotFlight = false;
        }

        private void PlayBounceSound(Collision collision, float impact)
        {
            if (audioSource == null || impact < minImpactForSound) return;

            AudioClip[] set = collision.gameObject.CompareTag("Rim") ? rimClips : bounceClips;
            if (set == null || set.Length == 0) return;

            audioSource.pitch = Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(set[Random.Range(0, set.Length)],
                Mathf.Clamp01(impact / 8f));
        }

        /// <summary>Teleport the ball somewhere at rest (check-ball resets).</summary>
        public void ResetTo(Vector3 position)
        {
            Body.velocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.position = position;
            InShotFlight = false;
        }
    }
}
