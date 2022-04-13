using System.Text;
using Sandbox.ModAPI;
using VRage.ModAPI;

namespace HostileTakeover.Common.Reporting
{
    public static class ExistingFactions
    {
		public static StringBuilder Report(StringBuilder sb)
		{
            sb.AppendLine();
            sb.AppendFormat("{0, -2}Existing Factions", " ");
            sb.AppendFormat("{0, -2}{1,-20}", " ", "_");
			
            // SteamId > 0 denotes player; no reason to see / save their ID though
            sb.AppendFormat("{0, -4}[FactionId][Tag][IsEveryoneNpc] Display Name\n", " ");
            sb.AppendFormat("{0, -6}[MemberId] Display Name\n", " ");
			//foreach (var faction in MyAPIGateway.Session.Factions.Factions)
   //         {
   //             sb.AppendFormat("{0, -4}[{1:D18}][{2}][{3}] {4}\n", " ", faction.Key, faction.Value.Tag, faction.Value.IsEveryoneNpc(), faction.Value.Name);
   //             foreach (var member in faction.Value.Members)
   //             {
   //                 IMyEntity ent = MyAPIGateway.Entities.GetEntityById(member.Key);
   //                 sb.AppendFormat("{0, -6}[{1:D18}] {3}\n", " ", member.Key, ent.DisplayName);
   //             }
   //         }

            return sb;
		}
	}
}
