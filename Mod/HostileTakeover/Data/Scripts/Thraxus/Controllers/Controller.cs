using System;
using System.Collections.Generic;
using System.Linq;
using HostileTakeover.Common.Extensions;
using HostileTakeover.Common.Generics;
using HostileTakeover.Common.Interfaces;
using HostileTakeover.Enums;
using HostileTakeover.Models;
using HostileTakeover.References.Settings;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace HostileTakeover.Controllers
{
    internal class Controller : ILog, IReset
    {
        public Controller(Construct construct)
        {
            Construct = construct;
            if (DefaultSettings.EnableHighlights)
            {
                HighlightController = new HighlightController(this);
                HighlightController.OnWriteToLog += WriteGeneral;
            }
            BlockController = new BlockController(this);
            BlockController.OnWriteToLog += WriteGeneral;
        }

        public void Init(List<IMyCubeGrid> grids)
        {
            MasterGridId = grids[0].EntityId;
            AddGrids(grids);
            _wasNpcOwned = IsNpcOwned();
        }

        // Fields
        public Construct Construct;
        public long MasterGridId;
        public readonly Dictionary<long, MyCubeGrid> ConstructGrids = new Dictionary<long, MyCubeGrid>();
        public HighlightController HighlightController;
        public BlockController BlockController;
        public ObjectPool<HighlightedBlock> HighlightPool;
        public ActionQueue ActionQueue;
        private bool _wasNpcOwned;

        public readonly Dictionary<HighlightType, HashSet<MyCubeBlock>> ImportantBlocks =
            new Dictionary<HighlightType, HashSet<MyCubeBlock>>
            {
                { HighlightType.Control, new HashSet<MyCubeBlock>() },
                { HighlightType.Medical, new HashSet<MyCubeBlock>() },
                { HighlightType.Weapon, new HashSet<MyCubeBlock>() },
                { HighlightType.Trap, new HashSet<MyCubeBlock>() }
            };

        // Events
        public event Action<string, string> OnWriteToLog;
        public event Action<IReset> OnReset;

        // Event Invokers
        public void WriteGeneral(string caller, string message) => OnWriteToLog?.Invoke(caller, message);

        // Props and Expressions
        public bool ContainsGrid(long gridId) => ConstructGrids.ContainsKey(gridId);
        public bool HasImportantBlocksRemaining => ImportantBlocks.Any(group => group.Value.Count > 0);

        public bool IsNpcOwned()
        {
            foreach (var kvp in ConstructGrids)
            {
                MyCubeGrid grid = kvp.Value;
                if (grid.BigOwners.Count <= 0) continue;
                if (MyAPIGateway.Players.TryGetSteamId(grid.BigOwners[0]) > 0) continue;
                return true;
            }
            return false;
        }

        public long GetOwnerId()
        {
            foreach (var kvp in ConstructGrids)
            {
                MyCubeGrid grid = kvp.Value;
                if (grid.BigOwners.Count <= 0) continue;
                return grid.BigOwners[0];
            }
            return 0;
        }

        public void AddImportantBlock(HighlightType type, MyCubeBlock block)
        {
            if (type == HighlightType.None) return;
            WriteGeneral(nameof(AddImportantBlock), $"Adding important block to the collection: [{type}] [{block.BlockDefinition.Id.TypeId}] {block.BlockDefinition.Id.SubtypeId}");
            ImportantBlocks[type].Add(block);
        }

        public void RemoveImportantBlock(HighlightType type, MyCubeBlock block)
        {
            if (type == HighlightType.None) return;
            WriteGeneral(nameof(AddImportantBlock), $"Removing important block from the collection: [{type}] [{block.BlockDefinition.Id.TypeId}] {block.BlockDefinition.Id.SubtypeId}");
            ImportantBlocks[type].Remove(block);
        }

        public void ClearImportantBlocks()
        {
            foreach (var kvp in ImportantBlocks)
            {
                kvp.Value.Clear();
            }
        }

        public void AddGrids(List<IMyCubeGrid> grids)
        {
            foreach (IMyCubeGrid grid in grids)
            {
                if (ContainsGrid(grid.EntityId)) continue;
                AddGrid((MyCubeGrid)grid);
            }
        }

        public void AddGrid(MyCubeGrid grid)
        {
            if (ContainsGrid(grid.EntityId)) return;
            WriteGeneral(nameof(AddGrid), $"Initializing new grid: [{IsNpcOwned().ToSingleChar()}][{grid.EntityId:D18}] {grid.DisplayName}");
            ConstructGrids.Add(grid.EntityId, grid);
            Construct.AddGrid(grid);
            BlockController.AddGrid(grid);
        }

        public void RemoveGrid(MyCubeGrid grid)
        {
            if (!ContainsGrid(grid.EntityId)) return;
            WriteGeneral(nameof(RemoveGrid), $"Removing grid: [{IsNpcOwned().ToSingleChar()}][{grid.EntityId:D18}] {grid.DisplayName}");
            ConstructGrids.Remove(grid.EntityId);
            BlockController.RemoveGrid(grid);
        }

        /// <summary>
        /// TODO Test this wild shit idea; don't need this firing a billion times when the delay is up
        /// </summary>
        /// <param name="delay"></param>
        /// <param name="action"></param>
        public void TickDelayedAction(int delay, Action action)
        {
            WriteGeneral(nameof(TickDelayedAction), $"Action registered with delay of: [{delay:D3}]");
            ActionQueue.Add(delay, action);
        }

        public void ReevaluateOwnership()
        {
            if (_wasNpcOwned && !IsNpcOwned())
            {
                // The NPC did own this, but now they don't...
                BlockController.SetConstructNoOwner();
            }

            if (!_wasNpcOwned && IsNpcOwned())
            {
                // The NPC didn't own this, but now they do...
                BlockController.SetConstructNpcOwner();
            }
        }

        public void Reset()
        {
            HighlightController.OnWriteToLog -= WriteGeneral;
            BlockController.OnWriteToLog -= WriteGeneral;
            BlockController.Reset();
            HighlightController.Reset();
            ConstructGrids.Clear();
            foreach (var dict in ImportantBlocks)
            {
                dict.Value.Clear();
            }
        }
    }
}