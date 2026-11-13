using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using MetroTrilithon.Lifetime;

namespace SylphyHorn.Services
{
	public class HookService : IDisposable
	{
		private readonly ShortcutKeyDetector _detector = new ShortcutKeyDetector();
		private readonly List<HookAction> _keyHookActions = new List<HookAction>();
		private readonly List<HookAction> _mouseHookActions = new List<HookAction>();
		private int _suspendRequestCount;
		private bool _disposed;
		private Action _reloadAction;

		public Action Reload
		{
			get
			{
				return () =>
				{
					if (_reloadAction != null)
					{
						_keyHookActions.Clear();
						_mouseHookActions.Clear();
						_reloadAction();
					}
				};
			}
			set
			{
				_reloadAction = value;
			}
		}

		/// <summary>
		/// Occurs when a hook service is suspended.
		/// </summary>
		public event Action Suspended;

		public HookService()
		{
			this._detector.KeyPressed += this.KeyHookOnPressed;
			this._detector.KeyUp += this.KeyHookOnUp;
			this._detector.ButtonPressed += this.MouseHookOnPressed;
			this._detector.ButtonUp += this.MouseHookOnUp;
			this._detector.Start();
			SystemEvents.SessionSwitch += this.OnSessionSwitch;
			SystemEvents.PowerModeChanged += this.OnPowerModeChanged;
		}

		private void OnSessionSwitch(object sender, SessionSwitchEventArgs args)
		{
			if (args.Reason == SessionSwitchReason.SessionUnlock
				|| args.Reason == SessionSwitchReason.RemoteConnect
				|| args.Reason == SessionSwitchReason.ConsoleConnect)
				this.QueueHookRecovery();
		}

		private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs args)
		{
			if (args.Mode == PowerModes.Resume) this.QueueHookRecovery();
		}

		private void QueueHookRecovery()
		{
			// Hooks pertencem à thread com o loop de mensagens que os instalou.
			VisualHelper.InvokeOnUIDispatcher(() =>
			{
				if (this._disposed) return;
				try
				{
					this._detector.Restart();
					LoggingService.Instance.Register("Atalhos", "Hooks reinstalados após retomada da sessão ou suspensão do Windows.");
				}
				catch (Exception ex) { LoggingService.Instance.Register(ex); }
			});
		}

		public IDisposable Suspend()
		{
			if (this._disposed) throw new ObjectDisposedException(nameof(HookService));
			this._suspendRequestCount++;
			this._detector.Stop();

			this.Suspended?.Invoke();

			return Disposable.Create(() =>
			{
				if (this._disposed) return;
				this._suspendRequestCount--;
				if (this._suspendRequestCount == 0)
				{
					try { this.Reload(); }
					finally { this._detector.Start(); }
				}
			});
		}

		public IDisposable RegisterKeyAction(Func<ShortcutKey> getShortcutKey, Action<IntPtr> action)
		{
			return this.Register(this._keyHookActions, getShortcutKey, action, () => true);
		}

		public IDisposable RegisterKeyAction(Func<ShortcutKey> getShortcutKey, Action<IntPtr> action, Func<bool> canExecute)
		{
			return this.Register(this._keyHookActions, getShortcutKey, action, canExecute);
		}

		public IDisposable RegisterMouseAction(Func<ShortcutKey> getShortcutKey, Action<IntPtr> action)
		{
			return this.Register(this._mouseHookActions, getShortcutKey, action, () => true);
		}

		public IDisposable RegisterMouseAction(Func<ShortcutKey> getShortcutKey, Action<IntPtr> action, Func<bool> canExecute)
		{
			return this.Register(this._mouseHookActions, getShortcutKey, action, canExecute);
		}

		private IDisposable Register(List<HookAction> hookActions, Func<ShortcutKey> getShortcutKey, Action<IntPtr> action, Func<bool> canExecute)
		{
			if (getShortcutKey().Key == Keys.None) return Disposable.Create(() => { });

			var hook = new HookAction(getShortcutKey, action, canExecute);
			hookActions.Add(hook);

			return Disposable.Create(() => hookActions.Remove(hook));
		}

		private void KeyHookOnPressed(object sender, ShortcutKeyPressedEventArgs args)
		{
			HookOnPressed(sender, this._keyHookActions, args);
		}

		private void KeyHookOnUp(object sender, ShortcutKeyPressedEventArgs args)
		{
			HookOnUp(sender, this._keyHookActions, args);
		}

		private void MouseHookOnPressed(object sender, ShortcutKeyPressedEventArgs args)
		{
			HookOnPressed(sender, this._mouseHookActions, args);
		}

		private void MouseHookOnUp(object sender, ShortcutKeyPressedEventArgs args)
		{
			HookOnUp(sender, this._mouseHookActions, args);
		}

		private void HookOnPressed(object sender, List<HookAction> hookActions, ShortcutKeyPressedEventArgs args)
		{
			if (args.ShortcutKey == ShortcutKey.None) return;

			var target = hookActions.FirstOrDefault(x => x.GetShortcutKey().MatchesPressed(args.ShortcutKey));
			if (target != null && target.CanExecute())
			{
				// Capturar o destino agora: o foco pode mudar antes de executar a ação agendada.
				var foreground = InteropHelper.GetForegroundWindowEx();
				var shortcutDescription = args.ShortcutKey.ToString();
				VisualHelper.InvokeOnUIDispatcher(() =>
				{
					if (!this._disposed && this._suspendRequestCount == 0)
					{
						LoggingService.Instance.Register("Atalho recebido", shortcutDescription);
						target.Action(foreground);
					}
				});
				args.Handled = true;
			}
		}

		private void HookOnUp(object sender, List<HookAction> hookActions, ShortcutKeyPressedEventArgs args)
		{
			if (args.ShortcutKey == ShortcutKey.None) return;

			var target = hookActions.FirstOrDefault(x => x.GetShortcutKey().MatchesPressed(args.ShortcutKey));
			if (target != null && target.CanExecute())
			{
				args.Handled = true;
			}
		}

		public void Dispose()
		{
			if (this._disposed) return;
			this._disposed = true;
			SystemEvents.SessionSwitch -= this.OnSessionSwitch;
			SystemEvents.PowerModeChanged -= this.OnPowerModeChanged;
			this._detector.Dispose();
			this._keyHookActions.Clear();
			this._mouseHookActions.Clear();
		}

		private class HookAction
		{
			public Func<ShortcutKey> GetShortcutKey { get; }

			public Action<IntPtr> Action { get; }

			public Func<bool> CanExecute { get; }

			public HookAction(Func<ShortcutKey> getShortcutKey, Action<IntPtr> action, Func<bool> canExecute)
			{
				this.GetShortcutKey = getShortcutKey;
				this.Action = action;
				this.CanExecute = canExecute;
			}
		}
	}
}
