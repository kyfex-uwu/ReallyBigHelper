using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.ReallyBigHelper;

public class CustomChapterPanel {
    public static readonly Dictionary<OuiChapterPanel, ChapterMetadata.Final> positions = new();
    public static readonly HashSet<OuiChapterPanel> storedFakeSwap = new();
    private static MapMetaMountain globalCurrentMountain__gross;

    private static ILHook hookOrigUpdate;
    
    public static readonly string reallyBigSectionName = Random.Shared.Next() + "ReallyBigSection_";

    public static void Load() {
        On.Celeste.OuiChapterPanel.SwapRoutine += swapMixin;
        On.Celeste.OuiChapterPanel.DrawCheckpoint += drawCheckpointMixin;
        On.Celeste.OuiChapterPanel.Start += startMixin;
        On.Celeste.OuiChapterPanel.Leave += leaveMixin;
        IL.Celeste.OuiChapterPanel.Render += renderMixin;
        IL.Celeste.MountainModel.BeforeRender += changeMountainMixin;

        hookOrigUpdate = new ILHook(typeof(OuiChapterPanel).GetMethod("orig_Update", BindingFlags.Public|BindingFlags.Instance), 
            updateMixin);
    }

    public static void Unload() {
        On.Celeste.OuiChapterPanel.SwapRoutine -= swapMixin;
        On.Celeste.OuiChapterPanel.DrawCheckpoint -= drawCheckpointMixin;
        On.Celeste.OuiChapterPanel.Start -= startMixin;
        On.Celeste.OuiChapterPanel.Leave -= leaveMixin;
        IL.Celeste.OuiChapterPanel.Render -= renderMixin;
        IL.Celeste.MountainModel.BeforeRender -= changeMountainMixin;

        hookOrigUpdate?.Dispose();
    }

    private static bool canShow(AreaKey area, List<string> names, int index) {
        if (index == 0) return true;
        if (index < 0 || index >= names.Count) return false;
        return SaveData.Instance.Areas_Safe[area.ID]
            .Modes[(int)area.Mode].Checkpoints.Contains(names[index]);
    }
    //this could be done with il hooks. but why
    private static IEnumerator swapMixin(On.Celeste.OuiChapterPanel.orig_SwapRoutine orig, OuiChapterPanel self) {
        if (!ReallyBigHelperModule.chapterData.ContainsKey(self.Area.SID)) {
            yield return orig(self);
        } else {
            var fromHeight = self.height;
            var toHeight = (self.selectingMode ^ storedFakeSwap.Contains(self)) ? 730 : self.GetModeHeight();
            //todo
            
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
                
                foreach (var group in positions[self].Chapters) group.selected = false;

                var checkpointNames = new List<string>();
                checkpointNames.Add(null);
                foreach (var checkpoint in AreaData.Get(self.Area).Mode[(int)self.Area.Mode].Checkpoints)
                    checkpointNames.Add(checkpoint.Level);
                
                foreach (var section in positions[self].Chapters) {
                    if (!SaveData.Instance.DebugMode && !SaveData.Instance.CheatMode) {
                        if (section.Chapters.Count > 0) {
                            var valid = false;
                            foreach (var id in section.childIds()) {
                                if (canShow(self.Area, checkpointNames, id)) {
                                    valid = true;
                                    break;
                                }
                            }

                            if (!valid) {
                                continue;
                            }
                        }else if (!canShow(self.Area, checkpointNames, section.id)) {
                            continue;
                        }
                    }

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
                    self.option = self.checkpoints.Count - 1; //"last" checkpoint (might need to edit)
                    for (var index = 0; index < self.checkpoints.Count - 1; ++index)
                        self.options[index].CheckpointSlideOut = 1f;
                } else {
                    self.option = 0;
                    for (var i = 0; i < positions[self].Chapters.Count; i++)
                        if (positions[self].Chapters[i].selected)
                            self.option = i;
                }

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
        Vector2 center, object option, int checkpointIndex) {
        if (option is CustomChapterOption customOption) {
            if (customOption.chapterIndex >= 0 && customOption.chapterIndex < customOption.Siblings) {
                //origDrawCheckpoint.Invoke(self, new object[] { center, option, 0 });//customOption.chapterIndex
                return;
            }
        }

        orig(self, center, option, checkpointIndex);
    }

    private static void startMixin(On.Celeste.OuiChapterPanel.orig_Start orig, OuiChapterPanel self,
        string id) {
        if (id == null || id != reallyBigSectionName) {
            orig(self, id);
            return;
        }

        var next = (self.options[self.option] as CustomChapterOption).position;
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
            var selected = position.Chapters.Find(child => child.selected)?.option;
            if (selected == null) {
                if (storedFakeSwap.Contains(self)) return;
                selected = self.options[self.option];
            }
            
            var mountainData = (selected as CustomChapterOption)?.position?.GetMountain();
            globalCurrentMountain__gross = mountainData;
            if (mountainData != null) {
                self.Overworld.Mountain.EaseCamera(self.Area.ID,
                    self.EnteringChapter ? mountainData.Zoom.Convert() : mountainData.Select.Convert(),
                    null, true);
                self.Overworld.Mountain.Model.EaseState(mountainData.State);
                
                self.Overworld.Maddy.Hide();
                //todo
                self.Overworld.Maddy.Position = new Vector3(
                    mountainData.Cursor.Length >= 1 ? mountainData.Cursor[0] : 0, 
                    mountainData.Cursor.Length >= 2 ? mountainData.Cursor[1] : 0, 
                    mountainData.Cursor.Length >= 3 ? mountainData.Cursor[2] : 0);
                //self.Overworld.ReloadMountainStuff();
            }

            // var toHeight = 0f;
            // switch ((selected as CustomChapterOption).position.displayType) {
            //     case ChapterMetadata.DisplayType.INFO:
            //         toHeight = self.GetModeHeight();
            //         break;
            //     case ChapterMetadata.DisplayType.PREVIEW:
            //         toHeight = 730;
            //         break;
            //     case ChapterMetadata.DisplayType.NONE:
            //         toHeight = 300;
            //         break;
            // }
            // self.Add(new Coroutine(toNewSize(toHeight, self)));
        } else {
            globalCurrentMountain__gross = null;
        }
    }

