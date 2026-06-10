using UnityEngine;
using Hesi.Core;
using Hesi.Ball;

namespace Hesi.Player
{
    /// <summary>
    /// Real contact-bounce dribbling. No "press to dribble" — your open hand
    /// pushing down on the ball is the dribble:
    ///
    ///   1. Hand is OPEN (low grip), near the top hemisphere of the ball,
    ///      moving downward → we apply a push impulse proportional to hand speed.
    ///   2. Ball bounces off the asphalt with real physics.
    ///   3. On the way up, if it reaches your hand again, contact slows it and
    ///      a slight magnetism helps it settle into your palm — this models the
    ///      finger control a real handler has, tuned so it assists but never
    ///      takes over.
    ///
    /// The HesiSystem's ControlPenalty (over-dribbling) degrades both the push
    /// accuracy and the catch magnetism — pound the rock too long and it starts
    /// wandering off line, exactly like a tired handle.
    ///
    /// Attach one to each hand anchor (same object as VRHandTracker).
    /// </summary>
    [RequireComponent(typeof(VRHandTracker))]
    public class DribbleController : MonoBehaviour
    {
        [SerializeField] private BasketballPhysics ball;
        [SerializeField] private HesiSystem hesi;

        [Header("Push (downward dribble)")]
        [Tooltip("Hand must be within this distance (m) of the ball surface to influence it.")]
        [SerializeField] private float contactRange = 0.06f;
        [Tooltip("Minimum downward hand speed (m/s) to count as a dribble push.")]
        [SerializeField] private float minPushSpeed = 0.5f;
        [Tooltip("Multiplier from hand velocity to ball impulse velocity.")]
        [SerializeField] private float pushTransfer = 1.25f;
        [Tooltip("Open hand = grip below this.")]
        [SerializeField] private float openHandThreshold = 0.4f;
        [SerializeField] private float pushCooldown = 0.15f;

        [Header("Catch assist (ball returning to hand)")]
        [Tooltip("Radius (m) around the palm where the rising ball gets guided in.")]
        [SerializeField] private float catchAssistRange = 0.18f;
        [Tooltip("Strength of the guidance acceleration (m/s²).")]
        [SerializeField] private float catchAssistForce = 18f;

        private VRHandTracker _hand;
        private float _lastPushTime;

        private void Awake()
        {
            _hand = GetComponent<VRHandTracker>();
            if (ball == null) ball = FindObjectOfType<BasketballPhysics>();
            if (hesi == null) hesi = FindObjectOfType<HesiSystem>();
        }

        private void FixedUpdate()
        {
            if (ball == null || ball.IsHeld || !_hand.IsTracked) return;
            if (_hand.GripAmount > openHandThreshold) return; // closed hand = grabbing, not dribbling

            float control = 1f - (hesi != null ? hesi.ControlPenalty : 0f);

            TryPush(control);
            ApplyCatchAssist(control);
        }

        private void TryPush(float control)
        {
            if (Time.time - _lastPushTime < pushCooldown) return;

            SphereCollider col = ball.Body.GetComponent<SphereCollider>();
            float distToSurface = Vector3.Distance(_hand.PalmPosition, ball.transform.position) - col.radius;
            if (distToSurface > contactRange) return;

            // Must be touching the TOP of the ball and pushing down.
            bool onTop = _hand.PalmPosition.y > ball.transform.position.y;
            float downSpeed = -_hand.InstantVelocity.y;
            if (!onTop || downSpeed < minPushSpeed) return;

            // Push direction = hand velocity, but with degraded control the
            // lateral component gets noisy (the ball squirts off line).
            Vector3 push = _hand.InstantVelocity * pushTransfer;
            if (control < 1f)
            {
                Vector3 noise = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                push += noise * (1f - control) * downSpeed * 0.6f;
            }
            // Floor the downward speed so soft pushes still produce a usable bounce.
            push.y = Mathf.Min(push.y, -Mathf.Max(downSpeed * pushTransfer, 2.2f));

            ball.Body.velocity = push;
            _lastPushTime = Time.time;
            GameEvents.RaiseDribbleBounce(ball.transform.position);
        }

        private void ApplyCatchAssist(float control)
        {
            // Only assist a RISING ball below the palm — never yank a shot or pass.
            if (ball.Body.velocity.y <= 0.2f) return;

            Vector3 toHand = _hand.PalmPosition - ball.transform.position;
            if (toHand.magnitude > catchAssistRange || toHand.y < 0f) return;

            // Steer the lateral velocity toward the palm; control penalty
            // weakens the assist so an over-dribbled ball gets away from you.
            Vector3 lateralCorrection = new Vector3(toHand.x, 0f, toHand.z).normalized;
            ball.Body.AddForce(lateralCorrection * catchAssistForce * control, ForceMode.Acceleration);

            // Slight damping near the palm so it doesn't slap through your hand.
            if (toHand.magnitude < catchAssistRange * 0.5f)
                ball.Body.velocity *= 1f - 4f * Time.fixedDeltaTime;
        }
    }
}
