using UnityEngine;
using UnityEngine.UIElements;

namespace Remains
{
    /// <summary>
    /// Match HUD built in code on a UIDocument (Docs/GDD.md, section 10): phase and timer, respawn pool,
    /// defenders alive, team badge, carry/nail hints, structure health and round results.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MatchHUD : MonoBehaviour
    {
        private UIDocument m_Document;
        private VisualElement m_Root;
        private Label m_Phase;
        private Label m_Timer;
        private Label m_Counters;
        private Label m_Score;
        private Label m_TeamBadge;
        private Label m_Hint;
        private VisualElement m_ProgressTrack;
        private VisualElement m_ProgressFill;
        private Label m_Status;
        private Label m_Banner;
        private Label m_PieceLabel;
        private VisualElement m_PieceTrack;
        private VisualElement m_PieceFill;
        private Label m_Controls;
        private VisualElement m_ObjectivePanel;
        private Label m_ObjectiveLabel;
        private VisualElement m_ObjectiveFill;
        private Label m_Marker;

        private BuildPiece m_LastPiece;
        private float m_PieceShownUntil;
        private float m_PhaseChangedAt = -10f;

        private void OnEnable()
        {
            m_Document = GetComponent<UIDocument>();
            Build();
            TeamMember.LocalStatusChanged += HandleStatusChanged;
            BuildPiece.AnyPieceDamaged += HandlePieceDamaged;
            MatchManager.PhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            TeamMember.LocalStatusChanged -= HandleStatusChanged;
            BuildPiece.AnyPieceDamaged -= HandlePieceDamaged;
            MatchManager.PhaseChanged -= HandlePhaseChanged;
        }

        #region Layout

        private void Build()
        {
            m_Root = m_Document.rootVisualElement;
            m_Root.Clear();
            m_Root.pickingMode = PickingMode.Ignore;
            m_Root.style.position = Position.Absolute;
            m_Root.style.left = 0;
            m_Root.style.right = 0;
            m_Root.style.top = 0;
            m_Root.style.bottom = 0;

            // Top centre: phase, timer, counters.
            var top = Panel(m_Root);
            top.style.position = Position.Absolute;
            top.style.top = 16;
            top.style.left = Length.Percent(50);
            top.style.translate = new Translate(Length.Percent(-50), 0);
            top.style.alignItems = Align.Center;
            top.style.paddingLeft = 24;
            top.style.paddingRight = 24;

            m_Phase = Text(top, 16, FontStyle.Bold);
            m_Timer = Text(top, 38, FontStyle.Bold);
            m_Counters = Text(top, 15, FontStyle.Normal);
            m_Score = Text(top, 13, FontStyle.Normal);
            m_Score.style.opacity = 0.8f;

            // Objective status under the timer.
            m_ObjectivePanel = new VisualElement { pickingMode = PickingMode.Ignore };
            m_ObjectivePanel.style.alignItems = Align.Center;
            m_ObjectivePanel.style.marginTop = 4;
            top.Add(m_ObjectivePanel);
            m_ObjectiveLabel = Text(m_ObjectivePanel, 14, FontStyle.Bold);
            Bar(m_ObjectivePanel, 260, 10, out m_ObjectiveFill, new Color(1f, 0.78f, 0.25f));

            // World marker over the objective.
            m_Marker = Text(m_Root, 14, FontStyle.Bold);
            m_Marker.style.position = Position.Absolute;
            m_Marker.style.color = new Color(1f, 0.85f, 0.35f);
            m_Marker.style.unityTextOutlineColor = Color.black;
            m_Marker.style.unityTextOutlineWidth = 0.2f;
            m_Marker.style.unityTextAlign = TextAnchor.MiddleCenter;

            // Top left: team badge.
            m_TeamBadge = Text(m_Root, 16, FontStyle.Bold);
            m_TeamBadge.style.position = Position.Absolute;
            m_TeamBadge.style.top = 16;
            m_TeamBadge.style.left = 16;
            m_TeamBadge.style.paddingLeft = 12;
            m_TeamBadge.style.paddingRight = 12;
            m_TeamBadge.style.paddingTop = 6;
            m_TeamBadge.style.paddingBottom = 6;
            SetRadius(m_TeamBadge, 6);

            // Centre: interaction hint + progress under the crosshair.
            var center = new VisualElement { pickingMode = PickingMode.Ignore };
            center.style.position = Position.Absolute;
            center.style.top = Length.Percent(56);
            center.style.left = Length.Percent(50);
            center.style.translate = new Translate(Length.Percent(-50), 0);
            center.style.alignItems = Align.Center;
            m_Root.Add(center);

            m_Hint = Text(center, 15, FontStyle.Bold);
            m_Hint.style.unityTextOutlineColor = new Color(0, 0, 0, 0.8f);
            m_Hint.style.unityTextOutlineWidth = 0.15f;

            m_ProgressTrack = Bar(center, 180, 8, out m_ProgressFill, new Color(1f, 0.85f, 0.3f));

            m_PieceLabel = Text(center, 13, FontStyle.Normal);
            m_PieceLabel.style.marginTop = 10;
            m_PieceTrack = Bar(center, 160, 6, out m_PieceFill, new Color(0.9f, 0.3f, 0.25f));

            // Status (respawn countdown, warnings) and round banner.
            m_Status = Text(m_Root, 22, FontStyle.Bold);
            m_Status.style.position = Position.Absolute;
            m_Status.style.top = Length.Percent(30);
            m_Status.style.left = 0;
            m_Status.style.right = 0;
            m_Status.style.unityTextAlign = TextAnchor.MiddleCenter;
            m_Status.style.unityTextOutlineColor = Color.black;
            m_Status.style.unityTextOutlineWidth = 0.2f;

            m_Banner = Text(m_Root, 48, FontStyle.Bold);
            m_Banner.style.position = Position.Absolute;
            m_Banner.style.top = Length.Percent(18);
            m_Banner.style.left = 0;
            m_Banner.style.right = 0;
            m_Banner.style.unityTextAlign = TextAnchor.MiddleCenter;
            m_Banner.style.unityTextOutlineColor = Color.black;
            m_Banner.style.unityTextOutlineWidth = 0.25f;

            // Bottom left: controls reminder.
            m_Controls = Text(m_Root, 12, FontStyle.Normal);
            m_Controls.style.position = Position.Absolute;
            m_Controls.style.left = 16;
            m_Controls.style.bottom = 150;
            m_Controls.style.opacity = 0.85f;
            m_Controls.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
            m_Controls.style.paddingLeft = 10;
            m_Controls.style.paddingRight = 10;
            m_Controls.style.paddingTop = 6;
            m_Controls.style.paddingBottom = 6;
            SetRadius(m_Controls, 6);
        }

        private static VisualElement Panel(VisualElement parent)
        {
            var panel = new VisualElement { pickingMode = PickingMode.Ignore };
            panel.style.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.55f);
            panel.style.paddingTop = 6;
            panel.style.paddingBottom = 8;
            SetRadius(panel, 10);
            parent.Add(panel);
            return panel;
        }

