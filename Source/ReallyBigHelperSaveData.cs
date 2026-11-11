using System.Collections.Generic;

namespace Celeste.Mod.ReallyBigHelper;

public class ReallyBigHelperSaveData : EverestModuleSaveData{
	public class SpecialBerryData {
		public bool goldened;
		public bool silvered;
		// public bool bronzed;
		public bool platinumed;
		//diamond
		//grabless b&b
		//speed berry
	}

	public enum SpecialBerryType {
		GOLD,
		SILVER,
		// BRONZE,
		PLATINUM,
	}
	public class AreaData {
		public Dictionary<int, int> deaths = new();
		public Dictionary<int, SpecialBerryData> specialBerryData = new();

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
			switch (berry) {
				case SpecialBerryType.GOLD:
					foreach (var checkpoint in checkpoints)
						if (specialBerryData.TryGetValue(checkpoint, out var data) && data.goldened)
							return true;
					return false;
				case SpecialBerryType.SILVER:
					foreach (var checkpoint in checkpoints)
						if (specialBerryData.TryGetValue(checkpoint, out var data) && data.silvered)
							return true;
					return false;
				case SpecialBerryType.PLATINUM:
					foreach (var checkpoint in checkpoints)
						if (specialBerryData.TryGetValue(checkpoint, out var data) && data.platinumed)
							return true;
					return false;
			}

			return false;
		}

		public void addBerry(SpecialBerryType berry, int checkpoint) {
			if (!specialBerryData.ContainsKey(checkpoint)) specialBerryData[checkpoint] = new SpecialBerryData();
			switch (berry) {
				case SpecialBerryType.GOLD:
					specialBerryData[checkpoint].goldened = true;
					break;
				case SpecialBerryType.SILVER:
					specialBerryData[checkpoint].silvered = true;
					break;
				case SpecialBerryType.PLATINUM:
					specialBerryData[checkpoint].platinumed = true;
					break;
			}
		}
	}
	public Dictionary<string, AreaData> areaData = new ();

	public AreaData getAreaData(string sID) {
		if (areaData.TryGetValue(sID, out var data)) return data;
		return areaData[sID] = new();
	}
}