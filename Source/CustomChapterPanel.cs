using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.ReallyBigHelper;

public class CustomChapterPanel {
    public static readonly Dictionary<OuiChapterPanel, ChapterMetadata.Final> positions = new();
    public static readonly HashSet<OuiChapterPanel> storedFakeSwap = new();

    private static ILHook hookOrigUpdate;

    private static MethodInfo getStrawberryWidth = typeof(OuiChapterPanel).GetMethod("getStrawberryWidth",
        BindingFlags.NonPublic | BindingFlags.Instance);
    private static MethodInfo correctInitialStrawberryOffset = typeof(OuiChapterPanel).GetMethod("correctInitialStrawberryOffset",
        BindingFlags.NonPublic | BindingFlags.Static);
    
    public static readonly string reallyBigSectionName = Random.Shared.Next() + "ReallyBigSection_";

    public static void Load() {
        On.Celeste.OuiChapterPanel.SwapRoutine += swapMixin;
        On.Celeste.OuiChapterPanel.DrawCheckpoint += drawCheckpointMixin;
        On.Celeste.OuiChapterPanel.Start += startMixin;
        On.Celeste.OuiChapterPanel.Leave += leaveMixin;
        IL.Celeste.OuiChapterPanel.Render += renderMixin;
        On.Celeste.OuiChapterPanel.GetModeHeight += getHeightMixin;
        On.Celeste.OuiChapterPanel.Update += customHeartUpdate;

        hookOrigUpdate = new ILHook(typeof(OuiChapterPanel).GetMethod("orig_Update", BindingFlags.Public|BindingFlags.Instance), 
            updateMixin);
    }

