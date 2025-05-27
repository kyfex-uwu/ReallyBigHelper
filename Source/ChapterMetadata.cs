using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.ReallyBigHelper;

public class ChapterMetadata {
    private static readonly Dictionary<string, string> iconColors = new();
    private static readonly Dictionary<string, string> tabColorsWithIcon = new();

    private static readonly double lightenAmt = 0.4;

    private MTexture _icon;

    private Color? _iconColor;

    private MTexture _tab = GFX.Gui["areaselect/ReallyBigHelper/tabs/chevron"];

    private Color? _tabColor;

    public List<ChapterMetadata> Chapters = new();
    private Final cleaned;
    private string iconName;
    public int id = -1;
    public string text;
    public MapMetaMountain Mountain;

    static ChapterMetadata() {
        iconColors["checkpoint"] = "172B48";
        iconColors["startpoint"] = "432007";
        iconColors["checkpoints"] = "311748";
        iconColors["circle"] = "2A5312";
        iconColors["heart"] = "8F226D";
        iconColors["berry"] = "5C151A";

        tabColorsWithIcon["checkpoint"] = "3C6180";
        tabColorsWithIcon["startpoint"] = "EABE26";
        tabColorsWithIcon["checkpoints"] = "6f3C80";
        tabColorsWithIcon["circle"] = "72975C";
        tabColorsWithIcon["heart"] = "E483B4";
        tabColorsWithIcon["berry"] = "DA6178";
    }

    public string tabColor {
        set => this._tabColor = Calc.HexToColor(value);
    }

    public string iconColor {
        set => this._iconColor = Calc.HexToColor(value);
    }

    public string icon {
        set {
            this._icon = GFX.Gui["areaselect/ReallyBigHelper/icons/" + value];
            this.iconName = value;
        }
    }

    public string tab {
        set => this._tab = GFX.Gui["areaselect/ReallyBigHelper/tabs/" + value];
    }

    public enum DisplayType {
        INFO,
        PREVIEW,
        NONE
    }

    private DisplayType _displayType = DisplayType.NONE;

    public string displayType {
        set {
            switch (value) {
                case "info": 
                    this._displayType = DisplayType.INFO;
                    break;
                case "preview": 
                    this._displayType = DisplayType.PREVIEW;
                    break;
                case "none": 
                    this._displayType = DisplayType.NONE;
                    break;
            }
        }
    }

#if DEBUG
    public override string ToString() {
        return this.ToString(0);
    }

    public string ToString(int depth) {
        var depthStr = new string(' ', depth * 2);
        var chaptersStr = "";
        foreach (var chapter in this.Chapters) chaptersStr += $"\n{chapter.ToString(depth + 1)}";

        return
            $"{depthStr}---\n{depthStr}tabColor={this._tabColor.ToString()}\n{depthStr}iconColor={this._iconColor.ToString()
            }\n{depthStr}icon={this._icon.AtlasPath}\n{depthStr}tab={this._tab.AtlasPath}\n{depthStr}id=${this.id
            }\n{depthStr}text={this.text}\n{depthStr}Chapters:{chaptersStr}";
    }
