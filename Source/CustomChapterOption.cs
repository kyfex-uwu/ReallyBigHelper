using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using MonoMod.Cil;

namespace Celeste.Mod.ReallyBigHelper;

public class CustomChapterOption : OuiChapterPanel.Option {
    public Color FgColor;
    public int chapterIndex;
    public ChapterMetadata.Final position;
    private string _untranslatedName;
    public MHHDataObj MHHData;

    public record MHHDataObj(string id, string path);

    public string untranslatedName {
        set {
            this.Label = Dialog.Clean(value);
            this._untranslatedName = value;
        }
        get => this._untranslatedName;
    }

    public static void Load() {
        IL.Celeste.OuiChapterPanel.Option.Render += iconColorMixin;
        Everest.Events.AssetReload.OnAfterReload += GetName;
    }

    public static void Unload() {
        IL.Celeste.OuiChapterPanel.Option.Render -= iconColorMixin;
        Everest.Events.AssetReload.OnAfterReload -= GetName;
    }

    private static void GetName(bool silent) {
        foreach(var smth in CustomChapterPanel.positions) {
            foreach (var option in smth.Key.options) {
                if (option is CustomChapterOption customOption) {
                    customOption.untranslatedName = customOption.untranslatedName;
                }
            }
        }
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