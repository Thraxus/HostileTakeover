using System;
using System.IO;
using Sandbox.ModAPI;

namespace HostileTakeover.Common.Utilities.Tools.Logging
{
	public class Log
	{
		private string LogName { get; set; }

		private TextWriter TextWriter { get; set; }

		private static string TimeStamp => DateTime.Now.ToString("ddMMMyy_HH:mm:ss:ffff");

		private const int DefaultIndent = 4;

		private static string Indent { get; } = new string(' ', DefaultIndent);

		public Log(string logName)
		{
			LogName = logName + ".log";
			Init();
		}

		private void Init()
		{
			if (TextWriter != null) return;
			TextWriter = MyAPIGateway.Utilities.WriteFileInLocalStorage(LogName, typeof(Log));
		}

		public void Close()
		{
			TextWriter?.Flush();
			TextWriter?.Close();
			TextWriter = null;
		}

		public void WriteGeneral(string caller = "", string message = "")
		{
			BuildLogLine(caller, message);
		}

		public void WriteException(string caller = "", string message = "")
		{
			BuildLogLine(caller, "Exception!\n\n" + message);
		}

		private readonly object _lockObject = new object();

		private void BuildLogLine(string caller, string message)
		{
			lock (_lockObject)
			{
				WriteLine($"{TimeStamp}{Indent}{caller}{Indent}{message}");
			}
		}

		private void WriteLine(string line)
		{
			TextWriter?.WriteLine(line);
			TextWriter?.Flush();
		}
	}
}
