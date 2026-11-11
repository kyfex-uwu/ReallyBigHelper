

using System;
using System.Collections.Generic;
using System.Reflection;
using Monocle;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.ReallyBigHelper;

public class PlatinumCompat {
    private static List<Hook> hooks = new();
    public static void Load() {
        hooks.Add(new Hook(System.Reflection.Assembly.GetAssembly(typeof(PlatinumStrawberry.PlatinumModule))
            .GetType("Celeste.Mod.PlatinumStrawberry.Entities.PlatinumBerry")
            .GetMethod("OnCollect", BindingFlags.Public|BindingFlags.Instance), onCollect));
    }

    public static void Unload() {
        foreach(var hook in hooks) hook.Dispose();
    }

    private static void onCollect(Action<Entity> orig, Entity self) {
        orig(self);
        ReallyBigHelperModule.SaveData.getAreaData(self.SceneAs<Level>().Session.Area.SID).addBerry(
            ReallyBigHelperSaveData.SpecialBerryType.PLATINUM, ReallyBigHelperModule.Session.currentCheckpoint);
    }
}