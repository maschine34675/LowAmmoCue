using System;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using LowAmmoCue;

internal static class Program
{
    private static int _checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAILED: " + name);
        _checks++;
    }

    private static void Main()
    {
        // Real installed Harmony runs in the supported CLR family, against a fake
        // firearm method. This proves injection behavior, not EFT/Unity audibility.
        var gun = new Weapon { Magazine = new Magazine { Count = 4, MaxCount = 30 }, Chambers = new object[1], ChamberAmmoCount = 1 };
        var controller = new Player.FirearmController { Item = gun };
        var local = new Player { IsYourPlayer = true, HealthController = new Player.Health { IsAlive = true }, HandsController = controller };
        var sink = new Plugin { Local = local };
        Plugin.Instance = sink;
        var harmony = new Harmony("com.maschine.LowAmmoCue.offline-checks");
        var skip = new Harmony("com.maschine.LowAmmoCue.skip-fixture");
        try
        {
            Check(ShotPatch.Target != null, "exact seven-argument hook found");
            harmony.Patch(ShotPatch.Target, new HarmonyMethod(typeof(ShotPatch), nameof(ShotPatch.Prefix)),
                new HarmonyMethod(typeof(ShotPatch), nameof(ShotPatch.Postfix)));
            int originalCalls = 0;
            controller.Body = () => { originalCalls++; gun.Magazine.Count = 29; };
            void Fire() => controller.InitiateShot(gun, new Ammo(), default, default, default, 0, 0);
            Fire();
            Check(originalCalls == 1 && sink.Plays == 1 && sink.LastRemaining == 5, "snapshot survives ammunition-changing callbacks");
            skip.Patch(ShotPatch.Target, prefix: new HarmonyMethod(typeof(SkipShotFixture), nameof(SkipShotFixture.Prefix)));
            Fire();
            Check(originalCalls == 1 && sink.Plays == 1, "skipped native method cannot add click");
            skip.UnpatchSelf();
            sink.ThrowCapture = true;
            Fire();
            Check(originalCalls == 2 && sink.Plays == 1 && sink.Warnings == 1, "capture exception cannot suppress native firing");
            sink.ThrowCapture = false; sink.ThrowPlay = true;
            Fire();
            Check(originalCalls == 3 && sink.Warnings == 2, "output exception cannot escape postfix");
            sink.ThrowPlay = false;
            controller.Body = () => throw new InvalidOperationException("native fixture");
            bool nativeException = false;
            try { Fire(); } catch (InvalidOperationException ex) { nativeException = ex.Message == "native fixture"; }
            Check(nativeException && sink.Plays == 1, "native exception preserved; failed original has no click");
            harmony.UnpatchSelf();
            controller.Body = () => originalCalls++;
            Fire();
            Check(originalCalls == 4 && sink.Plays == 1, "unpatch removes callbacks");
        }
        finally { skip.UnpatchSelf(); harmony.UnpatchSelf(); Plugin.Instance = null; }
        Console.WriteLine($"LOW_AMMO_HOOK_VERIFIED ({_checks} assertions; real Harmony with a stand-in firearm)");
    }
}
