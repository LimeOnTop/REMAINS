using UnityEngine;

namespace Remains
{
    /// <summary>
    /// Shows its target only during selected match phases (e.g. the attackers' barrier before the assault).
    /// </summary>
    public class PhaseVisibility : MonoBehaviour
    {
        [SerializeField] private GameObject target;
        [SerializeField] private bool visibleInLobby = true;
        [SerializeField] private bool visibleInBuild = true;
        [SerializeField] private bool visibleInAssault;
        [SerializeField] private bool visibleInResults;

        private void OnEnable()
        {
            MatchManager.PhaseChanged += HandlePhaseChanged;
            Apply(MatchManager.Instance != null ? MatchManager.Instance.Phase : MatchPhase.Lobby);
        }

        private void OnDisable()
        {
            MatchManager.PhaseChanged -= HandlePhaseChanged;
        }

        private void HandlePhaseChanged(MatchPhase previous, MatchPhase current) => Apply(current);

        private void Apply(MatchPhase phase)
        {
            if (target == null) return;

            bool visible = phase switch
            {
                MatchPhase.Lobby => visibleInLobby,
                MatchPhase.Build => visibleInBuild,
                MatchPhase.Assault => visibleInAssault,
                _ => visibleInResults
            };
            target.SetActive(visible);
        }
    }
}
