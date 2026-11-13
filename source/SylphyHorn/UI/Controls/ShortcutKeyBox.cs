using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SylphyHorn.Serialization;
using SylphyHorn.Services;
using VirtualKey = System.Windows.Forms.Keys;

namespace SylphyHorn.UI.Controls
{
	[TemplatePart(Name = PART_ModifyKeys, Type = typeof(ItemsControl))]
	[TemplatePart(Name = PART_Key, Type = typeof(Keytop))]
	public class ShortcutKeyBox : TextBox
	{
		private const string PART_ModifyKeys = nameof(PART_ModifyKeys);
		private const string PART_Key = nameof(PART_Key);

		static ShortcutKeyBox()
		{
			DefaultStyleKeyProperty.OverrideMetadata(
				typeof(ShortcutKeyBox),
				new FrameworkPropertyMetadata(typeof(ShortcutKeyBox)));
		}

		private readonly HashSet<Key> _pressedModifiers = new HashSet<Key>();
		private Key _pressedKey = Key.None;
		private ItemsControl _modifiersPresneter;
		private Keytop _keyPresenter;
		private IDisposable _hookDisposable;

		public static HookService HookService { get; set; }

		#region Current 依存関係プロパティ

		public IList<int> Current
		{
			get { return (IList<int>)this.GetValue(CurrentProperty); }
			set { this.SetValue(CurrentProperty, value); }
		}

		public static readonly DependencyProperty CurrentProperty =
			DependencyProperty.Register(nameof(Current), typeof(IList<int>), typeof(ShortcutKeyBox), new UIPropertyMetadata(null, CurrentPropertyChangedCallback));

		private static void CurrentPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs args)
		{
			var instance = (ShortcutKeyBox)d;
			instance.UpdateText();
		}

		private ShortcutKey? CurrentAsKeys
		{
			get { return this.Current?.ToShortcutKey(); }
			// Atualizações internas não podem substituir o binding que salva o atalho.
			set { this.SetCurrentValue(CurrentProperty, value?.ToSerializable()); }
		}

		#endregion

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			this._modifiersPresneter = this.GetTemplateChild(PART_ModifyKeys) as ItemsControl;
			this._keyPresenter = this.GetTemplateChild(PART_Key) as Keytop;

			this.UpdateText();
		}

		protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
		{
			base.OnGotKeyboardFocus(e);

			this._hookDisposable = HookService?.Suspend();
			this.UpdateText();
		}

		protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
		{
			base.OnLostKeyboardFocus(e);

			this._hookDisposable?.Dispose();
			this._hookDisposable = null;
		}

		protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			var keytop = FindAncestor<Keytop>(e.OriginalSource as DependencyObject);
			if (this.IsEnabled && !this.IsReadOnly && keytop != null && !ReferenceEquals(keytop, this._keyPresenter) && IsSideModifier(keytop.Key))
			{
				// O clique no modificador troca o lado sem entrar no modo de captura.
				this.CycleModifier(keytop.Key);
				e.Handled = true;
				return;
			}

			base.OnPreviewMouseLeftButtonDown(e);
		}

		private void CycleModifier(VirtualKey key)
		{
			var current = this.CurrentAsKeys ?? ShortcutKey.None;
			var modifiers = (current.ModifiersInternal ?? current.Modifiers ?? Array.Empty<VirtualKey>()).ToArray();
			for (var index = 0; index < modifiers.Length; index++)
			{
				if (modifiers[index] == key) modifiers[index] = NextModifierSide(key);
			}

			this.CurrentAsKeys = new ShortcutKey(current.Key, modifiers.Distinct().ToArray());
			this.UpdateText();
		}

		private static bool IsSideModifier(VirtualKey key)
		{
			return key == VirtualKey.LShiftKey || key == VirtualKey.RShiftKey || key == VirtualKey.Shift
				|| key == VirtualKey.LControlKey || key == VirtualKey.RControlKey || key == VirtualKey.Control
				|| key == VirtualKey.LMenu || key == VirtualKey.RMenu || key == VirtualKey.Menu
				|| key == VirtualKey.LWin || key == VirtualKey.RWin || key == ShortcutKey.AnyWindowsKey;
		}

		private static VirtualKey NextModifierSide(VirtualKey key)
		{
			switch (key)
			{
				case VirtualKey.LShiftKey: return VirtualKey.RShiftKey;
				case VirtualKey.RShiftKey: return VirtualKey.Shift;
				case VirtualKey.Shift: return VirtualKey.LShiftKey;
				case VirtualKey.LControlKey: return VirtualKey.RControlKey;
				case VirtualKey.RControlKey: return VirtualKey.Control;
				case VirtualKey.Control: return VirtualKey.LControlKey;
				case VirtualKey.LMenu: return VirtualKey.RMenu;
				case VirtualKey.RMenu: return VirtualKey.Menu;
				case VirtualKey.Menu: return VirtualKey.LMenu;
				case VirtualKey.LWin: return VirtualKey.RWin;
				case VirtualKey.RWin: return ShortcutKey.AnyWindowsKey;
				case ShortcutKey.AnyWindowsKey: return VirtualKey.LWin;
				default: return key;
			}
		}

		private static T FindAncestor<T>(DependencyObject source) where T : DependencyObject
		{
			while (source != null && !(source is T)) source = System.Windows.Media.VisualTreeHelper.GetParent(source);
			return source as T;
		}

		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			if (!e.IsRepeat)
			{
				var key = e.Key == Key.System ? e.SystemKey : e.Key;
				if (key == Key.Back)
				{
					this._pressedModifiers.Clear();
					this._pressedKey = Key.None;
				}
				else if (key.IsModifyKey())
				{
					this._pressedModifiers.Add(key);
				}
				else
				{
					this._pressedKey = key;
				}

				this.CurrentAsKeys = /*this._pressedModifiers.Any() && */this._pressedKey != Key.None
					? this.GetShortcutKey()
					: (ShortcutKey?)null;

				this.UpdateText();
			}

			e.Handled = true;
			base.OnPreviewKeyDown(e);
		}

		protected override void OnPreviewKeyUp(KeyEventArgs e)
		{
			if (!e.IsRepeat)
			{
				var key = e.Key == Key.System ? e.SystemKey : e.Key;
				if (key.IsModifyKey())
				{
					this._pressedModifiers.Remove(key);
				}

				this._pressedKey = Key.None;
				this.UpdateText();
			}

			e.Handled = true;
			base.OnPreviewKeyUp(e);
		}

		private void UpdateText()
		{
			var currentKey = this.CurrentAsKeys ?? this.GetShortcutKey();

			if (this._modifiersPresneter != null)
			{
				var modifiers = (currentKey.ModifiersInternal ?? currentKey.Modifiers ?? Enumerable.Empty<VirtualKey>())
					.OrderBy(x => x)
					.ToArray();

				this._modifiersPresneter.ItemsSource = modifiers;
				this._modifiersPresneter.Visibility = modifiers.Length == 0
					? Visibility.Collapsed
					: Visibility.Visible;
			}

			if (this._keyPresenter != null)
			{
				this._keyPresenter.Key = currentKey.Key;
				this._keyPresenter.Visibility = currentKey.Key == VirtualKey.None
					? Visibility.Collapsed
					: Visibility.Visible;
			}
		}

		private ShortcutKey GetShortcutKey()
		{
			return new ShortcutKey(
				this._pressedKey.ToVirtualKey(),
				this._pressedModifiers.Select(x => x.ToVirtualKey()).ToArray());
		}
	}
}
