using System;
using System.Collections.Generic;
using Celeste.Mod.Meta;

namespace Celeste.Mod.ReallyBigHelper;

public class ReallyBigHelperModule : EverestModule {
    public static Dictionary<string, ChapterMetadata.Final[]> chapterData = new();

    public ReallyBigHelperModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel("ReallyBigHelper", LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel("ReallyBigHelper", LogLevel.Info);
#endif
    }

    public static ReallyBigHelperModule Instance { get; private set; }
    
    public override void Load() {
        CustomChapterOption.Load();
        CustomChapterPanel.Load();
        MountainModelExtensions.Load();

        On.Celeste.AreaData.Load += AreaDataOnLoad;
        Everest.Content.OnUpdate += onUpdate;
        On.Celeste.OuiChapterSelect.Update += onChapterSelect;
        
        if(hasCollabUtils2) CollabUtilsCompat.Load();
        if (hasMultiheart) MHHFeatures.Load();
    }

    public override void Unload() {
        CustomChapterOption.Unload();
        CustomChapterPanel.Unload();
        MountainModelExtensions.Unload();

        On.Celeste.AreaData.Load -= AreaDataOnLoad;
        Everest.Content.OnUpdate -= onUpdate;
        On.Celeste.OuiChapterSelect.Update -= onChapterSelect;
        
        if(hasCollabUtils2) CollabUtilsCompat.Unload();
        if (hasMultiheart) MHHFeatures.Unload();
    }

    private static void AreaDataOnLoad(On.Celeste.AreaData.orig_Load orig) {
        orig();

        foreach (var area in AreaData.Areas) {
            foreach (var mode in area.Mode) {
                if (mode == null) continue; //a b- or c- side doesnt exist
                var name = "Maps/" + mode.Path + ".reallybig.meta";
                if (Everest.Content.TryGet(name, out var data)) {
                    processMeta(data, name);
                }
                name = "Maps/" + mode.Path + ".meta";
                if (Everest.Content.TryGet(name, out var data2)) {
                    processMountainExtraData(data2, name);
                }
            }
        }   
    }

    private static void processMeta(ModAsset modAsset, string path) {
        modAsset.Type = typeof(AssetTypeYaml);
        if (modAsset.TryDeserialize(out ChapterMetadata result)) {
            var areaModePath = path.Substring("Maps/".Length, path.Length - ".reallybig.meta".Length - "Maps/".Length);
            var found = false;
            foreach (var area in AreaData.Areas) {
                for(int i=0;i<area.Mode.Length;i++) {
                    if (areaModePath == area.Mode[i]?.Path) {
                        if (!chapterData.ContainsKey(area.SID))
                            chapterData.Add(area.SID, new ChapterMetadata.Final[area.Mode.Length]);
                        chapterData[area.SID][i] = result.Cleanup();
                        found = true;
                        break;
                    }
                }

                if (found) break;
            }
        }
    }

    private static Dictionary<int, Meow> mountainExtData = new();

    private class Meow : MapMetaMountain {
        public float ReallyBigHelper_SkyboxScale;
    }
    private class Meow2 {
        public Meow Mountain;
    }
    private static void processMountainExtraData(ModAsset modAsset, string path) {
        modAsset.Type = typeof(AssetTypeYaml);
        if (modAsset.TryDeserialize(out Meow2 result)) {
            var areaModePath = path.Substring("Maps/".Length, path.Length - ".meta".Length - "Maps/".Length);
            var found = false;
            foreach (var area in AreaData.Areas) {
                for(int i=0;i<area.Mode.Length;i++) {
                    if (areaModePath == area.Mode[i]?.Path) {
                        mountainExtData[area.ID] = result.Mountain;
                        found = true;
                        break;
                    }
                }

                if (found) break;
            }
        }
    }

    private static void onUpdate(ModAsset prev, ModAsset next) {
        if (next.Type == typeof(AssetTypeYaml) && next.PathVirtual.EndsWith(".reallybig.meta")) {
            if (Celeste.Instance.scene is Overworld overworld && overworld.Current is OuiChapterPanel panel) {
                panel.Reset();
            }
            AssetReloadHelper.Do($"{Dialog.Clean("ReallyBigHelper_ReloadMetadata")}", () => {
                processMeta(next, next.PathVirtual);
                CustomChapterPanel.positions.Clear();
            });
        }
    }

    private static void onChapterSelect(On.Celeste.OuiChapterSelect.orig_Update orig, OuiChapterSelect self) {
        orig(self);
        if (mountainExtData.TryGetValue(SaveData.Instance?.LastArea_Safe.ID ?? -1, out var data) && data!=null) {
            MountainModelExtensions.globalSkyboxScale = data.ReallyBigHelper_SkyboxScale;
        }else {
            MountainModelExtensions.globalSkyboxScale = 1;

        }
    }
    
    //--
    
    private static EverestModuleMetadata collabUtils2Dependency = new EverestModuleMetadata { Name = "CollabUtils2" };
    public static readonly bool hasCollabUtils2 = Everest.Loader.DependencyLoaded(collabUtils2Dependency);
    private static EverestModuleMetadata multiheartDependency = new EverestModuleMetadata { Name = "MultiheartHelper", Version = new Version(0,1,0)};
    public static readonly bool hasMultiheart = Everest.Loader.DependencyLoaded(multiheartDependency);
}