using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.ReallyBigHelper;

public class ThemeData {
    public string tabColor;

    public string iconColor;

    public string tab;

    public static Dictionary<string, ThemeData> apply(Dictionary<string, ThemeData> custom, Dictionary<string, ThemeData> parent) {
        var toReturn = new Dictionary<string, ThemeData>();
        toReturn["checkpoint"] = new ThemeData{
            tabColor = "3C6180",
            iconColor = "172B48",
            tab = "chevron",
        };
        toReturn["startpoint"] = new ThemeData{
            tabColor = "EABE26",
            iconColor = "432007",
            tab = "chevron",
        };
        toReturn["checkpoints"] = new ThemeData{
            tabColor = "6f3C80",
            iconColor = "311748",
            tab = "chevron",
        };
        toReturn["circle"] = new ThemeData{
            tabColor = "72975C",
            iconColor = "2A5312",
            tab = "chevron",
        };
        toReturn["heart"] = new ThemeData{
            tabColor = "E483B4",
            iconColor = "8F226D",
            tab = "chevron",
        };
        toReturn["berry"] = new ThemeData{
            tabColor = "DA6178",
            iconColor = "5C151A",
            tab = "chevron",
        };
        
        foreach (var (key, data) in parent) {
            toReturn[key] = data;
        }
        foreach (var (key, data) in custom) {
            toReturn[key] = data;
        }

        return toReturn;
    }
}

public class ChapterMetadata {
    private static readonly double darkenFactor = 0.4;


    public List<ChapterMetadata> Chapters = new();
    private Final cleaned;
    private string iconName;
    public int id = -1;
    public string text;
    public MapMetaMountain Mountain;

    public string MHH_HeartID = null;
    public string MHH_HeartXMLPath = null;

    public Dictionary<string, ThemeData> Theme;
    private Dictionary<string, ThemeData> parentTheme;

    private Color? _tabColor;
    public string tabColor {
        set => this._tabColor = Calc.HexToColor(value);
        get => this._tabColor == null ? null : this._tabColor.Value.R.ToString("X2") +
                                               this._tabColor.Value.G.ToString("X2") +
                                               this._tabColor.Value.B.ToString("X2") +
                                               this._tabColor.Value.A.ToString("X2");
    }

    private Color? _iconColor;
    public string iconColor {
        set => this._iconColor = Calc.HexToColor(value);
        get => this._iconColor == null ? null : this._iconColor.Value.R.ToString("X2") +
                                                this._iconColor.Value.G.ToString("X2") +
                                                this._iconColor.Value.B.ToString("X2") +
                                                this._iconColor.Value.A.ToString("X2");
    }

    private MTexture _icon;
    public string icon {
        set {
            this._icon = GFX.Gui["areaselect/ReallyBigHelper/icons/" + value];
            this.iconName = value;
        }
    }

