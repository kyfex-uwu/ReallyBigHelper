using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.ReallyBigHelper;

public class CustomChapterPanel {
    public static readonly Dictionary<OuiChapterPanel, ChapterMetadata.Final> positions = new();
    public static readonly HashSet<OuiChapterPanel> storedFakeSwap = new();

    private static ILHook hookOrigUpdate;
    // private static MethodInfo origDrawCheckpoint = typeof(OuiChapterPanel).GetMethod("orig_DrawCheckpoint",
    //     BindingFlags.NonPublic|BindingFlags.Instance);
    
    public static readonly string reallyBigSectionName = Random.Shared.Next() + "ReallyBigSection_";

    public static void Load() {
        On.Celeste.OuiChapterPanel.SwapRoutine += swapMixin;
        On.Celeste.OuiChapterPanel.DrawCheckpoint += drawCheckpointMixin;
        On.Celeste.OuiChapterPanel.Start += startMixin;
        On.Celeste.OuiChapterPanel.Leave += leaveMixin;
        IL.Celeste.OuiChapterPanel.Render += renderMixin;
        //IL.Celeste.Overworld.Update += mountainUpdateMixin;

        hookOrigUpdate = new ILHook(typeof(OuiChapterPanel).GetMethod("orig_Update", BindingFlags.Public|BindingFlags.Instance), 
            updateMixin);
        //Delegate.CreateDelegate(typeof(Func<List<OuiChapterPanel.Option>>), origGetOptions) as Func<List<OuiChapterPanel.Option>>;
    }

    public static void Unload() {
        On.Celeste.OuiChapterPanel.SwapRoutine -= swapMixin;
        On.Celeste.OuiChapterPanel.DrawCheckpoint -= drawCheckpointMixin;
        On.Celeste.OuiChapterPanel.Start -= startMixin;
        On.Celeste.OuiChapterPanel.Leave -= leaveMixin;
        IL.Celeste.OuiChapterPanel.Render -= renderMixin;
        //IL.Celeste.Overworld.Update -= mountainUpdateMixin;

        hookOrigUpdate?.Dispose();
    }

    //this could be done with il hooks. but why
    private static IEnumerator swapMixin(On.Celeste.OuiChapterPanel.orig_SwapRoutine orig, OuiChapterPanel self) {
        if (!ReallyBigHelperModule.chapterData.ContainsKey(self.Area.SID)) {
            yield return orig(self);
        } else {
            var fromHeight = self.height;
            var toHeight = (self.selectingMode ^ storedFakeSwap.Contains(self)) ? 730 : self.GetModeHeight();
            self.resizing = true;
            self.PlayExpandSfx(fromHeight, toHeight);
            var offset = 800f;
            float p;
            for (p = 0.0f; p < 1.0; p += Engine.DeltaTime * 4f) {
                yield return null;
                self.contentOffset.X = (float)(440.0 + offset * Ease.CubeIn(p));
                self.height = MathHelper.Lerp(fromHeight, toHeight, Ease.CubeOut(p * 0.5f));
            }
            
            if (!storedFakeSwap.Remove(self))
                self.selectingMode = !self.selectingMode;
            
            if (!self.selectingMode) {
                if (!positions.ContainsKey(self)) positions[self] = ReallyBigHelperModule.chapterData[self.Area.SID];
                self.checkpoints.Clear();

                var checkpointNames = new List<string>();
                checkpointNames.Add(null);
                foreach (var checkpoint in AreaData.Get(self.Area).Mode[(int)self.Area.Mode].Checkpoints)
                    checkpointNames.Add(checkpoint.Level);

                foreach (var section in positions[self].Chapters) {
                    var textLabel = section.text;
                    if (section.Chapters.Count == 0 &&
                        section.id >= 0 && section.id < checkpointNames.Count) {
                        textLabel = checkpointNames[section.id];
                    }

                    self.checkpoints.Add(section.getOption(self, textLabel,
                        (section.id >= 0 && section.id < checkpointNames.Count) ? 
                            checkpointNames[section.id] : null));
                }

                if (!self.RealStats.Modes[(int)self.Area.Mode].Completed && !SaveData.Instance.DebugMode &&
                    !SaveData.Instance.CheatMode) {
                    self.option = self.checkpoints.Count - 1; //"last" checkpoint
                    for (var index = 0; index < self.checkpoints.Count - 1; ++index)
                        self.options[index].CheckpointSlideOut = 1f;
                } else {
                    self.option = 0;
                    for (var i = 0; i < positions[self].Chapters.Count; i++)
                        if (positions[self].Chapters[i].selected)
                            self.option = i;
                }

                foreach (var group in positions[self].Chapters) group.selected = false;

                for (var index = 0; index < self.options.Count; ++index)
                    self.options[index].SlideTowards(index, self.options.Count, true);
            }

            self.options[self.option].Pop = 1f;
            for (p = 0.0f; p < 1.0; p += Engine.DeltaTime * 4f) {
                yield return null;
                self.height = MathHelper.Lerp(fromHeight, toHeight, Ease.CubeOut(Math.Min(1f, (float)(0.5 + p * 0.5))));
                self.contentOffset.X = (float)(440.0 + offset * (1.0 - Ease.CubeOut(p)));
            }

            self.contentOffset.X = 440f;
            self.height = toHeight;
            self.Focused = true;
            self.resizing = false;
        }
    }

