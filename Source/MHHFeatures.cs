using Celeste.Mod.MultiheartHelper;

namespace Celeste.Mod.ReallyBigHelper;

public class MHHFeatures {
    public static void Load() {
        
    }
    public static void Unload() {
        
    }

    public static bool heartIsCollected(AreaData area, string id) {
        if (MultiheartHelperModule.SaveData.TryGetData(area.ID, out var data)) {
            return data.unlockedHearts.Contains(id);
        }

        return false;
    }
}