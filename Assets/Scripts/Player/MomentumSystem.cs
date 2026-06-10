using UnityEngine;

namespace Hesi.Player
{
    /// <summary>
    /// Makes movement STYLE matter. Sustained, committed movement in one
    /// direction builds momentum; aimless jittering doesn't. Momentum feeds a
    /// speed multiplier into stick locomotion, and the HesiSystem's burst boost
    /// stacks on top — so a hesi→burst out of built-up momentum is the fastest
    /// thing in the game, exactly like a real first step off a rocker.
    ///
    /// Apply <see cref="SpeedMultiplier"/> to your locomotion provider's move
    /// speed every frame (see LocomotionTuner notes in Docs/04_GameFeel.md).
    /// Room-scale movement is unaffected (we can't speed up your real legs),
    /// but momentum still feeds the shot/AI systems for room-scale players.
    /// </summary>
    public class MomentumSystem : MonoBehaviour
    {
        [SerializeField] private PlayerMotionTracker motion;
        [SerializeField] private HesiSystem hesi;

        [Tooltip("Seconds of committed movement to reach full momentum.")]
        [SerializeField] private float buildTime = 1.6f;
        [Tooltip("Seconds for momentum to fully drain when stopped (outside a hesi).")]
        [SerializeField] private float drainTime = 0.9f;
        [Tooltip("Speed multiplier at zero momentum (heavy first step).")]
        [SerializeField] private float minMultiplier = 0.88f;
        [Tooltip("Speed multiplier at full momentum.")]
        [SerializeField] private float maxMultiplier = 1.18f;
        [Tooltip("Direction changes sharper than this (degrees) bleed momentum.")]
        [SerializeField] private float cutAngle = 70f;
        [Tooltip("Fraction of momentum kept through a hard cut.")]
        [SerializeField, Range(0f, 1f)] private float cutRetention = 0.45f;

        /// <summary>0..1 built-up momentum.</summary>
        public float Momentum { get; private set; }
        /// <summary>Final multiplier for locomotion: momentum × hesi burst.</summary>
        public float SpeedMultiplier =>
            Mathf.Lerp(minMultiplier, maxMultiplier, Momentum) * (hesi != null ? hesi.BurstSpeedBoost : 1f);

        private Vector3 _committedDir = Vector3.forward;

        private void Awake()
        {
            if (motion == null) motion = FindObjectOfType<PlayerMotionTracker>();
            if (hesi == null) hesi = FindObjectOfType<HesiSystem>();
        }

        private void Update()
        {
            float speed = motion.SmoothedSpeed;
            bool moving = speed > 0.6f;

            if (moving)
            {
                float angle = Vector3.Angle(_committedDir, motion.MoveDirection);
                if (angle > cutAngle)
                {
                    // Hard cut: bleed momentum, commit to the new line.
                    Momentum *= cutRetention;
                    _committedDir = motion.MoveDirection;
                }
                else
                {
                    _committedDir = Vector3.Slerp(_committedDir, motion.MoveDirection, 4f * Time.deltaTime);
                    Momentum = Mathf.MoveTowards(Momentum, 1f, Time.deltaTime / buildTime);
                }
            }
            else
            {
                // A hesitation PRESERVES momentum — that's the whole trick.
                // You only drain if you stand around in neutral.
                bool preserving = hesi != null && hesi.State != HesiSystem.HesiState.Neutral;
                if (!preserving)
                    Momentum = Mathf.MoveTowards(Momentum, 0f, Time.deltaTime / drainTime);
            }
        }
    }
}
