using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste.Mod.ReallyBigHelper;

[CustomEntity("ReallyBigHelper/ShowHideChapterBerriesTrigger")]
public class ShowHideChapterBerriesTrigger : Trigger {
    public class PauseMenuBerryController {
        public bool whitelist;
        public HashSet<int> checkpoints;
        public PauseMenuBerryController() {
            this.whitelist = false;
            this.checkpoints = new HashSet<int>();
        }
    }
    public static readonly string pauseMenuBerryController = "ReallyBigHelper_pauseMenuBerryController";

    public enum Mode {
        ADD,
        REMOVE,
        SET,
        RESET
    }
    public readonly bool onEnter;
    public readonly Mode mode;
    public readonly HashSet<int> list;
    
    // public readonly HashSet<>
    public ShowHideChapterBerriesTrigger(EntityData data, Vector2 offset) : base(data, offset) {
        this.onEnter = data.Bool("onEnter", true);
        this.mode = data.Enum("mode", Mode.SET);
        this.list = new HashSet<int>(data.String("list", "").Split(",").Select(num => int.TryParse(num, out var output) ? output : -1));
    }

    public override void OnEnter(Player player) {
        base.OnEnter(player);
        if(this.onEnter) this.run();
    }

    public override void OnLeave(Player player) {
        base.OnLeave(player);
        if(!this.onEnter) this.run();
    }

    private static PauseMenuBerryController getController(Level level) {
        var data = DynamicData.For(level.Session);
        var controller = data.Get<PauseMenuBerryController>(pauseMenuBerryController);
        if (controller == null) {
            controller = new PauseMenuBerryController();
            data.Add(pauseMenuBerryController, controller);
        }

        return controller;
    }
    private void run() {
        var controller = getController(this.SceneAs<Level>());

        switch (this.mode) {
            case Mode.ADD:
            case Mode.REMOVE:
                if(controller.whitelist == (this.mode == Mode.ADD))
                    controller.checkpoints.UnionWith(this.list);
                else
                    controller.checkpoints.ExceptWith(this.list);
                break;
            case Mode.SET:
                controller.whitelist = true;
                controller.checkpoints.Clear();
                controller.checkpoints.UnionWith(this.list);
                break;
            case Mode.RESET:
                controller.whitelist = false;
                controller.checkpoints.Clear();
                break;
        }
    }

    public static void Load() {
        IL.Celeste.GameplayStats.Render += filterBerries;
    }

    public static void Unload() {
        IL.Celeste.GameplayStats.Render -= filterBerries;
    }

    private static void filterBerries(ILContext ctx) {
        var cursor = new ILCursor(ctx);

        cursor.GotoNext(MoveType.Before, instr =>
            instr.MatchCall<GameplayStats>("getInitialPosition"));
        cursor.EmitLdloca(8);
        cursor.EmitLdarg0();
        cursor.EmitLdloc0();
        cursor.EmitDelegate(modifyOrigPos);
        
        //--
        
        cursor.GotoNext(MoveType.After, instr =>
            instr.MatchStloc(12));
        
        cursor.EmitLdloc(10);
        cursor.EmitLdarg0();
        cursor.EmitDelegate(shouldContinue);
        var heldPlace = cursor.Next;
        
        // IL_035c: ldloc.s      index1
        // IL_035e: ldc.i4.1
        // IL_035f: add
        // IL_0360: stloc.s      index1
        cursor.GotoNext(MoveType.Before,
            instr => instr.MatchLdloc(10) &&
            (instr.Next?.MatchLdcI4(1) ?? false) &&
            (instr.Next?.Next?.MatchAdd() ?? false) &&
            (instr.Next?.Next?.Next?.MatchStloc(10) ?? false));
        var continueSpot = cursor.Next;
        
        cursor.Goto(heldPlace, MoveType.Before);
        cursor.EmitBrtrue(continueSpot);
        
        //--

        cursor.GotoNext(MoveType.Before, instr =>
            instr.MatchBge(out var _));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(getLastChapter);
    }
    private static bool shouldContinue(int index, GameplayStats self) {
        var controller = getController(self.SceneAs<Level>());

        return controller.checkpoints.Contains(index) != controller.whitelist;
    }

    private static void modifyOrigPos(ref Vector2 orig, GameplayStats self, float easeAmt) {
        AreaKey area = self.SceneAs<Level>().Session.Area;
        AreaModeStats mode = SaveData.Instance.Areas_Safe[area.ID].Modes[(int) area.Mode];
        if (!mode.Completed && !SaveData.Instance.CheatMode && !SaveData.Instance.DebugMode)
            return;
        ModeProperties modeProperties = AreaData.Get(area).Mode[(int) area.Mode];
        
        var controller = getController(self.SceneAs<Level>());
        var checkpoints = 0;
        int totalStrawberries = 0;
        foreach (var checkpoint in controller.checkpoints)
            if (checkpoint < modeProperties.Checkpoints.Length) {
                checkpoints++;
                totalStrawberries += modeProperties.Checkpoints[checkpoint].Strawberries;
            }
        if(!controller.whitelist) {
            checkpoints = modeProperties.Checkpoints.Length-checkpoints;
            totalStrawberries = modeProperties.TotalStrawberries-totalStrawberries;
            
        }
        
        orig = new Vector2(
            (1920 - (totalStrawberries - 1) * 32 - 
                (totalStrawberries <= 0 || checkpoints == 0 ? 0 : checkpoints * 32)) / 2,
            (float)(1016.0 + (1.0 - easeAmt) * 80.0));
    }

    private static int getLastChapter(int i2, GameplayStats self) {
        var controller = getController(self.SceneAs<Level>());
        if (controller.whitelist) return controller.checkpoints.Max();
        
        AreaKey area = self.SceneAs<Level>().Session.Area;
        AreaModeStats mode = SaveData.Instance.Areas_Safe[area.ID].Modes[(int) area.Mode];
        ModeProperties modeProperties = AreaData.Get(area).Mode[(int) area.Mode];

        int max = modeProperties.Checkpoints.Length-1;
        while(max>=0) {
            if (!controller.checkpoints.Contains(max)) return max;
            max--;
        }
        return max;
    }
}