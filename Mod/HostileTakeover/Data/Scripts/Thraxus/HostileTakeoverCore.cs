using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HostileTakeover.Common.BaseClasses;
using HostileTakeover.Common.Enums;
using HostileTakeover.Common.Factions.Models;
using HostileTakeover.Models;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace HostileTakeover
{
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation, priority: int.MinValue + 1)]
    public class HostileTakeoverCore : BaseSessionComp
    {
	    protected override string CompName { get; } = "HostileTakeoverCore";
		protected override CompType Type { get; } = CompType.Server;
		protected override MyUpdateOrder Schedule { get; } = MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation;

		// The long is to store the first entity ID in a complex grid picked up with OnEntityAdd; the collection will hold all of the related grids ("constructs")
		private readonly ConcurrentDictionary<long, Construct> _constructs = new ConcurrentDictionary<long, Construct>();

		// This collection is used to map all grids to the construct they belong to.  (long, long) => (whatever grid EntityId, construct key)
		private readonly ConcurrentDictionary<long, long> _constructMap = new ConcurrentDictionary<long, long>();

		/// <summary>
		/// Holds a collection of generic actions to be fired at some specific time
		/// </summary>
		private readonly Queue<Action> _genericActionQueue = new Queue<Action>();

		// We want to wait half a tick between the grid being picked up and processing it to allow subgrids to populate, so this queue grabs all 
		//	grids OnEntityAdd and processes them the next AfterSimulation tick
		private readonly Queue<MyCubeGrid> _constructQueue = new Queue<MyCubeGrid>();

		private readonly List<IMyCubeGrid> _reusableGridCollection = new List<IMyCubeGrid>();

        private readonly List<MyEntity> _entList = new List<MyEntity>();

        private const double DetectionRange = 150;

        private void PopulateGridList(IMyCubeGrid thisGrid)
		{
			_reusableGridCollection.Clear();
			MyAPIGateway.GridGroups.GetGroup(thisGrid, GridLinkTypeEnum.Mechanical, _reusableGridCollection);
        }

		protected override void SuperEarlySetup()
		{
			base.SuperEarlySetup();
			MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
		}

        protected override void BeforeSimUpdate()
        {
            base.BeforeSimUpdate();
            //if (_constructs.Count <= 0) return;
            foreach (KeyValuePair<long, Construct> construct in _constructs)
            {
                construct.Value.ProcessPerTickActions();
            }
		}

        protected override void AfterSimUpdate10Ticks()
        {
            base.AfterSimUpdate10Ticks();
            while (_genericActionQueue.Count > 0)
            {
                _genericActionQueue.Dequeue()?.Invoke();
            }

            foreach (var construct in _constructs)
            {
                construct.Value.ProcessTickDelayedActions();
            }
        }
        
        protected override void Unload()
		{
			base.Unload();
			MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
			_constructQueue.Clear();
		}

		private void OnEntityAdd(IMyEntity entity)
		{
			var grid = entity as MyCubeGrid;
			if (grid != null)
            {
                _genericActionQueue.Enqueue(() => RunNewGridLogic(grid));
                return;
            }
            var grinder = entity as IMyAngleGrinder;
            if (grinder == null) return;
            _genericActionQueue.Enqueue(() => RunGrinderLogic(grinder));
        }

        private void RunNewGridLogic(MyCubeGrid grid)
        {
            // I don't exist!  So why am I here...
            if (grid == null)
            {
                WriteGeneral(nameof(RunNewGridLogic), $"Grid Rejected as NULL.");
                return;
            }

            WriteGeneral(nameof(RunNewGridLogic), $"Processing: [{grid.EntityId}] {grid.DisplayName}");

            // I'm a projection!  Begone fool! ...or lend me your... components.
            if (grid.Physics == null)
            {
                WriteGeneral(nameof(RunNewGridLogic), $"Grid Rejected as PROJECTION: [{grid.EntityId}] {grid.DisplayName}");
                return;
            }

            if (grid.IsStatic)
            {
                if (grid.BigOwners.Count > 0)
                {
                    IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(grid.BigOwners[0]);
                    if (faction != null && FactionDictionaries.VanillaTradeFactions.ContainsKey(faction.FactionId))
                    {
                        WriteGeneral(nameof(RunNewGridLogic),
                            $"Grid Rejected as VANILLA TRADE: [{grid.EntityId}] {grid.DisplayName}");
                        return;
                    }
                }
            }
            HandleConstruct(grid);
            WriteGeneral(nameof(RunNewGridLogic), $"New Grid: [{grid.EntityId}] {grid.DisplayName}");
		}

        private void HandleConstruct(MyCubeGrid grid)
        {
            PopulateGridList(grid);
            WriteGeneral(nameof(HandleConstruct), $"Processing Construct: [{_reusableGridCollection.Count:00}] [{grid.EntityId}] {grid.DisplayName}");

            //if (_reusableGridCollection.Count == 1)
            //{
            //    _constructs.TryAdd(grid.EntityId, new Construct(grid));
            //    _constructMap.TryAdd(grid.EntityId, grid.EntityId);
            //    RegisterConstructEvents(_constructs[grid.EntityId]);
            //    WriteGeneral(nameof(HandleConstruct), $"Single Grid Found: [{_reusableGridCollection.Count:00}] [{grid.EntityId}] {grid.DisplayName}");
            //    return;
            //}

            // if parentEntity = 0, then this grid collection isn't mapped yet
            // If parentEntity > 0, then this grid is part of a construct and needs to be mapped to it
            long parentEntity = GetParentGrid();
            WriteGeneral(nameof(HandleConstruct), $"Grid Collection Found: [{_reusableGridCollection.Count:00}] [{parentEntity:000000000000000000}] [{grid.EntityId}] {grid.DisplayName}");
            if (parentEntity == 0) parentEntity = _reusableGridCollection[0].EntityId;
            {
                _constructs.TryAdd(parentEntity, new Construct((MyCubeGrid)_reusableGridCollection[0]));
                RegisterConstructEvents(_constructs[parentEntity]);
                _constructMap.TryAdd(_constructs[parentEntity].GridId, _constructs[parentEntity].GridId);
            }

            if (_reusableGridCollection.Count <= 1) return;

            for (int i = 1; i < _reusableGridCollection.Count; i++)
            {
                _constructs[parentEntity].Add((MyCubeGrid)_reusableGridCollection[i]);
                _constructMap.TryAdd(_reusableGridCollection[i].EntityId, parentEntity);
            }


            //long primaryConstruct = 0;
            //for (int i = 0; i < _reusableGridCollection.Count; i++)
            //{
            //    if (i == 0)
            //    {
            //        primaryConstruct = _reusableGridCollection[i].EntityId;

            //        _constructs.TryAdd(_reusableGridCollection[i].EntityId, new Construct((MyCubeGrid)_reusableGridCollection[i]));
            //        RegisterConstructEvents(_constructs[primaryConstruct]);
            //        _constructMap.TryAdd(_constructs[primaryConstruct].GridId, _constructs[primaryConstruct].GridId);
            //        continue;
            //    }
            //    _constructs[primaryConstruct].Add((MyCubeGrid)_reusableGridCollection[i]);
            //    _constructMap.TryAdd(_reusableGridCollection[i].EntityId, primaryConstruct);
            //}

            //if (parentEntity == 0)
            //{
            //    long primaryConstruct = 0;
            //    for (int i = 0; i < _reusableGridCollection.Count; i++)
            //    {
            //        if (i == 0)
            //        {
            //            primaryConstruct = _reusableGridCollection[i].EntityId;

            //           _constructs.TryAdd(_reusableGridCollection[i].EntityId, new Construct((MyCubeGrid)_reusableGridCollection[i]));
            //            RegisterConstructEvents(_constructs[primaryConstruct]);
            //            _constructMap.TryAdd(_constructs[primaryConstruct].GridId, _constructs[primaryConstruct].GridId);
            //            continue;
            //        }
            //        _constructs[primaryConstruct].Add((MyCubeGrid)_reusableGridCollection[i]);
            //        _constructMap.TryAdd(_reusableGridCollection[i].EntityId, primaryConstruct);
            //    }
            //    return;
            //}

            //foreach (IMyCubeGrid subGrid in _reusableGridCollection)
            //{
            //    if (_constructMap.ContainsKey(subGrid.EntityId)) continue;
            //    _constructs[parentEntity].Add((MyCubeGrid)subGrid);
            //    _constructMap.TryAdd(subGrid.EntityId, parentEntity);
            //}
        }

        private void RegisterConstructEvents(Construct construct)
        {
            construct.OnCloseConstruct += CloseConstruct;
            construct.OnWriteToLog += WriteGeneral;
        }

        private long GetParentGrid()
        {
            foreach (IMyCubeGrid grid in _reusableGridCollection)
            {
                if (!_constructMap.ContainsKey(grid.EntityId)) continue;
                return _constructMap[grid.EntityId];
            }
            return 0;
        }

        private List<MyEntity> GrabNearbyGrids(Vector3D center)
        {
            _entList.Clear();
            var pruneSphere = new BoundingSphereD(center, DetectionRange);
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref pruneSphere, _entList);
            for (int i = _entList.Count - 1; i >= 0; i--)
            {
                if (!(_entList[i] is MyCubeGrid))
                    _entList.RemoveAtFast(i);
            }
            return _entList;
        }

		private void RunGrinderLogic(IMyAngleGrinder grinder)
        {
            IMyEntity entityById = MyAPIGateway.Entities.GetEntityById(grinder.OwnerId);
            List<MyEntity> entList = GrabNearbyGrids(entityById?.GetPosition() ?? grinder.GetPosition());
            WriteGeneral(nameof(RunGrinderLogic), $"Grinder: [{grinder.OwnerIdentityId:000000000000000000}] [{grinder.OwnerId:000000000000000000}] [{entList.Count:00}] [{grinder.GetPosition()}] [{entityById?.GetPosition()}]");
            PrintConstructMap();
            foreach (MyEntity target in entList)
            {
                if (grinder.OwnerIdentityId == 0) break;
                WriteGeneral(nameof(RunGrinderLogic), $"Looking for: [{(_constructMap.ContainsKey(target.EntityId) ? "T" : "F")}] [{target.EntityId:000000000000000000}] [{grinder.OwnerIdentityId:000000000000000000}]");
                if (!_constructMap.ContainsKey(target.EntityId)) continue;
                WriteGeneral(nameof(RunGrinderLogic), $"Found: [{target.EntityId:000000000000000000}] with parent: [{_constructMap[target.EntityId]:000000000000000000}]");
                if (!_constructs.ContainsKey(_constructMap[target.EntityId]))
                {
                    WriteGeneral(nameof(RunGrinderLogic), $"Construct lookup failed for: [{_constructMap[target.EntityId]:000000000000000000}]");
                    continue;
                }
                _constructs[_constructMap[target.EntityId]].EnableBlockHighlights(grinder.OwnerIdentityId);
            }
        }

        private void CloseConstruct(Construct construct)
        {
            WriteGeneral(nameof(CloseConstruct), $"Closing Construct: [{construct.GridId:000000000000000000}]");
            if (_constructs.ContainsKey(construct.GridId))
                _constructs.Remove(construct.GridId);
            if (_constructMap.ContainsKey(construct.GridId))
                _constructMap.Remove(construct.GridId);
            construct.OnWriteToLog -= WriteGeneral;
			construct.OnCloseConstruct -= CloseConstruct;
        }

        private void PrintConstructMap()
        {
            foreach (var kvp in _constructMap)
            {
                WriteGeneral(nameof(PrintConstructMap), $"[{kvp.Key:000000000000000000}] [{kvp.Value:000000000000000000}]");
            }
        }
    }
}