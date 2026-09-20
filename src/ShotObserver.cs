using EFT;
using EFT.InventoryLogic;

namespace LowAmmoCue;

internal readonly struct ShotSnapshot
{
    internal readonly Player Local;
    internal readonly int Remaining;
    internal readonly int Capacity;
    internal ShotSnapshot(Player local, int remaining, int capacity)
    { Local = local; Remaining = remaining; Capacity = capacity; }
}

internal static class ShotObserver
{
    internal static bool TryObserve(Player local, Player.FirearmController firearm, IWeapon firedWeapon,
        out int remaining, out int capacity)
    {
        remaining = capacity = 0;
        if (local == null || firearm == null || local.IsAI || !local.IsYourPlayer || local.HealthController?.IsAlive != true
            || !ReferenceEquals(local.HandsController, firearm) || firedWeapon == null
            || firedWeapon.IsUnderbarrelWeapon || firedWeapon is not Weapon weapon
            || !ReferenceEquals(firearm.Item, weapon) || weapon.IsGrenadeLauncher
            || weapon.IsOneOff || weapon.IsStationaryWeapon) return false;
        var magazine = weapon.GetCurrentMagazine();
        remaining = (magazine?.Count ?? 0) + weapon.ChamberAmmoCount;
        capacity = magazine is CylinderMagazine ? magazine.MaxCount : (magazine?.MaxCount ?? 0) + weapon.Chambers.Length;
        return capacity > 1 && remaining >= 0;
    }
}
