using System;
using System.Collections.Generic;
using System.Linq;
using HostileTakeover.Common.Interfaces;
using HostileTakeover.Enums;
using HostileTakeover.Extensions;
using HostileTakeover.References.Settings;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;

namespace HostileTakeover.Controllers
{
    internal class BlockController : ILog
    {
        // Fields
        private readonly HashSet<IMyTerminalBlock> _allTerminalBlocks = new HashSet<IMyTerminalBlock>();
        private readonly Controller _constructController;

        // Events
        public event Action<string, string> OnWriteToLog;

        // Event Invokers
        public void WriteGeneral(string caller, string message) => OnWriteToLog?.Invoke(caller, message);

        // Props & Expressions
        public bool HasBlocksRemaining => _constructController.HasImportantBlocksRemaining;

        public BlockController(Controller constructController)
        {
            _constructController = constructController;
        }

        /// <summary>
        /// Edge case for a grid being added to an existing construct
        /// Funneling ParseGrids() through here just to keep the flow in tact
        /// </summary>
        /// <param name="grid"></param>
        public void AddGrid(MyCubeGrid grid)
        {
            foreach (var block in grid.GetFatBlocks())
            {
                AddNewBlock(block);
            }
        }

        /// <summary>
        /// Sequence:
        ///     1) Check to make sure the block isn't on it's way out
        ///         if (block.Closed || block.MarkedForClose) return;
        ///     2) Check if is a terminal block
        ///         if no, return,
        ///         if yes...
        ///     3) Set ownership of the block
        ///          SetAppropriateOwnership(block);
        ///     4) Classify the block
        ///         ClassifyBlock(MyCubeBlock block)
        ///     5) If block is not HighlightType.None and valid for takeover, add it to the important blocks collection
        ///         _constructInformation.ImportantBlocks[type].Add(block);
        ///     6) Register block events
        ///         BlockRegisterEvents(block)
        ///     6) Add block to _allTerminalBlocks
        ///         _allTerminalBlocks.Add(tb);
        /// </summary>
        /// <param name="block">New block to be evaluated</param>
        private void AddNewBlock(MyCubeBlock block)
        {
            EnqueueAction(DefaultSettings.BlockAddTickDelay, () =>
            {
                if (block.Closed || block.MarkedForClose) return;
                var tb = (block as IMyTerminalBlock);
                if (tb == null) return;
                SetAppropriateOwnership(block);
                HighlightType type = block.Classify();
                if (type != HighlightType.None && block.IsValidForTakeover())
                    _constructController.AddImportantBlock(type, block);
                BlockRegisterEvents(block);
                _allTerminalBlocks.Add(tb);
            });
        }

        /// <summary>
        /// Sets the ownership of a block depending on the state of the block
        /// If the block is functional (integrity > breaking threshold), then it gets owned by the grid owner
        /// If the block is not functional, it is disowned
        /// </summary>
        /// <param name="block"></param>
        private void SetAppropriateOwnership(MyCubeBlock block)
        {
            if (!_constructController.IsNpcOwned()) return;
            EnqueueAction(DefaultSettings.BlockAddTickDelay, () => {
                var tb = block as IMyTerminalBlock;

                if (tb == null) return;

                switch (tb.IsFunctional)
                {
                    case true:
                        ClaimBlock(block);
                        break;
                    case false:
                        DisownBlock(block);
                        break;
                }
            });
        }

        /// <summary>
        /// This:
        ///     1) DeRegisters a block from all events
        ///     2) Removes a block from all collections
        ///     3) Disables all related highlights
        /// </summary>
        /// <param name="block"></param>
        public void RemoveBlock(MyCubeBlock block)
        {
            DeRegisterBlockEvents(block);
            _allTerminalBlocks.Remove((IMyTerminalBlock)block);
            HighlightType type = block.Classify();
            if (type != HighlightType.None)
                _constructController.RemoveImportantBlock(type, block);
            if (!HasBlocksRemaining) _constructController.Construct.DisownConstruct();
        }

        public void RemoveGrid(MyCubeGrid grid)
        {
            foreach (var block in grid.GetFatBlocks())
            {
                RemoveBlock(block);
            }
        }

        private void EnqueueAction(int delay, Action action)
        {
            _constructController.TickDelayedAction(delay, action);
        }

