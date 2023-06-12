using System;
using System.Collections.Generic;
using HostileTakeover.Common.Extensions;
using HostileTakeover.Common.Generics;
using HostileTakeover.Common.Interfaces;
using HostileTakeover.Controllers;
using HostileTakeover.References.Settings;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace HostileTakeover.Models
{
    internal sealed class Construct : ILog, IReset
    {
        public Construct(int id)
        {
            ConstructName = ConstructNamePrefix + $"{id:D4}";
            _recycleCount = 0;
            CoreConstruct = new CoreConstruct(this);
            RegisterResettableObject(CoreConstruct);
            NpcConstruct = new NpcConstruct(this);
            RegisterResettableObject(NpcConstruct);
            PlayerConstruct = new PlayerConstruct(this);
            RegisterResettableObject(PlayerConstruct);
        }

        #region Initialization
        public void AssignActions(ObjectPool<HighlightedBlock> highlightedBlockPool, ActionQueue actionQueue)
        {
            if (DefaultSettings.EnableHighlights)
                HighlightPool = highlightedBlockPool;
            ActionQueue = actionQueue;
            RegisterLogger(CoreConstruct);
            RegisterLogger(NpcConstruct);
            RegisterLogger(PlayerConstruct);
            _recycleCount++;
        }

        #endregion

        #region Fields
        public readonly CoreConstruct CoreConstruct;
        public readonly PlayerConstruct PlayerConstruct;
        public readonly NpcConstruct NpcConstruct;
        public readonly string ConstructName;
        private int _recycleCount;
        private const string ConstructNamePrefix = "C";
        public ObjectPool<HighlightedBlock> HighlightPool;
        public ActionQueue ActionQueue;

        #endregion

        #region Collections
        public readonly HashSet<IReset> ResettableObjects = new HashSet<IReset>();
        public readonly HashSet<ILog> LoggingObjects = new HashSet<ILog>();
        public readonly Dictionary<long, MyCubeGrid> ConstructGrids = new Dictionary<long, MyCubeGrid>();

        #endregion

        #region Events
        public event Action<string, string> OnWriteToLog;
        public event Action<long> TerminateGrid;
        public event Action<IReset> OnReset;

        #endregion

        #region Local Event Invokers
        public void WriteGeneral(string caller, string message)
        {
            OnWriteToLog?.Invoke($"[{ConstructName}][{_recycleCount:D3}] {caller}", message);
        }

        public void Reset()
        {
            foreach (var kvp in ConstructGrids)
            {
                GridDeRegisterCommonEvents(kvp.Value);
                GridDeRegisterNpcEvents(kvp.Value);
            }
            
            DeRegisterLoggers();
            ResetResetables();
            OnReset?.Invoke(this);
        }

        #endregion

        #region Event Registers

        private void RegisterResettableObject(IReset resettable)
        {
            ResettableObjects.Add(resettable);
        }

        private void RegisterLogger(ILog logger)
        {
            LoggingObjects.Add(logger);
            logger.OnWriteToLog += WriteGeneral;
        }

        private void RegisterCommonGridEvents(MyCubeGrid grid)
        {
            grid.OnClose += GridOnClose;
        }

        private void RegisterNpcGridEvents(MyCubeGrid grid)
        {
            grid.OnFatBlockAdded += Controller.BlockController.OnFatBlockAdded;
            grid.OnGridSplit += OnGridSplit;
        }

        #endregion

        #region External Event Handlers

        private void GridOnClose(MyEntity grid)
        {
            GridDeRegisterCommonEvents((MyCubeGrid)grid);
            GridDeRegisterNpcEvents((MyCubeGrid)grid);
            Controller.RemoveGrid((MyCubeGrid)grid);
            if (Controller.ConstructGrids.Count == 0) Reset();
            TerminateGrid?.Invoke(grid.EntityId);
        }

        private void OnGridSplit(MyCubeGrid originalGridUnused, MyCubeGrid newGrid)
        {
            Controller.RemoveGrid(newGrid);
        }

        #endregion

        #region Event DeRegisters

        private void ResetResetables()
        {
            foreach (var resettable in ResettableObjects)
            {
                resettable.Reset();
            }
        }

        private void DeRegisterLoggers()
        {
            foreach (var logger in LoggingObjects)
            {
                logger.OnWriteToLog -= WriteGeneral;
            }
            
        }

        private void GridDeRegisterCommonEvents(MyCubeGrid grid)
        {
            grid.OnClose -= GridOnClose;
        }

        private void GridDeRegisterNpcEvents(MyCubeGrid grid)
        {
            grid.OnFatBlockAdded -= Controller.BlockController.OnFatBlockAdded;
            grid.OnGridSplit -= OnGridSplit;
        }

        #endregion

        #region Props and Expressions
        public bool ContainsGrid(long gridId) => Controller.ContainsGrid(gridId);

        #endregion

        public void SwitchToPlayerConstruct()
        {

        }

        public void SwitchToNpcConstruct()
        {

        }

        public void AddGrids(List<IMyCubeGrid> grids)
        {
            foreach (var grid in grids)
            {
                AddGrid((MyCubeGrid) grid);
            }
        }

        public void AddGrid(MyCubeGrid grid)
        {
            if (ContainsGrid(grid.EntityId)) return;
            Controller.TickDelayedAction(DefaultSettings.BlockAddTickDelay, () =>
                WriteGeneral(nameof(AddGrid), $"Initializing new grid: [{Controller.IsNpcOwned().ToSingleChar()}][{grid.EntityId:D18}] {grid.DisplayName}"));
            InitializeCommonGrid(grid);
            if (!Controller.IsNpcOwned()) return;
            InitializeNpcGrid(grid);
        }

        private void InitializeNonNpcOwnedGrid(MyCubeGrid grid)
        {

        }

        private void RegisterNonNpcGridEvents(MyCubeGrid grid)
        {

        }

        private void DeRegisterNonNpcGridEvents(MyCubeGrid grid)
        {

        }

        private void InitializeNpcOwnedGrid(MyCubeGrid grid)
        {

        }

        private void InitializeCommonGrid(MyCubeGrid grid)
        {
            RegisterCommonGridEvents(grid);
        }
        
        private void InitializeNpcGrid(MyCubeGrid grid)
        {
            if (!Controller.IsNpcOwned()) return;
            RegisterNpcGridEvents(grid);
        }
        
        public void DisownConstruct()
        {
            foreach (var kvp in Controller.ConstructGrids)
            {
                DisownNpcGrid(kvp.Value);
            }
        }

        private void DisownNpcGrid(MyCubeGrid grid)
        {
            GridDeRegisterNpcEvents(grid);
        }


        
        
    }
}