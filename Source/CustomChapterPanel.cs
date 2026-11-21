using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.ReallyBigHelper;

public class CustomChapterPanel {
    public static readonly Dictionary<OuiChapterPanel, CheckpointMetadata.Final> positions = new();
    public static readonly HashSet<OuiChapterPanel> storedFakeSwap = new();

    private static List<ILHook> hooks = new();

    private static MethodInfo getStrawberryWidth = typeof(OuiChapterPanel).GetMethod("getStrawberryWidth",
        BindingFlags.NonPublic | BindingFlags.Instance);
    private static MethodInfo correctInitialStrawberryOffset = typeof(OuiChapterPanel).GetMethod("correctInitialStrawberryOffset",
        BindingFlags.NonPublic | BindingFlags.Static);
    
    public static readonly string reallyBigSectionName = Random.Shared.Next() + "ReallyBigSection_";
    private static MapMetaMountain currentMountain = null;

    public static void Load() {
        On.Celeste.OuiChapterPanel.SwapRoutine += swapMixin;
        On.Celeste.OuiChapterPanel.Start += startMixin;
        On.Celeste.OuiChapterPanel.Leave += leaveMixin;
        IL.Celeste.OuiChapterPanel.Render += renderMixin;
        On.Celeste.OuiChapterPanel.GetModeHeight += getHeightMixin;
        On.Celeste.OuiChapterPanel.Update += customHeartUpdate;
        On.Celeste.OuiChapterPanel.StartRoutine += customStartRoutine;
        On.Celeste.LevelEnter.Go += customLevelEnter;
        IL.Celeste.Maddy3D.SetRunAnim += textureMixin;

        hooks.Add(new ILHook(
            typeof(OuiChapterPanel).GetMethod("orig_Update", BindingFlags.Public | BindingFlags.Instance),
            updateMixin));
        hooks.Add(new ILHook(
            typeof(OuiChapterPanel).GetMethod("orig_DrawCheckpoint", BindingFlags.NonPublic | BindingFlags.Instance),
            drawCheckpointMixin));
    }

    public static void Unload() {
        On.Celeste.OuiChapterPanel.SwapRoutine -= swapMixin;
        On.Celeste.OuiChapterPanel.Start -= startMixin;
        On.Celeste.OuiChapterPanel.Leave -= leaveMixin;
        IL.Celeste.OuiChapterPanel.Render -= renderMixin;
        On.Celeste.OuiChapterPanel.GetModeHeight -= getHeightMixin;
        On.Celeste.OuiChapterPanel.Update -= customHeartUpdate;
        On.Celeste.OuiChapterPanel.StartRoutine -= customStartRoutine;
        On.Celeste.LevelEnter.Go -= customLevelEnter;
        IL.Celeste.Maddy3D.SetRunAnim -= textureMixin;

        foreach(var hook in hooks) hook.Dispose();
    }

