using UnityEngine;
using Hesi.Core;
using Hesi.Ball;
using Hesi.AI;

namespace Hesi.Player
{
    /// <summary>
    /// Skill-based shooting. The throw is ALWAYS your real hand velocity —
    /// we never take the shot away from you. What skill controls is how much
    /// the game *cleans up* your release:
    ///
    ///   shotQuality (0..1) decides how far your raw velocity is blended
    ///   toward the mathematically perfect arc to the rim:
    ///
    ///     quality = baseForm                  (how close your raw arc already was)
    ///             + hesi shot-bonus           (open look after a burst)
    ///             - over-dribble penalty      (tired handle = shaky wrist)
    ///             - contest penalty           (defender's hand in your face)
    ///
    ///   A max-quality release gets up to `maxAssistBlend` (35%) correction —
    ///   enough that good form is consistently rewarded, never so much that
    ///   a garbage throw goes in. A heavily contested, over-dribbled brick
    ///   gets zero help and even a touch of added wobble.
    ///
    /// This is the anti-arcade contract: assists shrink as situations get worse,
    /// instead of a button press deciding makes.
    /// </summary>
    public class ShootingSystem : MonoBehaviour
    {
        [SerializeField] private BallGrabHandler grabHandler;
        [SerializeField] private BasketballPhysics ball;
        [SerializeField] private HesiSystem hesi;
        [SerializeField] private DefenderAI defender;
        [SerializeField] private Transform rimCenter;

        [Header("Assist tuning")]
        [Tooltip("Max blend toward the perfect arc at quality = 1.")]
        [SerializeField, Range(0f, 1f)] private float maxAssistBlend = 0.35f;
        [Tooltip("A release only counts as a shot if its raw arc lands within this distance (m) of the rim. Beyond that it's a pass/throw and gets no assist.")]
        [SerializeField] private float shotIntentRadius = 1.8f;
        [Tooltip("Preferred launch angle for the assist arc (degrees). 48–52 is a real jumper.")]
        [SerializeField] private float idealLaunchAngle = 50f;

        [Header("Penalties")]
        [Tooltip("Contest distance (m): defender hand closer than this hurts quality.")]
        [SerializeField] private float contestRange = 0.9f;
        [SerializeField, Range(0f, 1f)] private float maxContestPenalty = 0.5f;
        [Tooltip("Extra random wobble (m/s) applied at quality 0.")]
        [SerializeField] private float maxWobble = 0.5f;

        [Header("Hesi bonus")]
        [SerializeField, Range(0f, 1f)] private float shotBonusAmount = 0.25f;

        private void Awake()
        {
            if (grabHandler == null) grabHandler = FindObjectOfType<BallGrabHandler>();
            if (ball == null) ball = FindObjectOfType<BasketballPhysics>();
            if (hesi == null) hesi = FindObjectOfType<HesiSystem>();
            if (defender == null) defender = FindObjectOfType<DefenderAI>();
        }

        private void OnEnable() => GameEvents.BallReleased += OnBallReleased;
        private void OnDisable() => GameEvents.BallReleased -= OnBallReleased;

        private void OnBallReleased(Vector3 rawVelocity)
        {
            if (rimCenter == null) return;

            // 1. Is this even a shot? Simulate the raw ballistic arc and see
            //    where it is when it crosses rim height on the way down.
            if (!TryGetArcPositionAtRimHeight(ball.transform.position, rawVelocity, out Vector3 arrival))
                return;
            float missDistance = Vector3.Distance(
                new Vector3(arrival.x, 0, arrival.z),
                new Vector3(rimCenter.position.x, 0, rimCenter.position.z));
            if (missDistance > shotIntentRadius) return; // a pass or a heave — leave it raw

            // 2. Score the shot quality.
            float baseForm = 1f - Mathf.Clamp01(missDistance / shotIntentRadius);

            bool contested = false;
            float contestPenalty = 0f;
            if (defender != null)
            {
                float d = defender.DistanceToContest(ball.transform.position);
                if (d < contestRange)
                {
                    contested = true;
                    contestPenalty = maxContestPenalty * (1f - d / contestRange);
                }
            }

            float bonus = (hesi != null && hesi.ShotBonusActive) ? shotBonusAmount : 0f;
            float dribblePenalty = hesi != null ? hesi.ControlPenalty * 0.4f : 0f;

            float quality = Mathf.Clamp01(baseForm + bonus - contestPenalty - dribblePenalty);

            // 3. Blend toward the perfect arc, scaled by quality.
            Vector3 perfect = ComputePerfectArcVelocity(ball.transform.position, rimCenter.position);
            float blend = maxAssistBlend * quality;
            Vector3 finalVel = Vector3.Lerp(rawVelocity, perfect, blend);

            // 4. Low quality adds wobble instead of help.
            if (quality < 0.35f)
                finalVel += Random.insideUnitSphere * maxWobble * (0.35f - quality) / 0.35f;

            ball.Body.velocity = finalVel;
            ball.InShotFlight = true;
            GameEvents.RaiseShotReleased(quality, contested);
        }

        /// <summary>Where the raw arc is when it descends through rim height. False if it never gets there.</summary>
        private bool TryGetArcPositionAtRimHeight(Vector3 origin, Vector3 v, out Vector3 pos)
        {
            pos = default;
            float h = rimCenter.position.y - origin.y;
            float g = Mathf.Abs(Physics.gravity.y);
            // Solve h = v.y*t - g*t²/2 for the LATER root (descending pass).
            float disc = v.y * v.y - 2f * g * h;
            if (disc < 0f) return false;          // never reaches rim height
            float t = (v.y + Mathf.Sqrt(disc)) / g;
            if (t <= 0.05f) return false;
            pos = origin + new Vector3(v.x * t, h, v.z * t);
            return true;
        }

        /// <summary>Ballistic velocity from origin to target at the ideal launch angle.</summary>
        private Vector3 ComputePerfectArcVelocity(Vector3 origin, Vector3 target)
        {
            Vector3 flat = new Vector3(target.x - origin.x, 0f, target.z - origin.z);
            float x = flat.magnitude;
            float y = target.y - origin.y;
            float g = Mathf.Abs(Physics.gravity.y);
            float angle = idealLaunchAngle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle);

            // v² = g·x² / (2·cos²θ·(x·tanθ − y))
            float denom = 2f * cos * cos * (x * Mathf.Tan(angle) - y);
            if (denom <= 0.01f) return (target - origin).normalized * 7f; // degenerate: point-blank
            float speed = Mathf.Sqrt(g * x * x / denom);

            return flat.normalized * speed * cos + Vector3.up * speed * Mathf.Sin(angle);
        }
    }
}
