using System.Collections.Generic;

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

        On.Celeste.AreaData.Load += AreaDataOnLoad;
        Everest.Content.OnUpdate += onUpdate;
        
        if(hasCollabUtils2) CollabUtilsCompat.Load();
        if (hasMultiheart) MHHFeatures.Load();
    }

    public override void Unload() {
        CustomChapterOption.Unload();
        CustomChapterPanel.Unload();

        On.Celeste.AreaData.Load -= AreaDataOnLoad;
        Everest.Content.OnUpdate -= onUpdate;
        
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
    
    //--
    
    private static EverestModuleMetadata collabUtils2Dependency = new EverestModuleMetadata { Name = "CollabUtils2" };
    public static readonly bool hasCollabUtils2 = Everest.Loader.DependencyLoaded(collabUtils2Dependency);
    private static EverestModuleMetadata multiheartDependency = new EverestModuleMetadata { Name = "MultiheartHelper" };
    public static readonly bool hasMultiheart = Everest.Loader.DependencyLoaded(multiheartDependency);
}