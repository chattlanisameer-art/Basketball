using System.Collections.Generic;
using UnityEngine;

namespace Hesi.Player
{
    /// <summary>
    /// Tracks the player's BODY movement (head position projected to the floor)
    /// and keeps a short history of speed/direction samples.
    ///
    /// This history is the raw material for two things:
    ///  1. HesiSystem — detects deceleration→burst patterns and computes unpredictability.
    ///  2. DefenderAI — the defender deliberately reads *old* samples from this buffer,
    ///     which is how reaction latency is modeled. A laggy defender isn't "dumb",
    ///     he's literally reacting to where you were 250–600 ms ago, exactly like a
    ///     real defender processing your moves.
    ///
    /// Works for both room-scale movement and stick locomotion because it tracks
    /// the head's world position, not input.
    /// </summary>
    public class PlayerMotionTracker : MonoBehaviour
    {
        [SerializeField] private Transform head; // Main Camera under XR Origin

        [Tooltip("Seconds of motion history to keep. 2s covers any hesi pattern.")]
        [SerializeField] private float historyLength = 2f;

        public struct MotionSample
        {
            public float time;
            public Vector3 position;     // world, projected to floor plane
            public Vector3 velocity;     // horizontal only
            public float speed;
        }

        private readonly List<MotionSample> _samples = new List<MotionSample>(256);
        private Vector3 _lastFloorPos;
        private bool _initialized;

        // ---- Public state ----
        public Vector3 FloorPosition { get; private set; }
        public Vector3 HorizontalVelocity { get; private set; }
        public float Speed { get; private set; }
        /// <summary>Speed smoothed over ~0.3s — use for gameplay thresholds, raw speed is jittery.</summary>
        public float SmoothedSpeed { get; private set; }
        public Vector3 MoveDirection { get; private set; } = Vector3.forward;

        private void Awake()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
        }

        private void Update()
        {
            if (head == null) return;

            FloorPosition = new Vector3(head.position.x, 0f, head.position.z);

            if (!_initialized)
            {
                _lastFloorPos = FloorPosition;
                _initialized = true;
                return;
            }

            if (Time.deltaTime > 0f)
                HorizontalVelocity = (FloorPosition - _lastFloorPos) / Time.deltaTime;
            _lastFloorPos = FloorPosition;

            Speed = HorizontalVelocity.magnitude;
            // Exponential smoothing, ~0.3s time constant.
            SmoothedSpeed = Mathf.Lerp(SmoothedSpeed, Speed, 1f - Mathf.Exp(-Time.deltaTime / 0.3f));
            if (Speed > 0.3f) MoveDirection = HorizontalVelocity.normalized;

            _samples.Add(new MotionSample
            {
                time = Time.time,
                position = FloorPosition,
                velocity = HorizontalVelocity,
                speed = Speed
            });
            while (_samples.Count > 0 && Time.time - _samples[0].time > historyLength)
                _samples.RemoveAt(0);
        }

        /// <summary>
        /// The player's motion state as it was <paramref name="secondsAgo"/> in the past.
        /// DefenderAI calls this with its current reaction latency.
        /// </summary>
        public MotionSample GetDelayedSample(float secondsAgo)
        {
            float target = Time.time - secondsAgo;
            for (int i = _samples.Count - 1; i >= 0; i--)
                if (_samples[i].time <= target) return _samples[i];
            return _samples.Count > 0 ? _samples[0] : default;
        }

        /// <summary>
        /// Standard deviation of speed over the last <paramref name="window"/> seconds.
        /// High variance = stop-start, shifty movement. This is the raw
        /// "unpredictability" signal the HesiSystem turns into an advantage.
        /// </summary>
        public float GetSpeedVariance(float window)
        {
            float cutoff = Time.time - window;
            float sum = 0f; int n = 0;
            for (int i = _samples.Count - 1; i >= 0 && _samples[i].time >= cutoff; i--) { sum += _samples[i].speed; n++; }
            if (n < 4) return 0f;
            float mean = sum / n;

            float sq = 0f;
            for (int i = _samples.Count - 1, c = 0; i >= 0 && c < n; i--, c++)
                sq += (_samples[i].speed - mean) * (_samples[i].speed - mean);
            return Mathf.Sqrt(sq / n);
        }

        /// <summary>Mean speed over the last <paramref name="window"/> seconds.</summary>
        public float GetAverageSpeed(float window)
        {
            float cutoff = Time.time - window;
            float sum = 0f; int n = 0;
            for (int i = _samples.Count - 1; i >= 0 && _samples[i].time >= cutoff; i--) { sum += _samples[i].speed; n++; }
            return n > 0 ? sum / n : 0f;
        }
    }
}
