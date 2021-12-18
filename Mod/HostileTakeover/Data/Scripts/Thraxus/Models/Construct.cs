using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using HostileTakeover.Common.BaseClasses;
using HostileTakeover.References;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace HostileTakeover.Models
{
	public class Construct : BaseLoggingClass
	{
        /// <summary>
        /// Alerts the world that this construct is no longer viable.
        /// </summary>
        public event Action<Construct> OnCloseConstruct;

        /// <summary>
        /// Owner of the grid used for all ownership changes
        /// </summary>
        private readonly long _gridOwner;

        /// <summary>
        /// This is the first grid in this construct - used only for the closing event
        /// </summary>
        public readonly long GridId;

        /// <summary>
        /// Tick counter for the gird
        /// Mainly used for highlighted blocks
        /// </summary>
        private long _gridTick;

        /// <summary>
        /// Stores all the grids in this construct ("subgrids")
        ///     (long, MyCubeGrid) => (gridId, grid)
        /// </summary>
        private readonly ConcurrentDictionary<long, MyCubeGrid> _grids = new ConcurrentDictionary<long, MyCubeGrid>();

        /// <summary>
        /// Stores all the blocks actively being tracked as important
        ///     Once this list is empty, the entire construct is disowned
        ///     (long, MyCubeBlock) => (blockId, block)
        /// </summary>
        private readonly ConcurrentDictionary<long, MyCubeBlock> _activeImportantBlocks = new ConcurrentDictionary<long, MyCubeBlock>();

        /// <summary>
        /// Stores all the blocks that were once tracked, or that should have been tracked had they been operational when scanned
        ///	    This is required to account for partially built blocks or blocks that may be repaired at some point
        ///     (long, MyCubeBlock) => (blockId, block)
        /// </summary>
        private readonly ConcurrentDictionary<long, MyCubeBlock> _inactiveImportantBlocks = new ConcurrentDictionary<long, MyCubeBlock>();

        /// <summary>
        /// Contains a queue of generic actions that fire on a schedule tick call from the mod core
        /// </summary>
        private readonly Queue<Action> _delayedActions = new Queue<Action>();

        /// <summary>
        /// Collection of all blocks currently highlighted
        ///     Long = tick to remove highlights
        ///     Hashset = blocks currently highlighted
        /// </summary>
        private readonly ConcurrentDictionary<long, HashSet<HighlightedBlocks>> _highlightedBlocks = new ConcurrentDictionary<long, HashSet<HighlightedBlocks>>();

        /// <summary>
        /// Time a block should be highlighted for
        /// </summary>
        private const long HighlightDuration = Common.Settings.TicksPerSecond * 10;

        /// <summary>
        /// Private class that controls basic highlighted block data
        /// </summary>
        private class HighlightedBlocks
        {
            public long TargetPlayer;
            public HashSet<MyCubeBlock> Blocks;

            public void RemoveBlock(MyCubeBlock block)
            {
                Blocks.Remove(block);
            }
        }

        /// <summary>
        /// Constructor.
        /// Sets up the original grid and owner
        /// </summary>
        /// <param name="grid"></param>
        public Construct(MyCubeGrid grid)
        {
            GridId = grid.EntityId;
            _gridOwner = grid.BigOwners.Count > 0 ? grid.BigOwners[0] : 0;
			Add(grid);
        }

        /// <summary>
        /// Adds a new grid to this construct
        /// Also sets up all grid level event registrations and identifies all important blocks
        /// </summary>
        /// <param name="grid"></param>
		public void Add(MyCubeGrid grid)
		{
			_grids.TryAdd(grid.EntityId, grid);
			GridRegisterEvents(grid);
			FindImportantBlocks(grid);
		}

        /// <summary>
        /// Registers all grid level events we care about
        /// </summary>
        /// <param name="grid"></param>
        private void GridRegisterEvents(MyCubeGrid grid)
        {   
            grid.OnFatBlockAdded += OnFatBlockAdded;
            grid.OnFatBlockRemoved += OnFatBlockRemoved;
            grid.OnFatBlockClosed += OnFatBlockRemoved;
            grid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;
            grid.OnGridSplit += OnGridSplit;
            grid.OnClose += GridOnClose;
        }

        /// <summary>
        /// Cleans up any grid level events for closed grids
        /// </summary>
        /// <param name="grid"></param>
        private void GridDeRegisterEvents(MyCubeGrid grid)
        {
			grid.OnFatBlockAdded -= OnFatBlockAdded;
            grid.OnFatBlockRemoved -= OnFatBlockRemoved;
            grid.OnFatBlockClosed -= OnFatBlockRemoved;
            grid.OnBlockOwnershipChanged -= OnBlockOwnershipChanged;
            grid.OnGridSplit -= OnGridSplit;
            grid.OnClose -= GridOnClose;
        }

        /// <summary>
        /// Fires when a fat block is added to the grid
        /// Any fat block can be an important block so this needs to be parsed through the important block finder
        /// Regardless if this is a block we care about or not, ownership also needs to be set to the grid owner
        /// </summary>
        /// <param name="block"></param>
		private void OnFatBlockAdded(MyCubeBlock block)
		{
            WriteToLog(nameof(OnFatBlockAdded), $"Block add detected: [{(block.IsWorking ? "T" : "F")}] {block.BlockDefinition.Id.SubtypeName}");
			ClaimBlockOwnership(block);
			IdentifyImportantBlock(block);
		}

        /// <summary>
        /// This fires whenever a fat block is removed from the grid
        /// Even though all fat blocks aren't important blocks, important blocks still need to be scrubbed from all possible tracking sources
        /// So just pass the block to the CloseBlock method for final processing
        /// This method covers both block removed and block closed cases
        /// </summary>
        /// <param name="block"></param>
        private void OnFatBlockRemoved(MyCubeBlock block)
        {
            WriteToLog(nameof(OnFatBlockRemoved), $"Block removal detected: {block.BlockDefinition.Id.SubtypeName}");
			CloseBlock(block);
        }

        /// <summary>
        /// Fires whenever a block on a grid changes ownership.  Currently unused but hooked for evaluation
        /// TODO: Evaluate whether this event registration makes sense and remove if not
        /// </summary>
        /// <param name="block"></param>
        private void OnBlockOwnershipChanged(MyCubeGrid block)
        {
            // I need to experiment with this; not sure it's performant or useful since it doesn't actually identify the block.
            // Block events should account for this issue.
            WriteToLog(nameof(OnBlockOwnershipChanged), $"Ownership change triggered on grid level...");
        }

        /// <summary>
        /// Fires when a grid splits for some reason
        /// Currently only used to prune blocks that are removed from the main grid
        /// However, this should be covered from the BlockRemoved event
        /// TODO: Evaluate whether this event registration makes sense and remove if not
        /// </summary>
        /// <param name="oldGrid"></param>
        /// <param name="newGrid"></param>
		private void OnGridSplit(MyCubeGrid oldGrid, MyCubeGrid newGrid)
		{
            WriteToLog(nameof(OnGridSplit), $"Grid split detected: {oldGrid.EntityId} | {newGrid.EntityId}");
			foreach (var block in newGrid.GetFatBlocks())
				CloseBlock(block);
		}

        /// <summary>
        /// Disowns the grid
        /// Known problem that not all blocks switch ownership - need to investigate why
        /// Disabled blocks sometimes do not change owner, this is hopefully fixed by the DisownBlock(MyCubeBlock block) method
        /// Requires further testing
        /// </summary>
        /// <param name="grid"></param>
		private static void GridSetOwnership(MyCubeGrid grid)
		{
			grid.ChangeGridOwnership(0, MyOwnershipShareModeEnum.All);
		}

        /// <summary>
        /// Closes out a block
        /// </summary>
        /// <param name="block"></param>
        private void CloseBlock(MyCubeBlock block)
        {
            RemoveFromImportantBlocks(block);
            RemoveFromHighlightedBlocks(block);
            BlockDeRegisterEvents(block);
            CheckBlockBalance();
        }

        /// <summary>
        /// OnClose block event
        /// Calls Close block
        /// TODO: Probably not needed since this is already covered on grid block removal and closure.  Evaluate and remove if deemed unnecessary.
        /// </summary>
        /// <param name="block"></param>
        private void BlockOnClose(MyEntity block)
        {
            CloseBlock((MyCubeBlock)block);
        }

        /// <summary>
        /// Sends all fat blocks on a grid for important block identification
        /// Can probably improve by pruning out all non-terminal blocks before sending for identification
        /// TODO: Determine if this method can be improved or not
        /// </summary>
        /// <param name="grid"></param>
    	private void FindImportantBlocks(MyCubeGrid grid)
		{
			foreach (MyCubeBlock block in grid.GetFatBlocks())
				IdentifyImportantBlock(block);
		}
		
        /// <summary>
        /// Identifies important blocks
        /// Also sends all important blocks out for event registration
        /// </summary>
        /// <param name="block"></param>
		private void IdentifyImportantBlock(MyCubeBlock block)
		{
			if (!ImportantBlocks.IsBlockImportant(block)) return;
            if (_activeImportantBlocks.ContainsKey(block.EntityId)) return;
            if (_inactiveImportantBlocks.ContainsKey(block.EntityId)) return;
			AddToImportantBlocks(block);
			BlockRegisterEvents(block);
			WriteToLog(nameof(IdentifyImportantBlock), $"Adding to important blocks: {block.BlockDefinition.Id.SubtypeName}");
		}

        /// <summary>
        /// Registers all block level events we care about
        /// </summary>
        /// <param name="block"></param>
        private void BlockRegisterEvents(MyCubeBlock block)
        {
            block.IsWorkingChanged += BlockOnWorkingChanged;
            block.OnClose += BlockOnClose;
            ((IMyTerminalBlock)block).OwnershipChanged += BlockOnOwnershipChanged;
        }

        /// <summary>
        /// Cleans up any block level events for closed blocks
        /// </summary>
        /// <param name="block"></param>
        private void BlockDeRegisterEvents(MyCubeBlock block)
        {
            block.IsWorkingChanged -= BlockOnWorkingChanged;
            ((IMyTerminalBlock)block).OwnershipChanged -= BlockOnOwnershipChanged;
            block.OnClose -= BlockOnClose;
        }

        /// <summary>
        /// Adds a block to the proper important block collection based on the IsWorking condition
        /// TODO: Investigate problem of blocks not accurately reporting IsWorking status; may need a tick delay?
        /// </summary>
        /// <param name="block"></param>
		private void AddToImportantBlocks(MyCubeBlock block)
		{
			if (block.IsWorking)
			{
				_activeImportantBlocks.TryAdd(block.EntityId, block);
				return;
			} 
			_inactiveImportantBlocks.TryAdd(block.EntityId, block);
            WriteToLog(nameof(AddToImportantBlocks), $"Adding important block: {block.BlockDefinition.Id.SubtypeName}");
        }

        /// <summary>
        /// Removes an important block from both active and inactive collections
        /// </summary>
        /// <param name="block"></param>
		private void RemoveFromImportantBlocks(MyCubeBlock block)
		{
			if (_activeImportantBlocks.ContainsKey(block.EntityId))
				_activeImportantBlocks.Remove(block.EntityId);
            if (_inactiveImportantBlocks.ContainsKey(block.EntityId))
				_inactiveImportantBlocks.Remove(block.EntityId);
            WriteToLog(nameof(RemoveFromImportantBlocks), $"Removing important block: {block.BlockDefinition.Id.SubtypeName}");
		}
        
        /// <summary>
        /// Fires whenever a block changes working status
        /// Only hooked for blocks we care about
        /// </summary>
        /// <param name="block"></param>
		private void BlockOnWorkingChanged(MyCubeBlock block)
        {   
            // TODO OnWorkingChanged and OnOwnershipChange kinda do the same thing on the block level.  Consider combining efforts and/or only hooking one of the events
            // Example: Working changed can fire if the block is disabled or repaired, but being disabled also fires an ownership change (most of the time).
            //  So, why hook both events?  OnWorkingChange should cover for Ownership change as well as I can decide to act on ownership requirements based on 
            //  if the block is working or not.
            WriteToLog(nameof(BlockOnWorkingChanged), $"Working Change Detected: [{(block.IsWorking ? "T" : "F")}] {block.BlockDefinition.Id.SubtypeName}");
            if (!block.IsWorking)
			{
				if (_activeImportantBlocks.ContainsKey(block.EntityId))
					_activeImportantBlocks.Remove(block.EntityId);
				_inactiveImportantBlocks.TryAdd(block.EntityId, block);
                DisownBlock(block);
				CheckBlockBalance();
				return;
			}

			if(_inactiveImportantBlocks.ContainsKey(block.EntityId))
				_inactiveImportantBlocks.Remove(block.EntityId);
			_activeImportantBlocks.TryAdd(block.EntityId, block);
			ClaimBlockOwnership(block);
		}

        /// <summary>
        /// Fires whenever a blocks ownership changes
        /// Only hooked for blocks we care about
        /// </summary>
        /// <param name="block"></param>
		private void BlockOnOwnershipChanged(IMyTerminalBlock block)
		{
            // TODO OnWorkingChanged and OnOwnershipChange kinda do the same thing.  Consider combining efforts and/or only hooking one of the events
            //  See BlockOnWorkingChanged for a better explanation
            WriteToLog(nameof(BlockOnOwnershipChanged), $"Ownership Change Detected: [{(block.IsWorking ? "T" : "F")}] {block.BlockDefinition.SubtypeName}");
            ClaimBlockOwnership((MyCubeBlock) block);
		}

        /// <summary>
        /// Sets the ownership of a given block to the owner of the grid (BigOwners[0])
        /// </summary>
        /// <param name="block"></param>
        private void ClaimBlockOwnership(MyCubeBlock block)
        {
            WriteToLog(nameof(ClaimBlockOwnership),
                $"Ownership change requested: [{_gridOwner}] [{block.OwnerId}] ([{(block.OwnerId == _gridOwner ? "T" : "F")}][{(block.IsWorking ? "T" : "F")}]) {block.BlockDefinition.Id.SubtypeName}");
            if (block.OwnerId == _gridOwner) return;
            var tb = block as IMyTerminalBlock;
            if (tb != null)
                _delayedActions.Enqueue(() =>
                {
                    block.ChangeOwner(_gridOwner, MyOwnershipShareModeEnum.Faction);
					IdentifyImportantBlock(block);
                });
        }

        /// <summary>
        /// Sets the ownership of a given block to nobody (id = 0)
        /// </summary>
        /// <param name="block"></param>
        private void DisownBlock(MyCubeBlock block)
        {   // SE likes to keep ownership of disabled blocks for... reasons.  Can cause issues with this mod idea.  Need to make sure the disabled blocks are properly disowned.
            WriteToLog(nameof(DisownBlock),
                $"Disown Block Requested: {block.BlockDefinition.Id.SubtypeName}");
            var tb = block as IMyTerminalBlock;
            if (tb != null)
                _delayedActions.Enqueue(() =>
                {
                    block.ChangeOwner(0, MyOwnershipShareModeEnum.All);
                    IdentifyImportantBlock(block);
                });
        }

		/// <summary>
		/// TODO
		/// 1) Make a collection for all blocks currently highlighted
		/// 2) Put that collection on a tick prune after say, 10 seconds
		///		When the prune comes up, all highlights go away
		/// 3) Add detection for the hand grinder
		///		This detection should come from the Core process and just alert a prefab it needs to show blocks
		/// 4) Separate methods for enabling and disabling highlights
		///		thickness = -1 disables the highlight, the rest of the settings are the same
		/// </summary>

        public void EnableBlockHighlights(long playerId)
        {
            if (playerId == 0)
            {
                EnableAllImportantBlockHighlightsForEveryone();
                return;
            }
            EnableHighlightForAllImportantBlocks(playerId);
        }

        private void EnableHighlightForAllImportantBlocks(long playerId)
        {
            var highlightedBlocks = new HighlightedBlocks()
            {
                TargetPlayer = playerId,
                Blocks = new HashSet<MyCubeBlock>()
            };
			foreach (KeyValuePair<long, MyCubeBlock> block in _activeImportantBlocks)
            {
                highlightedBlocks.Blocks.Add(block.Value);
                EnableBlockHighlight(block.Value, playerId);
            }
			if (!_highlightedBlocks.ContainsKey(_gridTick + HighlightDuration))
                _highlightedBlocks.TryAdd(_gridTick + HighlightDuration, new HashSet<HighlightedBlocks>());
			_highlightedBlocks[_gridTick + HighlightDuration].Add(highlightedBlocks);
        }

		private static void EnableBlockHighlight(MyCubeBlock block, long playerId)
        {
            MyVisualScriptLogicProvider.SetHighlight(block.Name, true, 2, 300, Color.MediumVioletRed, playerId);
        }

        private void EnableAllImportantBlockHighlightsForEveryone()
        {
            foreach (var block in _activeImportantBlocks)
            {
				MyVisualScriptLogicProvider.SetHighlightForAll(block.Value.Name, true, 2, 300, Color.MediumVioletRed);
			}
        }

        private void DisableAllImportantBlockHighlightsForEveryone()
        {
            foreach (var block in _activeImportantBlocks)
            {
                MyVisualScriptLogicProvider.SetHighlightForAll(block.Value.Name, false, -1, 300, Color.MediumVioletRed);
            }
        }

        private void DisableBlockHighlights(HashSet<HighlightedBlocks> hBlocks)
        {
            foreach (var blocks in hBlocks)
            {
				if (blocks.TargetPlayer == 0)
                {
                    DisableAllImportantBlockHighlightsForEveryone();
                    continue;
                }
                DisableHighlightForAllImportantBlocks(blocks);
			}
        }

        private static void DisableHighlightForAllImportantBlocks(HighlightedBlocks blocks)
        {
            foreach (var block in blocks.Blocks)
            {
				MyVisualScriptLogicProvider.SetHighlight(block.Name, false, -1, 300, Color.MediumVioletRed, blocks.TargetPlayer);
			}
        }

        private void RemoveFromHighlightedBlocks(MyCubeBlock block)
        {
            foreach (KeyValuePair<long, HashSet<HighlightedBlocks>> hBlocks in _highlightedBlocks)
            {
                foreach (HighlightedBlocks blocks in hBlocks.Value)
                {
                    blocks.RemoveBlock(block);
                }
            }
        }

        /// <summary>
        /// Processes generic actions delayed by 10 ticks from original call
        /// </summary>
		public void ProcessTickDelayedActions()
        {
            while (_delayedActions.Count > 0)
            {
                _delayedActions.Dequeue()?.Invoke();
            }
        }

        /// <summary>
        /// Processes items on a tick schedule
        /// </summary>
        public void ProcessPerTickActions()
        {
            _gridTick++;
            if (_highlightedBlocks.ContainsKey(_gridTick))
            {
				DisableBlockHighlights(_highlightedBlocks[_gridTick]);
            }
        }

        /// <summary>
        /// Checks the balance of important blocks and disowns the grid if none remain
        /// </summary>
		private void CheckBlockBalance()
		{
			if (_activeImportantBlocks.Count == 0)
				DisownGrid();
		}

        /// <summary>
        /// Sets the ownership of the grid to nobody (id = 0)
        /// TODO: Evaluate why this fails to change all ownership; specifically that of nonworking blocks which didn't properly drop ownership when hacked / disabled
        /// </summary>
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

        /// <summary>
        /// Closes the construct
        /// TODO: Clean this up
        /// </summary>
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

		/// <summary>
        /// Gets the nearest block to the source
        /// TODO: Probably unused, remove if not used by release
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
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

        //private readonly List<IMyPlayer> _reusablePlayerCollection = new List<IMyPlayer>();
		
        //MyAPIGateway.Players.GetPlayers(_reusablePlayerCollection, x => x.SteamUserId > 0 && x.Character != null);
		
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