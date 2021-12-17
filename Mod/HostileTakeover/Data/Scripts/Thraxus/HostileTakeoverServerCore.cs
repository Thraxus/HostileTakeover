using System.Collections.Concurrent;
using System.Collections.Generic;
using HostileTakeover.Common.BaseClasses;
using HostileTakeover.Common.Enums;
using HostileTakeover.Common.Factions.Models;
using HostileTakeover.Common.Interfaces;
using HostileTakeover.Models;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace HostileTakeover
{
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation, priority: int.MinValue + 1)]
    public class HostileTakeoverServerCore : BaseSessionComp
    {
	    protected override string CompName { get; } = "HostileTakeoverServerCore";
		protected override CompType Type { get; } = CompType.Server;
		protected override MyUpdateOrder Schedule { get; } = MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation;

		// The long is to store the first entity ID in a complex grid picked up with OnEntityAdd; the collection will hold all of the related grids ("constructs")
		private readonly ConcurrentDictionary<long, Construct> _constructs = new ConcurrentDictionary<long, Construct>();

		// This collection is used to map all grids to the construct they belong to.  (long, long) => (whatever grid EntityId, construct key)
		private readonly ConcurrentDictionary<long, long> _constructMap = new ConcurrentDictionary<long, long>();

		// We want to wait half a tick between the grid being picked up and processing it to allow subgrids to populate, so this queue grabs all 
		//	grids OnEntityAdd and processes them the next AfterSimulation tick
		private readonly Queue<MyCubeGrid> _constructQueue = new Queue<MyCubeGrid>();

		private readonly HashSet<IMyCubeGrid> _reusableGridCollection = new HashSet<IMyCubeGrid>();

		//private HandTools _handTools;

		private void PopulateGridList(IMyCubeGrid thisGrid)
		{
			_reusableGridCollection.Clear();
			MyAPIGateway.GridGroups.GetGroup(thisGrid, GridLinkTypeEnum.Mechanical, _reusableGridCollection);
		}

		protected override void SuperEarlySetup()
		{
			base.SuperEarlySetup();
			MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
			//_handTools = new HandTools();
			//_handTools.OnWriteToLog += WriteGeneral;
			//_handTools.OnClose += HandToolsOnClose;
		}

		private void HandToolsOnClose(ICommon obj)
		{
			
		}

		protected override void LateSetup()
		{
			base.LateSetup();
		}

        protected override void AfterSimUpdate()
        {
            
            base.AfterSimUpdate();
		}

        protected override void AfterSimUpdate10Ticks()
        {
            if (_constructs.IsEmpty) return;
            foreach (var construct in _constructs)
            {
                construct.Value.ProcessTickDelayedActions();
            }
			base.AfterSimUpdate10Ticks();
        }

        protected override void Unload()
		{
			base.Unload();
			//_handTools.Close();
			//_handTools.OnWriteToLog -= WriteGeneral;
			//_handTools.OnClose -= HandToolsOnClose;
			MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
			_constructQueue.Clear();
		}

		private void OnEntityAdd(IMyEntity entity)
		{
			MyCubeGrid grid = entity as MyCubeGrid;
			if (grid == null) return;
			_constructQueue.Enqueue(grid);
			WriteGeneral(nameof(OnEntityAdd), $"Added: [{entity.EntityId}] {grid.DisplayName}");
		}

		protected override void BeforeSimUpdate()
		{
			base.BeforeSimUpdate();
		}

		public override void UpdateAfterSimulation()
		{
			base.UpdateAfterSimulation();
			if (_constructQueue.Count < 1) return;
			while (_constructQueue.Count > 0)
			{   // Consider putting a catch in here to ensure that nothing makes this iterate more than x times. 
				MyCubeGrid grid = _constructQueue.Dequeue();

				// I don't exist!  So why am I here...
				if (grid == null)
				{
					WriteGeneral(nameof(UpdateAfterSimulation), $"Grid Rejected as NULL.");
					continue;
				}

				WriteGeneral(nameof(UpdateAfterSimulation), $"Processing: [{grid.EntityId}] {grid.DisplayName}");

				// I have no owner, so don't give me one, asshole. 
				if (grid.BigOwners.Count == 0)
				{
					// Consider not ignoring even if unowned.  It isn't owned now, but what about later?
					WriteGeneral(nameof(UpdateAfterSimulation), $"Grid Rejected as UNOWNED: [{grid.EntityId}] {grid.DisplayName}");
					continue;
				}

				// I'm a projection!  Begone fool! ...or lend me your... components.
				if (grid.Physics == null)
				{
					WriteGeneral(nameof(UpdateAfterSimulation), $"Grid Rejected as PROJECTION: [{grid.EntityId}] {grid.DisplayName}");
					continue;
				}

				if (grid.IsStatic)
				{
					IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(grid.BigOwners[0]);
					if (faction == null) continue;
					if (FactionDictionaries.VanillaTradeFactions.ContainsKey(faction.FactionId))
					{
						WriteGeneral(nameof(UpdateAfterSimulation), $"Grid Rejected as VANILLA TRADE: [{grid.EntityId}] {grid.DisplayName}");
						continue;
					}
				}
				// Catches player owned grids and ignores them
				ulong id = MyAPIGateway.Players.TryGetSteamId(grid.BigOwners[0]);
				if (id > 0)
				{
					// Consider not ignoring player grids.  What if they transfer ownership to a NPC?  Just disabling logic if player owned would be smarter.
					WriteGeneral(nameof(UpdateAfterSimulation), $"Grid Rejected as PLAYER OWNED: [{grid.EntityId}] {grid.DisplayName}");
					continue;
				}
				AddToOwnerships(grid);
			}
		}

		private void AddToOwnerships(MyCubeGrid grid)
		{
			PopulateGridList(grid);
            WriteGeneral(nameof(AddToOwnerships), $"Attaching logic to: [{_reusableGridCollection.Count:00}] [{grid.EntityId}] {grid.DisplayName}");
			// This grid is part of a grid collection, so it needs to be tracked with it's relatives if they are already being tracked
			if (_reusableGridCollection.Count > 1)
			{
                WriteGeneral(nameof(AddToOwnerships), $"Grid found as part of a construct, adding to collection: [{grid.EntityId}] {grid.DisplayName}");
				// Confirmed this grid is part of a tracked group, so add it to the rest of it's family
				foreach (IMyCubeGrid grids in _reusableGridCollection)
				{
					if (!_constructs.ContainsKey(grids.EntityId)) continue;
					_constructMap.TryAdd(grid.EntityId, grids.EntityId);
					_constructs[grids.EntityId].Add(grid);
                    WriteGeneral(nameof(AddToOwnerships), _constructs[grids.EntityId].ToString());
					return;
				}
			}

            WriteGeneral(nameof(AddToOwnerships), $"New grid identified, creating new collection: [{grid.EntityId}] {grid.DisplayName}");
			var construct = new Construct(grid);
			construct.OnCloseConstruct += CloseConstruct;
            construct.OnWriteToLog += WriteGeneral;
			_constructs.TryAdd(grid.EntityId, construct);
			_constructMap.TryAdd(grid.EntityId, grid.EntityId);
            WriteGeneral(nameof(AddToOwnerships), construct.ToString());
		}

        private void CloseConstruct(Construct construct)
        {
			if (_constructs.ContainsKey(construct.GridId))
                _constructs.Remove(construct.GridId);
            if (_constructMap.ContainsKey(construct.GridId))
                _constructMap.Remove(construct.GridId);
            construct.OnWriteToLog -= WriteGeneral;
			construct.OnCloseConstruct -= CloseConstruct;
        }
    }
}