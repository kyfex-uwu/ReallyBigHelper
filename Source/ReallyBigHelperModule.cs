using System;
using System.Collections.Generic;

namespace Celeste.Mod.ReallyBigHelper;

public class ReallyBigHelperModule : EverestModule {
    public static Dictionary<string, ChapterMetadata.Final> chapterData = new();

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
        
        if(hasCollabUtils2) CollabUtilsCompat.Load();
        if (hasMultiheart) MHHFeatures.Load();
    }

    public override void Unload() {
        CustomChapterOption.Unload();
        CustomChapterPanel.Unload();

        On.Celeste.AreaData.Load -= AreaDataOnLoad;
        
        if(hasCollabUtils2) CollabUtilsCompat.Unload();
        if (hasMultiheart) MHHFeatures.Unload();
    }

    private static void AreaDataOnLoad(On.Celeste.AreaData.orig_Load orig) {
        orig();

        foreach (var area in AreaData.Areas) {
            ModAsset metadata;
            ChapterMetadata result;
            if (Everest.Content.TryGet("Maps/" + area.Mode[0].Path + ".reallybig.meta.yaml", out metadata)) {
                metadata.Type = typeof(AssetTypeYaml);
                if (metadata.TryDeserialize(out result)) {
                    chapterData[area.SID] = result.Cleanup();
                }
            }
        }
    }
    
    //--
    
    private static EverestModuleMetadata collabUtils2Dependency = new EverestModuleMetadata { Name = "CollabUtils2" };
    public static readonly bool hasCollabUtils2 = Everest.Loader.DependencyLoaded(collabUtils2Dependency);
    private static EverestModuleMetadata multiheartDependency = new EverestModuleMetadata { Name = "MultiheartHelper" };
    public static readonly bool hasMultiheart = Everest.Loader.DependencyLoaded(multiheartDependency);
}