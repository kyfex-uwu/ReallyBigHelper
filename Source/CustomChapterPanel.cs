using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.ReallyBigHelper;

public class CustomChapterPanel {
    private static readonly Dictionary<OuiChapterPanel, ChapterMetadata.Final> positions = new();

    private static ILHook hookOrigUpdate;

    private static readonly string reallyBigSectionName = Random.Shared.Next() + "ReallyBigSection_";

    public static void Load() {
        On.Celeste.OuiChapterPanel.SwapRoutine += swapMixin;
        On.Celeste.OuiChapterPanel.DrawCheckpoint += drawCheckpointMixin;
        On.Celeste.OuiChapterPanel.Start += startMixin;
        On.Celeste.OuiChapterPanel.Leave += leaveMixin;

        hookOrigUpdate = new ILHook(typeof(OuiChapterPanel).GetMethod("orig_Update"), updateMixin);
    }

    public static void Unload() {
        On.Celeste.OuiChapterPanel.SwapRoutine -= swapMixin;
        On.Celeste.OuiChapterPanel.DrawCheckpoint -= drawCheckpointMixin;
        On.Celeste.OuiChapterPanel.Start -= startMixin;
        On.Celeste.OuiChapterPanel.Leave -= leaveMixin;

        hookOrigUpdate?.Dispose();
    }

