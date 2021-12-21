using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using HostileTakeover.Common.BaseClasses;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
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
        private readonly HashSet<MyCubeGrid> _grids = new HashSet<MyCubeGrid>();

        ///// <summary>
        ///// Stores all the blocks actively being tracked as important
        /////     Once this list is empty, the entire construct is disowned
        /////     (long, MyCubeBlock) => (blockId, block)
        ///// </summary>
        //private readonly ConcurrentDictionary<long, MyCubeBlock> _activeControlBlocks = new ConcurrentDictionary<long, MyCubeBlock>();

        ///// <summary>
        ///// Stores all the blocks actively being tracked as important
        /////     Once this list is empty, the entire construct is disowned
        /////     (long, MyCubeBlock) => (blockId, block)
        ///// </summary>
        //private readonly ConcurrentDictionary<long, MyCubeBlock> _activeWeaponBlocks = new ConcurrentDictionary<long, MyCubeBlock>();

        /// <summary>
        /// Stores all the blocks that are tracked
        /// </summary>
        private readonly HashSet<MyCubeBlock> _activeBlocks2 = new HashSet<MyCubeBlock>();

        ///// <summary>
        ///// Stores all the blocks that were once tracked, or that should have been tracked had they been operational when scanned
        /////	    This is required to account for partially built blocks or blocks that may be repaired at some point
        /////     (long, MyCubeBlock) => (blockId, block)
        ///// </summary>
        //private readonly HashSet<MyCubeBlock> _inactiveBlocks = new HashSet<MyCubeBlock>();

        /// <summary>
        /// Contains a queue of generic actions that fire on a 10 tick schedule from the mod core
        /// </summary>
        private readonly Queue<Action> _10TickDelayedActions = new Queue<Action>();

        /// <summary>
        /// Contains a queue of generic actions that fire on a 1 tick schedule from the mod core
        /// </summary>
        private readonly Queue<Action> _perTickDelayedActions = new Queue<Action>();

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

        private const int HighlightPulseDuration = 10;

        private const int DetectionRange = 50;

        /// <summary>
        /// Private class that controls basic highlighted block data
        /// </summary>
        private class HighlightedBlocks
        {
            public long TargetPlayer;
            public Color Color;
            public HashSet<MyCubeBlock> Blocks;
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
            if (_grids.Contains(grid)) return;
			_grids.Add(grid);
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
            //grid.OnFatBlockRemoved += OnFatBlockClosed;
            grid.OnFatBlockClosed += OnFatBlockClosed;
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
            //grid.OnFatBlockRemoved -= OnFatBlockClosed;
            grid.OnFatBlockClosed -= OnFatBlockClosed;
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
            if (!(block is IMyTerminalBlock)) return;
            //WriteToLog(nameof(OnFatBlockAdded), $"Block add detected: [{(block.IsWorking ? "T" : "F")}] {block.BlockDefinition.Id.SubtypeName}");
            SetAppropriateOwnership(block);
			IdentifyImportantBlock(block);
		}

        /// <summary>
        /// This fires whenever a fat block is removed from the grid
        /// Even though all fat blocks aren't important blocks, important blocks still need to be scrubbed from all possible tracking sources
        /// So just pass the block to the CloseBlock method for final processing
        /// This method covers both block removed and block closed cases
        /// </summary>
        /// <param name="block"></param>
        private void OnFatBlockClosed(MyCubeBlock block)
        {
            if (!(block is IMyTerminalBlock)) return;
            //WriteToLog(nameof(OnFatBlockClosed), $"Block removal detected: {block.BlockDefinition.Id.SubtypeName}");
			CloseBlock(block);
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
            //WriteToLog(nameof(OnGridSplit), $"Grid split detected: {oldGrid.EntityId} | {newGrid.EntityId}");
			foreach (var block in newGrid.GetFatBlocks())
            {
                if (!(block is IMyTerminalBlock)) continue;
                CloseBlock(block);
            }
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
            if (!(block is IMyTerminalBlock)) return;
            RemoveFromImportantBlocks(block);
            RemoveFromHighlightedBlocks(block);
            BlockDeRegisterEvents(block);
            CheckBlockBalance();
        }

        /// <summary>
        /// Sends all fat blocks on a grid for important block identification
        /// </summary>
        /// <param name="grid"></param>
    	private void FindImportantBlocks(MyCubeGrid grid)
		{
			foreach (MyCubeBlock block in grid.GetFatBlocks())
				IdentifyImportantBlock(block);
		}

        // TODO: Try to make the logic for blocks automatic and not based on a manual list
        // TODO: This idea will require a lot of testing 
        // TODO: Pay special attention to Weapon Core weapons
        // TODO:    Will probably need to import the Weapon Core API for proper weapon checking

        /// <summary>
        /// Important block types:
        ///     Groups shown in order of appearance for highlights
        /// 
        /// Group: Control [Blue]
        ///     Cockpits
        ///     Remote Controls
        ///     *AHEM* TBD BLOCK THAT CONTROLS STUFF
        ///
        /// Group: Medical [Red]
        ///     Medical Centers
        ///     Survival Kits
        ///     Cryo Chambers
        /// 
        /// Group: Weapon [Yellow?]
        ///     Turrets (all non-fixed fire weapons)
        ///     Upgrade module with "BotSpawner" subtype (jTurp's Ai bots)
        ///     Sorters with WeaponCore weapons (need WC API for this perhaps?  try without first)
        ///
        /// Group: Trap [Green?]
        ///     Warheads
        ///
        /// </summary>

        private readonly MyStringHash _control = MyStringHash.GetOrCompute("Control");
        private readonly MyStringHash _medical = MyStringHash.GetOrCompute("Medical");
        private readonly MyStringHash _weapon = MyStringHash.GetOrCompute("Weapon");
        private readonly MyStringHash _trap = MyStringHash.GetOrCompute("Trap");

        private readonly Dictionary<MyStringHash, HashSet<MyCubeBlock>> _importantBlocks =
            new Dictionary<MyStringHash, HashSet<MyCubeBlock>>(MyStringHash.Comparer)
            {
                { MyStringHash.GetOrCompute("Control"), new HashSet<MyCubeBlock>() },
                { MyStringHash.GetOrCompute("Medical"), new HashSet<MyCubeBlock>() },
                { MyStringHash.GetOrCompute("Weapon"), new HashSet<MyCubeBlock>() },
                { MyStringHash.GetOrCompute("Trap"), new HashSet<MyCubeBlock>() }
            };

        /// <summary>
        /// Identifies important blocks
        /// Also sends all important blocks out for event registration
        /// </summary>
        /// <param name="block"></param>
        private void IdentifyImportantBlock(MyCubeBlock block)
        {
            if (!(block is IMyTerminalBlock)) return;
            _10TickDelayedActions.Enqueue(() =>
            {
                if (!AssignBlock(block)) return;
                BlockRegisterEvents(block);
                //WriteToLog(nameof(IdentifyImportantBlock), $"Adding to important blocks: [{block.GetType()}] [{block.BlockDefinition.Id.TypeId}] [{block.BlockDefinition.Id.SubtypeName}]");
            });
        }

        private bool AssignBlock(MyCubeBlock block)
        {
            var controller = block as IMyShipController;
            if (controller != null && controller.CanControlShip)
            {
                _importantBlocks[_control].Add(block);
                SetAppropriateOwnership(block);
                //WriteToLog(nameof(AssignBlock), $"Adding new Controller: [{block.BlockDefinition.Id.SubtypeId}] [{block.BlockDefinition.Id.SubtypeName}]");
                return true;
            }

            var medical = block as IMyMedicalRoom;
            if (medical != null)
            {
                _importantBlocks[_medical].Add(block);
                SetAppropriateOwnership(block);
                //WriteToLog(nameof(AssignBlock), $"Adding new Medical: [{block.BlockDefinition.Id.SubtypeId}] [{block.BlockDefinition.Id.SubtypeName}]");
                return true;
            }
            
            var cryo = block as IMyCryoChamber;
            if (cryo!= null)
            {
                _importantBlocks[_medical].Add(block);
                SetAppropriateOwnership(block);
               //WriteToLog(nameof(AssignBlock), $"Adding new Medical: [{block.BlockDefinition.Id.SubtypeId}] [{block.BlockDefinition.Id.SubtypeName}]");
                return true;
            }

            var weapon = block as IMyLargeTurretBase;
            if (weapon != null)
            {
                _importantBlocks[_weapon].Add(block);
                SetAppropriateOwnership(block);
                //WriteToLog(nameof(AssignBlock), $"Adding new Weapon: [{block.BlockDefinition.Id.SubtypeId}] [{block.BlockDefinition.Id.SubtypeName}]");
                return true;
            }

            var sorter = block as MyConveyorSorter;
            if (sorter != null && !sorter.BlockDefinition.Context.IsBaseGame)
            {
                _importantBlocks[_weapon].Add(block);
                SetAppropriateOwnership(block);
                //WriteToLog(nameof(AssignBlock), $"Adding new Weapon: [{block.BlockDefinition.Id.SubtypeId}] [{block.BlockDefinition.Id.SubtypeName}]");
                return true;
            }

            var warhead = block as IMyWarhead;
            if (warhead != null)
            {
                _importantBlocks[_trap].Add(block);
                SetAppropriateOwnership(block);
                //WriteToLog(nameof(AssignBlock), $"Adding new Trap: [{block.BlockDefinition.Id.SubtypeId}] [{block.BlockDefinition.Id.SubtypeName}]");
                return true;
            }

            if (block.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_SurvivalKit))
            {
                _importantBlocks[_medical].Add(block);
                SetAppropriateOwnership(block);
                //WriteToLog(nameof(AssignBlock), $"Adding new Skit: [{block.BlockDefinition.Id.SubtypeId}] [{block.BlockDefinition.Id.SubtypeName}]");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes an important block from both active and inactive collections
        /// </summary>
        /// <param name="block"></param>
        private void RemoveFromImportantBlocks(MyCubeBlock block)
        {
            foreach (var kvp in _importantBlocks)
                WriteToLog(nameof(RemoveFromImportantBlocks), $"Removing important block: [{(kvp.Value.Remove(block) ? "T" : "F")}] {block.BlockDefinition.Id.SubtypeName}");
            //kvp.Value.Remove(block);
            //WriteToLog(nameof(RemoveFromImportantBlocks), $"Removing important block: {block.BlockDefinition.Id.SubtypeName}");
        }

        /// <summary>
        /// Registers all block level events we care about
        /// </summary>
        /// <param name="block"></param>
        private void BlockRegisterEvents(MyCubeBlock block)
        {
            block.IsWorkingChanged += BlockOnWorkingChanged;
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
            SetAppropriateOwnership(block);
            //WriteToLog(nameof(BlockOnWorkingChanged), $"Working Change Detected: [{(block.IsWorking ? "T" : "F")}] [{block.EntityId}] [{block.BlockDefinition.Id.SubtypeName}]");
        }

        /// <summary>
        /// Fires whenever a blocks ownership changes
        /// Only hooked for blocks we care about
        /// </summary>
        /// <param name="block"></param>
		private void BlockOnOwnershipChanged(IMyTerminalBlock block)
		{
            SetAppropriateOwnership((MyCubeBlock)block);
            //WriteToLog(nameof(BlockOnOwnershipChanged), $"Ownership Change Detected: [{(block.IsWorking ? "T" : "F")}] [{block.EntityId}] [{block.BlockDefinition.SubtypeName}]");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="block"></param>
        private void SetAppropriateOwnership(MyCubeBlock block)
        {
            _10TickDelayedActions.Enqueue(() =>
                {
                    //WriteToLog(nameof(SetAppropriateOwnership), $"Ownership Change Requested: [{(block.IsWorking ? "T" : "F")}] [{block.EntityId}] [{block.BlockDefinition.Id.SubtypeName}]");
                    if (block.MarkedForClose) return;
                    var tb = block as IMyTerminalBlock;
                    if (tb == null) return;
                    if (block.IsWorking && block.OwnerId != _gridOwner)
                        ClaimBlock(block);
                    else if (!block.IsWorking && block.OwnerId != 0)
                        DisownBlock(block);
                    CheckBlockBalance();
                }
            );
        }

        /// <summary>
        /// Sets the ownership of a given block to the owner of the grid (BigOwners[0])
        /// </summary>
        /// <param name="block"></param>
        private void ClaimBlock(MyCubeBlock block)
        {
            block.ChangeOwner(_gridOwner, MyOwnershipShareModeEnum.Faction);
            //TrackBlock(block);
            //WriteToLog(nameof(ClaimBlock), $"Claiming: [{(block.IsWorking ? "T" : "F")}] [{block.BlockDefinition.Id.SubtypeId}] [{block.BlockDefinition.Id.SubtypeName}]");
        }

        /// <summary>
        /// Sets the ownership of a given block to nobody (id = 0)
        /// </summary>
        /// <param name="block"></param>
        private void DisownBlock(MyCubeBlock block)
        {   
            block.ChangeOwner(0, MyOwnershipShareModeEnum.All);
            //CheckBlockBalance();
            //UntrackBlock(block);
            //WriteToLog(nameof(DisownBlock), $"Disowning: [{(block.IsWorking ? "T" : "F")}] [{block.BlockDefinition.Id.SubtypeId}] [{block.BlockDefinition.Id.SubtypeName}]");
        }

        //private bool TrackBlock(MyCubeBlock block)
        //{
        //    WriteToLog(nameof(TrackBlock), $"Tracking: [{(block.IsWorking ? "T" : "F")}] [{block.BlockDefinition.Id.SubtypeId}] [{block.BlockDefinition.Id.SubtypeName}]");
        //    return block.IsWorking && _activeBlocks.Add(block);
        //}

        //private bool UntrackBlock(MyCubeBlock block)
        //{
        //    WriteToLog(nameof(UntrackBlock), $"No longer tracking: [{(block.IsWorking ? "T" : "F")}] [{block.BlockDefinition.Id.SubtypeId}] [{block.BlockDefinition.Id.SubtypeName}]");
        //    if (block.IsWorking) return false;
        //    if (!_activeBlocks.Remove(block)) return false;
        //    CheckBlockBalance();
        //    return true;
        //}

        /// TODO Need to only care about active (i.e. working) blocks in the below collections when sending for highlighting...

        private readonly HashSet<MyCubeBlock> _reusableBlocksCollection = new HashSet<MyCubeBlock>();

        public void EnableBlockHighlights(long playerId)
        {
            WriteToLog(nameof(EnableBlockHighlights), this.ToString());
            _reusableBlocksCollection.Clear();
            if (_importantBlocks[_control].Count > 0)
            {
                //WriteToLog(nameof(EnableBlockHighlights), $"Highlighting [{_importantBlocks[_control].Count:00}] blocks.");
                foreach (var block in _importantBlocks[_control])
                {
                    //WriteToLog(nameof(EnableBlockHighlights), $"{" ",-6}[{(block.IsWorking ? "T" : "F")}][{block.EntityId,-18:000000000000000000}] TypeId: {block.BlockDefinition.Id.TypeId,-20}  SubtypeId: {block.BlockDefinition.Id.SubtypeName}");
                    if (block.IsWorking)
                        _reusableBlocksCollection.Add(block);
                }
            }
            if (CheckHighlightCollection(_reusableBlocksCollection, playerId, Color.Orange)) return;

            if (_importantBlocks[_medical].Count > 0)
            {
                //WriteToLog(nameof(EnableBlockHighlights), $"Highlighting [{_importantBlocks[_medical].Count:00}] blocks.");
                foreach (var block in _importantBlocks[_medical])
                {
                    if (block.IsWorking)
                        _reusableBlocksCollection.Add(block);
                }
            }
            if (CheckHighlightCollection(_reusableBlocksCollection, playerId, Color.Red)) return;

            if (_importantBlocks[_weapon].Count > 0)
            {
                //WriteToLog(nameof(EnableBlockHighlights), $"Highlighting [{_importantBlocks[_weapon].Count:00}] blocks.");
                foreach (var block in _importantBlocks[_weapon])
                {
                    if (block.IsWorking)
                        _reusableBlocksCollection.Add(block);
                }
            }
            if (CheckHighlightCollection(_reusableBlocksCollection, playerId, Color.Yellow)) return;

            if (_importantBlocks[_trap].Count > 0)
            {
                //WriteToLog(nameof(EnableBlockHighlights), $"Highlighting [{_importantBlocks[_trap].Count:00}] blocks.");
                foreach (var block in _importantBlocks[_trap])
                {
                    if (block.IsWorking)
                        _reusableBlocksCollection.Add(block);
                }
            }
            if (CheckHighlightCollection(_reusableBlocksCollection, playerId, Color.Green)) return;
        }

        private bool CheckHighlightCollection(HashSet<MyCubeBlock> blocks, long playerId, Color color)
        {
            //WriteToLog(nameof(CheckHighlightCollection), $"Validating [{blocks.Count:00}] blocks.");
            if (blocks.Count <= 0) return false;
            //WriteToLog(nameof(CheckHighlightCollection), $"Highlighting [{blocks.Count:00}] blocks.");
            EnableBlockListHighlights(blocks, playerId, color);
            return true;
        }

        // MyTuple<long, MyCubeBlock, long, Color> hlb = new MyTuple<long, MyCubeBlock, long, Color>();

        private void EnableBlockListHighlights(HashSet<MyCubeBlock> blocks, long playerId, Color color)
        {
            var hlb = new HighlightedBlocks()
            {
                TargetPlayer = playerId,
                Color = color,
                Blocks = new HashSet<MyCubeBlock>()
            };

            foreach (MyCubeBlock block in blocks)
            {
                EnableHighlight(block, playerId, color);
                hlb.Blocks.Add(block);

            }

            long removalTick = _gridTick + HighlightDuration;

            if (!_highlightedBlocks.ContainsKey(removalTick))
                _highlightedBlocks.TryAdd(removalTick, new HashSet<HighlightedBlocks>());
            _highlightedBlocks[removalTick].Add(hlb);
        }

        private static void EnableHighlight(MyCubeBlock block, long playerId, Color color)
        {
            MyVisualScriptLogicProvider.SetHighlight(block.Name, true, 2, HighlightPulseDuration, color, playerId);
        }

        private static void DisableHighlight(MyCubeBlock block, long playerId, Color color)
        {
            MyVisualScriptLogicProvider.SetHighlight(block.Name, false, -1, HighlightPulseDuration, color, playerId);
        }

        private void DisableHighlights(HashSet<HighlightedBlocks> hBlocks)
        {
            foreach (var x in hBlocks)
                foreach (var y in x.Blocks)
                    DisableHighlight(y, x.TargetPlayer, x.Color);
        }
        
        private void RemoveFromHighlightedBlocks(MyCubeBlock block)
        {
            foreach (KeyValuePair<long, HashSet<HighlightedBlocks>> kvp in _highlightedBlocks)
                foreach (HighlightedBlocks blocks in kvp.Value)
                    blocks.Blocks.Remove(block);
        }

        /// <summary>
        /// Processes generic actions delayed by 10 ticks from original call
        /// </summary>
        public void ProcessTickDelayedActions()
        {
            while (_10TickDelayedActions.Count > 0)
            {
                _10TickDelayedActions.Dequeue()?.Invoke();
            }
        }

        /// <summary>
        /// Processes items on a tick schedule
        /// </summary>
        public void ProcessPerTickActions()
        {
            _gridTick++;
            HashSet<HighlightedBlocks> blocks;
            if (_highlightedBlocks.TryRemove(_gridTick, out blocks))
                DisableHighlights(blocks);
        }

        private bool CheckAnyActiveBlocksExist()
        {
            foreach (var kvp in _importantBlocks)
            {
                foreach (var block in kvp.Value)
                {
                    if (!block.IsWorking) continue;
                    return true;
                }
            }
            return false;
        }

        private HashSet<MyCubeBlock> GetActiveBlockList()
        {
            HashSet<MyCubeBlock> activeBlocks = new HashSet<MyCubeBlock>();
            foreach (var kvp in _importantBlocks)
            {
                foreach (var block in kvp.Value)
                {
                    if (!block.IsWorking) continue;
                    activeBlocks.Add(block);
                }
            }
            return activeBlocks;
        }

        /// <summary>
        /// Checks the balance of important blocks and disowns the grid if none remain
        /// </summary>
		private void CheckBlockBalance()
        {
            //WriteToLog(nameof(CheckBlockBalance), $"{GetActiveBlockList().Count}");
            if (CheckAnyActiveBlocksExist()) return;
            DisownGrid();
			//if (_activeBlocks.Count == 0)
				//DisownGrid();
		}

        /// <summary>
        /// Sets the ownership of the grid to nobody (id = 0)
        /// TODO: Evaluate why this fails to change all ownership; specifically that of nonworking blocks which didn't properly drop ownership when hacked / disabled
        /// </summary>
        private void DisownGrid()
		{
            //WriteToLog(nameof(DisownGrid), $"Disowning Grid.");
            foreach (var grid in _grids)
				GridSetOwnership(grid);
			Close();
		}
		
        private void GridOnClose(MyEntity grid)
        {
            // There is no reason to close out the important blocks from this grid.
            // When the grid closes, the blocks will close, which will prune them from the other dictionaries
            GridDeRegisterEvents((MyCubeGrid)grid);
            _grids.Remove((MyCubeGrid)grid);
            if (_grids.Count == 0) Close();
        }

        /// <summary>
        /// Closes the construct
        /// </summary>
        public override void Close()
        {
            foreach (var kvp in _importantBlocks)
                kvp.Value.Clear();
            //_activeBlocks.Clear();                   
            _grids.Clear();
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
			foreach (var block in GetActiveBlockList())
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
		
		public override string ToString()
		{
			_report.Clear();
            _report.AppendLine();
            _report.AppendLine();
            _report.AppendFormat("{0,-2}Report for Grid Construct: [{1:000000000000000000}]", "", GridId);
			_report.AppendLine();
            _report.AppendFormat("{0,-4}Owner: {1}"," ", _gridOwner);
			_report.AppendLine();
            _report.AppendLine();

            _report.AppendFormat("{0,-4}***** Grids [{1}] *****", " ", _grids.Count);
            _report.AppendLine();
            foreach (var grid in _grids)
            {
                _report.AppendFormat("{0,-6}ID: {1,-20}  Name: {2}", " ", grid.EntityId, grid.DisplayName);
                _report.AppendLine();
            }

            _report.AppendLine();
            _report.AppendFormat("{0,-4}***** Active Important Blocks *****", " ");
            _report.AppendLine();
            foreach (var block in GetActiveBlockList())
			{
                _report.AppendFormat("{0,-6}[{1,-18:000000000000000000}] TypeId: {2,-20}  SubtypeId: {3}", " ", block.EntityId, block.BlockDefinition.Id.TypeId, block.BlockDefinition.Id.SubtypeName);
                _report.AppendLine();
			}
            
            _report.AppendLine();
            _report.AppendFormat("{0,-4}***** Important Block Dictionary *****", " ");
            _report.AppendLine();
            _report.AppendLine();
            _report.AppendFormat("{0,-6}**** [{1:00}] Control Blocks ****", " ", _importantBlocks[_control].Count);
            _report.AppendLine();
            foreach (var block in _importantBlocks[_control])
            {
                _report.AppendFormat("{0,-6}[{1}][{2,-18:000000000000000000}] TypeId: {3,-20}  SubtypeId: {4}", " ", (block.IsWorking ? "T" : "F"), block.EntityId, block.BlockDefinition.Id.TypeId, block.BlockDefinition.Id.SubtypeName);
                _report.AppendLine();
            }
            
            _report.AppendLine();
            _report.AppendFormat("{0,-6}**** [{1:00}] Medical Blocks ****", " ", _importantBlocks[_medical].Count);
            _report.AppendLine();
            foreach (var block in _importantBlocks[_medical])
            {
                _report.AppendFormat("{0,-6}[{1}][{2,-18:000000000000000000}] TypeId: {3,-20}  SubtypeId: {4}", " ", (block.IsWorking ? "T" : "F"), block.EntityId, block.BlockDefinition.Id.TypeId, block.BlockDefinition.Id.SubtypeName);
                _report.AppendLine();
            }
            
            _report.AppendLine();
            _report.AppendFormat("{0,-6}**** [{1:00}] Weapon Blocks ****", " ", _importantBlocks[_weapon].Count);
            _report.AppendLine();
            foreach (var block in _importantBlocks[_weapon])
            {
                _report.AppendFormat("{0,-6}[{1}][{2,-18:000000000000000000}] TypeId: {3,-20}  SubtypeId: {4}", " ", (block.IsWorking ? "T" : "F"), block.EntityId, block.BlockDefinition.Id.TypeId, block.BlockDefinition.Id.SubtypeName);
                _report.AppendLine();
            }
            
            _report.AppendLine();
            _report.AppendFormat("{0,-6}**** [{1:00}] Trap Blocks ****", " ", _importantBlocks[_trap].Count);
            _report.AppendLine();
            foreach (var block in _importantBlocks[_trap])
            {
                _report.AppendFormat("{0,-6}[{1}][{2,-18:000000000000000000}] TypeId: {3,-20}  SubtypeId: {4}", " ", (block.IsWorking ? "T" : "F"), block.EntityId, block.BlockDefinition.Id.TypeId, block.BlockDefinition.Id.SubtypeName);
                _report.AppendLine();
            }

            _report.AppendLine();
            return _report.ToString();
		}
	}
}