        /// <summary>
        /// No owner == owner = 0 or player owned
        /// This method will set the construct to appropriate monitoring conditions only
        ///
        /// A Non-NPC owned Construct has:
        ///     Construct level (i.e. all grids) level ownership change checks engaged
        ///         If ownership ever changes to a NPC on any block, the NPC now owns everything in the construct
        ///     Grid OnClose to remove grid level ownership and OnClose checks
        ///     BlockController turned off
        ///     Highlight Controller turned off
        /// </summary>
        public void SetConstructNoOwner()
        {
            _constructController.ClearImportantBlocks();
            foreach (var tb in _allTerminalBlocks)
            {
                DeRegisterBlockEvents((MyCubeBlock)tb);
            }
            _allTerminalBlocks.Clear();
        }

        public void SetConstructNpcOwner()
        {
            foreach (var kvp in _constructController.ConstructGrids)
            {
                AddGrid(kvp.Value);
            }
        }

        #region Event Registers

        // TODO There is a case of dueling banjo's here with OnOwnershipChanged between the grid and a block
        // TODO     Should only need one, so pick one and run with it. 

        /// PLAN!
        /// Remove OnBlockOwnershipChanged from the grid level
        /// Add a new collection to track ALL fat blocks that pass an (block is IMyTerminalBlock) check
        ///     Collection should be a hashset of IMyTerminalBlock
        /// Hook onto the OwnershipChanged event for them
        /// if ownership changes, check IsFunctional
        /// if IsFunctional == True, change block owner to grid owner
        /// if IsFunctional == False, change block owner to nobody

        /// <summary>
        /// Registers all block level events we care about
        /// </summary>
        /// <param name="block"></param>
        private void BlockRegisterEvents(MyCubeBlock block)
        {
            block.IsWorkingChanged += BlockOnWorkingChanged;
            ((IMyTerminalBlock)block).OwnershipChanged += BlockOnOwnershipChanged;
            block.OnClose += OnFatBlockClosed;
        }

        /// <summary>
        /// Fires whenever a block changes working status
        /// Only hooked for blocks we care about
        /// </summary>
        /// <param name="block"></param>
        private void BlockOnWorkingChanged(MyCubeBlock block)
        {
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

        #endregion

        #region Event Handlers

        public void OnFatBlockClosed(MyEntity block)
        {
            RemoveBlock((MyCubeBlock)block);
        }

        public void OnFatBlockAdded(MyCubeBlock block)
        {
            AddNewBlock(block);
        }

        #endregion

        #region Event DeRegisters

        /// <summary>
        /// Cleans up any block level events for closed blocks
        /// </summary>
        /// <param name="block"></param>
        private void DeRegisterBlockEvents(MyCubeBlock block)
        {
            block.IsWorkingChanged -= BlockOnWorkingChanged;
            ((IMyTerminalBlock)block).OwnershipChanged -= BlockOnOwnershipChanged;
            block.OnClose -= OnFatBlockClosed;
        }

        #endregion

        #region Ownership


        /// <summary>
        /// Sets the ownership of a given block to the owner of the grid (BigOwners[0])
        /// If MirrorEasyNpcTakeovers is true from DefaultSettings, then this method is not used. 
        /// </summary>
        /// <param name="block"></param>
        private void ClaimBlock(MyCubeBlock block)
        {
            WriteGeneral(nameof(ClaimBlock), $"Claiming block: [{block.BlockDefinition.Id.TypeId}] [{block.BlockDefinition.Id.SubtypeId}]");
            if (DefaultSettings.MirrorEasyNpcTakeovers) return;
            if (block.OwnerId != _constructController.GetOwnerId())
                block.ChangeOwner(_constructController.GetOwnerId(), MyOwnershipShareModeEnum.Faction);
        }

        /// <summary>
        /// Sets the ownership of a given block to nobody (id = 0)
        /// </summary>
        /// <param name="block"></param>
        private void DisownBlock(MyCubeBlock block)
        {
            WriteGeneral(nameof(DisownBlock), $"Disowning block: [{block.BlockDefinition.Id.TypeId}] [{block.BlockDefinition.Id.SubtypeId}]");
            if (block.OwnerId != 0)
                block.ChangeOwner(0, MyOwnershipShareModeEnum.All);
        }

        #endregion

        public void Reset()
        {
            foreach (var block in _allTerminalBlocks)
            {
                RemoveBlock((MyCubeBlock)block);
                DisownBlock((MyCubeBlock)block);
            }
        }
    }
}