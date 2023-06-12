using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HostileTakeover.Common.BaseClasses;
using HostileTakeover.Common.Enums;
using HostileTakeover.Common.Factions.Models;
using HostileTakeover.Common.Generics;
using HostileTakeover.Common.Interfaces;
using HostileTakeover.Controllers;
using HostileTakeover.Models;
using HostileTakeover.References.Settings;
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
        /// <summary>
        /// TODO
        /// Move getting grids to outer scope in the session / HTCore
        ///     MyAPIGateway.GridGroups.GetGroup(thisGrid, GridLinkTypeEnum.Mechanical, _reusableGridCollection);
        /// Start the Construct with that list of grids
        /// This will allow one less allocation for the grid list per grid entity
        /// Pass this list into the ConstructController for later use / setup
        /// Record the list as a construct map in the session / HTCore
        /// 
        /// Consider putting the grinder highlight option on an action
        /// This would eliminate the need for keeping a mapped list of constructs to check against
        /// Whenever a grid is targeted with a grinder, just fire the event, let all grids listen to it and react when
        ///     their number is called
        /// This should reduce some complexity and collection management needs
        /// </summary>

        protected override string CompName { get; } = "HostileTakeoverCore";
        protected override CompType Type { get; } = CompType.Server;
        protected override MyUpdateOrder Schedule { get; } = MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation;

        // The long is to store the first entity ID in a complex grid picked up with OnEntityAdd; the collection will hold all of the related grids ("constructs")
        //private readonly ConcurrentDictionary<long, Construct> _constructs = new ConcurrentDictionary<long, Construct>();

        // This collection is used to map all grids to the construct they belong to.  (long, long) => (whatever grid EntityId, construct key)
        private readonly ConcurrentDictionary<long, Construct> _constructMap = new ConcurrentDictionary<long, Construct>();

        public readonly ActionQueue ActionQueue = new ActionQueue();

        ///// <summary>
        ///// Holds a collection of generic actions to be fired at some specific time
        ///// </summary>
        //private readonly Queue<Action> _genericActionQueue = new Queue<Action>();

        //// We want to wait half a tick between the grid being picked up and processing it to allow subgrids to populate, so this queue grabs all 
        ////	grids OnEntityAdd and processes them the next AfterSimulation tick
        //private readonly Queue<MyCubeGrid> _constructQueue = new Queue<MyCubeGrid>();

        internal readonly ObjectPool<HighlightedBlock> HighlightedBlockPool = new ObjectPool<HighlightedBlock>(() => new HighlightedBlock());

        internal readonly ObjectPool<Construct> ConstructPool = new ObjectPool<Construct>(() => new Construct(++_constructCounter));

        private static int _constructCounter;

        private readonly List<IMyCubeGrid> _reusableGridCollection = new List<IMyCubeGrid>();

        private readonly List<MyEntity> _entList = new List<MyEntity>();

        private readonly HashSet<Construct> _constructs = new HashSet<Construct>();

        private SettingsController _settings;

        private void OnEntityReset(IReset resettableEntity)
        {
            var construct = resettableEntity as Construct;
            if (construct == null) return;
            TerminateConstruct(construct);
        }

        private void TerminateConstruct(Construct construct)
        {
            construct.OnWriteToLog -= WriteGeneral;
            construct.OnReset -= OnEntityReset;
            construct.TerminateGrid -= OnTerminateGrid;
            _constructs.Remove(construct);
            ConstructPool.Return(construct);
        }

        private void PopulateGridList(IMyCubeGrid thisGrid)
        {
            _reusableGridCollection.Clear();
            MyAPIGateway.GridGroups.GetGroup(thisGrid, GridLinkTypeEnum.Mechanical, _reusableGridCollection);
        }

        protected override void SuperEarlySetup()
        {
            base.SuperEarlySetup();
            _settings = new SettingsController(ModContext.ModName);
            _settings.Initialize();
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
        }

        public override void BeforeStart()
        {
            base.BeforeStart();
            var sb = new StringBuilder();
            DefaultSettings.PrintSettings(sb);
            WriteGeneral(nameof(SuperEarlySetup), sb.ToString());
        }

        protected override void UpdateBeforeSim()
        {
            base.UpdateBeforeSim();
            ActionQueue.Execute();
        }

        protected override void Unload()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            CloseConstructs();
            base.Unload();
        }

        private void CloseConstructs()
        {
            // This should never be needed, but leaving it here just in case. 
            //foreach (var construct in _constructs)
            //{
            //    construct.Close();
            //}
        }

        private void OnEntityAdd(IMyEntity entity)
        {
            var grid = entity as MyCubeGrid;
            if (grid != null)
            {
                ActionQueue.Add(DefaultSettings.EntityAddTickDelay, () => RunNewGridLogic(grid));
                return;
            }
            var grinder = entity as IMyAngleGrinder;
            if (grinder == null) return;
            ActionQueue.Add(DefaultSettings.GrinderTickDelay, () => RunGrinderLogic(grinder));
        }

        private void RunNewGridLogic(MyCubeGrid grid)
        {
            WriteGeneral(nameof(RunNewGridLogic), $"Processing: [{grid.EntityId:D18}] {grid.DisplayName}");
            if (!ValidateGrid(grid)) return;

            // Finally... something I can work with!
            HandleConstruct(grid);
            WriteGeneral(nameof(RunNewGridLogic), $"New Grid: [{grid.EntityId}] {grid.DisplayName}");
        }

        private bool ValidateGrid(MyCubeGrid grid)
        {
            // I don't exist!  So why am I here...
            if (grid == null)
            {
                WriteRejectionReason(null, "NULL");
                return false;
            }

            // You don't own me.  No one owns me! 
            if (grid.BigOwners == null || grid.BigOwners.Count == 0)
                return false;

            // I'm a projection!  Begone fool! ...or lend me your... components.
            if (grid.Physics == null)
            {
                WriteRejectionReason(grid, "NO PHYSICS");
                return false;
            }

            // I'm not destructible because someone said so.
            if (!grid.DestructibleBlocks)
            {
                WriteRejectionReason(grid, "INDESTRUCTIBLE");
                return false;
            }

            // Haha bitch, I'm immune to your garbage.
            if (grid.Immune)
            {
                WriteRejectionReason(grid, "IMMUNE");
                return false;
            }

            // Thou shall not edit me.  So saith ...me.
            if (!grid.Editable)
            {
                WriteRejectionReason(grid, "NOT EDITABLE");
                return false;
            }

            // I'm a station...!
            if (grid.IsStatic)
            {
                // ...that has an owner...!
                if (grid.BigOwners.Count > 0)
                {
                    IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(grid.BigOwners[0]);
                    if (faction != null && FactionDictionaries.VanillaTradeFactions.ContainsKey(faction.FactionId))
                    {
                        // ...that belongs to cheater NPC's, so back off! 
                        WriteRejectionReason(grid, "VANILLA TRADE");
                        return false;
                    }
                }
            }

            return true;
        }

        private void WriteRejectionReason(MyCubeGrid grid, string reason)
        {
            WriteGeneral(nameof(WriteRejectionReason), $"Grid Rejected as {reason}: [{grid?.EntityId:D18}] {grid?.DisplayName}");
        }

        private void HandleConstruct(MyCubeGrid grid)
        {
            PopulateGridList(grid);
            if (_reusableGridCollection.Count == 0) return; // Not sure how this can happen, but better safe than sorry.

            // if parentEntity = 0, then this grid collection isn't mapped yet
            // If parentEntity > 0, then this grid is part of a construct and needs to be mapped to it
            //long parentEntity = GetParentGrid();
            //WriteGeneral(nameof(HandleConstruct), $"Processing Construct: [{_reusableGridCollection.Count:D2}] [{parentEntity:D18}] [{grid.EntityId:D18}] {grid.DisplayName}");
            //if (parentEntity == 0)
            //    parentEntity = _reusableGridCollection[0].EntityId;

            //AddToConstructMap(parentEntity, parentEntity);

            //for (int i = 0; i < _reusableGridCollection.Count; i++)
            //{
            //    AddToConstructMap(parentEntity, _reusableGridCollection[i].EntityId);
            //}

            //PrintConstructMap();
            Construct construct = CheckExistingConstructs(_reusableGridCollection);
            if (construct == null)
                CreateConstruct(_reusableGridCollection);
            else UpdateExistingConstruct(construct, _reusableGridCollection);
            //AddToConstructCollection(parentEntity, (MyCubeGrid)_reusableGridCollection[0]);
        }

        private Construct CheckExistingConstructs(List<IMyCubeGrid> grids)
        {
            foreach (IMyCubeGrid grid in grids)
            {
                foreach (var construct in _constructs)
                {
                    if (construct.ContainsGrid(grid.EntityId))
                        return construct;
                }
            }
            return null;
        }

        private void UpdateExistingConstruct(Construct construct, IEnumerable<IMyCubeGrid> grids)
        {
            AddGridsToConstruct(construct, grids);
        }

        private void CreateConstruct(IEnumerable<IMyCubeGrid> grids)
        {
            Construct construct = ConstructPool.Get();
            _constructs.Add(construct);
            SetupConstruct(construct);
            AddGridsToConstruct(construct, grids);
        }

        private void SetupConstruct(Construct construct)
        {
            construct.AssignActions(HighlightedBlockPool, ActionQueue);
            construct.OnWriteToLog += WriteGeneral;
            construct.OnReset += OnEntityReset;
            construct.TerminateGrid += OnTerminateGrid;
        }

        private void OnTerminateGrid(long gridId)
        {
            _constructMap.Remove(gridId);
        }

        private void AddGridsToConstruct(Construct construct, IEnumerable<IMyCubeGrid> grids)
        {
            foreach (IMyCubeGrid grid in grids)
            {
                if (construct.ContainsGrid(grid.EntityId)) continue;
                AddToConstructMap(grid.EntityId, construct);
                construct.AddGrid((MyCubeGrid)grid);
            }
        }

        private void AddToConstructMap(long gridId, Construct construct)
        {
            _constructMap.TryAdd(gridId, construct);
        }

        private List<MyEntity> GrabNearbyGrids(Vector3D center)
        {
            _entList.Clear();
            var pruneSphere = new BoundingSphereD(center, DefaultSettings.DetectionRange);
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
            IMyEntity playerId = MyAPIGateway.Entities.GetEntityById(grinder.OwnerId);
            List<MyEntity> entList = GrabNearbyGrids(playerId?.GetPosition() ?? grinder.GetPosition());
            WriteGeneral(nameof(RunGrinderLogic), $"Grinder: [{grinder.OwnerIdentityId:D18}] [{grinder.OwnerId:D18}] [{entList.Count:D2}] [{grinder.GetPosition()}] [{playerId?.GetPosition()}]");
            PrintConstructMap();
            if (grinder.OwnerIdentityId == 0) return;
            MyEntity nearestGrid = GetNearestGrid(grinder, entList);
            if (nearestGrid == null) return;
            WriteGeneral(nameof(RunGrinderLogic), $"Bossing the construct around! [{nearestGrid.EntityId:D18}]");
            _constructMap[nearestGrid.EntityId].Controller.HighlightController.TriggerHighlights(grinder);
            //foreach (MyEntity target in entList)
            //{

            //    WriteGeneral(nameof(RunGrinderLogic), $"Looking for: [{(_constructMap.ContainsKey(target.EntityId) ? "T" : "F")}] [{target.EntityId:D18}] [{grinder.OwnerIdentityId:000000000000000000}]");
            //    if (!_constructMap.ContainsKey(target.EntityId)) continue;
            //    WriteGeneral(nameof(RunGrinderLogic), $"Found: [{target.EntityId:D18}] with parent: [{_constructMap[target.EntityId]:D18}]");
            //    if (!_constructs.ContainsKey(_constructMap[target.EntityId]))
            //    {
            //        WriteGeneral(nameof(RunGrinderLogic), $"Construct lookup failed for: [{_constructMap[target.EntityId]:D18}]");
            //        continue;
            //    }
            //    WriteGeneral(nameof(RunGrinderLogic), $"Bossing the construct around! [{target.EntityId:D18}]");
            //    _constructs[_constructMap[target.EntityId]].ConstructController.HighlightController.TriggerHighlights(grinder);
            //}
        }

        private MyEntity GetNearestGrid(IMyAngleGrinder grinder, List<MyEntity> grids)
        {
            if (grids.Count == 1) return (MyCubeGrid)grids[0];
            Vector3D entPos = grinder.GetPosition();
            var closestDistSq = double.MaxValue;
            MyEntity nearestGrid = null;
            foreach (var grid in grids)
            {
                double distSq = Vector3D.DistanceSquared(entPos, grid.PositionComp.GetPosition());
                WriteGeneral(nameof(GetNearestGrid), $"[{grinder.OwnerIdentityId:D18}] [{grid.EntityId:D18}] [{distSq}] [{closestDistSq}]");
                if (distSq > closestDistSq) continue;
                closestDistSq = distSq;
                nearestGrid = grid;
            }

            return nearestGrid;
        }

        private void PrintConstructMap()
        {
            foreach (var kvp in _constructMap)
            {
                WriteGeneral(nameof(PrintConstructMap), $"[{kvp.Key:D18}] [{kvp.Value:D18}]");
            }
        }
    }
}