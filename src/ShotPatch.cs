using System;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace LowAmmoCue;

internal static class ShotPatch
{
    internal static MethodInfo Target => AccessTools.DeclaredMethod(typeof(Player.FirearmController),
        nameof(Player.FirearmController.InitiateShot), new[] { typeof(IWeapon), typeof(Ammo),
            typeof(Vector3), typeof(Vector3), typeof(Vector3), typeof(int), typeof(float) });

    internal static void Prefix(Player.FirearmController __instance, IWeapon weapon, out ShotSnapshot __state)
    {
        __state = default;
        try { __state = Plugin.Instance?.CaptureShot(__instance, weapon) ?? default; }
        catch (Exception ex) { Plugin.Instance?.WarnOnce("shot-capture", ex); }
    }

    internal static void Postfix(Player.FirearmController __instance, IWeapon weapon, ShotSnapshot __state, bool __runOriginal)
    {
        try { if (__runOriginal) Plugin.Instance?.OnShot(__instance, weapon, __state); }
        catch (Exception ex) { Plugin.Instance?.WarnOnce("shot", ex); }
    }
}
