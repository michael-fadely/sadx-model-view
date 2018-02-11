namespace sadx_model_view
{
	public class Modifiable<T>
	{
		public bool Modified { get; private set; }

		public Modifiable()
		{
			current = default;
		}

		public Modifiable(T initialValue)
		{
			current = initialValue;
		}

		T current;

		public T Value
		{
			get => current;
			set
			{
				Modified = Modified || !current.Equals(value);
				current  = value;
			}
		}

		public void Clear()
		{
			Modified = false;
		}

		public static implicit operator Modifiable<T>(T value)
		{
			return new Modifiable<T>(value);
		}

		public static explicit operator T(Modifiable<T> value)
		{
			return value.Value;
		}
	}
}