using UnityEngine;

namespace Hesi.Core
{
    /// <summary>
    /// Bootstrap. Lives on the _Systems object in Court.unity along with
    /// MatchManager, PossessionManager, HesiSystem, MomentumSystem and
    /// PlayerMotionTracker. Sets global physics/runtime config that must be
    /// guaranteed in builds regardless of editor settings, and is the single
    /// place future scene-flow logic (rematch, park select) will hang off.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Tooltip("Physics rate. 200 Hz keeps a fast ball from tunneling through hands and rim.")]
        [SerializeField] private float fixedTimestep = 0.005f;
        [SerializeField] private int targetDisplayRefreshRate = 90;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Time.fixedDeltaTime = fixedTimestep;
            // On Pico, the actual refresh rate is requested through the OpenXR
            // display subsystem; Application.targetFrameRate is a no-op in VR
            // but harmless, and correct when testing flat in-editor.
            Application.targetFrameRate = targetDisplayRefreshRate;
        }

        private void OnEnable() => GameEvents.MatchEnded += OnMatchEnded;
        private void OnDisable() => GameEvents.MatchEnded -= OnMatchEnded;

        private void OnMatchEnded(bool playerWon)
        {
            // MVP: stand at the check spot, grab the ball, and a new game runs
            // back automatically after a short cooldown.
            Invoke(nameof(RunItBack), 4f);
        }

        private void RunItBack()
        {
            var match = FindObjectOfType<MatchManager>();
            if (match != null) match.Rematch();
        }
    }
}
