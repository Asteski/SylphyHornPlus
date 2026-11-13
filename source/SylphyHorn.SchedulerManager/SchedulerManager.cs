using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;


namespace SylphyHorn
{
	// COM por IDispatch evita depender de um assembly Interop gerado pelo Visual Studio.
	class SchedulerManager
	{
		static void Main(string[] args)
		{
			if (args.Length < 1)
			{
				Environment.Exit(-1);
			}
			var command = args[0];
			var process = args.Length > 1 ? new SchedulerProcess(appPath: args[1]) : new SchedulerProcess();
			switch (command)
			{
				case "register":
					Environment.Exit(process.Register());
					break;
				case "unregister":
					Environment.Exit(process.Unregister());
					break;
				case "start":
					Environment.Exit(process.Start());
					break;
				case "stop":
					Environment.Exit(process.Stop());
					break;
				case "restart":
					Environment.Exit(process.Restart());
					break;
				case "hastask":
					Environment.Exit(Convert.ToInt32(process.HasTask));
					break;
				case "isrunning":
					Environment.Exit(Convert.ToInt32(process.IsRunning));
					break;
			}

		}
	}

	class SchedulerProcess
	{
		private const string _schedulerDir = "\\";
		private const string _defaultAppName = "SylphyHorn";
		private const string _defaultAppExtension = ".exe";
		private readonly string _schedulerPath;
		private readonly string _taskName;
		private readonly string _appPath;
		private readonly string _appName;
		private readonly WindowsPrincipal _principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());

		public bool HasTask => this.FindStartupTask() != null;

		public bool IsRunning => this.FindStartupTask()?.State == 4;

		public bool IsAdministrator => _principal.IsInRole(WindowsBuiltInRole.Administrator);

		public SchedulerProcess()
			: this(GetDefaultApplicationPath())
		{
		}

		public SchedulerProcess(string appPath)
		{
			if (string.IsNullOrEmpty(appPath) || !File.Exists(appPath))
			{
				throw new ArgumentNullException($"Error: app path ({appPath}) is invalid.");
			}
			this._appPath = Path.GetFullPath(appPath);
			this._appName = Path.GetFileNameWithoutExtension(appPath);
			this._taskName = this._appName + " Startup";
			this._schedulerPath = _schedulerDir + this._taskName;
		}

