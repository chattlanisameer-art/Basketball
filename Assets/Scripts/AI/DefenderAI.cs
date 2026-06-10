using UnityEngine;
using Hesi.Core;
using Hesi.Player;
using Hesi.Ball;

namespace Hesi.AI
{
    /// <summary>
    /// Reactive 1v1 on-ball defender. The key design decision: the defender
    /// never reads your CURRENT state. He reads your state from
    /// (baseReactionTime + HesiSystem.DefenderReactionPenalty) seconds ago,
    /// via PlayerMotionTracker.GetDelayedSample().
    ///
    /// That one line is the whole hesi mechanic from the defense's side:
    ///   • Move at constant speed → delayed-you ≈ current-you → he's glued to you.
    ///   • Hesitate (penalty builds) → his picture of you ages → you freeze him.
    ///   • Burst out of the hesi → for ~0.5s he's still guarding a ghost.
    ///
    /// States:
    ///   OnBall     — mirrors you, stays between you and the hoop, pokes at lazy dribbles
    ///   Frozen     — feet planted; triggered when your burst beats his stale read badly
    ///   Recovering — beaten, sprints back toward the hoop-side recovery point
    ///   Contesting — you're shooting range + slowing; he closes out, hand up
    ///
    /// Movement is kinematic (Rigidbody.MovePosition) — character physics
    /// fighting ball physics is jank; only his steal/contest hand has a collider
    /// on the Ball layer.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DefenderAI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMotionTracker playerMotion;
        [SerializeField] private HesiSystem hesi;
        [SerializeField] private BasketballPhysics ball;
        [SerializeField] private Transform rimCenter;
        [SerializeField] private Transform stealHand; // child with small trigger collider

        [Header("Reaction")]
        [Tooltip("Base reaction latency (s). Humans: ~0.2 elite, 0.3 average, 0.45 slow. This is the main difficulty dial.")]
        [SerializeField] private float baseReactionTime = 0.28f;

        [Header("Movement")]
        [SerializeField] private float lateralSpeed = 3.2f;
        [SerializeField] private float recoverySpeed = 4.2f;
        [Tooltip("Preferred gap (m) to the ball-handler.")]
        [SerializeField] private float guardDistance = 1.1f;

        [Header("Freeze / beat detection")]
        [Tooltip("Angle (deg) between where he THINKS you're going and where you ARE going that counts as 'beaten'.")]
        [SerializeField] private float beatAngle = 55f;
        [SerializeField] private float frozenDuration = 0.55f;

        [Header("Steal")]
        [Tooltip("Chance per second to poke at the ball when it's dribbled low & slow nearby.")]
        [SerializeField] private float stealAggression = 0.35f;
        [SerializeField] private float stealReach = 0.8f;

        public enum AIState { OnBall, Frozen, Recovering, Contesting, Idle }
        public AIState State { get; private set; } = AIState.Idle;

        private Rigidbody _body;
        private float _frozenUntil;
        private bool _playerHasBall;
        private Vector3 _stealHandRest;