    private MTexture _tab;
    public string tab {
        set => this._tab = value==null?null:GFX.Gui["areaselect/ReallyBigHelper/tabs/" + value];
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
    
    private HashSet<string> _flags = new();

    public string flags {
        set {
            this._flags = new HashSet<string>(value.Split(","));
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

    private static string darken(string color) {
        if (color == null) return null;
        while (color.Length < 8) color += "f";
        return
            ((int)Math.Round(int.Parse(color.Substring(0, 2), NumberStyles.HexNumber) * darkenFactor)).ToString("X2") +
            ((int)Math.Round(int.Parse(color.Substring(2, 2), NumberStyles.HexNumber) * darkenFactor)).ToString("X2") +
            ((int)Math.Round(int.Parse(color.Substring(4, 2), NumberStyles.HexNumber) * darkenFactor)).ToString("X2") +
            ((int)Math.Round(int.Parse(color.Substring(6, 2), NumberStyles.HexNumber) * darkenFactor)).ToString("X2");
    }
    private static string lighten(string color) {
        if (color == null) return null;
        while (color.Length < 8) color += "f";
        return
            ((int)Math.Round(255 - (255 - int.Parse(color.Substring(0, 2), NumberStyles.HexNumber)) * darkenFactor))
                .ToString("X2") +
            ((int)Math.Round(255 - (255 - int.Parse(color.Substring(2, 2), NumberStyles.HexNumber)) * darkenFactor))
                .ToString("X2") +
            ((int)Math.Round(255 - (255 - int.Parse(color.Substring(4, 2), NumberStyles.HexNumber)) * darkenFactor))
                .ToString("X2") +
            ((int)Math.Round(255 - (255 - int.Parse(color.Substring(6, 2), NumberStyles.HexNumber)) * darkenFactor))
                .ToString("X2");
    }
    public Final Cleanup() {
        if (this.Chapters == null) this.Chapters = new();
        var theme = this.Theme ?? new Dictionary<string, ThemeData>();
        if (this.parentTheme != null) theme = ThemeData.apply(theme, this.parentTheme);
        foreach (var chapter in this.Chapters) chapter.GiveParentTheme(theme);
        
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

        string iconColor = null;
        string tabColor = null;
        if (theme.TryGetValue(this.iconName, out var v)) {
            iconColor = v.iconColor;
            tabColor = v.tabColor;
            this.tab = v.tab;
        }
        if (!this._tabColor.HasValue) {
            if (!this._iconColor.HasValue) {
                this.iconColor = darken(this.tabColor) ??
                                 iconColor ?? 
                                 darken(tabColor) ??
                                 "ffffff";
                this.tabColor = //lighten(this.iconColor) ?? //because we don't want to go off of a color we just set
                                tabColor ??
                                lighten(iconColor) ??
                                "ffffff";
            } else {
                this.tabColor = lighten(this.iconColor) ?? 
                                tabColor ??
                                lighten(iconColor) ??
                                "ffffff";
            }
        } else if (!this._iconColor.HasValue) {
            this.iconColor = darken(this.tabColor) ?? 
                             iconColor ??
                             darken(tabColor) ?? 
                             "ffffff";
        }
        if (this._tab == null) {
            this.tab = "chevron";
        }

        if (this.Chapters.Count > 0) this.id = -1;

        Logger.Info("ReallyBigHelper", "cleaning chapters");
        foreach (var chapter in this.Chapters) chapter.Cleanup();
        Logger.Info("ReallyBigHelper", this.text);
        Logger.Info("ReallyBigHelper", this.Chapters.Count+"");
        this.cleaned = new Final(this._tabColor.Value, this._iconColor.Value,
            this._tab, this._icon,
            this.id, this.text, this._displayType,
            this._flags,
            new List<Final>(this.Chapters.Select(metadata => metadata.cleaned)),
            this.Mountain, this.MHH_HeartID, this.MHH_HeartXMLPath);
        foreach (var chapter in this.Chapters) chapter.GiveParent(this.cleaned);

        return this.cleaned;
    }

    private void GiveParent(Final parent) {
        this.cleaned.parent = parent;
    }
    private void GiveParentTheme(Dictionary<string, ThemeData> theme) {
        this.parentTheme = theme;
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

        public HashSet<string> flags;

        public MapMetaMountain Mountain;

        public string MHH_HeartID;
        public string MHH_HeartDisplayPath;
        
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
            HashSet<string> flags,
            List<Final> chapters, MapMetaMountain mountain,
            string mhhId, string mhhPath) {
            this.tabColor = tabColor;
            this.iconColor = iconColor;
            this.tab = tab;
            this.icon = icon;
            this.id = Math.Max(id,-1);
            this.text = text;
            this.displayType = displayType;
            this.flags = flags;
            this.Chapters = chapters;
            this.Mountain = mountain;
            this.MHH_HeartID = mhhId;
            this.MHH_HeartDisplayPath = mhhPath;
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
                untranslatedName = $"ReallyBigHelper_{holder.Area.SID}_{textLabel ?? AreaData.GetStartName(holder.Area)}",
                CheckpointRotation = Calc.Random.Choose(-1, 1) * Calc.Random.Range(0.05f, 0.2f),
                CheckpointOffset = new Vector2(Calc.Random.Range(-16, 16), Calc.Random.Range(-16, 16)),
                Large = false,
                Siblings = CustomChapterPanel.positions[holder].Chapters.Count,
                MHHData = new CustomChapterOption.MHHDataObj(this.MHH_HeartID, this.MHH_HeartDisplayPath),
                
                position = this
            };
            return this.option;
        }

        public List<int> childIds() {
            if (this.id >= 0) {
                return new List<int>{this.id};
            }

            var toReturn = new List<int>();
            foreach (var child in this.Chapters) {
                toReturn.AddRange(child.childIds());
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