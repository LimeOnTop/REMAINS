using System.Collections.Generic;
using Blocks.Gameplay.Shooter;

namespace Remains
{
    /// <summary>
    /// Reference-counted input locks for the local player, so several systems (carrying, drawing on the battle map,
    /// manning a cannon, the armorer menu) can suppress weapons/movement/look without overriding each other.
    /// </summary>
    public static class LocalPlayerLocks
    {
        private static readonly HashSet<object> s_Weapon = new HashSet<object>();
        private static readonly HashSet<object> s_Movement = new HashSet<object>();
        private static readonly HashSet<object> s_Look = new HashSet<object>();

        public static bool WeaponLocked => s_Weapon.Count > 0;
        public static bool MovementLocked => s_Movement.Count > 0;
        public static bool LookLocked => s_Look.Count > 0;

        public static void SetWeapon(object owner, bool locked) => Set(s_Weapon, owner, locked);
        public static void SetMovement(object owner, bool locked) => Set(s_Movement, owner, locked);
        public static void SetLook(object owner, bool locked) => Set(s_Look, owner, locked);

        public static void ReleaseAll(object owner)
        {
            bool changed = s_Weapon.Remove(owner) | s_Movement.Remove(owner) | s_Look.Remove(owner);
            if (changed) Apply();
        }

        private static void Set(HashSet<object> set, object owner, bool locked)
        {
            bool changed = locked ? set.Add(owner) : set.Remove(owner);
            if (changed) Apply();
        }

        /// <summary>Pushes the current lock state to the local player's components.</summary>
        public static void Apply()
        {
            var member = TeamMember.Local;
            if (member == null || member.Player == null) return;

            var weapons = member.GetComponent<WeaponController>();
            if (weapons != null)
            {
                bool wasLocked = weapons.InputSuppressed;
                weapons.InputSuppressed = WeaponLocked;
                if (wasLocked != WeaponLocked && member.Player.PlayerState != null && member.Player.PlayerState.IsActive)
                {
                    weapons.SetCurrentWeaponActive(!WeaponLocked);
                }
            }

            var shooter = member.GetComponent<ShooterAddon>();
            if (shooter != null) shooter.AimInputSuppressed = WeaponLocked;

            member.Player.SetMovementInputEnabled(!MovementLocked);

            var camera = member.Player.CoreCamera;
            if (camera != null)
            {
                float sensitivity = shooter != null ? shooter.freeLookSensitivity : 1f;
                camera.SetLookSensitivity(LookLocked ? 0f : sensitivity);
            }
        }
    }
}
