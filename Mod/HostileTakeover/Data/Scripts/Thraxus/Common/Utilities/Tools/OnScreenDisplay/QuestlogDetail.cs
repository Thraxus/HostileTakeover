using System.Text;

namespace HostileTakeover.Common.Utilities.Tools.OnScreenDisplay
{
	public class QuestLogDetail
	{
		public StringBuilder OldQuest;
		public StringBuilder NewQuest;

		public QuestLogDetail(StringBuilder newQuest)
		{
			NewQuest = newQuest;
		}

		public void UpdateQuest(StringBuilder newQuest)
		{
			OldQuest = NewQuest;
			NewQuest = newQuest;
		}
	}
}
