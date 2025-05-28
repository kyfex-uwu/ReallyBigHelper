using Celeste.Mod.MultiheartHelper;

namespace Celeste.Mod.ReallyBigHelper;

public class MHHFeatures {
    public static void Load() {
        
    }
    public static void Unload() {
        
    }

    public static void drawHearts(AreaData key) {
        if (!MultiheartHelperModule.multiheartData.TryGetValue(key, out var multiheartMetadata) || 
            !MultiheartHelperModule.SaveData.TryGetData(key.ID, out var data)) return;
        
        foreach (HeartInfo heart in multiheartMetadata.Hearts) {
            if (data.unlockedHearts.Contains(heart.Name)) {
                //heart.Texture
            }
        }
    }
}