		public int Register()
		{
			if (!this.IsAdministrator)
			{
				return -1;
			}

			dynamic taskService = null;
			try
			{
				taskService = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true));
				taskService.Connect(null, null, null, null);
				dynamic rootfolder = null;
				try
				{
					rootfolder = taskService.GetFolder(_schedulerDir);
					var taskDefinition = this.CreateTaskDefinition(taskService);
					try
					{
						rootfolder.RegisterTaskDefinition(
							this._schedulerPath,
							taskDefinition,
							(int)6,
							null,
							null,
							3,
							null
						);
					}
					catch (UnauthorizedAccessException)
					{
						return -1;
					}
					catch (Exception e)
					{
						return e.HResult;
					}
				}
				finally
				{
					if (rootfolder != null) Marshal.ReleaseComObject(rootfolder);
				}
			}
			finally
			{
				if (taskService != null) Marshal.ReleaseComObject(taskService);
			}

			return 0;
		}

		public int Unregister()
		{
			if (!this.IsAdministrator)
			{
				return -1;
			}
			else if (!this.HasTask)
			{
				return 0;
			}

			dynamic taskService = null;
			try
			{
				taskService = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true));
				taskService.Connect(null, null, null, null);
				dynamic rootfolder = null;
				try
				{
					rootfolder = taskService.GetFolder(_schedulerDir);
					try
					{
						rootfolder.DeleteTask(this._taskName, 0);
					}
					catch (UnauthorizedAccessException)
					{
						return -1;
					}
					catch (Exception e)
					{
						return e.HResult;
					}
				}
				finally
				{
					if (rootfolder != null) Marshal.ReleaseComObject(rootfolder);
				}
			}
			finally
			{
				if (taskService != null) Marshal.ReleaseComObject(taskService);
			}

			return 0;
		}

		public int Start()
		{
			var task = this.FindStartupTask();
			if (task == null)
			{
				return -1;
			}
			task.Run(null);
			return 0;
		}

		public int Stop()
		{
			var task = this.FindStartupTask();
			if (task == null)
			{
				return -1;
			}
			task.Stop(0);
			return 0;
		}

		public int Restart()
		{
			var task = this.FindStartupTask();
			if (task == null || task.State == 1)
			{
				return -1;
			}

			var taskActions = task.Definition.Actions;
			var appPath = taskActions.Count > 0
				? (taskActions[1])?.Path ?? this._appPath
				: this._appPath;
			appPath = Regex.Replace(appPath, @"^""(.+)""$", "$1");
			if (string.IsNullOrEmpty(appPath) || !File.Exists(appPath))
			{
				throw new FileNotFoundException($"Error: app path ({appPath}) is invalid.");
			}
			else if (task.State == 4)
			{
				task.Stop(0);
			}

			var appName = Path.GetFileNameWithoutExtension(appPath);
			var totalTime = 0;
			const int timeout = 30000;
			const int interval = 100;
			while (Process.GetProcessesByName(appName).Length > 0)
			{
				Thread.Sleep(interval);
				totalTime += interval;
				if (totalTime >= timeout)
				{
					return -1;
				}
			}

			task.Run(null);
			return 0;
		}

		private dynamic CreateTaskDefinition(dynamic taskService)
		{
			var taskDefinition = taskService.NewTask(0);
			var registrationInfo = taskDefinition.RegistrationInfo;

			// Actions Property
			var actionCollection = taskDefinition.Actions;
			var execAction = actionCollection.Create(0);

			// Triggers Property
			var triggerCollection = taskDefinition.Triggers;
			var trigger = triggerCollection.Create(9);

			// Settings Property
			var taskSettings = taskDefinition.Settings;

			/* General Settings */
			registrationInfo.Author = "";
			registrationInfo.Description = "";
			var principal = taskDefinition.Principal;
			// A tarefa pertence ao usuário atual e executa somente em sua sessão interativa.
			principal.UserId = WindowsIdentity.GetCurrent().User.Value;
			trigger.UserId = principal.UserId;
			principal.LogonType = 3;
			principal.RunLevel = 1;
			taskSettings.DisallowStartIfOnBatteries = false;
			taskSettings.StopIfGoingOnBatteries = false;
			taskSettings.ExecutionTimeLimit = "PT0S";
			taskSettings.Compatibility = 2;
			taskSettings.Hidden = false;
			taskSettings.Priority = 6;

			/* Trigger Settings */
			trigger.Enabled = true;

			/* Operation Settings */
			execAction.Path = this._appPath;
			execAction.WorkingDirectory = Path.GetDirectoryName(this._appPath);
			//execAction.Arguments = "";

			return taskDefinition;
		}

		private dynamic FindStartupTask()
		{
			dynamic taskService = null;
			try
			{
				taskService = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true));
				taskService.Connect(null, null, null, null);
				dynamic rootfolder = null;
				try
				{
					rootfolder = taskService.GetFolder(_schedulerDir);
					try
					{
						return rootfolder.GetTask(this._schedulerPath);
					}
					catch (FileNotFoundException)
					{
						return null;
					}
					catch (UnauthorizedAccessException)
					{
						return null;
					}
					catch (Exception)
					{
						return null;
					}
				}
				finally
				{
					if (rootfolder != null) Marshal.ReleaseComObject(rootfolder);
				}
			}
			finally
			{
				if (taskService != null) Marshal.ReleaseComObject(taskService);
			}
		}

		private static string GetDefaultApplicationPath()
		{
			var appDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
			return Path.Combine(appDir, SchedulerProcess._defaultAppName + SchedulerProcess._defaultAppExtension);
		}
	}
}
