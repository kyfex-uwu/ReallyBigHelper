using Microsoft.Xna.Framework;
using MonoMod.Cil;

namespace Celeste.Mod.ReallyBigHelper;

public class CustomChapterOption : OuiChapterPanel.Option {
    public Color FgColor;
    public int chapterIndex;

    public static void Load() {
        IL.Celeste.OuiChapterPanel.Option.Render += iconColorMixin;
    }

    public static void Unload() {
        IL.Celeste.OuiChapterPanel.Option.Render -= iconColorMixin;
    }

    private static void iconColorMixin(ILContext ctx) {
        var cursor = new ILCursor(ctx);
        cursor.GotoNext(MoveType.After, instr =>
            instr.MatchCall<Color>("get_White"));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(getColor);
    }

    private static Color getColor(Color white, OuiChapterPanel.Option option) {
        if (option is CustomChapterOption customOption) return customOption.FgColor;
        return white;
    }
}