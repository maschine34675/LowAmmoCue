// Only the inventory/ownership surface used by the production observer.
// These stand-ins cannot establish native Unity or Harmony runtime behavior.
using System;
namespace EFT
{
    public class Player
    {
        public bool IsAI { get; set; }
        public bool IsYourPlayer { get; set; }
        public Health HealthController { get; set; }
        public object HandsController { get; set; }
        public class Health { public bool IsAlive { get; set; } }
        public class FirearmController
        {
            public InventoryLogic.Weapon Item { get; set; }
            public Action Body { get; set; }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            public virtual void InitiateShot(InventoryLogic.IWeapon weapon, InventoryLogic.Ammo ammo,
                UnityEngine.Vector3 position, UnityEngine.Vector3 direction, UnityEngine.Vector3 fireport, int chamber, float overheat)
                => Body?.Invoke();
        }
    }
}
namespace EFT.InventoryLogic
{
    public interface IWeapon { bool IsUnderbarrelWeapon { get; } }
    public class Ammo { }
    public class Launcher : IWeapon { public bool IsUnderbarrelWeapon => true; }
    public class Weapon : IWeapon
    {
        public bool IsUnderbarrelWeapon { get; set; }
        public bool IsGrenadeLauncher { get; set; }
        public bool IsOneOff { get; set; }
        public bool IsStationaryWeapon { get; set; }
        public int ChamberAmmoCount { get; set; }
        public object[] Chambers { get; set; } = [];
        public Magazine Magazine { get; set; }
        public Magazine GetCurrentMagazine() => Magazine;
    }
    public class Magazine
    {
        public int Count { get; set; }
        public int MaxCount { get; set; }
    }
    public class CylinderMagazine : Magazine { }
}
namespace UnityEngine { public struct Vector3 { } }
#if HOOK_CHECKS
namespace LowAmmoCue
{
    // Test sink only; production ShotPatch and the actual installed Harmony library run below.
    internal class Plugin
    {
        internal static Plugin Instance;
        internal EFT.Player Local;
        internal int Captures, Plays, Warnings, LastRemaining;
        internal bool ThrowCapture, ThrowPlay;
        internal ShotSnapshot CaptureShot(EFT.Player.FirearmController firearm, EFT.InventoryLogic.IWeapon weapon)
        {
            Captures++;
            if (ThrowCapture) throw new InvalidOperationException("capture fixture");
            return ShotObserver.TryObserve(Local, firearm, weapon, out int left, out int capacity)
                ? new ShotSnapshot(Local, left, capacity) : default;
        }
        internal void OnShot(EFT.Player.FirearmController firearm, EFT.InventoryLogic.IWeapon weapon, ShotSnapshot snapshot)
        {
            if (ThrowPlay) throw new InvalidOperationException("play fixture");
            if (snapshot.Local == null) return;
            Plays++;
            LastRemaining = snapshot.Remaining;
        }
        internal void WarnOnce(string key, Exception error) => Warnings++;
    }
    internal static class SkipShotFixture
    {
        internal static bool Prefix() => false;
    }
}
#endif
