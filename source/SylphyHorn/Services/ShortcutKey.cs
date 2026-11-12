using System;
using System.Collections.Generic;
using System.Linq;
using MetroTrilithon.Linq;

#if WINDOWS_UWP
using Windows.System;
#else
using VirtualKey = System.Windows.Forms.Keys;
#endif

namespace SylphyHorn.Services
{
	/// <summary>
	/// Represents a shortcut key ([modifer key(s)] + [key] style).
	/// </summary>
	public struct ShortcutKey
	{
		public VirtualKey Key { get; }
		public VirtualKey[] Modifiers { get; }

		internal ICollection<VirtualKey> ModifiersInternal { get; }

		public ShortcutKey(VirtualKey key, params VirtualKey[] modifiers)
		{
			this.Key = key;
			this.Modifiers = modifiers;
			this.ModifiersInternal = modifiers;
		}

		internal ShortcutKey(VirtualKey key, ICollection<VirtualKey> modifiers) : this()
		{
			this.Key = key;
			this.ModifiersInternal = modifiers;
		}

		public bool Equals(ShortcutKey other)
		{
			return this == other;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is ShortcutKey && this.Equals((ShortcutKey)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				// A igualdade compara o conteúdo sem considerar a ordem dos modificadores.
				var hashCode = 0;
				foreach (var modifier in (this.ModifiersInternal ?? this.Modifiers ?? Array.Empty<VirtualKey>()).OrderBy(key => key))
					hashCode = (hashCode * 397) ^ (int)modifier;
				hashCode = (hashCode * 397) ^ (int)this.Key;
				return hashCode;
			}
		}

		public override string ToString()
		{
			return (this.ModifiersInternal ?? this.Modifiers ?? Enumerable.Empty<VirtualKey>())
				.OrderBy(x => x)
				.Select(x => x + " + ")
				.Concat(EnumerableEx.Return(this.Key == VirtualKey.None ? "" : this.Key.ToString()))
				.JoinString("");
		}

		/// <summary>
		/// O atalho gravado pode pedir um modificador de qualquer lado.
		/// Shift, Control, Menu e o valor de Win genérico aceitam a tecla esquerda ou a direita.
		/// </summary>
		public bool MatchesPressed(ShortcutKey pressed)
		{
			if (this.Key != pressed.Key) return false;

			var expected = (this.ModifiersInternal ?? this.Modifiers ?? Array.Empty<VirtualKey>()).ToList();
			var actual = (pressed.ModifiersInternal ?? pressed.Modifiers ?? Array.Empty<VirtualKey>()).ToList();
			foreach (var modifier in expected)
			{
				if (!ConsumeModifier(actual, modifier)) return false;
			}

			return actual.Count == 0;
		}

		private static bool ConsumeModifier(List<VirtualKey> actual, VirtualKey expected)
		{
			VirtualKey left;
			VirtualKey right;
			var anySide = expected == VirtualKey.Shift || expected == VirtualKey.Control || expected == VirtualKey.Menu || expected == AnyWindowsKey;
			if (anySide)
			{
				if (expected == VirtualKey.Shift) { left = VirtualKey.LShiftKey; right = VirtualKey.RShiftKey; }
				else if (expected == VirtualKey.Control) { left = VirtualKey.LControlKey; right = VirtualKey.RControlKey; }
				else if (expected == VirtualKey.Menu) { left = VirtualKey.LMenu; right = VirtualKey.RMenu; }
				else { left = VirtualKey.LWin; right = VirtualKey.RWin; }

				var found = actual.Remove(left) || actual.Remove(right);
				if (!found) return false;
				actual.Remove(left);
				actual.Remove(right);
				return true;
			}

			return actual.Remove(expected);
		}

		public const VirtualKey AnyWindowsKey = (VirtualKey)0x4000005B;

		public static bool operator ==(ShortcutKey key1, ShortcutKey key2)
		{
			return key1.Key == key2.Key
				&& Equals(
					key1.ModifiersInternal ?? key1.Modifiers ?? Array.Empty<VirtualKey>(),
					key2.ModifiersInternal ?? key2.Modifiers ?? Array.Empty<VirtualKey>());
		}

		public static bool operator !=(ShortcutKey key1, ShortcutKey key2)
		{
			return !(key1 == key2);
		}

		private static bool Equals(ICollection<VirtualKey> key1, ICollection<VirtualKey> key2)
		{
			return key1.Count == key2.Count && !key1.Except(key2).Any();
		}


		public static readonly ShortcutKey None = new ShortcutKey(VirtualKey.None);
	}
}