    private static IEnumerator toNewSize(float toHeight, OuiChapterPanel self) {
        float fromHeight = self.height;
        self.resizing = true;
        self.PlayExpandSfx(fromHeight, toHeight);
        float offset = 800f;
        
        for (var p = 0.0f; p < 1.0; p += Engine.DeltaTime * 4f) {
            yield return null;
            self.contentOffset.X = (float)(440.0 + offset * Ease.CubeIn(p));
            self.height = MathHelper.Lerp(fromHeight, toHeight, Ease.CubeOut(p * 0.5f));
        }
    }

    private static void renderMixin(ILContext ctx) {
        var cursor = new ILCursor(ctx);

        cursor.GotoNext(MoveType.Before, instr =>
            instr.Next.Next.MatchLdfld<OuiChapterPanel>("strawberries"));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(CustomRender);
        
        cursor.GotoNext(MoveType.After, instr =>
            instr.MatchLdfld<OuiChapterPanel>("selectingMode"));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(disableRender);
    }

    private static bool CustomRender(bool orig, OuiChapterPanel self) {
        if (self.options.Count == 0 || !(self.options[0] is CustomChapterOption)) {
            return orig;
        }
        
        if (positions.TryGetValue(self, out var position)) {// !storedFakeSwap.Contains(self) && 
            switch ((self.options[self.option] as CustomChapterOption).position.displayType) {
                case ChapterMetadata.DisplayType.INFO:
                    self.strawberries.Position = self.contentOffset + new Vector2(0.0f, 170f) + self.strawberriesOffset;
                    self.deaths.Position = self.contentOffset + new Vector2(0.0f, 170f) + self.deathsOffset;
                    self.heart.Position = self.contentOffset + new Vector2(0.0f, 170f) + self.heartOffset;
                    self.Components.Render();
                    break;
                case ChapterMetadata.DisplayType.PREVIEW:
                    Vector2 center = self.Position + new Vector2(self.contentOffset.X, 340f);
                    for (int index = self.options.Count - 1; index >= 0; --index)
                        self.DrawCheckpoint(center, self.options[index], index);
                    break;
                case ChapterMetadata.DisplayType.NONE: break;
            }
        }

        return false;//dont render
    }

    private static bool disableRender(bool orig, OuiChapterPanel self) {
        if (self.options.Count == 0 || !(self.options[0] is CustomChapterOption)) {
            return orig;
        }

        return true;//dont render
    }
    
    /*
    private static void mountainUpdateMixin(ILContext ctx) {
        var cursor = new ILCursor(ctx);

        cursor.GotoNext(MoveType.After, 
            instr => instr.MatchCallvirt<MapMetaMountain>("get_ShowCore"));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(customMountain);
    }

    private static bool customMountain(bool val, Overworld self) {
        var panel = self.UIs.Find(oui => (oui is OuiChapterPanel ouiPanel) && positions.ContainsKey(ouiPanel))
            as OuiChapterPanel;
        if (panel != null) {
            return positions[panel].Mountain.ShowCore;
        }
        return val;
    }
    */

    private static void changeMountainMixin(ILContext ctx) {
        var cursor = new ILCursor(ctx);
        
        cursor.GotoNext(MoveType.After, instr =>
            instr.MatchCall<MountainModel>("hasCustomSettings"));//end of the if condition
        var endCondLabel = ctx.DefineLabel(cursor.Next.Next);//the first instruction inside the if block's body
        cursor.GotoPrev(MoveType.Before, instr =>
            instr.MatchLdsfld<SaveData>("Instance"));//right before the start of the if condition
        cursor.EmitDelegate(isCustom);
        cursor.EmitBrtrue(endCondLabel);//if is custom, jump to inside the if block
    
        cursor.GotoNext(MoveType.After, instr =>
            instr.Previous?.MatchLdsfld("Celeste.MTNExt", "MountainMappings") ?? false);
        cursor.EmitDelegate(customMountain);
    }

    private static bool isCustom() {
        Logger.Log("ReallyBigHelper", $"{globalCurrentMountain__gross} exists");
        if (globalCurrentMountain__gross != null) return true;
        return false;
    }

    private static string customMountain(string oldID) {
        return Path.Combine("Maps", "SpringCollab2020/0-Lobbies/5-Grandmaster").Replace('\\', '/');
        
        return oldID;
    }
}