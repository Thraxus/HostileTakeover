using System.Text;
using HostileTakeover.Common.Enums;
using HostileTakeover.Common.Utilities.Tools.Logging;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;

namespace HostileTakeover.Common.BaseClasses
{
	public abstract class BaseSessionComp : MySessionComponentBase
	{
		protected abstract string CompName { get; }

		protected abstract CompType Type { get; }

		protected abstract MyUpdateOrder Schedule { get; }

		private Log _generalLog;

		private bool _superEarlySetupComplete;
		private bool _earlySetupComplete;
		private bool _lateSetupComplete;

		private bool BlockUpdates()
		{
			switch (Type)
			{
				case CompType.Both:
					return false;
				case CompType.Client:
					return References.IsServer;
				case CompType.Server:
					return !References.IsServer;
				default:
					return false;
			}
		}

		/// <summary>
		///	 Amongst the earliest execution points, but not everything is available at this point.
		///	 Main entry point: MyAPIGateway
		///	 Entry point for reading/editing definitions: MyDefinitionManager.Static
		/// </summary>
		public override void LoadData()
		{
			if (BlockUpdates())
			{
				MyAPIGateway.Utilities.InvokeOnGameThread(() => SetUpdateOrder(MyUpdateOrder.NoUpdate)); // sets the proper update schedule to the desired schedule
				return;
			};
			base.LoadData();
			if (!_superEarlySetupComplete) SuperEarlySetup();
		}

		/// <summary>
		///  Always return base.GetObjectBuilder(); after your code!
		///  Do all saving here, make sure to return the OB when done;
		/// </summary>
		/// <returns> Object builder for the session component </returns>
		public override MyObjectBuilder_SessionComponent GetObjectBuilder()
		{
			return base.GetObjectBuilder();
		}

		/// <summary>
		///  This save happens after the game save, so it has limited uses really
		/// </summary>
		public override void SaveData()
		{

			base.SaveData();
		}

		protected virtual void SuperEarlySetup()
		{
			_superEarlySetupComplete = true;
			_generalLog = new Log(CompName);
			WriteGeneral("SuperEarlySetup", $"Waking up.  Is Server: {References.IsServer}");
		}

		/// <summary>
		///  Executed before the world starts updating
		/// </summary>
		public override void BeforeStart()
		{
			if (BlockUpdates()) return;
			base.BeforeStart();
			BasicInformationDump();
		}

        private void BasicInformationDump()
        {
            var sb = new StringBuilder();
            Reporting.GameSettings.Report(sb);
            Reporting.InstalledMods.Report(sb);
            Reporting.ExistingFactions.Report(sb);
            Reporting.StoredIdentities.Report(sb);
			WriteGeneral(sb.ToString());
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sessionComponent"></param>
		public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
		{
			if (BlockUpdates()) return;
			base.Init(sessionComponent);
			if (!_earlySetupComplete) EarlySetup();
		}

		protected virtual void EarlySetup()
		{
			_earlySetupComplete = true;
		}

		/// <summary>
		///  Executed every tick, 60 times a second, before physics simulation and only if game is not paused.
		/// </summary>
		public override void UpdateBeforeSimulation()
		{
			if (BlockUpdates()) return;
			base.UpdateBeforeSimulation();
			if (!_lateSetupComplete) LateSetup();
            UpdateBeforeSim();
		}

		protected virtual void UpdateBeforeSim() { }

		protected virtual void LateSetup()
		{
			_lateSetupComplete = true;
			if (UpdateOrder != Schedule)
				MyAPIGateway.Utilities.InvokeOnGameThread(() => SetUpdateOrder(Schedule)); // sets the proper update schedule to the desired schedule
			WriteGeneral("LateSetup", $"Fully online.");
		}

		/// <summary>
		///  Executed every tick, 60 times a second, after physics simulation and only if game is not paused.
		/// </summary>
		public override void UpdateAfterSimulation()
		{
			if (BlockUpdates()) return;
            base.UpdateAfterSimulation();
			UpdateAfterSim();
        }

        protected virtual void UpdateAfterSim() { }

		protected override void UnloadData()
		{
			Unload();
			base.UnloadData();
		}

		protected virtual void Unload()
		{
			if (BlockUpdates()) return;
			WriteGeneral("Unload", $"Retired.");
			_generalLog?.Close();
		}

		/// <summary>
		///  Gets called 60 times a second before all other update methods, regardless of frame rate, game pause or MyUpdateOrder.
		/// </summary>
		public override void HandleInput()
		{
			base.HandleInput();
		}

		/// <summary>
		///  Executed every tick, 60 times a second, during physics simulation and only if game is not paused.
		///  NOTE: In this example this won't actually be called because of the lack of MyUpdateOrder.Simulation argument in MySessionComponentDescriptor
		/// </summary>
		public override void Simulate()
		{
			base.Simulate();
		}

		/// <summary>
		///  Gets called 60 times a second after all other update methods, regardless of framerate, game pause or MyUpdateOrder.
		///  NOTE: This is the only place where the camera matrix (MyAPIGateway.Session.Camera.WorldMatrix) is accurate, everywhere else it's 1 frame behind.
		/// </summary>
		public override void Draw()
		{
			base.Draw();
		}

		/// <summary>
		///  Executed when game is paused
		/// </summary>
		public override void UpdatingStopped()
		{
			base.UpdatingStopped();
		}


		public void WriteException(string caller, string message)
		{
			_generalLog?.WriteException($"{CompName}: {caller}", message);
		}

		public void WriteGeneral(string caller = "", string message = "")
		{
			_generalLog?.WriteGeneral($"{CompName}: {caller}", message);
		}
	}
}