    private static void drawCheckpointMixin(On.Celeste.OuiChapterPanel.orig_DrawCheckpoint orig, OuiChapterPanel self,
        Vector2 center, object _option, int checkpointIndex) {
        var option = _option as OuiChapterPanel.Option;
        if (!(option is CustomChapterOption customOption)) {
            orig(self, center, option, checkpointIndex);
            return;
        }

        if (customOption.chapterIndex >= 0 && customOption.chapterIndex < customOption.Siblings) {
            //origDrawCheckpoint.Invoke(self, new object[] { center, option, 0 });//customOption.chapterIndex
        }
    }

    private static void startMixin(On.Celeste.OuiChapterPanel.orig_Start orig, OuiChapterPanel self,
        string id) {
        if (id == null || id != reallyBigSectionName) {
            orig(self, id);
            return;
        }

        var next = positions[self].Chapters[self.option];
        if (next.Chapters.Count < 1) return;

        positions[self] = next;
        positions[self].selected = true;
        storedFakeSwap.Add(self);//to couteract the swapping back
        self.Swap();
    }

    private static IEnumerator leaveMixin(On.Celeste.OuiChapterPanel.orig_Leave orig, OuiChapterPanel self, Oui next) {
        positions.Remove(self);
        return orig(self, next);
    }

    private static void updateMixin(ILContext ctx) {
        var cursor = new ILCursor(ctx);

        cursor.GotoNext(MoveType.Before, instr =>
            instr.MatchCall<Entity>("Update"));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(updateMountain);

        cursor.GotoNext(MoveType.After, instr =>
            instr.MatchLdstr("event:/ui/world_map/chapter/checkpoint_back"));
        cursor.GotoNext(MoveType.Before, instr =>
            instr.MatchCallvirt<OuiChapterPanel>("Swap"));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(interceptBack);
    }
    private static void interceptBack(OuiChapterPanel self) {
        if (positions.ContainsKey(self) && positions[self].parent != null) {
            storedFakeSwap.Add(self);//to couteract the swapping back
            positions[self] = positions[self].parent;
        }
    }
    private static void updateMountain(OuiChapterPanel self) {
        if (positions.TryGetValue(self, out var position)) {
            if (self.option >= 0 && self.option < position.Chapters.Count) {
                var mountainPos = position.Chapters[self.option].GetMountain();
                if (mountainPos != null) {
                    self.Overworld.Mountain.EaseCamera(self.Area.ID,
                        mountainPos.Select.Convert(), null, true);
                }
            }
        }
    }

    private static void renderMixin(ILContext ctx) {
        var cursor = new ILCursor(ctx);

        cursor.GotoNext(MoveType.Before, instr =>
            instr.Next.Next.MatchLdfld<OuiChapterPanel>("strawberries"));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(CustomRender);
    }

    private static bool CustomRender(bool dontRenderCover, OuiChapterPanel self) {
        if (self.options.Count == 0 || !(self.options[0] is CustomChapterOption)) {
            return dontRenderCover;
        }
        
        if (positions.TryGetValue(self, out var position) && 
            self.option < position.Chapters.Count &&
            self.option >=0 &&
            position.Chapters[self.option].id == -1) {// !storedFakeSwap.Contains(self) && 
            self.strawberries.Position = self.contentOffset + new Vector2(0.0f, 170f) + self.strawberriesOffset;
            self.deaths.Position = self.contentOffset + new Vector2(0.0f, 170f) + self.deathsOffset;
            self.heart.Position = self.contentOffset + new Vector2(0.0f, 170f) + self.heartOffset;
            self.Components.Render();
        }

        var parent = positions[self].parent;
        if (parent != null) {
            Vector2 center = self.Position + new Vector2(self.contentOffset.X, 340f);
            for (int index = parent.Chapters.Count - 1; index >= 0; --index) {
                self.DrawCheckpoint(center, parent.Chapters[index].option, index);
            }
        }

        return false;
    }

    /*
    private static void mountainUpdateMixin(ILContext ctx) {
        var cursor = new ILCursor(ctx);

        cursor.GotoNext(MoveType.After, 
            instr => instr.MatchCallvirt<MapMeta>("get_Mountain"));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(customMountain);
    }

    private static MapMetaMountain customMountain(MapMetaMountain mountain, Overworld self) {
        var panel = self.UIs.Find(oui => (oui is OuiChapterPanel ouiPanel) && positions.ContainsKey(ouiPanel))
            as OuiChapterPanel;
        if (panel != null) {
            return positions[panel].Mountain;
        }
        return mountain;
    }
    */
}