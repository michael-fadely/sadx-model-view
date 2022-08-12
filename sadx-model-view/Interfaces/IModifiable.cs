namespace sadx_model_view.Interfaces;

public interface IModifiable : IReadOnlyModifiable
{
	void Clear();
	void Mark();
}