    private static IEnumerator swapMixin(On.Celeste.OuiChapterPanel.orig_SwapRoutine orig, OuiChapterPanel self) {
        if (!ReallyBigHelperModule.chapterData.ContainsKey(self.Area.SID) || !self.selectingMode) {
            yield return orig(self);
        } else {
            var fromHeight = self.height;
            var toHeight = self.selectingMode ? 730 : self.GetModeHeight();
            self.resizing = true;
            self.PlayExpandSfx(fromHeight, toHeight);
            var offset = 800f;
            float p;
            for (p = 0.0f; p < 1.0; p += Engine.DeltaTime * 4f) {
                yield return null;
                self.contentOffset.X = (float)(440.0 + offset * Ease.CubeIn(p));
                self.height = MathHelper.Lerp(fromHeight, toHeight, Ease.CubeOut(p * 0.5f));
            }

            self.selectingMode = !self.selectingMode;
            if (!self.selectingMode) {
                if (!positions.ContainsKey(self)) positions[self] = ReallyBigHelperModule.chapterData[self.Area.SID];
                self.checkpoints.Clear();

                var checkpointNames = new List<string>();
                checkpointNames.Add(null);
                foreach (var checkpoint in AreaData.Get(self.Area).Mode[(int)self.Area.Mode].Checkpoints)
                    checkpointNames.Add(checkpoint.Level);

                foreach (var section in positions[self].Chapters) {
                    var checkpointLevelName = reallyBigSectionName + section.text;
                    if (section.Chapters.Count == 0 &&
                        section.id >= 0 && section.id < checkpointNames.Count)
                        checkpointLevelName = checkpointNames[section.id];
                    self.checkpoints.Add(new CustomChapterOption {
                        Bg = section.tab,
                        Icon = section.icon,
                        BgColor = section.tabColor,
                        FgColor = section.iconColor,
                        CheckpointLevelName = section.Chapters.Count == 0
                            ? checkpointLevelName
                            : reallyBigSectionName + section.text,
                        CheckpointRotation = Calc.Random.Choose(-1, 1) * Calc.Random.Range(0.05f, 0.2f),
                        CheckpointOffset = new Vector2(Calc.Random.Range(-16, 16), Calc.Random.Range(-16, 16)),
                        Large = false,
                        Siblings = positions[self].Chapters.Count
                    });
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
        if (true) return;
        var option = _option as OuiChapterPanel.Option;

        // MTexture checkpointPreview = self.GetCheckpointPreview(this.Area, option.CheckpointLevelName);
        // MTexture checkpoint = MTN.Checkpoints["polaroid"];
        // float checkpointRotation = option.CheckpointRotation;
        // Vector2 position1 = center + option.CheckpointOffset + Vector2.UnitX * 800f * Ease.CubeIn(option.CheckpointSlideOut);
        // Vector2 position2 = position1;
        // Color white = Color.White;
        // double rotation = (double) checkpointRotation;
        // checkpoint.DrawCentered(position2, white, 0.75f, (float) rotation);
        // MTexture mtexture = GFX.Gui["collectables/strawberry"];
        // if (checkpointPreview != null)
        // {
        //   Vector2 scale = Vector2.One * 0.75f;
        //   if (SaveData.Instance.Assists.MirrorMode)
        //     scale.X = -scale.X;
        //   scale *= 720f / (float) checkpointPreview.Width;
        //   HiresRenderer.EndRender();
        //   HiresRenderer.BeginRender(BlendState.AlphaBlend, SamplerState.PointClamp);
        //   checkpointPreview.DrawCentered(position1, Color.White, scale, checkpointRotation);
        //   HiresRenderer.EndRender();
        //   HiresRenderer.BeginRender();
        // }
        // int mode = (int) this.Area.Mode;
        // if (!this.RealStats.Modes[mode].Completed && !SaveData.Instance.CheatMode && !SaveData.Instance.DebugMode)
        //   return;
        // Vector2 vec = new Vector2(300f, 220f);
        // Vector2 vector2 = position1 + vec.Rotate(checkpointRotation);
        // int length = checkpointIndex != 0 ? this.Data.Mode[mode].Checkpoints[checkpointIndex - 1].Strawberries : this.Data.Mode[mode].StartStrawberries;
        // bool[] flagArray = new bool[length];
        // foreach (EntityID strawberry in this.RealStats.Modes[mode].Strawberries)
        // {
        //   for (int index = 0; index < length; ++index)
        //   {
        //     EntityData entityData = this.Data.Mode[mode].StrawberriesByCheckpoint[checkpointIndex, index];
        //     if (entityData != null && entityData.Level.Name == strawberry.Level && entityData.ID == strawberry.ID)
        //       flagArray[index] = true;
        //   }
        // }
        // Vector2 vector = Calc.AngleToVector(checkpointRotation, 1f);
        // Vector2 position3 = OuiChapterPanel.correctInitialStrawberryOffset(vector2 - vector * (float) length * this.getStrawberryWidth(44f, flagArray.Length, checkpointIndex), vector);
        // if (this.Area.Mode == AreaMode.Normal && this.Data.CassetteCheckpointIndex == checkpointIndex)
        // {
        //   Vector2 position4 = position3 - vector * 60f;
        //   if (this.RealStats.Cassette)
        //     MTN.Journal["cassette"].DrawCentered(position4, Color.White, 1f, checkpointRotation);
        //   else
        //     MTN.Journal["cassette_outline"].DrawCentered(position4, Color.DarkGray, 1f, checkpointRotation);
        // }
        // for (int index = 0; index < length; ++index)
        // {
        //   mtexture.DrawCentered(position3, flagArray[index] ? Color.White : Color.Black * 0.3f, 0.5f, checkpointRotation);
        //   position3 += vector * this.getStrawberryWidth(44f, flagArray.Length, checkpointIndex);
        // }
    }

    private static void startMixin(On.Celeste.OuiChapterPanel.orig_Start orig, OuiChapterPanel self,
        string id) {
        if (id == null || !id.StartsWith(reallyBigSectionName)) {
            orig(self, id);
            return;
        }

        var next = positions[self].Chapters.Find(section =>
            reallyBigSectionName + section.text == id);
        if (next.Chapters.Count < 1) return;

        positions[self] = next;
        positions[self].selected = true;
        self.selectingMode = !self.selectingMode; //to couteract the swapping back
        self.Swap();
    }

    private static IEnumerator leaveMixin(On.Celeste.OuiChapterPanel.orig_Leave orig, OuiChapterPanel self, Oui next) {
        positions.Remove(self);
        return orig(self, next);
    }

    private static void updateMixin(ILContext ctx) {
        var cursor = new ILCursor(ctx);

        cursor.GotoNext(MoveType.After, instr =>
            instr.MatchLdstr("event:/ui/world_map/chapter/checkpoint_back"));
        cursor.GotoNext(MoveType.Before, instr =>
            instr.MatchCallvirt<OuiChapterPanel>("Swap"));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(interceptBack);
    }

    private static void interceptBack(OuiChapterPanel self) {
        if (positions.ContainsKey(self) && positions[self].parent != null) {
            self.selectingMode = !self.selectingMode; //to couteract the swapping back
            positions[self] = positions[self].parent;
        }
    }
}