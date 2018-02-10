using SharpDX;

namespace sadx_model_view
{
	public struct DebugPoint
	{
		public static int SizeInBytes => Vector3.SizeInBytes + Vector4.SizeInBytes;

		public Vector3 Point;
		public Color4 Color;

		public DebugPoint(Vector3 point, Color4 color)
		{
			Point = point;
			Color = color;
		}
	}
}
