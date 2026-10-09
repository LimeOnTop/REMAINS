using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Remains
{
    /// <summary>
    /// The armorer's kit selection screen (UI Toolkit, built in code). Opening it frees the cursor and freezes the
    /// player; picking a kit applies it immediately.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LoadoutMenu : MonoBehaviour
    {
        public static LoadoutMenu Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.m_Open;

        private static readonly Dictionary<string, string> s_WeaponNames = new Dictionary<string, string>
        {
            { "Weapon_AssaultRifle", "Автомат" },
            { "Weapon_HandGun", "Пистолет" },
            { "Weapon_Shotgun", "Дробовик" },
            { "Weapon_GrenadeLauncher", "Гранатомёт" }
        };

        private UIDocument m_Document;
        private VisualElement m_Root;
        private VisualElement m_List;
        private Label m_Title;
        private Label m_Message;
        private bool m_Open;
        private string m_Speaker = "Оружейник";
        private int m_OpenedFrame;

        private void Awake()
        {
            Instance = this;
            m_Document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            Build();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!m_Open || Time.frameCount == m_OpenedFrame) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            bool close = keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame);
            bool dead = TeamMember.Local == null || TeamMember.Local.Player == null || TeamMember.Local.Player.PlayerState == null || !TeamMember.Local.Player.PlayerState.IsActive;
            if (close || dead) Close();
        }

        public void Open(string speaker)
        {
            if (PlayerLoadout.Local == null) return;
            m_Speaker = speaker;
            m_Open = true;
            m_OpenedFrame = Time.frameCount;
            m_Message.text = string.Empty;
            Refresh();
            SetVisible(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            LocalPlayerLocks.SetLook(this, true);
            LocalPlayerLocks.SetMovement(this, true);
            LocalPlayerLocks.SetWeapon(this, true);
        }

        public void Close()
        {
            if (!m_Open) return;
            m_Open = false;
            SetVisible(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            LocalPlayerLocks.ReleaseAll(this);
        }

        private void SetVisible(bool visible)
        {
            if (m_Root != null) m_Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #region Layout

        private void Build()
        {
            var root = m_Document.rootVisualElement;
            root.Clear();

            m_Root = new VisualElement();
            m_Root.style.position = Position.Absolute;
            m_Root.style.left = 0;
            m_Root.style.right = 0;
            m_Root.style.top = 0;
            m_Root.style.bottom = 0;
            m_Root.style.backgroundColor = new Color(0.03f, 0.03f, 0.05f, 0.72f);
            m_Root.style.alignItems = Align.Center;
            m_Root.style.justifyContent = Justify.Center;
            root.Add(m_Root);

            var panel = new VisualElement();
            panel.style.width = Length.Percent(86);
            panel.style.maxWidth = 1180;
            panel.style.backgroundColor = new Color(0.16f, 0.12f, 0.09f, 0.97f);
            panel.style.paddingLeft = 24;
            panel.style.paddingRight = 24;
            panel.style.paddingTop = 18;
            panel.style.paddingBottom = 18;
            SetRadius(panel, 12);
            SetBorder(panel, new Color(0.72f, 0.52f, 0.28f), 2);
            m_Root.Add(panel);

            m_Title = Text(panel, 26, FontStyle.Bold, new Color(1f, 0.86f, 0.6f));
            var subtitle = Text(panel, 14, FontStyle.Normal, new Color(0.9f, 0.85f, 0.78f));
            subtitle.text = "«Каждому — своё железо. Выбирай, с чем пойдёшь на стены.»   Лимиты — доля от команды атаки.";
            subtitle.style.marginBottom = 12;

            m_List = new VisualElement();
            m_List.style.flexDirection = FlexDirection.Row;
            m_List.style.flexWrap = Wrap.Wrap;
            m_List.style.justifyContent = Justify.SpaceBetween;
            panel.Add(m_List);

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.alignItems = Align.Center;
            footer.style.marginTop = 10;
            panel.Add(footer);

            m_Message = Text(footer, 15, FontStyle.Bold, new Color(1f, 0.55f, 0.4f));
            var close = new Button(Close) { text = "Закрыть (E / Esc)" };
            StyleButton(close, new Color(0.35f, 0.26f, 0.18f));
            footer.Add(close);
        }

        private void Refresh()
        {
            m_Title.text = $"{m_Speaker}: снаряжение для штурма";
            m_List.Clear();

            var loadout = PlayerLoadout.Local;
            var catalog = loadout.Catalog;
            if (catalog == null) return;

            var match = MatchManager.Instance;
            int teamSize = match != null ? Mathf.Max(1, match.AttackersTotal) : 1;
            var current = loadout.Kit;

            for (int i = 0; i < catalog.attackerKits.Count; i++)
            {
                var kit = catalog.attackerKits[i];
                int index = i;
                bool selected = kit == current;
                int users = PlayerLoadout.CountUsers(kit);
                int limit = kit.GetLimit(teamSize);

                var card = new VisualElement();
                card.style.width = Length.Percent(32.5f);
                card.style.marginBottom = 10;
                card.style.paddingLeft = 12;
                card.style.paddingRight = 12;
                card.style.paddingTop = 10;
                card.style.paddingBottom = 10;
                card.style.backgroundColor = selected ? new Color(0.32f, 0.23f, 0.13f) : new Color(0.22f, 0.17f, 0.12f);
                SetRadius(card, 8);
                SetBorder(card, selected ? kit.color : new Color(0.4f, 0.3f, 0.2f), selected ? 3 : 1);
                m_List.Add(card);

                var header = new VisualElement();
                header.style.flexDirection = FlexDirection.Row;
                header.style.justifyContent = Justify.SpaceBetween;
                card.Add(header);
                var name = Text(header, 19, FontStyle.Bold, kit.color);
                name.text = kit.kitName;
                var usage = Text(header, 13, FontStyle.Normal, new Color(0.85f, 0.8f, 0.7f));
                usage.text = limit == int.MaxValue ? $"{users} чел." : $"{users}/{limit}";

                var purpose = Text(card, 13, FontStyle.Italic, new Color(1f, 0.9f, 0.7f));
                purpose.text = kit.purpose;
                purpose.style.marginBottom = 4;

                var weapons = new List<string>();
                foreach (var weapon in kit.weapons) weapons.Add(s_WeaponNames.TryGetValue(weapon, out var display) ? display : weapon);
                var weaponLabel = Text(card, 13, FontStyle.Bold, Color.white);
                weaponLabel.text = "Оружие: " + string.Join(", ", weapons);

                var description = Text(card, 12, FontStyle.Normal, new Color(0.88f, 0.84f, 0.78f));
                description.text = kit.description;
                description.style.whiteSpace = WhiteSpace.Normal;
                description.style.marginTop = 4;
                description.style.marginBottom = 8;
                description.style.minHeight = 58;

                var button = new Button(() => Select(index)) { text = selected ? "Выбрано" : "Взять комплект" };
                StyleButton(button, selected ? new Color(0.3f, 0.45f, 0.25f) : new Color(0.62f, 0.38f, 0.16f));
                button.SetEnabled(!selected);
                card.Add(button);
            }
        }

        private void Select(int index)
        {
            var loadout = PlayerLoadout.Local;
            if (loadout == null) return;

            string error = loadout.TrySelectKit(index);
            Refresh();
            m_Message.style.color = error == null ? new Color(0.6f, 1f, 0.55f) : new Color(1f, 0.55f, 0.4f);
            m_Message.text = error ?? $"Готово: «{loadout.Kit.kitName}». Удачи на стенах!";
        }

        private static Label Text(VisualElement parent, int size, FontStyle style, Color color)
        {
            var label = new Label();
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = style;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);
            return label;
        }

        private static void StyleButton(Button button, Color color)
        {
            button.style.backgroundColor = color;
            button.style.color = Color.white;
            button.style.fontSize = 14;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.height = 32;
            SetRadius(button, 6);
            SetBorder(button, new Color(0f, 0f, 0f, 0.3f), 1);
        }

        private static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        private static void SetBorder(VisualElement element, Color color, float width)
        {
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
        }

        #endregion
    }
}
