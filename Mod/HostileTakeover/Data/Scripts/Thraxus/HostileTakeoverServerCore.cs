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
		private readonly ConcurrentDictionary<long, Constructs> _constructs = new ConcurrentDictionary<long, Constructs>();

		// This collection is used to map all grids to the construct they belong to.  (long, long) => (whatever grid EntityId, construct key)
		private readonly ConcurrentDictionary<long, long> _constructMap = new ConcurrentDictionary<long, long>();

		// We want to wait half a tick between the grid being picked up and processing it to allow subgrids to populate, so this queue grabs all 
		//	grids OnEntityAdd and processes them the next AfterSimulation tick
		private readonly Queue<MyCubeGrid> _constructQueue = new Queue<MyCubeGrid>();

		private readonly HashSet<IMyCubeGrid> _reusableGridCollection = new HashSet<IMyCubeGrid>();

		private HandTools _handTools;

		private void PopulateGridList(IMyCubeGrid thisGrid)
		{
			_reusableGridCollection.Clear();
			MyAPIGateway.GridGroups.GetGroup(thisGrid, GridLinkTypeEnum.Mechanical, _reusableGridCollection);
		}

		protected override void SuperEarlySetup()
		{
			base.SuperEarlySetup();
			MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
			_handTools = new HandTools();
			_handTools.OnWriteToLog += WriteToLog;
			_handTools.OnClose += HandToolsOnClose;
		}

		private void HandToolsOnClose(ICommon obj)
		{
			
		}

		protected override void LateSetup()
		{
			base.LateSetup();
		}

		protected override void Unload()
		{
			base.Unload();
			_handTools.Close();
			_handTools.OnWriteToLog -= WriteToLog;
			_handTools.OnClose -= HandToolsOnClose;
			MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
			_constructQueue.Clear();
		}

		private void OnEntityAdd(IMyEntity entity)
		{
			MyCubeGrid grid = entity as MyCubeGrid;
			if (grid == null) return;
			_constructQueue.Enqueue(grid);
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
				if (grid == null) continue; // I don't exist!  So why am I here... 
				if (grid.BigOwners.Count == 0) continue; // I have no owner, so don't give me one, asshole. 
				if (grid.Physics == null) continue; // I'm a projection!  Begone fool! ...or lend me your... components.
				if (grid.IsStatic)
				{
					IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(grid.BigOwners[0]);
					if (faction == null) continue;
					if (FactionDictionaries.VanillaTradeFactions.ContainsKey(faction.FactionId))
					{
						WriteToLog("UpdateAfterSimulation", $"Ignoring {grid.DisplayName} : vanilla faction trade station", LogType.General);
						continue;
					}
				}
				// Catches player owned grids and ignores them
				ulong id = MyAPIGateway.Players.TryGetSteamId(grid.BigOwners[0]);
				if (id > 0) continue;
				AddToOwnerships(grid);
			}
		}

		private void AddToOwnerships(MyCubeGrid grid)
		{
			PopulateGridList(grid);

			// This grid is part of a grid collection, so it needs to be tracked with it's relatives if they are already being tracked
			if (_reusableGridCollection.Count > 1)
			{
				// Confirmed this grid is part of a tracked group, so add it to the rest of it's family
				foreach (var grids in _reusableGridCollection)
				{
					if (!_constructs.ContainsKey(grids.EntityId)) continue;
					_constructMap.TryAdd(grid.EntityId, grids.EntityId);
					_constructs[grids.EntityId].Add(grid);
					return;
				}
			}

			Constructs construct = new Constructs();
			construct.Add(grid);
			construct.OnClose += CloseGrid;
			_constructs.TryAdd(grid.EntityId, construct);
			_constructMap.TryAdd(grid.EntityId, grid.EntityId);
		}

		private void CloseGrid(long gridId)
		{
			if (_constructs.ContainsKey(gridId))
				_constructs.Remove(gridId);
			if(_constructMap.ContainsKey(gridId))
				_constructMap.Remove(gridId);
		}
	}
}