    private static bool canShow(AreaKey area, List<string> names, int index) {
        if (index == 0) return true;
        if (index < 0 || index >= names.Count) return false;
        return SaveData.Instance.Areas_Safe[area.ID]
            .Modes[(int)area.Mode].Checkpoints.Contains(names[index]);
    }
    //this could be done with il hooks. but why
    private static float targetHeight;
    private static IEnumerator swapMixin(On.Celeste.OuiChapterPanel.orig_SwapRoutine orig, OuiChapterPanel self) {
        lastMovingFrom = CameFrom.VERT;
        if (self.option < 0) self.option = 0;
        else if (self.option >= self.options.Count) self.option = self.options.Count - 1;
        
        if (!ReallyBigHelperModule.chapterData.ContainsKey(self.Area.SID)) {
            yield return new SwapImmediately(orig(self));
        } else {
            var fromHeight = self.height;
            targetHeight = 540;
            if (self.selectingMode ^ storedFakeSwap.Contains(self)) {
                positions.TryGetValue(self, out var pos);
                if(self.selectingMode && pos == null) 
                    pos = ReallyBigHelperModule.chapterData[self.Area.SID][(int)self.Area.Mode].Root();
                pos.transitionAmt = 1;

                var first = true;
                foreach (var chapter in pos.Chapters) {
                    if (chapter.selected || first) {
                        switch (chapter.displayType) {
                            case CheckpointMetadata.DisplayType.INFO: targetHeight = 540; break;
                            case CheckpointMetadata.DisplayType.PREVIEW: targetHeight = 730; break;
                            default: targetHeight = 300; break;
                        }

                        if (chapter.selected) break;
                    }

                    first = false;
                }
            }
            self.resizing = true;
            self.PlayExpandSfx(fromHeight, targetHeight);
            var offset = 800f;
            float p;
            for (p = 0.0f; p < 1.0; p += Engine.DeltaTime * 4f) {
                yield return null;
                self.contentOffset.X = (float)(440.0 + offset * Ease.CubeIn(p));
                //self.height = MathHelper.Lerp(fromHeight, toHeight, Ease.CubeOut(p * 0.5f));
            }
            
            if (!storedFakeSwap.Remove(self))
                self.selectingMode = !self.selectingMode;
            
            if (!self.selectingMode) {
                if (!positions.ContainsKey(self)) 
                    positions[self] = ReallyBigHelperModule.chapterData[self.Area.SID][(int)self.Area.Mode].Root();
                self.checkpoints.Clear();

                var checkpointNames = new List<string>();
                checkpointNames.Add(null);
                foreach (var checkpoint in AreaData.Get(self.Area).Mode[(int)self.Area.Mode].Checkpoints)
                    checkpointNames.Add(checkpoint.Level);
                
                foreach (var section in positions[self].Chapters) {
                    if (!SaveData.Instance.DebugMode && !SaveData.Instance.CheatMode) {
                        if (section.Chapters.Count > 0) {
                            var valid = false;
                            foreach (var id in section.allIds()) {
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
                    if (textLabel == null && section.Chapters.Count == 0 &&
                        section.id >= 0 && section.id < checkpointNames.Count) {
                        textLabel = checkpointNames[section.id];
                    }

                    self.checkpoints.Add(section.getOption(self, textLabel,
                        (section.id >= 0 && section.id < checkpointNames.Count) ? 
                            checkpointNames[section.id] : null));
                }

                self.option = self.checkpoints.Count - 1;
                for (int i = 0; i < positions[self].Chapters.Count; i++) {
                    if (positions[self].Chapters[i].selected) {
                        self.option = i;
                        break;
                    }
                }

                if (self.option >= self.options.Count) self.option = self.options.Count - 1;//todo

                for (var index = 0; index < self.checkpoints.Count - 1; ++index)
                    self.options[index].CheckpointSlideOut = 1f;
                for (var index = 0; index < self.options.Count; ++index)
                    self.options[index].SlideTowards(index, self.options.Count, true);
                
                targetHeight = self.GetModeHeight();
            }

            self.options[self.option].Pop = 1f;
            for (p = 0.0f; p < 1.0; p += Engine.DeltaTime * 4f) {
                yield return null;
                //self.height = MathHelper.Lerp(fromHeight, toHeight, Ease.CubeOut(Math.Min(1f, (float)(0.5 + p * 0.5))));
                self.contentOffset.X = (float)(440.0 + offset * (1.0 - Ease.CubeOut(p)));
            }

            self.contentOffset.X = 440f;
            //self.height = toHeight;
            self.Focused = true;
            self.resizing = false;
        }
    }

    private static bool[] getBerryList(OuiChapterPanel self, CustomCheckpointOption customOption, out int trueBerryCount) {
        List<bool[]> flagArray = new();
        //Dictionary<int, bool[]> flagArray = new();
        var mode = (int) self.Area.Mode;
        var subchapterIds = customOption.position.allIds().ToList().FindAll(id => id>=0);

        ReallyBigHelperModule.SaveData.getAreaData(self.Area.SID);
        trueBerryCount = customOption.position.Chapters.Count > self.option
            ? ReallyBigHelperModule.SaveData.getAreaData(self.Area.SID)
                .getSpecialCount(customOption.position.allIds().ToList())
            : 0;
        for(int i=0;i<subchapterIds.Count;i++) {
            bool[] list = new bool[subchapterIds[i] != 0
                ? self.Data.Mode[mode].Checkpoints[subchapterIds[i] - 1].Strawberries
                : self.Data.Mode[mode].StartStrawberries];
            flagArray.Add(list);

            for (int i2 = 0; i2 < list.Length;i2++) {
                EntityData entityData = self.Data.Mode[mode].StrawberriesByCheckpoint[subchapterIds[i], i2]; // Changed from 0 to make the polaroid berries work correctly
                foreach (EntityID strawberry in self.RealStats.Modes[mode].Strawberries) {
                    if (entityData != null && entityData.Level.Name == strawberry.Level &&
                        entityData.ID == strawberry.ID) {
                        trueBerryCount++;
                        list[i2] = true;
                    }
                }
            }
            
            //levels dont have an order we can rely on unfortunately
            // foreach (EntityID id in self.RealStats.Modes[mode].Strawberries) {
            //     EntityData strawberry = self.Data.Mode[mode].MapData.Strawberries.Find(data => data.ID == id.ID);
            //     // If it is a normal strawberry and has checkpointID assigned use the checkpointID
            //     if(strawberry != null && strawberry.Int("checkpointID") == subchapterIds[i]-1)
            //     {
            //         trueBerryCount++;
            //         continue;
            //     }
            //     
            //     // If not, find the first previous room that's a checkpoint and use that id.
            //     if(strawberry == null || strawberry.Int("checkpointID") == -1)
            //     {
            //         int levelI = self.Data.Mode[mode].MapData.Levels.FindIndex(data => data.Name == id.Level);
            //         while(levelI > 0)
            //         {
            //             LevelData data = self.Data.Mode[mode].MapData.Levels[levelI];
            //             if(data.HasCheckpoint)
            //             {
            //                 if(self.Data.Mode[mode].Checkpoints[subchapterIds[i]].Level == data.Name)
            //                 {
            //                     trueBerryCount++;
            //                 }
            //                 break;
            //             }
            //             levelI--;
            //         }
            //     }
            // }
        }
        var berryList = new bool[flagArray.Aggregate(0, (total, arr) => total + arr.Length)];
        var index = 0;
        foreach (var list in flagArray) {
            list.CopyTo(berryList, index);
            index += list.Length;
        }

        return berryList;
    }

    private static void drawCheckpointMixin(ILContext ctx) {
        var cursor = new ILCursor(ctx);
        // IL_000d: callvirt     instance class Monocle.MTexture Celeste.OuiChapterPanel::GetCheckpointPreview(valuetype Celeste.AreaKey, string)
        cursor.GotoNext(MoveType.After, instr =>
            instr.MatchCallvirt<OuiChapterPanel>("GetCheckpointPreview"));
        cursor.EmitLdarg2();
        cursor.EmitLdarg0();
        cursor.EmitDelegate(polaroidCustomPolaroid);
        
        // IL_012e: ldfld        bool Celeste.SaveData::DebugMode
        cursor.GotoNext(MoveType.After, instr => instr.MatchLdfld<SaveData>("DebugMode"));
        cursor.EmitPop();
        cursor.EmitLdcI4(1);
        
        // IL_0240: ldloc.1      // checkpointRotation
        // IL_0241: ldc.r4       1
        // IL_0246: call         valuetype [FNA]Microsoft.Xna.Framework.Vector2 Monocle.Calc::AngleToVector(float32, float32)
        cursor.GotoNext(MoveType.Before,
            instr => instr.MatchLdloc1(),
            instr => instr.MatchLdcR4(1),
            instr => instr.MatchCall("Monocle.Calc", "AngleToVector"));
        cursor.EmitLdarg2();
        cursor.EmitLdarg0();
        cursor.EmitLdloc(7);
        cursor.EmitLdloc(8);
        cursor.EmitDelegate(polaroidReplaceBerries);
        
        // IL_0292: ldfld        int32 Celeste.AreaData::CassetteCheckpointIndex
        // IL_0297: ldarg.3      // checkpointIndex
        // IL_0298: bne.un.s     IL_0300
        cursor.GotoNext(MoveType.After,
            instr => instr.MatchLdfld<AreaData>("CassetteCheckpointIndex"),
            instr => instr.MatchLdarg3(),
            instr => instr.MatchBneUn(out var _));
        cursor.GotoPrev(MoveType.Before, instr => instr.MatchBneUn(out var _));
        cursor.EmitPop();
        cursor.EmitPop();
        cursor.EmitLdcI4(0);
        cursor.EmitLdcI4(1);
    }

    private static MTexture polaroidCustomPolaroid(MTexture orig, OuiChapterPanel.Option option, OuiChapterPanel self) {
        if (!(option is CustomCheckpointOption customOption)) return orig;
        return MTN.Checkpoints[Path.Join(self.Area.SID, customOption.position.text).Replace('\\', '/')];
    }

    private static void polaroidReplaceBerries(OuiChapterPanel.Option option, OuiChapterPanel self,
        ref int length, ref bool[] strawberryArray) {
        if (!(option is CustomCheckpointOption customOption)) return;
        strawberryArray = getBerryList(self, customOption, out int _);
        length = strawberryArray.Length;
    }

    private static void startMixin(On.Celeste.OuiChapterPanel.orig_Start orig, OuiChapterPanel self,
        string id) {
        if (id == null || id != reallyBigSectionName) {
            orig(self, id);
            return;
        }

        var next = (self.options[self.option] as CustomCheckpointOption).position;
        if (next.Chapters.Count < 1) return;

        foreach (var group in positions[self].Chapters) group.selected = false;
        positions[self] = next;
        positions[self].selected = true;
        storedFakeSwap.Add(self);//to counteract the swapping back
        Audio.Play("event:/ui/world_map/chapter/level_select");
        self.Swap();
    }

    private static IEnumerator leaveMixin(On.Celeste.OuiChapterPanel.orig_Leave orig, OuiChapterPanel self, Oui next) {
        positions.Remove(self);
        return orig(self, next);
    }

    private enum CameFrom{ LEFT, RIGHT, VERT }
    private static CameFrom lastMovingFrom = CameFrom.VERT;
    private static void onLeftOrRight(OuiChapterPanel self, CameFrom dir) {
        if (positions.TryGetValue(self, out var position)) {
            position.transitionAmt = 1;
            targetHeight = self.GetModeHeight();
            lastMovingFrom = dir;
        }
    }
    private static void updateMixin(ILContext ctx) {
        var cursor = new ILCursor(ctx);

        cursor.GotoNext(MoveType.Before, instr =>
            instr.MatchCall<Entity>("Update"));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(updateMountain);
        
        cursor.GotoNext(MoveType.After, instr =>
            instr.MatchLdstr("event:/ui/world_map/chapter/tab_roll_left"));
        cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt<OuiChapterPanel>("set_option"));
        cursor.EmitLdarg0();
        cursor.EmitLdcI4(0);
        cursor.EmitDelegate(onLeftOrRight);
        cursor.GotoNext(MoveType.After, instr =>
            instr.MatchLdstr("event:/ui/world_map/chapter/tab_roll_right"));
        cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt<OuiChapterPanel>("set_option"));
        cursor.EmitLdarg0();
        cursor.EmitLdcI4(1);
        cursor.EmitDelegate(onLeftOrRight);
        
        cursor.GotoNext(MoveType.After, instr =>
            instr.MatchLdstr("event:/ui/world_map/chapter/checkpoint_back"));
        cursor.GotoNext(MoveType.Before, instr =>
            instr.MatchCallvirt<OuiChapterPanel>("Swap"));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(interceptBack);
    }
    private static void interceptBack(OuiChapterPanel self) {
        if (positions.ContainsKey(self) && positions[self].parent != null){
            storedFakeSwap.Add(self); //to couteract the swapping back
            foreach (var group in positions[self].Chapters) group.selected = false;
            positions[self].Chapters[self.option].selected = true;
            positions[self] = positions[self].parent;
        }
    }
    private static void updateMountain(OuiChapterPanel self) {
        if (positions.TryGetValue(self, out var position)) {
            if (storedFakeSwap.Contains(self)) return;
            var selected = self.options[self.option];
            
            var mountainData = (selected as CustomCheckpointOption)?.position?.GetMountain();
            if (mountainData != null) {
                self.Overworld.Mountain.EaseCamera(self.Area.ID,
                    self.EnteringChapter ? mountainData.Zoom.Convert() : mountainData.Select.Convert(),
                    null, true);
                self.Overworld.Mountain.Model.EaseState(mountainData.State);

                if (mountainData != currentMountain) {
                    self.Overworld.Maddy.Hide();
                    currentMountain = mountainData;
                }else if(self.Overworld.Maddy.alpha == 0) {
                    self.Overworld.Maddy.Position = new Vector3(
                        mountainData.Cursor.Length >= 1 ? mountainData.Cursor[0] : 0, 
                        mountainData.Cursor.Length >= 2 ? mountainData.Cursor[1] : 0, 
                        mountainData.Cursor.Length >= 3 ? mountainData.Cursor[2] : 0);
                    self.Overworld.Maddy.Running();
                }
            }
            else {
                currentMountain = null;
                self.Overworld.Maddy.Hide();
            }
            
            //--

            if (position.transitionAmt!=0) { //!self.selectingMode && !self.resizing && 
                self.height = MathHelper.Lerp(self.height, targetHeight,
                    Ease.CubeOut((1 - position.transitionAmt)*0.5f));
                position.transitionAmt -= Engine.DeltaTime;
            }

            if (position.transitionAmt < 0.001) {
                position.transitionAmt = 0;
            }
            
            //--
            
            // if(self.options[self.option] is CustomCheckpointOption customOption && customOption.position != null)
            //     updateBerryList(self, self.options[self.option] as CustomCheckpointOption);
        }
        else {
            currentMountain = null;
        }
    }

    private static void textureMixin(ILContext ctx) {
        var cursor = new ILCursor(ctx);

        cursor.GotoNext(MoveType.Before, instr => instr.MatchStloc0());
        cursor.EmitDelegate(customMarkerTexture);
    }

    private static string customMarkerTexture(string dfault) {
        return currentMountain?.MarkerTexture ?? dfault;
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

    private static Sprite customHeartSprite1;
    private static Sprite customHeartSprite2;
    private static string customHeartID1;
    private static string customHeartID2;

    private static void renderInsides(CustomCheckpointOption option, float transitionAmt, OuiChapterPanel self,
        ref Sprite heartSprite, ref string heartID) {
        switch (option.position.displayType) {
            case CheckpointMetadata.DisplayType.INFO:
                self.deaths.Visible = true;
                var xOffs = 800 * Ease.CubeIn(transitionAmt);

                var drawnHeart = false;
                var shouldDrawHeartSprite = false;
                var heartPos = self.contentOffset + new Vector2(xOffs, 170f) + self.heartOffset;
                switch (option.position.showHeartWhen) {
                    case "default":
                        var customHeart = false;
                        if (ReallyBigHelperModule.hasMultiHeartCached &&
                            option.position.MHH_HeartID != null &&
                            option.position.MHH_HeartDisplayPath != null) {
                            customHeart = true;
                            if (MHHFeatures.heartIsCollected(AreaData.Get(self.Area), option.position.MHH_HeartID)) {
                                if (heartSprite == null || heartID != option.position.MHH_HeartID) {
                                    heartSprite = GFX.GuiSpriteBank.Create(option.position.MHH_HeartDisplayPath);
                                    heartID = option.position.MHH_HeartID;
                                }

                                shouldDrawHeartSprite = true;
                            }
                        }
                        
                        if (!customHeart) {
                            self.heart.Position = heartPos;
                            heartSprite = null;
                            heartID = null;
                            drawnHeart = true;
                        }else{
                            self.heart.Position = new Vector2(9999, 9999);
                        }
                        break;
                    case "moonberry":
                        self.heart.Position = new Vector2(9999, 9999);
                        drawnHeart = ReallyBigHelperModule.SaveData.getAreaData(self.Area.SID)
                            .hasBerry(ReallyBigHelperSaveData.SpecialBerryType.MOONBERRY,
                                option.position.allIds().ToList().FindAll(id => id >= 0));
                        if (drawnHeart) {
                            if (heartSprite == null || heartID != option.position.heartTexture) {
                                heartSprite = GFX.GuiSpriteBank.Create(option.position.heartTexture);
                                heartID = option.position.heartTexture;
                            }

                            shouldDrawHeartSprite = true;
                        }
                        break;
                }
                if (shouldDrawHeartSprite) {
                    if(!heartSprite.Animating) heartSprite.Play("spin");
                    heartSprite.Position = heartPos + self.Position;
                    heartSprite.Render();
                    drawnHeart = true;
                }
                if (drawnHeart) {
                    self.strawberriesOffset = new Vector2(120f, -40f);
                    self.deathsOffset = new Vector2(120f, 40.0f);
                }else {
                    self.strawberriesOffset = new Vector2(0f, -40f);
                    self.deathsOffset = new Vector2(0f, 40.0f);
                }

                self.strawberries.Position =
                    self.contentOffset + new Vector2(xOffs, 170f) + self.strawberriesOffset;
                self.deaths.Position = self.contentOffset + new Vector2(xOffs, 170f) + self.deathsOffset;

                var realStrawbCount = self.strawberries.Amount;
                var realStrawbOutOf = self.strawberries.OutOf;
                var realDeathCount = self.deaths.Amount;

                var berries = getBerryList(self, option, out int berryCount);
                self.strawberries.Amount = berryCount;
                self.strawberries.OutOf = berries.Length;
                self.deaths.Amount = ReallyBigHelperModule.SaveData.getAreaData(self.Area.SID)
                    .getDeaths(option.position.allIds().ToList().FindAll(id => id>=0));
                self.deaths.flashTimer = 0;
                self.Components.Render();

                self.strawberries.Amount = realStrawbCount;
                self.strawberries.OutOf = realStrawbOutOf;
                self.deaths.Amount = realDeathCount;

                break;
            case CheckpointMetadata.DisplayType.PREVIEW:
                self.deaths.Visible = false;
                option.shouldDrawPreview = true;
                Vector2 center = self.Position + new Vector2(self.contentOffset.X +
                    800 * Ease.CubeIn(transitionAmt), 340f);
                self.DrawCheckpoint(center, option, self.option);
                option.shouldDrawPreview = false;
                break;
            case CheckpointMetadata.DisplayType.NONE: 
                self.deaths.Visible = false;
                break;
        }
    }
    private static bool CustomRender(bool orig, OuiChapterPanel self) {
        if (self.options.Count == 0 || !(self.options[0] is CustomCheckpointOption)) {
            self.strawberries.Amount = self.DisplayedStats.Modes[(int) self.Area.Mode].TotalStrawberries;
            self.strawberries.OutOf = self.Data.Mode[(int) self.Area.Mode].TotalStrawberries;
            return orig;
        }
        
        if (positions.TryGetValue(self, out var position)) {// !storedFakeSwap.Contains(self) && 
            var selectedOption = self.options[self.option] as CustomCheckpointOption;
            CustomCheckpointOption prevOption = null;
            if(lastMovingFrom != CameFrom.VERT &&
               self.option + (lastMovingFrom==CameFrom.LEFT ? 1 : -1)>=0 && 
               self.option + (lastMovingFrom==CameFrom.LEFT ? 1 : -1)<self.options.Count)
                prevOption = self.options[self.option + (lastMovingFrom==CameFrom.LEFT ? 1 : -1)] as CustomCheckpointOption;
            if(lastMovingFrom == CameFrom.VERT)
                prevOption = position.option as CustomCheckpointOption;
            if(prevOption!=null) 
                if(self.option%2==0)
                    renderInsides(prevOption, Math.Clamp(1 - position.transitionAmt, 0, 0.25f) * 4, self, 
                        ref customHeartSprite1, ref customHeartID1);
                else renderInsides(prevOption, Math.Clamp(1 - position.transitionAmt, 0, 0.25f) * 4, self, 
                        ref customHeartSprite2, ref customHeartID2);
            if(selectedOption!=null) 
                if(self.option%2==0)
                    renderInsides(selectedOption, (Math.Clamp(position.transitionAmt, 0.75f, 1) - 0.75f)*4, self, 
                        ref customHeartSprite2, ref customHeartID2);
                else renderInsides(selectedOption, (Math.Clamp(position.transitionAmt, 0.75f, 1) - 0.75f)*4, self, 
                        ref customHeartSprite1, ref customHeartID1);
        }

        return false;//dont render
    }

    private static void customHeartUpdate(On.Celeste.OuiChapterPanel.orig_Update orig, OuiChapterPanel self) {
        if (self.initialized) {
            if(customHeartSprite1!=null) customHeartSprite1.Update();
            if(customHeartSprite2!=null) customHeartSprite2.Update();
        }
        orig(self);
    }

    public static CheckpointMetadata.Final EnteringPosition;
    private static IEnumerator customStartRoutine(On.Celeste.OuiChapterPanel.orig_StartRoutine orig, OuiChapterPanel self, string checkpoint) {
        if (positions.TryGetValue(self, out var position) && position.Chapters.Count>0) {
            EnteringPosition = position.Chapters.Find(final => final.option != null && final.option.CheckpointLevelName == checkpoint);
        } else {
            EnteringPosition = null;
        }

        yield return new SwapImmediately(orig(self, checkpoint));
    }

    private static void customLevelEnter(On.Celeste.LevelEnter.orig_Go orig, Session session, bool fromSaveData) {
        while (EnteringPosition != null) {
            foreach(var flag in EnteringPosition.flags)
                session.SetFlag(flag);
            EnteringPosition = EnteringPosition.parent;
        }
        EnteringPosition = null;

        orig(session, fromSaveData);
    }

    private static bool disableRender(bool orig, OuiChapterPanel self) {
        if (self.options.Count == 0 || !(self.options[0] is CustomCheckpointOption)) {
            return orig;
        }

        return true;//dont render
    }

    private static int getHeightMixin(On.Celeste.OuiChapterPanel.orig_GetModeHeight orig, OuiChapterPanel self) {
        if (positions.ContainsKey(self)) {
            if (self.option >= 0 && self.option < self.options.Count) {
                switch ((self.options[self.option] as CustomCheckpointOption)?.position.displayType) {
                    case CheckpointMetadata.DisplayType.INFO: return 540;
                    case CheckpointMetadata.DisplayType.PREVIEW: return 730;
                    case CheckpointMetadata.DisplayType.NONE: return 300;
                }
            }

            return 540;
        }

        return orig(self);
    }
}