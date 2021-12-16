using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using HostileTakeover.References;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace HostileTakeover.Models
{
	public class Constructs
	{
		public event Action<long> OnClose;

		private long _gridOwner;

		// This is the first grid in this construct - used only for the closing event
		private long _originalGridId;

		// Stores all the grids in this construct ("subgrids")
		//	(long, MyCubeGrid) => (gridId, grid)
		private readonly ConcurrentDictionary<long, MyCubeGrid> _grids = new ConcurrentDictionary<long, MyCubeGrid>();

		// Stores the map of what block belongs to what grid
		//	(long, HashSet<MyCubeBlock>) => (gridId, blockCollection)
		private readonly ConcurrentDictionary<long, HashSet<MyCubeBlock>> _gridBlockMap = new ConcurrentDictionary<long, HashSet<MyCubeBlock>>();

		// Stores all the blocks actively being tracked as important
		//	Once this list is empty, the entire construct is disowned
		//	(long, MyCubeBlock) => (blockId, block)
		private readonly ConcurrentDictionary<long, MyCubeBlock> _activeImportantBlocks = new ConcurrentDictionary<long, MyCubeBlock>();

		// Stores all the blocks that were once tracked, or that should have been tracked had they been operational when scanned
		//	This is required to account for partially built blocks or blocks that may be repaired at some point
		//	(long, MyCubeBlock) => (blockId, block)
		private readonly ConcurrentDictionary<long, MyCubeBlock> _inactiveImportantBlocks = new ConcurrentDictionary<long, MyCubeBlock>();

		public void Add(MyCubeGrid grid)
		{
			if (_grids.Count == 0)
				SetupNewConstruct(grid);
			_grids.TryAdd(grid.EntityId, grid);
			_gridBlockMap.TryAdd(grid.EntityId, new HashSet<MyCubeBlock>());
			GridRegisterEvents(grid);
			FindImportantBlocks(grid);
		}

		private void SetupNewConstruct(MyCubeGrid grid)
		{
			_gridOwner = grid.BigOwners[0];
			_originalGridId = grid.EntityId;
		}

		private void GridRegisterEvents(MyCubeGrid grid)
		{	// Watch for block adds here
			grid.OnFatBlockAdded += GridOnFatBlockAdded;
			grid.OnGridSplit += GridOnGridSplit;
			grid.OnClose += GridOnClose;
		}

		private void GridDeRegisterEvents(MyCubeGrid grid)
		{
			grid.OnFatBlockAdded -= GridOnFatBlockAdded;
			grid.OnClose -= GridOnClose;
		}

		private void GridOnFatBlockAdded(MyCubeBlock block)
		{
			if (block.IsWorking) BlockSetOwnership(block);
			IdentifyImportantBlock(block, false);
		}

		private void GridOnGridSplit(MyCubeGrid oldGrid, MyCubeGrid newGrid)
		{
			foreach (var block in newGrid.GetFatBlocks())
				IdentifyImportantBlock(block, true);
		}

		private void GridSetOwnership(MyCubeGrid grid)
		{
			grid.ChangeGridOwnership(0, MyOwnershipShareModeEnum.All);
		}

		private void GridOnClose(MyEntity grid)
		{
			// There is no reason to close out the important blocks from this grid.
			// When the grid closes, the blocks will close, which will prune them from the other dictionaries
			GridDeRegisterEvents((MyCubeGrid) grid);
			_grids.Remove(grid.EntityId);
		}

		private void FindImportantBlocks(MyCubeGrid grid)
		{
			foreach (var block in grid.GetFatBlocks())
				IdentifyImportantBlock(block, false);
		}
		
		private void IdentifyImportantBlock(MyCubeBlock block, bool remove)
		{
			if (!ImportantBlocks.IsBlockImportant(block)) return;
			if (remove)
				BlockOnClose(block);
			else AddToImportantBlocks(block);
			BlockRegisterEvents(block);
		}

		private void AddToImportantBlocks(MyCubeBlock block)
		{
			if (block.IsWorking)
			{
				_activeImportantBlocks.TryAdd(block.EntityId, block);
				return;
			} 
			_inactiveImportantBlocks.TryAdd(block.EntityId, block);
			_gridBlockMap[block.CubeGrid.EntityId].Add(block);
		}

		private void RemoveFromImportantBlocks(MyCubeBlock block)
		{
			if (_activeImportantBlocks.ContainsKey(block.EntityId))
				_activeImportantBlocks.Remove(block.EntityId);

			if (_inactiveImportantBlocks.ContainsKey(block.EntityId))
				_inactiveImportantBlocks.Remove(block.EntityId);

			_gridBlockMap[block.CubeGrid.EntityId].Remove(block);
		}

		private void BlockRegisterEvents(MyCubeBlock block)
		{
			block.IsWorkingChanged += BlockOnWorkingChanged;
			block.OnClose += BlockOnClose;
			((IMyTerminalBlock)block).OwnershipChanged += BlockOnOwnershipChanged;
		}

		private void BlockDeRegisterEvents(MyCubeBlock block)
		{
			block.IsWorkingChanged -= BlockOnWorkingChanged;
			((IMyTerminalBlock)block).OwnershipChanged -= BlockOnOwnershipChanged;
			block.OnClose -= BlockOnClose;
		}

		private void BlockOnWorkingChanged(MyCubeBlock block)
		{
			if (!block.IsWorking)
			{
				if (_activeImportantBlocks.ContainsKey(block.EntityId))
					_activeImportantBlocks.Remove(block.EntityId);
				_inactiveImportantBlocks.TryAdd(block.EntityId, block);
				return;
			}

			if(_inactiveImportantBlocks.ContainsKey(block.EntityId))
				_inactiveImportantBlocks.Remove(block.EntityId);
			_activeImportantBlocks.TryAdd(block.EntityId, block);
			BlockSetOwnership(block);
		}

		private void BlockOnOwnershipChanged(IMyTerminalBlock block)
		{
			if (!block.IsWorking) return;
			BlockSetOwnership((MyCubeBlock) block);
		}

		private void BlockSetOwnership(MyCubeBlock block)
		{
			if (block.OwnerId != _gridOwner && block.IsWorking)
				block.ChangeOwner(_gridOwner, MyOwnershipShareModeEnum.Faction);
		}

		private void BlockOnClose(MyEntity block)
		{

			RemoveFromImportantBlocks((MyCubeBlock) block);
			BlockDeRegisterEvents((MyCubeBlock)block);
			CheckBlockBalance();
		}

		private void CheckBlockBalance()
		{
			if (_activeImportantBlocks.Count == 0)
				DisownGrid();
		}

		private void DisownGrid()
		{
			foreach (var grid in _grids)
				GridSetOwnership(grid.Value);
			Close();
		}

		private void Close()
		{
			foreach (var block in _activeImportantBlocks)
				BlockDeRegisterEvents(block.Value);
			foreach (var block in _inactiveImportantBlocks)
				BlockDeRegisterEvents(block.Value);
			foreach (var grid in _grids)
				GridDeRegisterEvents(grid.Value);
			_activeImportantBlocks.Clear();
			_inactiveImportantBlocks.Clear();
			_grids.Clear();
			_gridBlockMap.Clear();
			OnClose?.Invoke(_originalGridId);
		}

		// External Methods
		public MyCubeBlock GetNearestBlock(Vector3D source)
		{
			double distance = double.MaxValue;
			MyCubeBlock closestBlock = null;
			foreach (var block in _activeImportantBlocks.Values)
			{
				double abs = Math.Abs(((IMyCubeBlock) block).GetPosition().LengthSquared() - source.LengthSquared());
				if ((abs > distance)) continue;
				distance = abs;
				closestBlock = block;
			}
			return closestBlock;
		}

		private readonly StringBuilder _report = new StringBuilder();
		private const string Indent = "    ";

		public override string ToString()
		{
			_report.Clear();

			_report.AppendLine($"Report for Grid Construct: {_originalGridId}");
			_report.AppendLine($"{Indent} Owner: {_gridOwner}");
			_report.AppendLine();

			_report.AppendLine("** Grids **");
			_report.AppendLine($"{Indent} Total: {_grids.Count}");
			foreach (var grid in _grids)
			{
				_report.AppendLine($"{Indent} ID: {grid.Key, -20}  Name: {grid.Value.DisplayName}");
			}

			_report.AppendLine();
			_report.AppendLine("** Active Important Blocks **");
			_report.AppendLine($"{Indent} Total: {_activeImportantBlocks.Count}");
			foreach (var block in _activeImportantBlocks)
			{
				_report.AppendLine($"{Indent} TypeId: {block.Value.BlockDefinition.Id.TypeId, -30} SubtypeId: {block.Value.BlockDefinition.Id.SubtypeId}");
			}

			_report.AppendLine();
			_report.AppendLine("** Inactive Important Blocks **");
			_report.AppendLine($"{Indent} Total: {_inactiveImportantBlocks.Count}");
			foreach (var block in _inactiveImportantBlocks)
			{
				_report.AppendLine($"{Indent} TypeId: {block.Value.BlockDefinition.Id.TypeId,-30} SubtypeId: {block.Value.BlockDefinition.Id.SubtypeId}");
			}
			_report.AppendLine();

			return _report.ToString();
		}
	}
}