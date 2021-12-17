using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using HostileTakeover.Common.BaseClasses;
using HostileTakeover.References;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace HostileTakeover.Models
{
	public class Construct : BaseLoggingClass
	{
		// Alerts the world that this construct is no longer viable.
        public event Action<Construct> OnCloseConstruct;

		// Owner of the grid used for all ownership changes
		private readonly long _gridOwner;

		// This is the first grid in this construct - used only for the closing event
		public readonly long GridId;

		// Stores all the grids in this construct ("subgrids")
		//	(long, MyCubeGrid) => (gridId, grid)
		private readonly ConcurrentDictionary<long, MyCubeGrid> _grids = new ConcurrentDictionary<long, MyCubeGrid>();

		// Stores the map of what block belongs to what grid
		//	(long, HashSet<MyCubeBlock>) => (gridId, blockCollection)
		//private readonly ConcurrentDictionary<long, HashSet<MyCubeBlock>> _gridBlockMap = new ConcurrentDictionary<long, HashSet<MyCubeBlock>>();

		// Stores all the blocks actively being tracked as important
		//	Once this list is empty, the entire construct is disowned
		//	(long, MyCubeBlock) => (blockId, block)
		private readonly ConcurrentDictionary<long, MyCubeBlock> _activeImportantBlocks = new ConcurrentDictionary<long, MyCubeBlock>();

		// Stores all the blocks that were once tracked, or that should have been tracked had they been operational when scanned
		//	This is required to account for partially built blocks or blocks that may be repaired at some point
		//	(long, MyCubeBlock) => (blockId, block)
		private readonly ConcurrentDictionary<long, MyCubeBlock> _inactiveImportantBlocks = new ConcurrentDictionary<long, MyCubeBlock>();

        public Construct(MyCubeGrid grid)
        {
            GridId = grid.EntityId;
            _gridOwner = grid.BigOwners.Count > 0 ? grid.BigOwners[0] : 0;
			Add(grid);
        }

		public void Add(MyCubeGrid grid)
		{
			_grids.TryAdd(grid.EntityId, grid);
			//_gridBlockMap.TryAdd(grid.EntityId, new HashSet<MyCubeBlock>());
			GridRegisterEvents(grid);
			FindImportantBlocks(grid);
		}

        private void GridRegisterEvents(MyCubeGrid grid)
        {   // Watch for block adds here
            grid.OnBlockAdded += OnBlockAdded;
            grid.OnFatBlockAdded += OnFatBlockAdded;
            grid.OnFatBlockRemoved += OnFatBlockRemoved;
            grid.OnFatBlockClosed += OnFatBlockClosed;
            grid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;
            grid.OnGridSplit += OnGridSplit;
            grid.OnClose += GridOnClose;
        }

        private void OnBlockAdded(IMySlimBlock block)
        {
			WriteToLog(nameof(OnBlockAdded), $"Block add detected: {block.BlockDefinition.Id.SubtypeName}");
		}

        private void GridDeRegisterEvents(MyCubeGrid grid)
        {
            grid.OnBlockAdded -= OnBlockAdded;
			grid.OnFatBlockAdded -= OnFatBlockAdded;
            grid.OnFatBlockRemoved -= OnFatBlockRemoved;
            grid.OnFatBlockClosed -= OnFatBlockClosed;
            grid.OnBlockOwnershipChanged -= OnBlockOwnershipChanged;
            grid.OnGridSplit -= OnGridSplit;
            grid.OnClose -= GridOnClose;
        }

		private void OnFatBlockAdded(MyCubeBlock block)
		{
            WriteToLog(nameof(OnFatBlockAdded), $"Block add detected: [{(block.IsWorking ? "T" : "F")}] {block.BlockDefinition.Id.SubtypeName}");
			//if (block.IsWorking)
			BlockSetOwnership(block);
			IdentifyImportantBlock(block, false);
		}

        private void OnFatBlockRemoved(MyCubeBlock block)
        {
            WriteToLog(nameof(OnFatBlockRemoved), $"Block removal detected: {block.BlockDefinition.Id.SubtypeName}");
			CloseBlock(block);
        }

        private void OnFatBlockClosed(MyCubeBlock block)
        {
            WriteToLog(nameof(OnFatBlockClosed), $"Block close detected: {block.BlockDefinition.Id.SubtypeName}");
			CloseBlock(block);
        }

        private void OnBlockOwnershipChanged(MyCubeGrid block)
        {
            // I need to experiment with this; not sure it's performant or useful since it doesn't actually identify the block.
            // Block events should account for this issue.
            WriteToLog(nameof(OnBlockOwnershipChanged), $"Ownership change triggered on grid level...");
        }

		private void OnGridSplit(MyCubeGrid oldGrid, MyCubeGrid newGrid)
		{
            WriteToLog(nameof(OnFatBlockClosed), $"Grid split detected: {oldGrid.EntityId} | {newGrid.EntityId}");
			foreach (var block in newGrid.GetFatBlocks())
				IdentifyImportantBlock(block, true);
		}

		private void GridSetOwnership(MyCubeGrid grid)
		{
			grid.ChangeGridOwnership(0, MyOwnershipShareModeEnum.All);
		}

        private void CloseBlock(MyCubeBlock block)
        {
            if (_activeImportantBlocks.ContainsKey(block.EntityId))
            {
                _activeImportantBlocks.Remove(block.EntityId);
                BlockDeRegisterEvents(block);
            }
            if (_inactiveImportantBlocks.ContainsKey(block.EntityId))
            {
                _inactiveImportantBlocks.Remove(block.EntityId);
                BlockDeRegisterEvents(block);
            }
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
            {
                BlockOnClose(block);
                return;
            }

            if (_activeImportantBlocks.ContainsKey(block.EntityId)) return;
            if (_inactiveImportantBlocks.ContainsKey(block.EntityId)) return;
			AddToImportantBlocks(block);
			BlockRegisterEvents(block);
			WriteToLog(nameof(IdentifyImportantBlock), $"Adding to important blocks: {block.BlockDefinition.Id.SubtypeName}");
		}

		private void AddToImportantBlocks(MyCubeBlock block)
		{
			if (block.IsWorking)
			{
				_activeImportantBlocks.TryAdd(block.EntityId, block);
				return;
			} 
			_inactiveImportantBlocks.TryAdd(block.EntityId, block);
			//_gridBlockMap[block.CubeGrid.EntityId].Add(block);
		}

		private void RemoveFromImportantBlocks(MyCubeBlock block)
		{
			if (_activeImportantBlocks.ContainsKey(block.EntityId))
				_activeImportantBlocks.Remove(block.EntityId);

			if (_inactiveImportantBlocks.ContainsKey(block.EntityId))
				_inactiveImportantBlocks.Remove(block.EntityId);

			//_gridBlockMap[block.CubeGrid.EntityId].Remove(block);
            WriteToLog(nameof(IdentifyImportantBlock), $"Removing important block: {block.BlockDefinition.Id.SubtypeName}");
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
				CheckBlockBalance();
				return;
			}

			if(_inactiveImportantBlocks.ContainsKey(block.EntityId))
				_inactiveImportantBlocks.Remove(block.EntityId);
			_activeImportantBlocks.TryAdd(block.EntityId, block);
			BlockSetOwnership(block);
		}

		private void BlockOnOwnershipChanged(IMyTerminalBlock block)
		{
			BlockSetOwnership((MyCubeBlock) block);
		}

        private readonly Queue<Action> _delayedActions = new Queue<Action>();

        private void BlockSetOwnership(MyCubeBlock block)
        {
            WriteToLog(nameof(BlockSetOwnership),
                $"Ownership change requested: [{_gridOwner}] [{block.OwnerId}] ([{(block.OwnerId == _gridOwner ? "T" : "F")}][{(block.IsWorking ? "T" : "F")}]) {block.BlockDefinition.Id.SubtypeName}");
            if (block.OwnerId == _gridOwner) return;
            var tb = block as IMyTerminalBlock;
            if (tb != null)
                _delayedActions.Enqueue(() =>
                {
                    block.ChangeOwner(_gridOwner, MyOwnershipShareModeEnum.Faction);
					IdentifyImportantBlock(block, false);
                });
        }

        public void ProcessTickDelayedActions()
        {
            while (_delayedActions.Count > 0)
            {
                _delayedActions.Dequeue()?.Invoke();
            }
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

        private void GridOnClose(MyEntity grid)
        {
            // There is no reason to close out the important blocks from this grid.
            // When the grid closes, the blocks will close, which will prune them from the other dictionaries
            GridDeRegisterEvents((MyCubeGrid)grid);
            CloseGrid((MyCubeGrid)grid);
            if (_grids.Count == 0) Close();
        }
		
		private void CloseGrid(MyCubeGrid grid)
        {
			_grids.Remove(grid.EntityId);
            //if (!_gridBlockMap.ContainsKey(grid.EntityId)) return;
            //_gridBlockMap[grid.EntityId].Clear();
            //_gridBlockMap.Remove(grid.EntityId);
        }

        public override void Close()
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
            //_gridBlockMap.Clear();
            OnCloseConstruct?.Invoke(this);
            base.Close();
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

        public override void WriteToLog(string caller, string message)
        {
            base.WriteToLog($"[{GridId}] {caller}", message);
        }

        private readonly StringBuilder _report = new StringBuilder();
		private const string Indent = "    ";

		public override string ToString()
		{
			_report.Clear();

			_report.AppendLine($"Report for Grid Construct: {GridId}");
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