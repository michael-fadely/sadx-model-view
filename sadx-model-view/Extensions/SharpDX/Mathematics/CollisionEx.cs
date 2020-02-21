using SharpDX;

namespace sadx_model_view.Extensions.SharpDX.Mathematics
{
	public static class CollisionEx
	{
		public struct RayHit
		{
			public Vector3 Point;
			public float Distance;

			public RayHit(in Vector3 point, float distance)
			{
				Point = point;
				Distance = distance;
			}
		}

		/// <summary>
		/// Determines whether there is an intersection between a <see cref="Ray"/> and a <see cref="Plane"/>.
		/// </summary>
		/// <param name="ray">The ray to test.</param>
		/// <param name="plane">The plane to test</param>
		/// <returns>Whether the two objects intersected.</returns>
		public static bool Intersects(this Ray ray, ref Plane plane, out RayHit hit)
		{
			//Source: Real-Time Collision Detection by Christer Ericson
			//Reference: Page 175

			hit = new RayHit();

			if (!Collision.RayIntersectsPlane(ref ray, ref plane, out hit.Distance))
			{
				hit.Point = Vector3.Zero;
				return false;
			}

			hit.Point = ray.Position + (ray.Direction * hit.Distance);
			return true;
		}

		/// <summary>
		/// Determines whether there is an intersection between a <see cref="Ray"/> and a triangle.
		/// </summary>
		/// <param name="ray">The ray to test.</param>
		/// <param name="vertex1">The first vertex of the triangle to test.</param>
		/// <param name="vertex2">The second vertex of the triangle to test.</param>
		/// <param name="vertex3">The third vertex of the triangle to test.</param>
		/// <returns>Whether the two objects intersected.</returns>
		public static bool IntersectsTriangle(this Ray ray, Vector3 vertex1, Vector3 vertex2, Vector3 vertex3, out RayHit hit)
		{
			hit = new RayHit();

			if (!Collision.RayIntersectsTriangle(ref ray, ref vertex1, ref vertex2, ref vertex3, out hit.Distance))
			{
				hit.Point = Vector3.Zero;
				return false;
			}

			hit.Point = ray.Position + (ray.Direction * hit.Distance);
			return true;
		}

		/// <summary>
		/// Determines whether there is an intersection between a <see cref="Ray"/> and a <see cref="Plane"/>.
		/// </summary>
		/// <param name="ray">The ray to test.</param>
		/// <param name="box">The box to test.</param>
		/// <returns>Whether the two objects intersected.</returns>
		public static bool Intersects(this Ray ray, ref BoundingBox box, out RayHit hit)
		{
			hit = new RayHit();

			if (!Collision.RayIntersectsBox(ref ray, ref box, out hit.Distance))
			{
				hit.Point = Vector3.Zero;
				return false;
			}

			hit.Point = ray.Position + (ray.Direction * hit.Distance);
			return true;
		}

		/// <summary>
		/// Determines whether there is an intersection between a <see cref="Ray"/> and a <see cref="BoundingSphere"/>. 
		/// </summary>
		/// <param name="ray">The ray to test.</param>
		/// <param name="sphere">The sphere to test.</param>
		/// <returns>Whether the two objects intersected.</returns>
		public static bool Intersects(this Ray ray, ref BoundingSphere sphere, out RayHit hit)
		{
			hit = new RayHit();

			if (!Collision.RayIntersectsSphere(ref ray, ref sphere, out hit.Distance))
			{
				hit.Point = Vector3.Zero;
				return false;
			}

			hit.Point = ray.Position + (ray.Direction * hit.Distance);
			return true;
		}
	}
}
