using SharpDX;

namespace sadx_model_view.Ninja
{
	// TODO: normal
	public struct Triangle
	{
		public Vector3 A;
		public Vector3 B;
		public Vector3 C;

		public Vector3[] ToArray()
		{
			return new Vector3[]
			{
				A, B, C
			};
		}

		public Vector3 Position => (A + B + C) / 3f;
	}
}