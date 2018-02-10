namespace sadx_model_view.Interfaces
{
	public interface IModifiable
	{
		bool Modified { get; }
		void Clear();
	}
}