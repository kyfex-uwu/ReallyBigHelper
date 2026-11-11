using System.Reflection;
using Celeste.Mod.CollabUtils2.Entities;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.ReallyBigHelper;

public class CollabUtilsCompat {

    private static MethodInfo swapMethod = typeof(CollabUtils2.UI.InGameOverworldHelper).GetMethod("OnChapterPanelSwap",
        BindingFlags.Static | BindingFlags.NonPublic);

    private static ILHook swapHook;
    public static void Load() {
        swapHook = new ILHook(swapMethod, swapHookMethod);
    }

    public static void Unload() {
        swapHook?.Dispose();
    }

    private static void swapHookMethod(ILContext ctx) {
        var cursor = new ILCursor(ctx);
        cursor.EmitLdarg1();
        cursor.EmitDelegate(shouldSwapOverride);
        
        var label = ctx.DefineLabel();
        cursor.EmitBrfalse(label);
        cursor.EmitRet();
        cursor.MarkLabel(label);
    }
    
    private static bool shouldSwapOverride(OuiChapterPanel self) {
        if (ReallyBigHelperModule.chapterData.ContainsKey(self.Area.SID)) {
            //this is bad and gross and why cant i call orig???
            self.Focused = false;
            self.Overworld.ShowInputUI = !self.selectingMode;
            self.Add(new Coroutine(self.SwapRoutine()));
            return true;
        }

        return false;
    }

    public static ReallyBigHelperSaveData.SpecialBerryType? getBerryType(Strawberry berry) {
        if (berry is SilverBerry) return ReallyBigHelperSaveData.SpecialBerryType.SILVER;
        return null;
    }
}