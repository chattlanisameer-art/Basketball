using UnityEngine;
using Hesi.Core;

namespace Hesi.Player
{
    /// <summary>
    /// ★ THE core mechanic. Models streetball psychology as a state machine
    /// driven by the player's real body movement:
    ///
    ///   Neutral ──(decelerate while possessing)──► Hesitating
    ///   Hesitating ──(speed spike within burst window)──► Bursting
    ///   Hesitating ──(window expires)──► Neutral (defender recovers, no harm done)
    ///   Bursting ──(boost duration ends)──► Neutral
    ///
    /// Outputs (read by DefenderAI, ShootingSystem, MomentumSystem):
    ///   • DefenderReactionPenalty — extra seconds of defender reaction latency.
    ///     Grows while hesitating and with unpredictable movement. This is WHY
    ///     hesitation works: the defender literally processes you on a delay.
    ///   • UnpredictabilityScore  — 0..1 from speed variance. Robotic, constant-
    ///     speed movement scores 0; shifty stop-start movement scores high.
    ///   • ControlPenalty         — 0..1 from over-dribbling. Pounding the rock
    ///     without going anywhere degrades dribble control and shot accuracy.
    ///   • ShotBonusActive        — true during the post-burst grace window.
    ///     A shot released here gets an accuracy bonus (the "open look").
    ///   • BurstSpeedBoost        — multiplier handed to MomentumSystem while bursting.
    ///
    /// Nothing here reads input. It reads MOVEMENT. You can't macro it —
    /// you have to actually move like a hooper.
    /// </summary>
    public class HesiSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMotionTracker motion;

        [Header("Hesitation detection")]
        [Tooltip("You must have been moving at least this fast (m/s) for a slowdown to count as a hesi, not just standing around.")]
        [SerializeField] private float minPriorSpeed = 1.0f;
        [Tooltip("Dropping below this speed (m/s) after moving = hesitation begins.")]
        [SerializeField] private float hesiSpeedThreshold = 0.45f;
        [Tooltip("How long (s) a burst remains 'armed' after hesitation starts. Hold the freeze too long and the defender resets.")]
        [SerializeField] private float burstWindow = 1.1f;

        [Header("Burst")]
        [Tooltip("Accelerating past this speed (m/s) inside the window = burst.")]
        [SerializeField] private float burstSpeedThreshold = 1.6f;
        [SerializeField] private float burstBoostDuration = 1.4f;
        [Tooltip("Max locomotion speed multiplier at full burst strength.")]
        [SerializeField] private float maxBurstBoost = 1.35f;
        [Tooltip("Shot accuracy grace window after a burst — the 'open look'.")]
        [SerializeField] private float shotBonusDuration = 2.0f;

        [Header("Defender freeze")]
        [Tooltip("Max extra reaction latency (s) a perfect hesi inflicts on the defender.")]
        [SerializeField] private float maxReactionPenalty = 0.45f;
        [Tooltip("How fast the penalty builds while hesitating (s of penalty per s of hesi).")]
        [SerializeField] private float penaltyBuildRate = 0.5f;
        [Tooltip("How fast the defender shakes the penalty off in neutral.")]
        [SerializeField] private float penaltyDecayRate = 0.8f;

        [Header("Over-dribble")]
        [Tooltip("Dribbles inside this radius (m) with no progress count as pounding the rock.")]
        [SerializeField] private float overDribbleRadius = 1.2f;
        [Tooltip("Dribbles-in-place before penalties start.")]
        [SerializeField] private int freeDribbles = 4;
        [SerializeField] private float controlPenaltyPerDribble = 0.12f;
        [SerializeField] private float controlPenaltyDecayRate = 0.25f;

        public enum HesiState { Neutral, Hesitating, Bursting }

        // ---- Public outputs ----
        public HesiState State { get; private set; } = HesiState.Neutral;
        public float DefenderReactionPenalty { get; private set; }
        public float UnpredictabilityScore { get; private set; }
        public float ControlPenalty { get; private set; }
        public bool ShotBonusActive => Time.time < _shotBonusUntil;
        /// <summary>0..1 — how strong the current/last burst was.</summary>
        public float BurstStrength { get; private set; }
        public float BurstSpeedBoost =>
            State == HesiState.Bursting ? Mathf.Lerp(1f, maxBurstBoost, BurstStrength) : 1f;

        private bool _hasBall;
        private float _hesiStartTime;
        private float _burstEndTime;
        private float _shotBonusUntil;
        private float _penaltyAtHesiStart;

        private Vector3 _dribbleAnchor;
        private int _dribblesInPlace;

        private void Awake()
        {
            if (motion == null) motion = FindObjectOfType<PlayerMotionTracker>();
        }

        private void OnEnable()
        {
            GameEvents.BallGrabbed += OnBallGrabbed;
            GameEvents.BallReleased += OnBallReleased;
            GameEvents.DribbleBounce += OnDribbleBounce;
            GameEvents.BallStolen += OnBallLost;
            GameEvents.PossessionChanged += OnPossessionChanged;
        }

        private void OnDisable()
        {
            GameEvents.BallGrabbed -= OnBallGrabbed;
            GameEvents.BallReleased -= OnBallReleased;
            GameEvents.DribbleBounce -= OnDribbleBounce;
            GameEvents.BallStolen -= OnBallLost;
            GameEvents.PossessionChanged -= OnPossessionChanged;
        }

