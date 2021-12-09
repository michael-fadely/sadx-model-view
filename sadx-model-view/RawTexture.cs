namespace sadx_model_view
{
	public class RawTexture
	{
		public readonly int    Width;
		public readonly int    Height;
		public readonly byte[] Data;

		public RawTexture(int width, int height, byte[] data)
		{
			Width = width;
			Height = height;
			Data = data;
		}
	}
}