    public static void Unload() {
        On.Celeste.OuiChapterPanel.SwapRoutine -= swapMixin;
        On.Celeste.OuiChapterPanel.DrawCheckpoint -= drawCheckpointMixin;
        On.Celeste.OuiChapterPanel.Start -= startMixin;
        On.Celeste.OuiChapterPanel.Leave -= leaveMixin;
        IL.Celeste.OuiChapterPanel.Render -= renderMixin;
        On.Celeste.OuiChapterPanel.GetModeHeight -= getHeightMixin;
        On.Celeste.OuiChapterPanel.Update -= customHeartUpdate;

        hookOrigUpdate?.Dispose();
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
        if (!ReallyBigHelperModule.chapterData.ContainsKey(self.Area.SID)) {
            yield return new SwapImmediately(orig(self));
        } else {
            var fromHeight = self.height;
            targetHeight = 540;
            if (self.selectingMode ^ storedFakeSwap.Contains(self)) {
                positions.TryGetValue(self, out var pos);
                if(self.selectingMode && pos == null) 
                    pos = ReallyBigHelperModule.chapterData[self.Area.SID][(int)self.Area.Mode];
                pos.transitionAmt = 1;

                var first = true;
                foreach (var chapter in pos.Chapters) {
                    if (chapter.selected || first) {
                        switch (chapter.displayType) {
                            case ChapterMetadata.DisplayType.INFO: targetHeight = 540; break;
                            case ChapterMetadata.DisplayType.PREVIEW: targetHeight = 730; break;
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
                    positions[self] = ReallyBigHelperModule.chapterData[self.Area.SID][(int)self.Area.Mode];
                self.checkpoints.Clear();

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

                for (var index = 0; index < self.checkpoints.Count - 1; ++index)
                    self.options[index].CheckpointSlideOut = 1f;
                foreach (var group in positions[self].Chapters) group.selected = false;
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

    private static bool[] getBerryList(OuiChapterPanel self, CustomChapterOption customOption) {
        List<bool[]> flagArray = new();
        //Dictionary<int, bool[]> flagArray = new();
        var mode = (int) self.Area.Mode;
        var subchapterIds = customOption.position.childIds().FindAll(id => id>=0);
        
        for(int i=0;i<subchapterIds.Count;i++) {
            bool[] list = new bool[subchapterIds[i] != 0
                ? self.Data.Mode[mode].Checkpoints[subchapterIds[i] - 1].Strawberries
                : self.Data.Mode[mode].StartStrawberries];
            flagArray.Add(list);

            for (int i2 = 0; i2 < list.Length;i2++) {
                foreach (EntityID strawberry in self.RealStats.Modes[mode].Strawberries) {
                    EntityData entityData = self.Data.Mode[mode].StrawberriesByCheckpoint[subchapterIds[i], 0];
                    if (entityData != null && entityData.Level.Name == strawberry.Level &&
                        entityData.ID == strawberry.ID) {
                        list[i2] = true;
                    }
                }
            }

        }
        var berryList = new bool[flagArray.Aggregate(0, (total, arr) => total + arr.Length)];
        var index = 0;
        foreach (var list in flagArray) {
            list.CopyTo(berryList, index);
            index += list.Length;
        }

        return berryList;
    }

    private static bool[] updateBerryList(OuiChapterPanel self, CustomChapterOption customOption) {
        var berryList = getBerryList(self, customOption);
        
        if (!storedFakeSwap.Contains(self)) {
            self.strawberries.Amount = berryList.Count(b => b);
            self.strawberries.OutOf = berryList.Length;
        }

        return berryList;
    }

    private static void drawCheckpointMixin(On.Celeste.OuiChapterPanel.orig_DrawCheckpoint orig, OuiChapterPanel self,
        Vector2 center, object option, int checkpointIndex) {
        if (option is CustomChapterOption customOption) {
            if (!customOption.shouldDrawPreview) return;
            
            var berryList = updateBerryList(self, customOption);

            var mode = (int)self.Area.Mode;
            MTexture checkpointPreview = MTN.Checkpoints[Path.Join(self.Area.SID, customOption.position.text).Replace('\\', '/')];
            MTexture checkpoint = MTN.Checkpoints["polaroid"];
            float checkpointRotation = customOption.CheckpointRotation;
            Vector2 position1 = center + customOption.CheckpointOffset +
                                Vector2.UnitX * 800f * Ease.CubeIn(customOption.CheckpointSlideOut);
            checkpoint.DrawCentered(position1, Color.White, 0.75f, checkpointRotation);
            if (checkpointPreview != null) {
                Vector2 scale = Vector2.One * 0.75f;
                if (SaveData.Instance.Assists.MirrorMode)
                    scale.X = -scale.X;
                scale *= 720f / checkpointPreview.Width;
                HiresRenderer.EndRender();
                HiresRenderer.BeginRender(BlendState.AlphaBlend, SamplerState.PointClamp);
                checkpointPreview.DrawCentered(position1, Color.White, scale, checkpointRotation);
                HiresRenderer.EndRender();
                HiresRenderer.BeginRender();
            }

            if (!self.RealStats.Modes[mode].Completed && !SaveData.Instance.CheatMode &&
                !SaveData.Instance.DebugMode) return;

            Vector2 vec = new Vector2(300f, 220f);
            Vector2 vector2 = position1 + vec.Rotate(checkpointRotation);
            
            Vector2 vector = Calc.AngleToVector(checkpointRotation, 1f);
            Vector2 position3 = (Vector2)correctInitialStrawberryOffset.Invoke(null, new object[] {
                vector2 - vector * berryList.Length *
                (float)getStrawberryWidth.Invoke(self,
                    new object[] { 44f, berryList.Length, checkpointIndex }),
                vector
            });
            if (self.Data.CassetteCheckpointIndex == checkpointIndex) { //todo: redo
                Vector2 position4 = position3 - vector * 60f;
                if (self.RealStats.Cassette)
                    MTN.Journal["cassette"].DrawCentered(position4, Color.White, 1f, checkpointRotation);
                else
                    MTN.Journal["cassette_outline"]
                        .DrawCentered(position4, Color.DarkGray, 1f, checkpointRotation);
            }

            MTexture berryTexture = GFX.Gui["collectables/strawberry"];
            for (int i = 0; i < berryList.Length; ++i) {
                berryTexture.DrawCentered(position3, berryList[i] ? Color.White : Color.Black * 0.3f, 0.5f,
                    checkpointRotation);
                position3 += vector * (float)getStrawberryWidth.Invoke(self,
                    new object[] { 44f, berryList.Length, checkpointIndex });
            }

            return;
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
            if (mountainData != null) {
                self.Overworld.Mountain.EaseCamera(self.Area.ID,
                    self.EnteringChapter ? mountainData.Zoom.Convert() : mountainData.Select.Convert(),
                    null, true);
                self.Overworld.Mountain.Model.EaseState(mountainData.State);
                
                self.Overworld.Maddy.Position = new Vector3(
                    mountainData.Cursor.Length >= 1 ? mountainData.Cursor[0] : 0, 
                    mountainData.Cursor.Length >= 2 ? mountainData.Cursor[1] : 0, 
                    mountainData.Cursor.Length >= 3 ? mountainData.Cursor[2] : 0);
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
            
            if(self.options[self.option] is CustomChapterOption customOption && customOption.position != null)
                updateBerryList(self, self.options[self.option] as CustomChapterOption);
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

    private static Sprite customHeartSprite;
    private static string customHeartID;
    private static bool CustomRender(bool orig, OuiChapterPanel self) {
        if (self.options.Count == 0 || !(self.options[0] is CustomChapterOption)) {
            return orig;
        }
        
        if (positions.TryGetValue(self, out var position)) {// !storedFakeSwap.Contains(self) && 
            var selectedOption = self.options[self.option] as CustomChapterOption;
            CustomChapterOption prevOption = null;
            if(lastMovingFrom != CameFrom.VERT &&
               self.option + (lastMovingFrom==CameFrom.LEFT ? 1 : -1)>=0 && 
               self.option + (lastMovingFrom==CameFrom.LEFT ? 1 : -1)<self.options.Count)
                prevOption = self.options[self.option + (lastMovingFrom==CameFrom.LEFT ? 1 : -1)] as CustomChapterOption;
            if(lastMovingFrom == CameFrom.VERT)
                prevOption = position.option as CustomChapterOption;
            var renderPrevPolaroid = false;
            switch (prevOption?.position.displayType) {
                case ChapterMetadata.DisplayType.INFO:
                    var xOffs = 800 * Ease.CubeIn(Math.Clamp(1-position.transitionAmt, 0, 0.25f)*4);
                    if (lastMovingFrom == CameFrom.VERT) xOffs = position.transitionAmt>0.92?0:800;
                    
                    var customHeart = false;
                    var drawnHeart = false;
                    var heartPos = self.contentOffset + new Vector2(xOffs, 170f) + self.heartOffset;
                    if (ReallyBigHelperModule.hasMultiheart && 
                        prevOption.position.MHH_HeartDisplayPath != null) {
                        customHeart = true;
                        if (MHHFeatures.heartIsCollected(AreaData.Get(self.Area), prevOption.position.MHH_HeartID)) {
                            if (customHeartSprite == null || customHeartID != prevOption.position.MHH_HeartDisplayPath) {
                                customHeartSprite = GFX.GuiSpriteBank.Create(prevOption.position.MHH_HeartDisplayPath);
                                customHeartID = prevOption.position.MHH_HeartDisplayPath;
                                customHeartSprite.Play("spin");
                            }

                            customHeartSprite.Position = heartPos + self.Position;
                            customHeartSprite.Render();
                            drawnHeart = true;
                        }
                    }

                    if (!customHeart) {
                        self.heart.Position = heartPos;
                        customHeartSprite = null;
                        customHeartID = null;
                        drawnHeart = true;
                    } else {
                        self.heart.Position = new Vector2(9999, 9999);
                    }

                    if (drawnHeart) {
                        self.strawberriesOffset = new Vector2(120f, (self.deaths.Visible||true) ? -40f : 0.0f);
                    } else {
                        self.strawberriesOffset = new Vector2(0f, (self.deaths.Visible||true) ? -40f : 0.0f);
                    }

                    self.strawberries.Position = self.contentOffset + new Vector2(xOffs, 170f+40f) + self.strawberriesOffset;
                    //self.deaths.Position = self.contentOffset + new Vector2(0.0f, 170f) + self.deathsOffset;
                    self.deaths.Position = new Vector2(9999, 9999);
                    
                    var realStrawbCount = self.strawberries.Amount;
                    var realStrawbOutOf = self.strawberries.OutOf;
                    var berries = getBerryList(self, prevOption);
                    self.strawberries.Amount = berries.Count(b => b);
                    self.strawberries.OutOf = berries.Length;
                    self.Components.Render();
                    self.strawberries.Amount = realStrawbCount;
                    self.strawberries.OutOf = realStrawbOutOf;
                    break;
                case ChapterMetadata.DisplayType.PREVIEW:
                    renderPrevPolaroid = true;
                    break;
                case ChapterMetadata.DisplayType.NONE: break;
            }

            switch (selectedOption?.position.displayType) {
                case ChapterMetadata.DisplayType.INFO:
                    var xOffs = 800 * Ease.CubeIn((Math.Clamp(position.transitionAmt, 0.75f, 1) - 0.75f)*4);
                    var heartPos = self.contentOffset + new Vector2(xOffs, 170f) + self.heartOffset;
                    
                    var customHeart = false;
                    var drawnHeart = false;
                    if (ReallyBigHelperModule.hasMultiheart && 
                        selectedOption.position.displayType == ChapterMetadata.DisplayType.INFO &&
                        selectedOption.position.MHH_HeartDisplayPath != null) {
                        customHeart = true;
                        if (MHHFeatures.heartIsCollected(AreaData.Get(self.Area), selectedOption.position.MHH_HeartID)) {
                            if (customHeartSprite == null || customHeartID != selectedOption.position.MHH_HeartDisplayPath) {
                                customHeartSprite = GFX.GuiSpriteBank.Create(selectedOption.position.MHH_HeartDisplayPath);
                                customHeartID = selectedOption.position.MHH_HeartDisplayPath;
                                customHeartSprite.Play("spin");
                            }

                            customHeartSprite.Position = heartPos + self.Position;
                            customHeartSprite.Render();
                            drawnHeart = true;
                        }
                    }

                    if (!customHeart) {
                        self.heart.Position = heartPos;
                        customHeartSprite = null;
                        customHeartID = null;
                        drawnHeart = true;
                    } else {
                        self.heart.Position = new Vector2(9999, 9999);
                    }
                    
                    if (drawnHeart) {
                        self.strawberriesOffset = new Vector2(120f, (self.deaths.Visible||true) ? -40f : 0.0f);
                    } else {
                        self.strawberriesOffset = new Vector2(0f, (self.deaths.Visible||true) ? -40f : 0.0f);
                    }

                    self.strawberries.Position = self.contentOffset + new Vector2(xOffs, 170f+40f) + self.strawberriesOffset;
                    //self.deaths.Position = self.contentOffset + new Vector2(0.0f, 170f) + self.deathsOffset;
                    self.deaths.Position = new Vector2(9999, 9999);
                    
                    self.Components.Render();
                    break;
                case ChapterMetadata.DisplayType.PREVIEW:
                    selectedOption.shouldDrawPreview = true;
                    Vector2 center = self.Position + new Vector2(self.contentOffset.X +
                        800 * Ease.CubeIn(Math.Clamp(position.transitionAmt-0.75f, 0, 0.25f) * 4), 340f);
                    self.DrawCheckpoint(center, selectedOption, self.option);
                    selectedOption.shouldDrawPreview = false;
                    break;
                case ChapterMetadata.DisplayType.NONE: break;
            }

            if (renderPrevPolaroid) {
                prevOption.shouldDrawPreview = true;
                var center = self.Position + new Vector2(self.contentOffset.X +
                    800 * Ease.CubeIn(Math.Clamp(1-position.transitionAmt, 0, 0.25f) * 4), 340f);
                self.DrawCheckpoint(center, prevOption, 
                    self.option+(lastMovingFrom==CameFrom.LEFT ? 1 : -1));
                prevOption.shouldDrawPreview = false;
            }
        }

        return false;//dont render
    }

    private static void customHeartUpdate(On.Celeste.OuiChapterPanel.orig_Update orig, OuiChapterPanel self) {
        if (self.initialized && customHeartSprite!=null) customHeartSprite.Update();
        orig(self);
    }

    private static bool disableRender(bool orig, OuiChapterPanel self) {
        if (self.options.Count == 0 || !(self.options[0] is CustomChapterOption)) {
            return orig;
        }

        return true;//dont render
    }

    private static int getHeightMixin(On.Celeste.OuiChapterPanel.orig_GetModeHeight orig, OuiChapterPanel self) {
        if (positions.TryGetValue(self, out var position)) {
            if (self.option >= 0 && self.option < self.options.Count) {
                switch ((self.options[self.option] as CustomChapterOption)?.position.displayType) {
                    case ChapterMetadata.DisplayType.INFO: return 540;
                    case ChapterMetadata.DisplayType.PREVIEW: return 730;
                    case ChapterMetadata.DisplayType.NONE: return 300;
                }
            }

            return 540;
        }

        return orig(self);
    }

}