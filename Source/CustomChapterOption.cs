using System;
using System.Collections;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Cil;

namespace Celeste.Mod.ReallyBigHelper;

public class CustomChapterOption : OuiChapterPanel.Option {
    public Color FgColor;

    public static void Load() {
        IL.Celeste.OuiChapterPanel.Option.Render += iconColorMixin;
    }

    public static void Unload() {
        IL.Celeste.OuiChapterPanel.Option.Render -= iconColorMixin;
    }

    private static void iconColorMixin(ILContext ctx) {
        var cursor = new ILCursor(ctx);
        cursor.GotoNext(MoveType.Before, instr =>
            instr.MatchCall<Color>("get_White"));
        cursor.Remove(); //nyehehehe
        cursor.EmitLdarg0();
        cursor.EmitDelegate(getColor);
    }
    private static Color getColor(OuiChapterPanel.Option option) {
        if (option is CustomChapterOption customOption) return customOption.FgColor;
        return Color.White;
    }

}