        /// <summary>Current effective reaction latency, hesi penalty included.</summary>
        public float ReactionTime =>
            baseReactionTime + (hesi != null ? hesi.DefenderReactionPenalty : 0f);

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            if (playerMotion == null) playerMotion = FindObjectOfType<PlayerMotionTracker>();
            if (hesi == null) hesi = FindObjectOfType<HesiSystem>();
            if (ball == null) ball = FindObjectOfType<BasketballPhysics>();
            if (stealHand != null) _stealHandRest = stealHand.localPosition;
        }

        private void OnEnable()
        {
            GameEvents.PossessionChanged += OnPossessionChanged;
            GameEvents.DefenderFrozen += OnFrozen;
            GameEvents.PlayStarted += OnPlayStarted;
        }

        private void OnDisable()
        {
            GameEvents.PossessionChanged -= OnPossessionChanged;
            GameEvents.DefenderFrozen -= OnFrozen;
            GameEvents.PlayStarted -= OnPlayStarted;
        }

        private void FixedUpdate()
        {
            if (State == AIState.Idle || !_playerHasBall) return;

            // ★ The defender's entire worldview is this delayed snapshot.
            PlayerMotionTracker.MotionSample perceived = playerMotion.GetDelayedSample(ReactionTime);

            switch (State)
            {
                case AIState.OnBall: TickOnBall(perceived); break;
                case AIState.Frozen: TickFrozen(); break;
                case AIState.Recovering: TickRecovering(); break;
                case AIState.Contesting: TickContesting(perceived); break;
            }

            FacePlayer();
        }

        private void TickOnBall(PlayerMotionTracker.MotionSample perceived)
        {
            // Beaten check: compare his stale picture of your direction with reality.
            Vector3 actualVel = playerMotion.HorizontalVelocity;
            if (actualVel.magnitude > 2.0f && perceived.speed > 0.1f)
            {
                float surprise = Vector3.Angle(perceived.velocity, actualVel);
                if (surprise > beatAngle && IsPlayerPastMe())
                {
                    State = AIState.Recovering;
                    return;
                }
            }

            // Guard position: on the line from the PERCEIVED player to the rim.
            Vector3 toRim = (RimFloor() - perceived.position).normalized;
            Vector3 guardPos = perceived.position + toRim * guardDistance;
            MoveToward(guardPos, lateralSpeed);

            TrySteal();

            // Shot threat: player slowing down in scoring range → close out.
            float distToRim = Vector3.Distance(playerMotion.FloorPosition, RimFloor());
            if (perceived.speed < 0.6f && distToRim < 8.5f && ball.IsHeld)
                State = AIState.Contesting;
        }

        private void TickFrozen()
        {
            // Feet in cement. The penalty already slowed his perception; this is
            // the visible payoff of a clean ankle-breaker.
            if (Time.time >= _frozenUntil)
                State = AIState.Recovering;
        }

        private void TickRecovering()
        {
            // Sprint to the spot between the player and the rim, hoop-side.
            Vector3 recovery = Vector3.Lerp(RimFloor(), playerMotion.FloorPosition, 0.35f);
            MoveToward(recovery, recoverySpeed);
            if (Vector3.Distance(FloorPos(transform.position), recovery) < 0.4f)
                State = AIState.OnBall;
        }

        private void TickContesting(PlayerMotionTracker.MotionSample perceived)
        {
            // Close out under control, contest hand raised toward the ball.
            MoveToward(playerMotion.FloorPosition, lateralSpeed * 0.9f, guardDistance * 0.7f);
            if (stealHand != null)
                stealHand.position = Vector3.Lerp(stealHand.position,
                    ball.transform.position + Vector3.up * 0.1f, 8f * Time.fixedDeltaTime);

            // Player speeds back up → he drove out of the pump threat.
            if (perceived.speed > 1.4f)
            {
                State = AIState.OnBall;
                ResetStealHand();
            }
        }

        private void TrySteal()
        {
            if (stealHand == null || ball.IsHeld) return;

            // Pokes at a LOW, SLOW dribble within reach. High crossovers and
            // quick rhythm are safe; lazy pounding gets stripped — which pairs
            // with the over-dribble penalty making the ball stray closer to him.
            float dist = Vector3.Distance(stealHand.position, ball.transform.position);
            bool lowBall = ball.transform.position.y < 0.6f;
            bool slowBall = ball.Body.velocity.magnitude < 3.5f;
            if (dist < stealReach && lowBall && slowBall &&
                Random.value < stealAggression * Time.fixedDeltaTime)
            {
                // Lunge the hand at the ball; actual contact = steal.
                stealHand.position = Vector3.MoveTowards(stealHand.position,
                    ball.transform.position, 0.5f);
                if (Vector3.Distance(stealHand.position, ball.transform.position) < 0.15f)
                {
                    Vector3 knock = (ball.transform.position - transform.position).normalized + Vector3.up * 0.3f;
                    ball.Body.AddForce(knock * 3f, ForceMode.VelocityChange);
                    GameEvents.RaiseBallStolen();
                }
            }
            else
            {
                ResetStealHand();
            }
        }

        private void ResetStealHand()
        {
            if (stealHand != null)
                stealHand.localPosition = Vector3.Lerp(stealHand.localPosition, _stealHandRest, 6f * Time.fixedDeltaTime);
        }

        /// <summary>Distance from his contest hand to a shot point — used by ShootingSystem.</summary>
        public float DistanceToContest(Vector3 shotOrigin)
        {
            Vector3 hand = stealHand != null ? stealHand.position : transform.position + Vector3.up * 1.8f;
            return Vector3.Distance(hand, shotOrigin);
        }

        // ---- movement helpers ----
        private void MoveToward(Vector3 floorTarget, float speed, float stopDistance = 0.05f)
        {
            Vector3 current = FloorPos(transform.position);
            Vector3 to = floorTarget - current;
            if (to.magnitude <= stopDistance) return;
            Vector3 step = to.normalized * Mathf.Min(speed * Time.fixedDeltaTime, to.magnitude);
            _body.MovePosition(transform.position + step);
        }

        private void FacePlayer()
        {
            Vector3 look = playerMotion.FloorPosition - FloorPos(transform.position);
            if (look.sqrMagnitude > 0.01f)
                _body.MoveRotation(Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(look), 6f * Time.fixedDeltaTime));
        }

        private bool IsPlayerPastMe()
        {
            // Player is closer to the rim than I am, on my hoop side.
            return Vector3.Distance(playerMotion.FloorPosition, RimFloor()) <
                   Vector3.Distance(FloorPos(transform.position), RimFloor()) + 0.2f;
        }

        private Vector3 RimFloor() => FloorPos(rimCenter.position);
        private static Vector3 FloorPos(Vector3 p) => new Vector3(p.x, 0f, p.z);

        // ---- events ----
        private void OnPossessionChanged(bool playerBall)
        {
            _playerHasBall = playerBall;
            State = playerBall ? AIState.OnBall : AIState.Idle;
            ResetStealHand();
        }

        private void OnPlayStarted() => State = _playerHasBall ? AIState.OnBall : AIState.Idle;

        private void OnFrozen()
        {
            State = AIState.Frozen;
            _frozenUntil = Time.time + frozenDuration;
        }
    }
}
