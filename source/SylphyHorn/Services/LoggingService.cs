using System;
using System.Collections.Generic;
using System.Linq;
using Livet;
using MetroTrilithon.Threading.Tasks;

namespace SylphyHorn.Services
{
	public interface ILog
	{
		DateTimeOffset DateTime { get; }
		string Header { get; }
		string Content { get; }
	}

	public class LoggingService
	{
		public static LoggingService Instance { get; } = new LoggingService();

		public ObservableSynchronizedCollection<ILog> Logs { get; } = new ObservableSynchronizedCollection<ILog>();

		private LoggingService() { }

		public void Register(ILog log)
		{
			this.Logs.Add(log);
			// Diagnósticos de atalhos são frequentes. Limitar o histórico em memória.
			while (this.Logs.Count > 1000) this.Logs.RemoveAt(0);
		}

		public void Register(string header, string content)
		{
			// Diagnóstico local, sem encaminhar eventos de teclado à telemetria.
			this.Register(new Log(header, content));
		}

		public void Register(Exception exception)
		{
			if (exception is AggregateException aggregateException)
			{
				foreach (var innerException in aggregateException.InnerExceptions) this.Register(new Log(innerException));
			}
			else
			{
				this.Register(new Log(exception));
			}
		}

		public void Register(TaskLog log)
		{
			this.Register(new Log(log));
		}

		private class Log : ILog
		{
			public DateTimeOffset DateTime { get; } = DateTimeOffset.Now;

			public string Header { get; }

			public string Content { get; }

			public Log(string header, string content)
			{
				this.Header = header;
				this.Content = content;
			}

			public Log(Exception ex)
			{
				this.Header = ex.GetType().Name;
				this.Content = ex.ToString();

				Application.TelemetryClient.TrackException(ex);
			}

			public Log(TaskLog log)
			{
				this.Header = log.Exception.GetType().Name;
				this.Content = $@"Unhandled exception was thrown by Task<T>.
{log.CallerMemberName} ({System.IO.Path.GetFileName(log.CallerFilePath)}#{log.CallerLineNumber})
-----
{log.Exception}";

				Application.TelemetryClient.TrackException(
					log.Exception,
					properties: new Dictionary<string, string>()
					{
						{ "CallerMemberName", log.CallerMemberName },
						{ "CallerFilePath&LineNumber", $"{log.CallerFilePath}#{log.CallerLineNumber}" },
					});
			}
		}
	}
}
