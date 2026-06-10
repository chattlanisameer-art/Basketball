using UnityEngine;
using Hesi.Core;
using Hesi.Ball;
using Hesi.Player;

namespace Hesi.Core
{
    /// <summary>
    /// Streetball possession rules with zero UI:
    ///
    ///   • CHECK BALL — every possession starts with the ball-handler at the
    ///     check spot (top of the arc). Play goes live on the first dribble or
    ///     after a short grace, not on a menu button.
    ///   • TAKE-BACKS — after a steal or defensive rebound, the new ball-handler
    ///     must clear the ball beyond the arc before attacking ("take it back").
    ///   • OUT OF BOUNDS — last touch loses it; ball resets to the check spot.
    ///   • SCORES — loser's ball (configurable to make-it-take-it).
    ///
    /// For the MVP the AI's offense is stubbed: when the defender wins
    /// possession, after a beat the ball returns to the check spot and it's
    /// the player's ball again — the AI is a pure defender. The state flow is
    /// already two-sided so plugging in AI offense later is one method.
    /// </summary>
    public class PossessionManager : MonoBehaviour
    {
        [SerializeField] private BasketballPhysics ball;
        [SerializeField] private PlayerMotionTracker playerMotion;
        [SerializeField] private Transform checkBallSpot;
        [SerializeField] private Transform rimCenter;

        [Tooltip("Make-it-take-it (true) or loser's ball (false, classic park rules).")]
        [SerializeField] private bool makeItTakeIt = false;
        [SerializeField] private float arcDistance = 6.75f;
        [Tooltip("Court half-extents (m) for out-of-bounds. X = sideline, Z = baseline-to-halfcourt.")]
        [SerializeField] private Vector2 courtHalfExtents = new Vector2(7.6f, 5.5f);
        [SerializeField] private Vector3 courtCenter = Vector3.zero;

        public bool PlayerHasPossession { get; private set; } = true;
        public bool PlayIsLive { get; private set; }
        public bool NeedsTakeBack { get; private set; }

        private void Awake()
        {
            if (ball == null) ball = FindObjectOfType<BasketballPhysics>();
            if (playerMotion == null) playerMotion = FindObjectOfType<PlayerMotionTracker>();
        }

        private void OnEnable()
        {
            GameEvents.Scored += OnScored;
            GameEvents.BallStolen += OnBallStolen;
            GameEvents.DribbleBounce += OnFirstDribble;
            GameEvents.BallGrabbed += OnBallGrabbed;
        }

        private void OnDisable()
        {
            GameEvents.Scored -= OnScored;
            GameEvents.BallStolen -= OnBallStolen;
            GameEvents.DribbleBounce -= OnFirstDribble;
            GameEvents.BallGrabbed -= OnBallGrabbed;
        }

        private void Start() => ResetToCheckBall(playerBall: true);

        private void Update()
        {
            CheckOutOfBounds();
            CheckTakeBackCleared();
        }

        // ---- rules ----

        private void CheckOutOfBounds()
        {
            if (!PlayIsLive || ball.IsHeld) return;
            Vector3 p = ball.transform.position - courtCenter;
            if (Mathf.Abs(p.x) > courtHalfExtents.x || Mathf.Abs(p.z) > courtHalfExtents.y || p.y < -2f)
            {
                GameEvents.RaiseBallOutOfBounds(PlayerHasPossession);
                // Park rule: whoever touched it last loses it. MVP: ball-handler's
                // team touched it last unless a steal poke sent it out — the steal
                // event already flipped possession before it went out.
                ResetToCheckBall(playerBall: !PlayerHasPossession);
            }
        }

        private void CheckTakeBackCleared()
        {
            if (!NeedsTakeBack || !PlayerHasPossession) return;
            float dist = Vector2.Distance(
                new Vector2(playerMotion.FloorPosition.x, playerMotion.FloorPosition.z),
                new Vector2(rimCenter.position.x, rimCenter.position.z));
            if (dist >= arcDistance)
                NeedsTakeBack = false; // cleared — attack when ready
        }

        private void OnScored(int points, bool clean)
        {
            // A make while needing a take-back doesn't count — handled by
            // MatchManager checking ScoreIsValid before tallying.
            bool nextIsPlayer = makeItTakeIt ? PlayerHasPossession : !PlayerHasPossession;
            ResetToCheckBall(nextIsPlayer);
        }

        private void OnBallStolen()
        {
            PlayerHasPossession = false;
            GameEvents.RaisePossessionChanged(false);
            // MVP stub: AI doesn't attack. Brief pause, then player checks up again.
            Invoke(nameof(GiveBallBackToPlayer), 2.0f);
        }

        private void GiveBallBackToPlayer() => ResetToCheckBall(playerBall: true);

        private void OnFirstDribble(Vector3 p)
        {
            if (!PlayIsLive && PlayerHasPossession)
            {
                PlayIsLive = true;
                GameEvents.RaisePlayStarted();
            }
        }

        private void OnBallGrabbed(Transform hand)
        {
            // Grabbing at the check spot is the "check" — live on first dribble.
        }

        public bool ScoreIsValid => !NeedsTakeBack;

        private void ResetToCheckBall(bool playerBall)
        {
            CancelInvoke(nameof(GiveBallBackToPlayer));
            PlayerHasPossession = playerBall;
            PlayIsLive = false;
            NeedsTakeBack = false;
            ball.ResetTo(checkBallSpot.position + Vector3.up * 1.2f);
            GameEvents.RaisePossessionChanged(playerBall);
        }

        /// <summary>Called after a defensive sequence where the player recovers a live ball (future: rebounds).</summary>
        public void RequireTakeBack() => NeedsTakeBack = true;
    }
}