        private void Update()
        {
            UpdateUnpredictability();
            UpdateStateMachine();
            DecayPenalties();
        }

        // --------------------------------------------------------------
        //  Unpredictability: speed variance over the last 1.5 s, mapped
        //  so that ~0.8 m/s standard deviation = maximally shifty.
        // --------------------------------------------------------------
        private void UpdateUnpredictability()
        {
            float variance = motion.GetSpeedVariance(1.5f);
            float target = Mathf.Clamp01(variance / 0.8f);
            // Rises quickly, fades slowly — one good jab keeps paying off briefly.
            float rate = target > UnpredictabilityScore ? 4f : 0.6f;
            UnpredictabilityScore = Mathf.MoveTowards(UnpredictabilityScore, target, rate * Time.deltaTime);
        }

        private void UpdateStateMachine()
        {
            float speed = motion.SmoothedSpeed;

            switch (State)
            {
                case HesiState.Neutral:
                    // A hesi requires: ball in hand, recent real movement, then a hard slowdown.
                    if (_hasBall &&
                        motion.GetAverageSpeed(0.8f) > minPriorSpeed &&
                        speed < hesiSpeedThreshold)
                    {
                        State = HesiState.Hesitating;
                        _hesiStartTime = Time.time;
                        _penaltyAtHesiStart = DefenderReactionPenalty;
                        GameEvents.RaiseHesiStarted();
                    }
                    break;

                case HesiState.Hesitating:
                    {
                        float hesiTime = Time.time - _hesiStartTime;

                        // Freeze builds while you hold the hesitation, amplified by
                        // how unpredictable your movement has been. A telegraphed,
                        // rhythmic hesi barely moves the needle.
                        float build = penaltyBuildRate * (0.5f + 0.8f * UnpredictabilityScore);
                        DefenderReactionPenalty = Mathf.Min(maxReactionPenalty,
                            _penaltyAtHesiStart + build * hesiTime);

                        if (speed > burstSpeedThreshold)
                        {
                            BeginBurst(hesiTime);
                        }
                        else if (hesiTime > burstWindow)
                        {
                            // Held too long — defender re-sets his feet.
                            State = HesiState.Neutral;
                        }
                        break;
                    }

                case HesiState.Bursting:
                    if (Time.time > _burstEndTime || !_hasBall)
                        State = HesiState.Neutral;
                    break;
            }
        }

        private void BeginBurst(float hesiTime)
        {
            // Burst strength peaks for a hesi held 0.3–0.6 s (the believable pause)
            // and is scaled by unpredictability. Instant flick = weak; perfectly
            // timed freeze with shifty setup = full ankle-breaker.
            float timing = Mathf.Clamp01(Mathf.InverseLerp(0.1f, 0.35f, hesiTime))
                         * Mathf.Clamp01(Mathf.InverseLerp(1.1f, 0.7f, hesiTime));
            BurstStrength = Mathf.Clamp01(timing * (0.55f + 0.45f * UnpredictabilityScore));

            State = HesiState.Bursting;
            _burstEndTime = Time.time + burstBoostDuration * Mathf.Lerp(0.6f, 1f, BurstStrength);
            _shotBonusUntil = Time.time + shotBonusDuration * Mathf.Lerp(0.5f, 1f, BurstStrength);

            GameEvents.RaiseHesiBurst(BurstStrength);
            if (BurstStrength > 0.75f && DefenderReactionPenalty > maxReactionPenalty * 0.7f)
                GameEvents.RaiseDefenderFrozen();
        }

        private void DecayPenalties()
        {
            if (State != HesiState.Hesitating)
                DefenderReactionPenalty = Mathf.MoveTowards(
                    DefenderReactionPenalty, 0f, penaltyDecayRate * Time.deltaTime);

            ControlPenalty = Mathf.MoveTowards(
                ControlPenalty, 0f, controlPenaltyDecayRate * Time.deltaTime);
        }

        // --------------------------------------------------------------
        //  Over-dribble tracking: every bounce that happens within
        //  overDribbleRadius of the anchor is "pounding the rock".
        //  Moving past the radius resets the anchor — progress is rewarded.
        // --------------------------------------------------------------
        private void OnDribbleBounce(Vector3 contactPoint)
        {
            Vector3 here = motion.FloorPosition;
            if (Vector3.Distance(here, _dribbleAnchor) > overDribbleRadius)
            {
                _dribbleAnchor = here;
                _dribblesInPlace = 0;
                return;
            }

            _dribblesInPlace++;
            if (_dribblesInPlace > freeDribbles)
                ControlPenalty = Mathf.Clamp01(ControlPenalty + controlPenaltyPerDribble);
        }

        private void OnBallGrabbed(Transform hand)
        {
            _hasBall = true;
            _dribbleAnchor = motion.FloorPosition;
            _dribblesInPlace = 0;
        }

        private void OnBallReleased(Vector3 v) { /* still "has ball" during a dribble cycle; possession events decide */ }

        private void OnBallLost()
        {
            _hasBall = false;
            State = HesiState.Neutral;
        }

        private void OnPossessionChanged(bool playerBall)
        {
            _hasBall = playerBall;
            if (!playerBall) State = HesiState.Neutral;
            ControlPenalty = 0f;
            DefenderReactionPenalty = 0f;
        }
    }
}
