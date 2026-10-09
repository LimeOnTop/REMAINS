using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Remains
{
    /// <summary>
    /// Vivox voice chat (Docs/GDD.md, section 9): a team channel (hold V) and a positional proximity channel
    /// everybody nearby hears, enemies included (hold B). The team channel is re-joined whenever roles swap.
    /// Requires the project to be linked to Unity Cloud with Vivox enabled; otherwise it disables itself.
    /// </summary>
    public class TeamVoiceChat : MonoBehaviour
    {
        public enum VoiceState
        {
            Offline,
            Connecting,
            Ready,
            Unavailable
        }

        public static TeamVoiceChat Instance { get; private set; }

        [SerializeField] private Key teamPushToTalk = Key.V;
        [SerializeField] private Key proximityPushToTalk = Key.B;
        [SerializeField] private int proximityAudibleDistance = 25;
        [SerializeField] private int proximityConversationalDistance = 6;

        private string m_TeamChannel;
        private string m_ProximityChannel;
        private bool m_NeedsSync;
        private bool m_Busy;
        private float m_PositionTimer;
        private bool m_Transmitting;

        public VoiceState State { get; private set; } = VoiceState.Offline;
        public string StatusText { get; private set; } = string.Empty;
        /// <summary>"КОМАНДА", "РЯДОМ" or empty when not transmitting.</summary>
        public string TransmittingTo { get; private set; } = string.Empty;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            MatchManager.TeamsChanged += HandleTeamsChanged;
        }

        private void OnDisable()
        {
            MatchManager.TeamsChanged -= HandleTeamsChanged;
        }

        private async void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (State != VoiceState.Ready) return;
            try
            {
                await VivoxService.Instance.LeaveAllChannelsAsync();
                await VivoxService.Instance.LogoutAsync();
            }
            catch (Exception)
            {
                // Shutting down; nothing useful to do.
            }
        }

        private void Update()
        {
            var match = MatchManager.Instance;
            if (match == null || !match.IsSpawned || TeamMember.Local == null)
            {
                return;
            }

            if (State == VoiceState.Offline && !m_Busy)
            {
                _ = ConnectAsync();
                return;
            }

            if (State != VoiceState.Ready) return;

            if (m_NeedsSync && !m_Busy)
            {
                m_NeedsSync = false;
                _ = SyncChannelsAsync();
            }

            UpdatePushToTalk();

            m_PositionTimer -= Time.deltaTime;
            if (m_PositionTimer <= 0f && !string.IsNullOrEmpty(m_ProximityChannel) && IsJoined(m_ProximityChannel))
            {
                m_PositionTimer = 0.25f;
                var camera = Camera.main;
                if (camera != null)
                {
                    Vector3 speaker = TeamMember.Local.transform.position + Vector3.up * 1.6f;
                    VivoxService.Instance.Set3DPosition(speaker, camera.transform.position, camera.transform.forward, camera.transform.up, m_ProximityChannel);
                }
            }
        }

        private void HandleTeamsChanged()
        {
            m_NeedsSync = true;
        }

        private async Task ConnectAsync()
        {
            m_Busy = true;
            State = VoiceState.Connecting;
            StatusText = "Голосовой чат: подключение...";

            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                }
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                await VivoxService.Instance.InitializeAsync();
                if (!VivoxService.Instance.IsLoggedIn)
                {
                    await VivoxService.Instance.LoginAsync(new LoginOptions
                    {
                        DisplayName = TeamMember.Local != null && TeamMember.Local.Player != null ? TeamMember.Local.Player.PlayerName : "Player",
                        ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.FivePerSecond
                    });
                }

                VivoxService.Instance.MuteInputDevice();
                State = VoiceState.Ready;
                StatusText = string.Empty;
            }
            catch (Exception exception)
            {
                State = VoiceState.Unavailable;
                StatusText = "Голосовой чат недоступен";
                Debug.LogWarning($"[TeamVoiceChat] Voice chat disabled: {exception.Message}\n" +
                                 "Link the project to Unity Cloud (Project Settings > Services) and enable Vivox in the Unity Cloud Dashboard.");
            }
            finally
            {
                m_Busy = false;
            }

            if (State == VoiceState.Ready)
            {
                m_NeedsSync = false;
                await SyncChannelsAsync();
            }
        }

        private async Task SyncChannelsAsync()
        {
            var match = MatchManager.Instance;
            if (match == null || string.IsNullOrEmpty(match.MatchId)) return;

            m_Busy = true;
            try
            {
                string prefix = $"remains_{match.MatchId}";
                var team = match.LocalTeam;
                string teamChannel = team == Team.None ? null : $"{prefix}_{(int)team}_{match.Round}";

                if (m_TeamChannel != teamChannel)
                {
                    if (!string.IsNullOrEmpty(m_TeamChannel) && IsJoined(m_TeamChannel))
                    {
                        await VivoxService.Instance.LeaveChannelAsync(m_TeamChannel);
                    }
                    m_TeamChannel = null;

                    if (teamChannel != null)
                    {
                        await VivoxService.Instance.JoinGroupChannelAsync(teamChannel, ChatCapability.AudioOnly);
                        m_TeamChannel = teamChannel;
                    }
                }

                string proximityChannel = $"{prefix}_prox";
                if (m_ProximityChannel != proximityChannel)
                {
                    var properties = new Channel3DProperties(proximityAudibleDistance, proximityConversationalDistance, 1f, AudioFadeModel.InverseByDistance);
                    await VivoxService.Instance.JoinPositionalChannelAsync(proximityChannel, ChatCapability.AudioOnly, properties);
                    m_ProximityChannel = proximityChannel;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TeamVoiceChat] Failed to join voice channels: {exception.Message}");
            }
            finally
            {
                m_Busy = false;
            }
        }

        private static bool IsJoined(string channel)
        {
            return VivoxService.Instance.ActiveChannels != null && VivoxService.Instance.ActiveChannels.ContainsKey(channel);
        }

        private void UpdatePushToTalk()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool alive = TeamMember.Local.Player != null && TeamMember.Local.Player.PlayerState != null && TeamMember.Local.Player.PlayerState.IsActive;
            string target = null;
            string label = string.Empty;

            if (keyboard[teamPushToTalk].isPressed && !string.IsNullOrEmpty(m_TeamChannel))
            {
                target = m_TeamChannel;
                label = "КОМАНДА";
            }
            else if (keyboard[proximityPushToTalk].isPressed && alive && !string.IsNullOrEmpty(m_ProximityChannel))
            {
                target = m_ProximityChannel;
                label = "РЯДОМ";
            }

            bool transmit = target != null;
            if (transmit == m_Transmitting && label == TransmittingTo) return;

            m_Transmitting = transmit;
            TransmittingTo = label;

            if (transmit)
            {
                _ = VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, target);
                VivoxService.Instance.UnmuteInputDevice();
            }
            else
            {
                VivoxService.Instance.MuteInputDevice();
            }
        }
    }
}
