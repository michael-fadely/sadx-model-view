using SharpDX;

namespace sadx_model_view.Extensions.SharpDX.Mathematics.Collision
{
	public static class RayExtensions
	{
		/// <summary>
		/// Determines whether there is an intersection between a <see cref="Ray"/> and a <see cref="Plane"/>.
		/// </summary>
		/// <param name="ray">The ray to test.</param>
		/// <param name="plane">The plane to test</param>
		/// <param name="hit">Result of the intersection test.</param>
		/// <returns>Whether the two objects intersected.</returns>
		public static bool Intersects(this Ray ray, ref Plane plane, out RayHit hit)
		{
			//Source: Real-Time Collision Detection by Christer Ericson
			//Reference: Page 175

			hit = new RayHit();

			if (!global::SharpDX.Collision.RayIntersectsPlane(ref ray, ref plane, out hit.Distance))
			{
				hit.Point = Vector3.Zero;
				return false;
			}

			hit.Point = ray.Position + (ray.Direction * hit.Distance);
			return true;
		}

		public static bool RayIntersectsTriangle(in Ray ray,
		                                         in Vector3 vertex1,
		                                         in Vector3 vertex2,
		                                         in Vector3 vertex3,
		                                         out float distance,
		                                         bool doubleSided = true)
		{
			// Source: Fast Minimum Storage Ray / Triangle Intersection
			// Reference: https://cadxfem.org/inf/Fast%20MinimumStorage%20RayTriangle%20Intersection.pdf

			// NOTE: all of these cross products are hard-coded for right-handed winding order.
			// To be more useful, the parameters of all the cross products should be swapped
			// and a specifiable winding order should be implemented.

			// Compute vectors along two edges of the triangle.
			Vector3 edge1 = vertex2 - vertex1;
			Vector3 edge2 = vertex3 - vertex1;

			// Cross product of ray direction and edge2 - first part of determinant.
			Vector3 directionCrossEdge2 = Vector3.Cross(edge2, ray.Direction);

			// Compute the determinant.
			// Dot product of edge1 and the first part of determinant.
			float determinant = Vector3.Dot(edge1, directionCrossEdge2);

			if (!doubleSided)
			{
				if (determinant < float.Epsilon)
				{
					distance = 0f;
					return false;
				}

				Vector3 distanceFromVertex = ray.Position - vertex1;

				float triangleU = Vector3.Dot(distanceFromVertex, directionCrossEdge2);

				if (triangleU < 0f || triangleU > determinant)
				{
					distance = 0f;
					return false;
				}

				Vector3 distanceCrossEdge1 = Vector3.Cross(edge1, distanceFromVertex);

				float triangleV = Vector3.Dot(ray.Direction, distanceCrossEdge1);

				if (triangleV < 0f || (triangleU + triangleV) > determinant)
				{
					distance = 0f;
					return false;
				}

				float inverseDeterminant = 1.0f / determinant;
				float rayDistance = Vector3.Dot(edge2, distanceCrossEdge1) * inverseDeterminant;

				if (rayDistance < 0f)
				{
					distance = 0f;
					return false;
				}

				distance = rayDistance;
			}
			else
			{
				// If the ray is parallel to the triangle plane, there is no collision.
				// This also means that we are not culling, the ray may hit both the
				// back and the front of the triangle.
				if (MathUtil.IsZero(determinant))
				{
					distance = 0f;
					return false;
				}

				float inverseDeterminant = 1.0f / determinant;

				// Calculate the U parameter of the intersection point.
				Vector3 distanceFromVertex = ray.Position - vertex1;

				float triangleU = Vector3.Dot(distanceFromVertex, directionCrossEdge2) * inverseDeterminant;

				// Make sure it is inside the triangle.
				if (triangleU < 0f || triangleU > 1f)
				{
					distance = 0f;
					return false;
				}

				// Calculate the V parameter of the intersection point.
				Vector3 distanceCrossEdge1 = Vector3.Cross(edge1, distanceFromVertex);

				float triangleV = Vector3.Dot(ray.Direction, distanceCrossEdge1) * inverseDeterminant;

				// Make sure it is inside the triangle.
				if (triangleV < 0f || triangleU + triangleV > 1f)
				{
					distance = 0f;
					return false;
				}

				// Compute the distance along the ray to the triangle.
				float rayDistance = Vector3.Dot(edge2, distanceCrossEdge1) * inverseDeterminant;

				// Is the triangle behind the ray origin?
				if (rayDistance < 0f)
				{
					distance = 0f;
					return false;
				}

				distance = rayDistance;
			}

			return true;
		}

		/// <summary>
		/// Determines whether there is an intersection between a <see cref="Ray"/> and a triangle.
		/// </summary>
		/// <param name="ray">The ray to test.</param>
		/// <param name="vertex1">The first vertex of the triangle to test.</param>
		/// <param name="vertex2">The second vertex of the triangle to test.</param>
		/// <param name="vertex3">The third vertex of the triangle to test.</param>
		/// <param name="hit">Result of the intersection test.</param>
		/// <returns>Whether the two objects intersected.</returns>
		public static bool IntersectsTriangle(this Ray ray, Vector3 vertex1, Vector3 vertex2, Vector3 vertex3, out RayHit hit, bool doubleSided = true)
		{
			hit = new RayHit();

			if (!RayIntersectsTriangle(in ray, in vertex1, in vertex2, in vertex3, out hit.Distance, doubleSided))
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
		/// <param name="hit">Result of the intersection test.</param>
		/// <returns>Whether the two objects intersected.</returns>
		public static bool Intersects(this Ray ray, ref BoundingBox box, out RayHit hit)
		{
			hit = new RayHit();

			if (!global::SharpDX.Collision.RayIntersectsBox(ref ray, ref box, out hit.Distance))
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
		/// <param name="hit">Result of the intersection test.</param>
		/// <returns>Whether the two objects intersected.</returns>
		public static bool Intersects(this Ray ray, ref BoundingSphere sphere, out RayHit hit)
		{
			hit = new RayHit();

			if (!global::SharpDX.Collision.RayIntersectsSphere(ref ray, ref sphere, out hit.Distance))
			{
				hit.Point = Vector3.Zero;
				return false;
			}

			hit.Point = ray.Position + (ray.Direction * hit.Distance);
			return true;
		}
	}
}