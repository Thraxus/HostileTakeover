using System.Text;
using Sandbox.ModAPI;

namespace HostileTakeover.Common.Reporting
{
	public static class GameSettings
	{
		public static StringBuilder Report(StringBuilder sb)
		{

            sb.AppendLine();
            sb.AppendFormat("{0, -2}Game Settings", " ");
            sb.AppendFormat("{0, -2}{1,-20}", " ", "_");
			sb.AppendFormat("{0, -4}Adaptive Sim Enabled:{1}", " ", MyAPIGateway.Session.SessionSettings.AdaptiveSimulationQuality);
			sb.AppendFormat("{0, -4}Cargo Ships Enabled:{1}", " ", MyAPIGateway.Session.SessionSettings.CargoShipsEnabled);
			sb.AppendFormat("{0, -4}Stop Grids Period (minutes):{1}", " ", MyAPIGateway.Session.SessionSettings.StopGridsPeriodMin);
			sb.AppendFormat("{0, -4}Encounters Enabled:{1}", " ", MyAPIGateway.Session.SessionSettings.EnableEncounters);
			sb.AppendFormat("{0, -4}Economy Enabled:{1}", " ", MyAPIGateway.Session.SessionSettings.EnableEconomy);
			sb.AppendFormat("{0, -4}Economy Ticks (seconds):{1}", " ", MyAPIGateway.Session.SessionSettings.EconomyTickInSeconds);
			sb.AppendFormat("{0, -4}Bounty Contracts Enabled:{1}", " ", MyAPIGateway.Session.SessionSettings.EnableBountyContracts);
			sb.AppendFormat("{0, -4}Drones Enabled:{1}", " ", MyAPIGateway.Session.SessionSettings.EnableDrones);
			sb.AppendFormat("{0, -4}Scripts Enabled:{1}", " ", MyAPIGateway.Session.SessionSettings.EnableIngameScripts);
			sb.AppendFormat("{0, -4}Asteroid Density:{1}", " ", MyAPIGateway.Session.SessionSettings.ProceduralDensity);
			sb.AppendFormat("{0, -4}Weather Enabled:{1}", " ", MyAPIGateway.Session.SessionSettings.WeatherSystem);
			sb.AppendFormat("{0, -4}Online Mode:{1}", " ", MyAPIGateway.Session.SessionSettings.OnlineMode);
			sb.AppendFormat("{0, -4}Game Mode:{1}", " ", MyAPIGateway.Session.SessionSettings.GameMode);
			sb.AppendFormat("{0, -4}Spiders Enabled:{1}", " ", MyAPIGateway.Session.SessionSettings.EnableSpiders);
			sb.AppendFormat("{0, -4}Wolves Enabled:{1}", " ", MyAPIGateway.Session.SessionSettings.EnableWolfs);
			sb.AppendFormat("{0, -4}Sync Distance:{1}", " ", MyAPIGateway.Session.SessionSettings.SyncDistance);
			sb.AppendFormat("{0, -4}View Distance:{1}", " ", MyAPIGateway.Session.SessionSettings.ViewDistance);
			sb.AppendFormat("{0, -4}Player Inventory Size Multiplier:{1}", " ", MyAPIGateway.Session.SessionSettings.InventorySizeMultiplier);
			sb.AppendFormat("{0, -4}Grid Inventory Size Multiplier:{1}", " ", MyAPIGateway.Session.SessionSettings.BlocksInventorySizeMultiplier);
			sb.AppendFormat("{0, -4}Total Pirate PCU:{1}", " ", MyAPIGateway.Session.SessionSettings.PiratePCU);
			sb.AppendFormat("{0, -4}Total Player PCU:{1}", " ", MyAPIGateway.Session.SessionSettings.TotalPCU);

			return sb;
		}
	}
}