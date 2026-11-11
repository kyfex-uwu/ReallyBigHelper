using Celeste.Mod.Entities;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste.Mod.ReallyBigHelper;

[CustomEntity("ReallyBigHelper/SetEndScreenTrigger")]
public class SetEndscreenTrigger : Trigger {
    public readonly string name;
    public readonly bool onEnter;
    public SetEndscreenTrigger(EntityData data, Vector2 offset) : base(data, offset) {
        this.onEnter = data.Bool("onEnter", true);
        this.name = data.Bool("clear") ? null : data.String("name");
    }
    public static readonly string customEndscreenName = "ReallyBigHelper_customEndscreenName";
    public static readonly string reallyBigSessionMeta = "ReallyBigHelper_reallyBigSessionMeta";
    
    private static void setCustomEndScreen(Level level, string name) {
        var data = DynamicData.For(level.Session);
        data.Set(customEndscreenName, name);
    }
    
    public override void OnEnter(Player player) {
        base.OnEnter(player);
        if(this.onEnter) setCustomEndScreen(this.SceneAs<Level>(), this.name);
    }
    public override void OnLeave(Player player) {
        base.OnLeave(player);
        if(!this.onEnter) setCustomEndScreen(this.SceneAs<Level>(), this.name);
    }
    
    public static void Load() {
        IL.Celeste.LevelExit.playCustomCompleteScreenMusic += customMusic;
        IL.Celeste.LevelExit.LoadCompleteThread += customMetaInject;
    }

    public static void Unload() {
        IL.Celeste.LevelExit.playCustomCompleteScreenMusic -= customMusic;
        IL.Celeste.LevelExit.LoadCompleteThread -= customMetaInject;
    }

    private static void customMusic(ILContext ctx) {
        var cursor = new ILCursor(ctx);

        cursor.GotoNext(MoveType.After, instr => instr.MatchStloc0());
        cursor.EmitLdloc0();
        cursor.EmitLdarg0();
        cursor.EmitDelegate(getCustomMusicMaybe);
        cursor.EmitStloc0();
    }

    private static MapMetaCompleteScreen getCompleteScreen(LevelExit self) {
        var customEndScreen = DynamicData.For(self.session).Get(customEndscreenName) as string;
        if (!ReallyBigHelperModule.chapterData.TryGetValue(self.session.Area.SID, out var meta) ||
            (int) self.session.Area.Mode >= meta.Length || customEndScreen == null) return null;

        return meta[(int)self.session.Area.Mode].GetCompleteScreen(customEndScreen);
    }

    private static string[] getCustomMusicMaybe(string[] orig, LevelExit self) {
        var completeScreen = getCompleteScreen(self);
        if (completeScreen == null) return orig;
        
        return completeScreen.MusicBySide ?? orig;
    }

    private static void customMetaInject(ILContext ctx) {
        var cursor = new ILCursor(ctx);
        
        /*
         * IL_000e: ldfld        class Celeste.Mod.Meta.MapMeta Celeste.AreaData::Meta
           IL_0013: dup

           IL_0014: brtrue.s     IL_001a
           IL_0016: pop
           IL_0017: ldnull
           IL_0018: br.s         IL_001f
           IL_001a: callvirt     instance class Celeste.Mod.Meta.MapMetaCompleteScreen Celeste.Mod.Meta.MapMeta::get_CompleteScreen()
           IL_001f: dup
         */
        //IL_000e: ldfld        class Celeste.Mod.Meta.MapMeta Celeste.AreaData::Meta
        cursor.GotoNext(instr => instr.MatchLdfld<AreaData>("Meta"));
        cursor.GotoNext(MoveType.After, instr => instr.MatchLdnull());
        cursor.EmitLdarg0();
        cursor.EmitDelegate(getCustomMeta);
            
        cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt<MapMeta>("get_CompleteScreen"));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(getCustomMeta);
    }
    private static MapMetaCompleteScreen getCustomMeta(MapMetaCompleteScreen orig, LevelExit self) {
        return getCompleteScreen(self) ?? orig;
    }
}