using System.Collections.Generic;

namespace Celeste.Mod.ReallyBigHelper;

public class ReallyBigHelperSaveData : EverestModuleSaveData{
	public enum SpecialBerryType {
		MOONBERRY,
		
		PLATINUM,
		GOLD,
		SILVER,
		// BRONZE,
		//diamond
		//grabless b&b
		//speed berry
	}
	public class AreaData {
		public Dictionary<int, int> deaths = new();
		public Dictionary<int, HashSet<SpecialBerryType>> specialBerryData = new();

		public int getDeaths(List<int> checkpoints) {
			var toReturn = 0;
			foreach(var checkpoint in checkpoints)
				if (deaths.TryGetValue(checkpoint, out var count))
					toReturn += count;
			
			return toReturn;

		}

		public void addDeath(int checkpoint) {
			if (!deaths.ContainsKey(checkpoint)) deaths[checkpoint] = 1;
			else deaths[checkpoint]++;
		}

		public bool hasBerry(SpecialBerryType berry, List<int> checkpoints) {
			foreach (var checkpoint in checkpoints)
				if (specialBerryData.TryGetValue(checkpoint, out var data) && data.Contains(berry))
					return true;

			return false;
		}

		public int getSpecialCount(List<int> checkpoints) {
			var toReturn = 0;
			foreach (var checkpoint in checkpoints) 
				if (specialBerryData.TryGetValue(checkpoint, out var data)) 
					toReturn += data.Count;
			return toReturn;			
		}

		public void addBerry(SpecialBerryType berry, int checkpoint) {
			if (!specialBerryData.ContainsKey(checkpoint)) specialBerryData[checkpoint] = new();
			specialBerryData[checkpoint].Add(berry);
		}
	}
	public Dictionary<string, AreaData> areaData = new ();

	public AreaData getAreaData(string sID) {
		if (areaData.TryGetValue(sID, out var data)) return data;
		return areaData[sID] = new();
	}
}