        private static Label Text(VisualElement parent, int size, FontStyle style)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = style;
            label.style.color = Color.white;
            label.style.marginTop = 0;
            label.style.marginBottom = 0;
            parent.Add(label);
            return label;
        }

        private static VisualElement Bar(VisualElement parent, float width, float height, out VisualElement fill, Color color)
        {
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.style.width = width;
            track.style.height = height;
            track.style.marginTop = 4;
            track.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            SetRadius(track, height * 0.5f);

            fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.style.height = Length.Percent(100);
            fill.style.width = Length.Percent(0);
            fill.style.backgroundColor = color;
            SetRadius(fill, height * 0.5f);
            track.Add(fill);

            parent.Add(track);
            return track;
        }

        private static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        private static void Show(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #endregion

        #region Updates

        private void HandleStatusChanged(string status)
        {
            if (m_Status == null) return;
            m_Status.text = status;
            Show(m_Status, !string.IsNullOrEmpty(status));
        }

        private void HandlePhaseChanged(MatchPhase previous, MatchPhase current)
        {
            if (previous != current) m_PhaseChangedAt = Time.time;
        }

        private void HandlePieceDamaged(BuildPiece piece, float damage)
        {
            m_LastPiece = piece;
            m_PieceShownUntil = Time.time + 2.5f;
        }

        private void LateUpdate()
        {
            if (m_Root == null) return;
            UpdateInteraction();
        }

        private void Update()
        {
            if (m_Root == null) return;

            var match = MatchManager.Instance;
            bool inMatch = match != null && match.IsSpawned;
            m_Root.style.display = inMatch ? DisplayStyle.Flex : DisplayStyle.None;
            if (!inMatch) return;

            UpdateTop(match);
            UpdateTeamBadge(match);
            UpdatePiece();
            UpdateBanner(match);
            UpdateObjective(match);
            UpdateControls(match);
        }

        private void UpdateTop(MatchManager match)
        {
            m_Phase.text = TeamInfo.GetPhaseName(match.Phase);
            float remaining = match.TimeRemaining;
            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);

            switch (match.Phase)
            {
                case MatchPhase.Lobby:
                    m_Timer.text = match.IsLobbyCountdownRunning ? $"{Mathf.CeilToInt(remaining)}" : "--";
                    m_Counters.text = match.IsLobbyCountdownRunning
                        ? $"Игроков: {match.PlayerCount} · старт скоро"
                        : $"Игроков: {match.PlayerCount} / нужно {match.Config.minPlayersToStart}";
                    break;
                case MatchPhase.Build:
                case MatchPhase.Assault:
                    m_Timer.text = $"{minutes}:{seconds:00}";
                    m_Counters.text = $"Возрождений атаки: {match.AttackerTickets}   ·   Защитников: {match.DefendersAlive}/{match.DefendersTotal}";
                    break;
                default:
                    m_Timer.text = $"{Mathf.CeilToInt(remaining)}";
                    m_Counters.text = $"Защитников: {match.DefendersAlive}/{match.DefendersTotal}";
                    break;
            }

            bool urgent = match.Phase == MatchPhase.Build && remaining <= 10f;
            m_Timer.style.color = urgent ? new Color(1f, 0.45f, 0.3f) : Color.white;

            match.GetScores(out int ours, out int theirs);
            m_Score.text = $"Раунд {match.Round + 1}   ·   счёт {ours} : {theirs}";
        }

        private void UpdateTeamBadge(MatchManager match)
        {
            var team = match.LocalTeam;
            string badge = team == Team.None ? "НАБЛЮДАТЕЛЬ" : $"ВЫ — {TeamInfo.GetName(team)}";
            var kit = PlayerLoadout.Local != null ? PlayerLoadout.Local.Kit : null;
            if (kit != null && team != Team.None) badge += $" · {kit.kitName}";
            var voice = TeamVoiceChat.Instance;
            if (voice != null)
            {
                if (!string.IsNullOrEmpty(voice.TransmittingTo)) badge += $"   ● ЭФИР: {voice.TransmittingTo}";
                else if (!string.IsNullOrEmpty(voice.StatusText)) badge += $"   ({voice.StatusText})";
            }
            m_TeamBadge.text = badge;
            var color = TeamInfo.GetColor(team);
            color.a = 0.85f;
            m_TeamBadge.style.backgroundColor = color;
        }

        private void UpdateInteraction()
        {
            string hint = HudHint.Text;
            float progress = HudHint.Progress;
            HudHint.Clear();

            m_Hint.text = hint;
            Show(m_Hint, !string.IsNullOrEmpty(hint));
            Show(m_ProgressTrack, progress > 0.001f && progress < 0.999f);
            m_ProgressFill.style.width = Length.Percent(progress * 100f);
        }

        private void UpdatePiece()
        {
            bool show = m_LastPiece != null && m_LastPiece.IsSpawned && Time.time < m_PieceShownUntil;
            Show(m_PieceLabel, show);
            Show(m_PieceTrack, show);
            if (!show) return;

            var grabbable = m_LastPiece.GetComponent<Grabbable>();
            string name = grabbable != null ? grabbable.DisplayName : m_LastPiece.name;
            m_PieceLabel.text = $"{name}: {Mathf.CeilToInt(m_LastPiece.Health)}/{Mathf.CeilToInt(m_LastPiece.MaxHealth)}";
            m_PieceFill.style.width = Length.Percent(m_LastPiece.HealthFraction * 100f);
        }

        private void UpdateObjective(MatchManager match)
        {
            var objective = CaptureObjective.Instance;
            bool show = objective != null && objective.IsSpawned && match.Phase != MatchPhase.Lobby;
            Show(m_ObjectivePanel, show);
            if (!show)
            {
                Show(m_Marker, false);
                return;
            }

            string state;
            Color color = new Color(1f, 0.78f, 0.25f);
            if (objective.IsCaptured) state = objective.CapturedText;
            else if (objective.IsContested) state = "ЗАХВАТ ОСПАРИВАЕТСЯ";
            else if (objective.IsBeingCaptured) { state = $"ЗАХВАТ {Mathf.FloorToInt(objective.Progress * 100f)}%"; color = TeamInfo.AttackerColor; }
            else if (objective.Progress > 0f) state = $"захвачено {Mathf.FloorToInt(objective.Progress * 100f)}%";
            else state = match.LocalTeam == Team.Defenders ? "под защитой" : "захватите в тронном зале";

            string overtime = match.IsOvertime ? "  ·  ОВЕРТАЙМ" : string.Empty;
            m_ObjectiveLabel.text = $"{objective.DisplayName.ToUpperInvariant()}: {state}{overtime}";
            m_ObjectiveFill.style.width = Length.Percent(objective.Progress * 100f);
            m_ObjectiveFill.style.backgroundColor = color;

            // Marker: projected into the panel; hidden when behind the camera.
            var camera = Camera.main;
            if (camera == null || m_Root.panel == null)
            {
                Show(m_Marker, false);
                return;
            }
            Vector3 world = objective.transform.position + Vector3.up * 2.6f;
            Vector3 screen = camera.WorldToScreenPoint(world);
            bool visible = screen.z > 0f;
            Show(m_Marker, visible);
            if (!visible) return;

            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(m_Root.panel, new Vector2(screen.x, Screen.height - screen.y));
            float distance = Vector3.Distance(camera.transform.position, objective.transform.position);
            m_Marker.text = $"▼ {objective.DisplayName} · {Mathf.RoundToInt(distance)} м";
            m_Marker.style.left = panelPoint.x - 90f;
            m_Marker.style.top = panelPoint.y - 12f;
            m_Marker.style.width = 180f;
            m_Marker.style.color = objective.IsBeingCaptured && Mathf.Repeat(Time.time, 0.6f) > 0.3f ? TeamInfo.AttackerColor : new Color(1f, 0.85f, 0.35f);
        }

        private void UpdateBanner(MatchManager match)
        {
            string banner = string.Empty;
            Color color = Color.white;

            if (match.Phase == MatchPhase.Results)
            {
                var winner = match.LastWinner;
                bool weWon = winner == match.LocalTeam;
                var objective = CaptureObjective.Instance;
                banner = match.LastReason == RoundEndReason.ObjectiveCaptured && objective != null ? objective.CapturedText
                    : winner == Team.Defenders ? "ЗАМОК ВЫСТОЯЛ" : "ЗАМОК ПАЛ";
                banner += weWon ? "\nПОБЕДА" : "\nПОРАЖЕНИЕ";
                color = TeamInfo.GetColor(winner);
            }
            else if (match.Phase == MatchPhase.Assault && CaptureObjective.Instance != null && CaptureObjective.Instance.IsBeingCaptured && match.LocalTeam == Team.Defenders)
            {
                banner = Mathf.Repeat(Time.time, 1f) > 0.35f ? CaptureObjective.Instance.UnderAttackText : string.Empty;
                color = TeamInfo.AttackerColor;
            }
            else if (match.Phase == MatchPhase.Assault && Time.time - m_PhaseChangedAt < 3f)
            {
                banner = "ШТУРМ!";
                color = TeamInfo.AttackerColor;
            }
            else if (match.Phase == MatchPhase.Build && Time.time - m_PhaseChangedAt < 3f)
            {
                banner = match.LocalTeam == Team.Defenders ? "УКРЕПЛЯЙТЕ ЗАМОК!" : "ПЛАНИРУЙТЕ ШТУРМ!";
                color = TeamInfo.GetColor(match.LocalTeam);
            }

            m_Banner.text = banner;
            m_Banner.style.color = color;
            Show(m_Banner, !string.IsNullOrEmpty(banner));
        }

        private void UpdateControls(MatchManager match)
        {
            string text;
            var team = match.LocalTeam;
            if (match.Phase == MatchPhase.Build && team == Team.Defenders)
            {
                text = "Укрепляйте замок: E — взять предмет, ПКМ — прибить баррикаду\nF — чинить стены и ворота · займите стены и башни";
            }
            else if (match.Phase == MatchPhase.Build && team == Team.Attackers)
            {
                text = "Планируйте штурм в шатрах: стол с картой — ЛКМ рисовать план\nОружейник (E) — выбор комплекта · пушки, ядра и тараны — у лагеря";
            }
            else if (match.Phase == MatchPhase.Assault && team == Team.Attackers)
            {
                text = "Пробейте стены: пушки (F — встать, ядро поднести к стволу), таран (E),\nзаряды сапёров · уничтожьте всех защитников";
            }
            else if (match.Phase == MatchPhase.Assault && team == Team.Defenders)
            {
                text = "Держите замок! Возрождений нет\nF — заложить пролом · E + ПКМ — забаррикадировать дыру";
            }
            else if (match.Phase == MatchPhase.Lobby)
            {
                text = "Разминка: всё можно трогать — пушки, тараны, ящики, оружейник в шатрах";
            }
            else
            {
                text = string.Empty;
            }

            m_Controls.text = text;
            Show(m_Controls, !string.IsNullOrEmpty(text));
        }

        #endregion
    }
}
