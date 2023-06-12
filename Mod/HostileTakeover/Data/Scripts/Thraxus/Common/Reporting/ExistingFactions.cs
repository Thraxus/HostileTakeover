using System.Text;

namespace HostileTakeover.Common.Reporting
{
    public static class ExistingFactions
    {
		public static StringBuilder Report(StringBuilder sb)
		{
            sb.AppendLine();
            sb.AppendFormat("{0, -2}Existing Factions", " ");
            sb.AppendLine("__________________________________________________");
            sb.AppendLine();

            // SteamId > 0 denotes player; no reason to see / save their ID though
            sb.AppendFormat("{0, -4}[FactionId][Tag][IsEveryoneNpc] Display Name\n", " ");
            sb.AppendLine();
            sb.AppendFormat("{0, -6}[MemberId] Display Name\n", " ");
            sb.AppendLine();

            return sb;
		}
	}
}
