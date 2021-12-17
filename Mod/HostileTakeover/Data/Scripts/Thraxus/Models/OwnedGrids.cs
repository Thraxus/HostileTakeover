using System.Collections.Generic;
using System.Linq;
using HostileTakeover.Common.BaseClasses;
using HostileTakeover.Common.Enums;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace HostileTakeover.Models
{
	public class OwnedGrids : BaseLoggingClass
	{
		// TODO: Need to account for mines.  Perhaps disregard this if a grid is small and has a warhead? 
		// TODO: Could also just make a warhead an important type... that doesn't sound like a bad idea really
		// TODO: Test with "Military Station" encounter / SQUASH mines
		// TODO: Decided to just add warheads.  Leaving this as a note for one last review before considering this done
		private static readonly List<MyObjectBuilderType> ImportantTypes = new List<MyObjectBuilderType>()
		{
			typeof(MyObjectBuilder_InteriorTurret),
			typeof(MyObjectBuilder_LargeGatlingTurret),
			typeof(MyObjectBuilder_LargeMissileTurret),
			typeof(MyObjectBuilder_RemoteControl),
			typeof(MyObjectBuilder_SafeZone),
			typeof(MyObjectBuilder_Warhead)
		};

		private static readonly List<MyObjectBuilderType> ImportantPartialTypes = new List<MyObjectBuilderType>()
		{
			typeof(MyObjectBuilder_Cockpit),
		};

		private static readonly List<MyStringHash> ImportantSubTypes = new List<MyStringHash>()
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
		
		protected string Id = nameof(OwnedGrids);

		private readonly ConcurrentCachingList<MyCubeBlock> _importantBlocks = new ConcurrentCachingList<MyCubeBlock>();
		private readonly MyCubeGrid _thisGrid;
		private readonly long _ownerId;


		public OwnedGrids(MyCubeGrid grid)
		{
			Id += $" ({grid.EntityId}) {grid.DisplayName}";
			_thisGrid = grid;
			_ownerId = _thisGrid.BigOwners[0];
			_thisGrid.OnFatBlockAdded += IdentifyImportantBlock;
			_thisGrid.OnFatBlockRemoved += FatBlockRemoved;
			_thisGrid.OnClose += GridClose;
			FindImportantBlocks();
		}

		private void GridClose(MyEntity obj)
		{
			Close();
		}

		private readonly List<IMyCubeGrid> _reusableGridCollection = new List<IMyCubeGrid>();

		private void PopulateGridList()
		{
			_reusableGridCollection.Clear();
			MyAPIGateway.GridGroups.GetGroup(_thisGrid, GridLinkTypeEnum.Mechanical, _reusableGridCollection);
		}

		private void FindImportantBlocks()
		{
			if (IsClosed) return;

			foreach (var block in _thisGrid.GetFatBlocks())
			{
				IdentifyImportantBlock(block);
			}

			PopulateGridList();

			foreach (var grid in _reusableGridCollection)
			{
				foreach (var block in ((MyCubeGrid)grid).GetFatBlocks())
				{
					IdentifyImportantBlock(block);
				}
			}
			// Accounts for the case where a partial floating grid is still around owned by a NPC
			// The collection will be empty, so disown it. 
			CheckRemainingBlocks();
		}

		private void IdentifyImportantBlock(MyCubeBlock block)
		{
			if (block.OwnerId != _ownerId) return;
			MyDefinitionId blockDefinitionId = block.BlockDefinition.Id;
			if (ImportantTypes.Contains(blockDefinitionId.TypeId))
			{
				AddToImportantBlocks(block);
				return;
			}

			if (!ImportantPartialTypes.Contains(blockDefinitionId.TypeId)) return;
			if (!ImportantSubTypes.Contains(blockDefinitionId.SubtypeId)) return;
			AddToImportantBlocks(block);
		}

		private void BlockClose(MyEntity block)
		{
			RemoveFromImportantBlocks((MyCubeBlock) block);
		}

		private void WorkingChanged(MyCubeBlock block)
		{
			if (block.OwnerId == _ownerId && block.IsFunctional) return;
			RemoveFromImportantBlocks(block);
		}

		private void OnOwnershipChanged(IMyTerminalBlock block)
		{
			if (block.OwnerId != _ownerId)
				RemoveFromImportantBlocks((MyCubeBlock)block);
		}

		private void FatBlockRemoved(MyCubeBlock block)
		{
			RemoveFromImportantBlocks(block);
		}

		private void AddToImportantBlocks(MyCubeBlock block)
		{
			if (IsClosed) return;
			if (block.OwnerId != _ownerId) return;
			RegisterBlockEvents(block);
			_importantBlocks.Add(block);
			_importantBlocks.ApplyAdditions();
		}

		private void RemoveFromImportantBlocks(MyCubeBlock block)
		{
			if (IsClosed) return;
			if (!_importantBlocks.Contains(block)) return;
			DeRegisterBlockEvents(block);
			_importantBlocks.Remove(block);
			_importantBlocks.ApplyRemovals();
			CheckRemainingBlocks();
		}

		private void RegisterBlockEvents(MyCubeBlock block)
		{

			block.IsWorkingChanged += WorkingChanged;
			block.OnClose += BlockClose;
			((IMyTerminalBlock)block).OwnershipChanged += OnOwnershipChanged;
		}
		
		private void DeRegisterBlockEvents(MyCubeBlock block)
		{
			block.IsWorkingChanged -= WorkingChanged;
			((IMyTerminalBlock)block).OwnershipChanged -= OnOwnershipChanged;
			block.OnClose -= BlockClose;
		}

		private void CheckRemainingBlocks()
		{
			_importantBlocks.ApplyChanges();
			if (_importantBlocks.Count > 0) return;
			WriteToLog("CheckRemainingBlocks", $"Disowning Grid...");
			if (_thisGrid.MarkedForClose)
			{	// TODO: Test this to make sure it's right.  Goal is to avoid the whole change ownership bit if the grid is closing.
				Close();
				return;
			}
			_thisGrid.ChangeGridOwnership(0,MyOwnershipShareModeEnum.None);
			PopulateGridList();
			foreach (IMyCubeGrid grid in _reusableGridCollection)
				grid.ChangeGridOwnership(0, MyOwnershipShareModeEnum.None);
			Close();
		}

		public void Report()
		{
			WriteToLog("Report", $"Online: {!IsClosed} |Important Blocks: {_importantBlocks.Count}");
		}

		public override void Close()
		{
			base.Close();
			_thisGrid.OnFatBlockAdded -= IdentifyImportantBlock;
			_thisGrid.OnFatBlockRemoved -= FatBlockRemoved;
			foreach (var block in _importantBlocks)
			{
				DeRegisterBlockEvents(block);
			}
			_importantBlocks.ClearList();
			_importantBlocks.ApplyChanges();
		}

        public override void WriteToLog(string caller, string message)
        {
            base.WriteToLog($"[{Id}] {caller}", message);
        }
	}
}