using System.Reflection;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.ReallyBigHelper;

public class MountainModelExtensions {
    private static ILHook mountainModelFarPlane;
    public static void Load() {
        On.Celeste.Skybox.Draw += skyboxScale;
        mountainModelFarPlane = new ILHook(
            typeof(MountainModel).GetMethod("orig_BeforeRender", BindingFlags.Public | BindingFlags.Instance),
            setFarPlane);
        IL.Celeste.MountainModel.BeforeRender += setFarPlane;
    }

    public static void Unload() {
        On.Celeste.Skybox.Draw -= skyboxScale;
        mountainModelFarPlane?.Dispose();
        IL.Celeste.MountainModel.BeforeRender -= setFarPlane;
    }

    public static float globalSkyboxScale=1;
    private static void skyboxScale(On.Celeste.Skybox.orig_Draw orig, Skybox skybox, Matrix matrix, Color color) {
        var scale = Matrix.CreateScale(new Vector3(globalSkyboxScale, globalSkyboxScale, globalSkyboxScale));
        orig(skybox, Matrix.Multiply(scale, matrix), color);
    }

    private static void setFarPlane(ILContext ctx) {
        var cursor = new ILCursor(ctx);
        
        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(50f));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(setFarPlane_internal);
    }

    private static float setFarPlane_internal(float orig, MountainModel self) {
        return 50*globalSkyboxScale;
    }
}