#endif

    public Final Cleanup() {
        if (this._icon == null) {
            if (this.Chapters.Count == 0) {
                if (this.id == 0)
                    this.icon = "startpoint";
                else
                    this.icon = "checkpoint";
            } else {
                this.icon = "checkpoints";
            }
        }

        if (!this._tabColor.HasValue) {
            if (!this._iconColor.HasValue) {
                if (iconColors.ContainsKey(this.iconName)) {
                    this.iconColor = iconColors[this.iconName];
                    this.tabColor = tabColorsWithIcon[this.iconName];
                } else {
                    this.iconColor = "ffffff";
                    this.tabColor = tabColorsWithIcon["checkpoint"];
                }
            } else {
                this._tabColor = new Color(
                    (int)(255 - (255 - this._iconColor.Value.R) * lightenAmt),
                    (int)(255 - (255 - this._iconColor.Value.G) * lightenAmt),
                    (int)(255 - (255 - this._iconColor.Value.B) * lightenAmt),
                    this._iconColor.Value.A);
            }
        } else if (!this._iconColor.HasValue) {
            this._iconColor = new Color(
                (int)(255 - (255 - this._tabColor.Value.R) / lightenAmt),
                (int)(255 - (255 - this._tabColor.Value.G) / lightenAmt),
                (int)(255 - (255 - this._tabColor.Value.B) / lightenAmt),
                this._tabColor.Value.A);
        }

        foreach (var chapter in this.Chapters) chapter.Cleanup();
        this.cleaned = new Final(this._tabColor.Value, this._iconColor.Value,
            this._tab, this._icon,
            this.id, this.text, this._displayType,
            new List<Final>(this.Chapters.Select(metadata => metadata.cleaned)),
            this.Mountain);
        foreach (var chapter in this.Chapters) chapter.GiveParent(this.cleaned);

        return this.cleaned;
    }

    private void GiveParent(Final parent) {
        this.cleaned.parent = parent;
    }

    public record Final {
        public List<Final> Chapters = new();

        public MTexture icon;

        public Color iconColor;
        public int id;


        public MTexture tab;
        public Color tabColor;
        public string text;
        public DisplayType displayType;

        public MapMetaMountain Mountain;
        
        public Final parent;
        public bool selected = false;
        public float transitionAmt = 0;

        public MapMetaMountain GetMountain() {
            if (this.Mountain == null) {
                if (this.parent == null) return null;
                return this.parent.GetMountain();
            }

            return this.Mountain;
        }

        public Final(Color tabColor, Color iconColor,
            MTexture tab, MTexture icon,
            int id, string text, DisplayType displayType,
            List<Final> chapters, MapMetaMountain mountain) {
            this.tabColor = tabColor;
            this.iconColor = iconColor;
            this.tab = tab;
            this.icon = icon;
            this.id = Math.Max(id,-1);
            this.text = text;
            this.displayType = displayType;
            this.Chapters = chapters;
            this.Mountain = mountain;
        }

        public OuiChapterPanel.Option option;
        public OuiChapterPanel.Option getOption(OuiChapterPanel holder, string textLabel, string roomName) {
            if (this.option != null) return this.option;
            
            this.option = new CustomChapterOption {
                Bg = this.tab,
                Icon = this.icon,
                BgColor = this.tabColor,
                FgColor = this.iconColor,
                chapterIndex = this.id,
                CheckpointLevelName = this.Chapters.Count == 0
                    ? roomName
                    : CustomChapterPanel.reallyBigSectionName,
                Label = Dialog.Clean($"ReallyBigHelper_{holder.Area.SID}_{textLabel ?? AreaData.GetStartName(holder.Area)}"),
                CheckpointRotation = Calc.Random.Choose(-1, 1) * Calc.Random.Range(0.05f, 0.2f),
                CheckpointOffset = new Vector2(Calc.Random.Range(-16, 16), Calc.Random.Range(-16, 16)),
                Large = false,
                Siblings = CustomChapterPanel.positions[holder].Chapters.Count,
                
                position = this
            };
            return this.option;
        }

        public HashSet<int> childIds() {
            if (this.id >= 0) {
                return new HashSet<int>{this.id};
            }

            var toReturn = new HashSet<int>();
            foreach (var child in this.Chapters) {
                toReturn.UnionWith(child.childIds());
            }

            return toReturn;
        }

#if DEBUG
        public override string ToString() {
            return this.ToString(0);
        }

        public string ToString(int depth) {
            var depthStr = new string(' ', depth * 2);
            var chaptersStr = "";
            foreach (var chapter in this.Chapters) chaptersStr += $"\n{chapter.ToString(depth + 1)}";

            return
                $"{depthStr}---\n{depthStr}tabColor={this.tabColor.ToString()}\n{depthStr}iconColor={this.iconColor.ToString()
                }\n{depthStr}icon={this.icon.AtlasPath}\n{depthStr}tab={this.tab.AtlasPath}\n{depthStr}id=${this.id
                }\n{depthStr}text={this.text}\n{depthStr}Chapters:{chaptersStr}";
        }
#endif
    }
}