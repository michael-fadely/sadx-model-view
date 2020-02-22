using SharpDX;

namespace sadx_model_view.Extensions.SharpDX.Mathematics.Collision
{
	public struct RayHit
	{
		public Vector3 Point;
		public float   Distance;

		public RayHit(in Vector3 point, float distance)
		{
			Point    = point;
			Distance = distance;
		}
	}
}