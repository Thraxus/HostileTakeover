using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using HostileTakeover.Common.BaseClasses;
using HostileTakeover.Common.Extensions;
using HostileTakeover.Common.Utilities.Statics;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
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
        private long _gridOwner;

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

        private readonly StringBuilder _report = new StringBuilder();

        /// <summary>
        /// Time a block should be highlighted for
        /// </summary>
        private const long HighlightDuration = Common.Settings.TicksPerSecond * 10;

        private const int HighlightPulseDuration = 10;
        
        private bool _npcOwned;

        private readonly MyCubeGrid _mainGrid;

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
            _mainGrid = grid;
            SetGridOwner();
            _npcOwned = IsNpcOwned();
            Add(grid);
        }

        private void SetGridOwner()
        {
            _gridOwner = _mainGrid.BigOwners.Count > 0 ? _mainGrid.BigOwners[0] : 0;
        }

        private bool IsNpcOwned()
        {
            return _mainGrid.BigOwners.Count != 0 && MyAPIGateway.Players.TryGetSteamId(_mainGrid.BigOwners[0]) <= 0;
        }
        
        private void GridOnClose(MyEntity grid)
        {
            // There is no reason to close out the important blocks from this grid.
            // When the grid closes, the blocks will close, which will prune them from the other dictionaries
            WriteToLog(nameof(GridOnClose), $"Closing grid: [{grid.EntityId:D18}] ");
            GridDeRegisterCommonEvents((MyCubeGrid)grid);
            if (IsNpcOwned()) GridDeRegisterNpcEvents((MyCubeGrid)grid);
            _grids.Remove((MyCubeGrid)grid);
            if (_grids.Count == 0) Close();
        }

        /// <summary>
        /// Closes the construct
        /// </summary>
        public override void Close()
        {
            WriteToLog(nameof(GridOnClose), $"Closing construct: [{GridId:D18}] [{_grids.Count:D2}]");
            _grids.Clear();
            OnCloseConstruct?.Invoke(this);
            base.Close();
        }

        /// <summary>
        /// Adds a new grid to this construct
        /// Also sets up all grid level event registrations and identifies all important blocks
        /// </summary>
        /// <param name="grid"></param>
        public void Add(MyCubeGrid grid)
        {
            _perTickDelayedActions.Enqueue(() =>
            {
                if (!_grids.Add(grid)) return;
                GridRegisterCommonEvents(grid);

                if (!IsNpcOwned()) return;

                GridRegisterNpcEvents(grid);
                FindImportantBlocks(grid);
            });
        }

        /// <summary>
        /// Sets the ownership of the grid to nobody (id = 0)
        /// </summary>
        private void DisownGrid()
        {
            //WriteToLog(nameof(DisownGrid), $"Disowning Grid.");
            foreach (var grid in _grids)
                GridSetNoOwnership(grid);
            DeRegisterAllImportantBlockEvents();
        }
        
        /// <summary>
        /// Determines whether a grid should be subject to logic or not
        /// Logic should only run on NPC owned grids
        /// </summary>
        /// <param name="grid"></param>
        private void GridOnBlockOwnershipChanged(MyCubeGrid grid)
        {
            bool wasNpcOwned = _npcOwned;
            _npcOwned = IsNpcOwned();
            WriteToLog(nameof(GridOnBlockOwnershipChanged), $"Was Npc Owned: [{wasNpcOwned.ToSingleChar()}]");
            WriteToLog(nameof(GridOnBlockOwnershipChanged), $" Is Npc Owned: [{IsNpcOwned().ToSingleChar()}]");
            //if (wasNpcOwned == IsNpcOwned()) return;

            _report.Clear();
            _report.AppendLine();
            _report.AppendLine();
            _report.AppendFormat("{0,-2}**** {1} ****", " ", nameof(GridOnBlockOwnershipChanged));
            _report.AppendLine();
            _report.AppendFormat("{0,-4}Grid Tick: [{1:D18}]", " ", _gridTick);
            _report.AppendLine();
            _report.AppendFormat("{0,-4}Was Npc Owned: [{1}]", " ", wasNpcOwned.ToSingleChar());
            _report.AppendLine();
            _report.AppendFormat("{0,-4} Is Npc Owned: [{1}]", " ", IsNpcOwned().ToSingleChar());
            _report.AppendLine();
            _report.AppendFormat("{0,-4}     No Owner: [{1}]", " ", (grid.BigOwners.Count == 0).ToSingleChar());
            _report.AppendLine();
            _report.AppendFormat("{0,-4}Main Grid: [{1:D18}] {2}", " ", _mainGrid.EntityId, _mainGrid.DisplayName);
            _report.AppendLine();
            _report.AppendFormat("{0,-4}Act Owner: [{1:D18}]", " ", _mainGrid.BigOwners.Count > 0 ? _mainGrid.BigOwners[0] : 0);
            _report.AppendLine();
            _report.AppendFormat("{0,-4}Rec Owner: [{1:D18}]", " ", _gridOwner);
            _report.AppendLine();
            _report.AppendFormat("{0,-4}Triggered Grid: [{1:D18}] {2}", " ", grid.EntityId, grid.DisplayName);
            _report.AppendLine();
            _report.AppendFormat("{0,-4}         Owner: [{1:D18}]", " ", grid.BigOwners.Count > 0 ? grid.BigOwners[0] : 0);
            _report.AppendLine();
            _report.AppendLine();
            
            // Was NPC Owned: False
            //  Is NPC Owned: True
            // == Was player owned, now is NPC owned, need to setup scanning for blocks

            // Was NPC Owned: False
            //  Is NPC Owned: False
            // == Was not NPC owned and is still not NPC owned, Do nothing

            // Was NPC Owned: True
            //  Is NPC Owned: False
            // == Was NPC owned and, now is not NPC Owned, deRegister everything

            if (!wasNpcOwned && IsNpcOwned())
            {
                _report.AppendFormat("{0,-4}Running: Some other owner and is now NPC Owned logic...", " ");
                _report.AppendLine();
                _gridOwner = _mainGrid.BigOwners[0];
                //_npcOwned = IsNpcOwned();
                foreach (var subGrid in _grids)
                {
                    _report.AppendFormat("{0,-6}Processing: [{1:D18}] {2}", " ", subGrid.EntityId, subGrid.DisplayName);
                    _report.AppendLine();
                    GridRegisterNpcEvents(subGrid);
                    FindImportantBlocks(subGrid);
                }
                _report.AppendLine();
                WriteToLog(nameof(GridOnBlockOwnershipChanged), _report.ToString());
                return;
            }

            if (wasNpcOwned && !IsNpcOwned())
            {
                _report.AppendFormat("{0,-4}Running: Was NPC Owned Now some other owner...", " ");
                _report.AppendLine();
                _gridOwner = _mainGrid.BigOwners.Count == 0 ? 0 : _mainGrid.BigOwners[0];
                //_npcOwned = IsNpcOwned();
                foreach (var subGrid in _grids)
                {
                    _report.AppendFormat("{0,-6}Processing: [{1:D18}] {2}", " ", subGrid.EntityId, subGrid.DisplayName);
                    _report.AppendLine();
                    GridDeRegisterNpcEvents(subGrid);
                    DeRegisterAllImportantBlockEvents();
                }
                _report.AppendLine();
                WriteToLog(nameof(GridOnBlockOwnershipChanged), _report.ToString());
                return;
            }

            _report.AppendLine();
            _report.AppendFormat("{0,-4}Ownership unchanged... nothing to see here!", " ");
            _report.AppendLine();
            WriteToLog(nameof(GridOnBlockOwnershipChanged), _report.ToString());
        }
        
        private void DeRegisterAllImportantBlockEvents()
        {
            foreach (var kvp in _importantBlocks)
            {
                foreach (MyCubeBlock block in kvp.Value)
                {
                    BlockDeRegisterEvents(block);
                }
            }
            _importantBlocks[_control].Clear();
            _importantBlocks[_medical].Clear();
            _importantBlocks[_weapon].Clear();
            _importantBlocks[_trap].Clear();
        }

        private void GridRegisterCommonEvents(MyCubeGrid grid)
        {
            grid.OnBlockOwnershipChanged += GridOnBlockOwnershipChanged;
            grid.OnClose += GridOnClose;
        }

        private void GridRegisterNpcEvents(MyCubeGrid grid)
        {
            grid.OnFatBlockAdded += OnFatBlockAdded;
            grid.OnFatBlockClosed += OnFatBlockClosed;
            grid.OnGridSplit += OnGridSplit;
            MyCubeGrid.OnSplitGridCreated += OnSplitGridCreated;
        }

        private void GridDeRegisterCommonEvents(MyCubeGrid grid)
        {
            grid.OnBlockOwnershipChanged -= GridOnBlockOwnershipChanged;
            grid.OnClose -= GridOnClose;
        }

        private void GridDeRegisterNpcEvents(MyCubeGrid grid)
        {
            grid.OnFatBlockAdded -= OnFatBlockAdded;
            grid.OnFatBlockClosed -= OnFatBlockClosed;
            grid.OnGridSplit -= OnGridSplit;
            MyCubeGrid.OnSplitGridCreated -= OnSplitGridCreated;
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
            _report.Clear();
            _report.AppendLine();
            _report.AppendLine();
            _report.AppendFormat("{0,-2}**** {1} ****", " ", nameof(OnGridSplit));
            _report.AppendLine();
            _report.AppendFormat("{0,-4}Parent Grid: [{1:D18}] {2}", " ", _mainGrid.EntityId, _mainGrid.DisplayName);
            _report.AppendLine();
            _report.AppendFormat("{0,-4}   Old Grid: [{1:D18}] {2}", " ", oldGrid.EntityId, oldGrid.DisplayName);
            _report.AppendLine();
            _report.AppendFormat("{0,-4}   New Grid: [{1:D18}] {2}", " ", newGrid.EntityId, newGrid.DisplayName);
            _report.AppendLine();
            foreach (var gridX in _grids)
            {
                _report.AppendFormat("{0,-6}Related Grid: [{1:D18}] {2}", " ", gridX.EntityId, gridX.DisplayName);
                _report.AppendLine();
            }
            _report.AppendLine();

            WriteToLog(nameof(OnGridSplit), _report.ToString());

            //WriteToLog(nameof(OnGridSplit), $"Grid split detected: {oldGrid.EntityId:D18} | {newGrid.EntityId:D18}");
			foreach (var block in newGrid.GetFatBlocks())
            {
                if (!(block is IMyTerminalBlock)) continue;
                CloseBlock(block);
            }
		}

        private void OnSplitGridCreated(MyCubeGrid grid)
        {
            _report.Clear();
            _report.AppendLine();
            _report.AppendLine();
            _report.AppendFormat("{0,-2}**** {1} ****", " ", nameof(OnSplitGridCreated));
            _report.AppendLine();
            _report.AppendFormat("{0,-4}Parent Grid: [{1:D18}] {2}", " ", _mainGrid.EntityId, _mainGrid.DisplayName);
            _report.AppendLine();
            _report.AppendFormat("{0,-4} Split Grid: [{1:D18}] {2}", " ", grid.EntityId, _mainGrid.DisplayName);
            _report.AppendLine();
            _report.AppendLine();
            foreach (var gridX in _grids)
            {
                _report.AppendFormat("{0,-6}Related Grid: [{1:D18}] {2}", " ", gridX.EntityId, gridX.DisplayName);
                _report.AppendLine();
            }
            _report.AppendLine();

            WriteToLog(nameof(OnSplitGridCreated), _report.ToString());
        }

        /// <summary>
        /// Disowns the grid
        /// Known problem that not all blocks switch ownership - need to investigate why
        /// Disabled blocks sometimes do not change owner, this is hopefully fixed by the DisownBlock(MyCubeBlock block) method
        /// Requires further testing
        /// </summary>
        /// <param name="grid"></param>
		private void GridSetNoOwnership(MyCubeGrid grid)
		{
            _gridOwner = 0;
            grid.ChangeGridOwnership(_gridOwner, MyOwnershipShareModeEnum.All);
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
            if (!IsNpcOwned()) return;
            if (!(block is IMyTerminalBlock)) return;
            _10TickDelayedActions.Enqueue(() =>
            {
                if (!AssignBlock(block)) return;
                BlockRegisterEvents(block);
                CheckBlockBalance();
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
                return true;
            }

            var medical = block as IMyMedicalRoom;
            if (medical != null)
            {
                _importantBlocks[_medical].Add(block);
                SetAppropriateOwnership(block);
                return true;
            }
            
            var cryo = block as IMyCryoChamber;
            if (cryo!= null)
            {
                _importantBlocks[_medical].Add(block);
                SetAppropriateOwnership(block);
                return true;
            }

            var weapon = block as IMyLargeTurretBase;
            if (weapon != null)
            {
                _importantBlocks[_weapon].Add(block);
                SetAppropriateOwnership(block);
                return true;
            }

            var sorter = block as MyConveyorSorter;
            if (sorter != null && !sorter.BlockDefinition.Context.IsBaseGame)
            {
                _importantBlocks[_weapon].Add(block);
                SetAppropriateOwnership(block);
                return true;
            }

            var warhead = block as IMyWarhead;
            if (warhead != null)
            {
                _importantBlocks[_trap].Add(block);
                SetAppropriateOwnership(block);
                return true;
            }

            if (block.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_SurvivalKit))
            {
                _importantBlocks[_medical].Add(block);
                SetAppropriateOwnership(block);
                return true;
            }

            var upgrade = block as IMyUpgradeModule;
            if (upgrade != null && block.BlockDefinition.Id.SubtypeId == MyStringHash.GetOrCompute("BotSpawner"))
            {
                _importantBlocks[_weapon].Add(block);
                SetAppropriateOwnership(block);
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
            WriteToLog(nameof(BlockOnWorkingChanged), $"Working Change Triggered: [{block.EntityId:D18}] {block.GetFriendlyName()}");
            SetAppropriateOwnership(block);
        }

        /// <summary>
        /// Fires whenever a blocks ownership changes
        /// Only hooked for blocks we care about
        /// </summary>
        /// <param name="block"></param>
		private void BlockOnOwnershipChanged(IMyTerminalBlock block)
		{
            SetAppropriateOwnership((MyCubeBlock)block);
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
                    if (_gridOwner == 0) return;
                    if (!IsNpcOwned()) return;
                    var tb = block as IMyTerminalBlock;
                    if (tb == null) return;
                    if (block.IsFunctional && block.OwnerId != _gridOwner)
                        ClaimBlock(block);
                    else if (!block.IsFunctional && block.OwnerId != 0)
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
        }

        /// <summary>
        /// Sets the ownership of a given block to nobody (id = 0)
        /// </summary>
        /// <param name="block"></param>
        private void DisownBlock(MyCubeBlock block)
        {   
            block.ChangeOwner(0, MyOwnershipShareModeEnum.All);
        }

        private readonly HashSet<MyCubeBlock> _reusableBlocksCollection = new HashSet<MyCubeBlock>();

        public void EnableBlockHighlights(long playerId)
        {
            //WriteToLog(nameof(EnableBlockHighlights), this.ToString());
            _reusableBlocksCollection.Clear();
            if (_importantBlocks[_control].Count > 0)
            {
                //WriteToLog(nameof(EnableBlockHighlights), $"Highlighting [{_importantBlocks[_control].Count:00}] blocks.");
                foreach (var block in _importantBlocks[_control])
                {
                    //WriteToLog(nameof(EnableBlockHighlights), $"{" ",-6}[{(block.IsWorking ? "T" : "F")}][{block.EntityId,-18:000000000000000000}] TypeId: {block.BlockDefinition.Id.TypeId,-20}  SubtypeId: {block.BlockDefinition.Id.SubtypeName}");
                    if (block.IsFunctional)
                        _reusableBlocksCollection.Add(block);
                }
            }
            if (CheckHighlightCollection(_reusableBlocksCollection, playerId, Color.Orange)) return;

            if (_importantBlocks[_medical].Count > 0)
            {
                //WriteToLog(nameof(EnableBlockHighlights), $"Highlighting [{_importantBlocks[_medical].Count:00}] blocks.");
                foreach (var block in _importantBlocks[_medical])
                {
                    if (block.IsFunctional)
                        _reusableBlocksCollection.Add(block);
                }
            }
            if (CheckHighlightCollection(_reusableBlocksCollection, playerId, Color.Red)) return;

            if (_importantBlocks[_weapon].Count > 0)
            {
                //WriteToLog(nameof(EnableBlockHighlights), $"Highlighting [{_importantBlocks[_weapon].Count:00}] blocks.");
                foreach (var block in _importantBlocks[_weapon])
                {
                    if (block.IsFunctional)
                        _reusableBlocksCollection.Add(block);
                }
            }
            if (CheckHighlightCollection(_reusableBlocksCollection, playerId, Color.Yellow)) return;

            if (_importantBlocks[_trap].Count > 0)
            {
                //WriteToLog(nameof(EnableBlockHighlights), $"Highlighting [{_importantBlocks[_trap].Count:00}] blocks.");
                foreach (var block in _importantBlocks[_trap])
                {
                    if (block.IsFunctional)
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

            while (_perTickDelayedActions.Count > 0)
            {
                _perTickDelayedActions.Dequeue()?.Invoke();
            }
        }

        private bool CheckAnyActiveBlocksExist()
        {
            foreach (var kvp in _importantBlocks)
            {
                foreach (var block in kvp.Value)
                {
                    if (!block.IsFunctional) continue;
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
                    if (!block.IsFunctional) continue;
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
            if (!IsNpcOwned()) return;
            WriteToLog(nameof(CheckBlockBalance), $"{GetActiveBlockList().Count:D2}");
            if (CheckAnyActiveBlocksExist()) return;
            DisownGrid();
			//if (_activeBlocks.Count == 0)
				//DisownGrid();
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
            base.WriteToLog($"[{GridId:D18}] {caller}", message);
        }

        #region Debug Printouts
        
        private void PrintOwnersInfo()
        {
            PrintMainGridInfo();
            if (_mainGrid.BigOwners.Count == 0) return;
            IMyPlayer player = MyAPIGateway.Players.GetPlayerById(_mainGrid.BigOwners[0]);
            if (player != null)
            {
                WriteToLog(nameof(PrintOwnersInfo), $"BigOwner[0]: [{player.IdentityId:D18}] [{player.DisplayName}]");
                WriteToLog(nameof(PrintOwnersInfo), $"BigOwner[0]: Is Bot [{player.IsBot.ToSingleChar()}] Is Player [{player.Character.IsPlayer.ToSingleChar()}]");
            }
            else
            {
                WriteToLog(nameof(PrintOwnersInfo), $"BigOwner[0]: IMyPlayer was NULL");
            }

            foreach (var owner in _mainGrid.BigOwners)
            {
                WriteToLog(nameof(PrintOwnersInfo), $"All Owners: [{owner:D18}]");
            }
        }
        
        private void PrintMainGridInfo()
        {
            WriteToLog(nameof(PrintMainGridInfo), $"[{_mainGrid.EntityId:D18}] {_mainGrid.DisplayName}");
            WriteToLog(nameof(PrintMainGridInfo), $"BigOwner[0]: [{(_mainGrid.BigOwners.Count > 0 ? _mainGrid.BigOwners[0] : 0):D18}] {_mainGrid.DisplayName}");
        }

        public override string ToString()
        {
            _report.Clear();
            _report.AppendLine();
            _report.AppendLine();
            _report.AppendFormat("{0,-2}Report for Grid Construct: [{1:D18}]", "", GridId);
            _report.AppendLine();
            _report.AppendFormat("{0,-4}Owner: {1}", " ", _gridOwner);
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
                _report.AppendFormat("{0,-6}[{1,-18:D18}] TypeId: {2,-20}  SubtypeId: {3}", " ", block.EntityId, block.BlockDefinition.Id.TypeId, block.BlockDefinition.Id.SubtypeName);
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
                _report.AppendFormat("{0,-6}[{1}][{2,-18:D18}] TypeId: {3,-20}  SubtypeId: {4}", " ", (block.IsFunctional ? "T" : "F"), block.EntityId, block.BlockDefinition.Id.TypeId, block.BlockDefinition.Id.SubtypeName);
                _report.AppendLine();
            }

            _report.AppendLine();
            _report.AppendFormat("{0,-6}**** [{1:00}] Medical Blocks ****", " ", _importantBlocks[_medical].Count);
            _report.AppendLine();
            foreach (var block in _importantBlocks[_medical])
            {
                _report.AppendFormat("{0,-6}[{1}][{2,-18:D18}] TypeId: {3,-20}  SubtypeId: {4}", " ", (block.IsFunctional ? "T" : "F"), block.EntityId, block.BlockDefinition.Id.TypeId, block.BlockDefinition.Id.SubtypeName);
                _report.AppendLine();
            }

            _report.AppendLine();
            _report.AppendFormat("{0,-6}**** [{1:00}] Weapon Blocks ****", " ", _importantBlocks[_weapon].Count);
            _report.AppendLine();
            foreach (var block in _importantBlocks[_weapon])
            {
                _report.AppendFormat("{0,-6}[{1}][{2,-18:D18}] TypeId: {3,-20}  SubtypeId: {4}", " ", (block.IsFunctional ? "T" : "F"), block.EntityId, block.BlockDefinition.Id.TypeId, block.BlockDefinition.Id.SubtypeName);
                _report.AppendLine();
            }

            _report.AppendLine();
            _report.AppendFormat("{0,-6}**** [{1:00}] Trap Blocks ****", " ", _importantBlocks[_trap].Count);
            _report.AppendLine();
            foreach (var block in _importantBlocks[_trap])
            {
                _report.AppendFormat("{0,-6}[{1}][{2,-18:D18}] TypeId: {3,-20}  SubtypeId: {4}", " ", (block.IsFunctional ? "T" : "F"), block.EntityId, block.BlockDefinition.Id.TypeId, block.BlockDefinition.Id.SubtypeName);
                _report.AppendLine();
            }

            _report.AppendLine();
            return _report.ToString();
        }
        
        // MyTuple<long, MyCubeBlock, long, Color> hlb = new MyTuple<long, MyCubeBlock, long, Color>();
        
        #endregion

    }
}