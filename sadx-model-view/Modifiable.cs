using sadx_model_view.Interfaces;

namespace sadx_model_view
{
	public class Modifiable<T> : IModifiable
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
	}
}