using sadx_model_view.Interfaces;

namespace sadx_model_view
{
	public class Modifiable<T> : IModifiable where T : struct
	{
		private T _current;

		public bool Modified { get; private set; }

		public T Value
		{
			get => _current;
			set
			{
				Modified = Modified || !_current.Equals(value);
				_current = value;
			}
		}

		public ref T ValueReference => ref _current;

		public Modifiable()
		{
			_current = default;
		}

		public Modifiable(in T initialValue)
		{
			_current = initialValue;
		}

		public void Clear()
		{
			Modified = false;
		}

		public void Mark()
		{
			Modified = true;
		}

		public static explicit operator T(in Modifiable<T> value)
		{
			return value.Value;
		}
	}
}
