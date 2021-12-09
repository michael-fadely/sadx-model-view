namespace sadx_model_view
{
	public class Modifiable<T> where T : struct
	{
		public bool Modified { get; private set; }

		public Modifiable()
		{
			_current = default;
		}

		public Modifiable(T initialValue)
		{
			_current = initialValue;
		}

		private T _current;

		public T Value
		{
			get => _current;
			set
			{
				Modified = Modified || !_current.Equals(value);
				_current = value;
			}
		}

		public void Clear()
		{
			Modified = false;
		}

		public static explicit operator T(Modifiable<T> value)
		{
			return value.Value;
		}
	}
}
