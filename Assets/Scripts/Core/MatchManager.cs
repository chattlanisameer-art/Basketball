using UnityEngine;

namespace Hesi.Core
{
    /// <summary>
    /// Match flow: game to 11 by 1s and 2s, win by 2 (classic park rules).
    /// No scoreboard UI in your face — score is announced through audio
    /// (crowd "ooooh" on ankle-breakers, a call-out on game point) and an
    /// optional small painted scoreboard on the fence (diegetic, look at it
    /// if you want it). Hook those up via the serialized AudioSource.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        [SerializeField] private PossessionManager possession;
        [SerializeField] private int targetScore = 11;
        [SerializeField] private bool winByTwo = true;

        [Header("Diegetic feedback")]
        [SerializeField] private AudioSource announcer;
        [SerializeField] private AudioClip scoreClip;
        [SerializeField] private AudioClip gamePointClip;
        [SerializeField] private AudioClip gameOverClip;
        [SerializeField] private AudioClip ankleBreakerClip;

        public int PlayerScore { get; private set; }
        public int DefenderScore { get; private set; }
        public bool MatchOver { get; private set; }

        private void Awake()
        {
            if (possession == null) possession = FindObjectOfType<PossessionManager>();
        }

        private void OnEnable()
        {
            GameEvents.Scored += OnScored;
            GameEvents.DefenderFrozen += OnAnkleBreaker;
        }

        private void OnDisable()
        {
            GameEvents.Scored -= OnScored;
            GameEvents.DefenderFrozen -= OnAnkleBreaker;
        }

        private void OnScored(int points, bool clean)
        {
            if (MatchOver) return;
            if (possession != null && !possession.ScoreIsValid) return; // didn't take it back

            if (possession == null || possession.PlayerHasPossession)
                PlayerScore += points;
            else
                DefenderScore += points;

            Play(scoreClip);

            if (HasWon(PlayerScore, DefenderScore) || HasWon(DefenderScore, PlayerScore))
            {
                MatchOver = true;
                Play(gameOverClip);
                GameEvents.RaiseMatchEnded(PlayerScore > DefenderScore);
            }
            else if (PlayerScore == targetScore - 1 || DefenderScore == targetScore - 1)
            {
                Play(gamePointClip);
            }
        }

        private bool HasWon(int a, int b) =>
            a >= targetScore && (!winByTwo || a - b >= 2);

        private void OnAnkleBreaker() => Play(ankleBreakerClip);

        private void Play(AudioClip clip)
        {
            if (announcer != null && clip != null) announcer.PlayOneShot(clip);
        }

        public void Rematch()
        {
            PlayerScore = 0;
            DefenderScore = 0;
            MatchOver = false;
        }
    }
}
