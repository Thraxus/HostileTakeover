using HostileTakeover.Common.BaseClasses;
using HostileTakeover.Common.Enums;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace HostileTakeover
{
	public class HostileTakeoverClientCore : BaseSessionComp
	{
		protected override string CompName { get; } = "HostileTakeoverClientCore";
		protected override CompType Type { get; } = CompType.Client;
		protected override MyUpdateOrder Schedule { get; } = MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation;

		protected override void SuperEarlySetup()
		{
			base.SuperEarlySetup();
			//MyEntities.OnEntityCreate += OnEntityCreate;
		}


		protected override void Unload()
		{
			//MyEntities.OnEntityCreate -= OnEntityCreate;
			base.Unload();
		}
	}
}