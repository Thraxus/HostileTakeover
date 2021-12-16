using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace HostileTakeover.References
{
	public static class ImportantBlocks
	{
		private static bool _setupComplete;
		private static readonly HashSet<MyObjectBuilderType> ImportantTypes = new HashSet<MyObjectBuilderType>()
		{
			typeof(MyObjectBuilder_InteriorTurret),
			typeof(MyObjectBuilder_LargeGatlingTurret),
			typeof(MyObjectBuilder_LargeMissileTurret),
			typeof(MyObjectBuilder_RemoteControl),
			typeof(MyObjectBuilder_SafeZone),
			typeof(MyObjectBuilder_Warhead)
		};

		private static readonly HashSet<MyObjectBuilderType> ImportantPartialTypes = new HashSet<MyObjectBuilderType>()
		{
			typeof(MyObjectBuilder_Cockpit),
		};

		private static readonly HashSet<MyStringHash> ImportantSubTypes = new HashSet<MyStringHash>(MyStringHash.Comparer)
		{
			MyStringHash.GetOrCompute("CockpitOpen"),
			MyStringHash.GetOrCompute("DBSmallBlockFighterCockpit"),
			MyStringHash.GetOrCompute("LargeBlockCockpit"),
			MyStringHash.GetOrCompute("LargeBlockCockpitIndustrial"),
			MyStringHash.GetOrCompute("LargeBlockCockpitSeat"),
			MyStringHash.GetOrCompute("OpenCockpitLarge"),
			MyStringHash.GetOrCompute("OpenCockpitSmall"),
			MyStringHash.GetOrCompute("SmallBlockCockpit"),
			MyStringHash.GetOrCompute("SmallBlockCockpitIndustrial")
		};

		public static bool IsBlockImportant(MyCubeBlock block)
		{
			if (!_setupComplete)
			{
				SetupHashSets();
				_setupComplete = true;
			}
			MyDefinitionId def = block.BlockDefinition.Id;
			if (ImportantTypes.Contains(def.TypeId)) return true;
			return ImportantPartialTypes.Contains(def.TypeId) && ImportantSubTypes.Contains(def.SubtypeId);
		}

		private static void SetupHashSets()
		{
			// cock != null && cock.EnableShipControl
			// Build a hashset based on this and get rid of the ImportantPartialTypes & ImportantSubTypes combo
			// just parse all cockpit definitions and look for .EnableShipControl or something similar
		}
	}
}