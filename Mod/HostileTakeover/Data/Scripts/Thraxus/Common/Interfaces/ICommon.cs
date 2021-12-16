using System;
using HostileTakeover.Common.Enums;
using VRage.Game;

namespace HostileTakeover.Common.Interfaces
{
	public interface ICommon
	{
		event Action<ICommon> OnClose;
		event Action<string, string, LogType, bool, int, string> OnWriteToLog;

		void Update(ulong tick);

		bool IsClosed { get; }

		void Close();

		void WriteToLog(string caller, string message, LogType type, bool showOnHud = false, int duration = Settings.DefaultLocalMessageDisplayTime, string color = MyFontEnum.Green);
	}
}
