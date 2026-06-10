using System;
using UnityEngine;

namespace Hesi.Core
{
    /// <summary>
    /// Static event bus. All cross-system game events flow through here so
    /// systems never need direct references to each other. This is the seam
    /// that later lets us swap local listeners for networked ones.
    /// </summary>
    public static class GameEvents
    {
        // ---- Ball / possession ----
        /// <summary>Ball was grabbed by the player. arg: hand transform.</summary>
        public static event Action<Transform> BallGrabbed;
        /// <summary>Ball left the player's hand (throw, pass, or shot). arg: release velocity.</summary>
        public static event Action<Vector3> BallReleased;
        /// <summary>A dribble bounce driven by the player's hand. arg: world contact point.</summary>
        public static event Action<Vector3> DribbleBounce;
        /// <summary>Defender got a hand on the ball.</summary>
        public static event Action BallStolen;
        /// <summary>Ball crossed a court boundary. arg: last touch was player (true) or defender.</summary>
        public static event Action<bool> BallOutOfBounds;

        // ---- Scoring ----
        /// <summary>A made basket. args: points (1 or 2), clean swish (no rim).</summary>
        public static event Action<int, bool> Scored;

        // ---- Shooting ----
        /// <summary>A shot was released. args: estimated quality 0..1, was contested.</summary>
        public static event Action<float, bool> ShotReleased;

        // ---- Hesi ----
        /// <summary>Player entered a hesitation (deceleration hold).</summary>
        public static event Action HesiStarted;
        /// <summary>Player burst out of a hesitation. arg: burst strength 0..1.</summary>
        public static event Action<float> HesiBurst;
        /// <summary>The defender got frozen badly enough to count as an ankle-break.</summary>
        public static event Action DefenderFrozen;

        // ---- Match flow ----
        /// <summary>Possession changed. arg: player has the ball (true) or defender.</summary>
        public static event Action<bool> PossessionChanged;
        /// <summary>Check-ball completed; live play begins.</summary>
        public static event Action PlayStarted;
        /// <summary>Match over. arg: player won.</summary>
        public static event Action<bool> MatchEnded;

        public static void RaiseBallGrabbed(Transform hand) => BallGrabbed?.Invoke(hand);
        public static void RaiseBallReleased(Vector3 v) => BallReleased?.Invoke(v);
        public static void RaiseDribbleBounce(Vector3 p) => DribbleBounce?.Invoke(p);
        public static void RaiseBallStolen() => BallStolen?.Invoke();
        public static void RaiseBallOutOfBounds(bool byPlayer) => BallOutOfBounds?.Invoke(byPlayer);
        public static void RaiseScored(int points, bool clean) => Scored?.Invoke(points, clean);
        public static void RaiseShotReleased(float quality, bool contested) => ShotReleased?.Invoke(quality, contested);
        public static void RaiseHesiStarted() => HesiStarted?.Invoke();
        public static void RaiseHesiBurst(float strength) => HesiBurst?.Invoke(strength);
        public static void RaiseDefenderFrozen() => DefenderFrozen?.Invoke();
        public static void RaisePossessionChanged(bool playerBall) => PossessionChanged?.Invoke(playerBall);
        public static void RaisePlayStarted() => PlayStarted?.Invoke();
        public static void RaiseMatchEnded(bool playerWon) => MatchEnded?.Invoke(playerWon);
    }
}
