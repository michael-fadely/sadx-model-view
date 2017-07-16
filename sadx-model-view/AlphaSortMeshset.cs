using sadx_model_view.Ninja;
using SharpDX;

namespace sadx_model_view
{
	internal class AlphaSortMeshset
	{
		public NJS_MODEL Model { get; }
		public NJS_MESHSET Set { get; }
		public Matrix Transform { get; }

		/// <summary>
		/// The bounding box of this queued meshset.
		/// Note that it is a standard public field so
		/// that it can be passed by reference.
		/// </summary>
		public BoundingBox BoundingBox;
		/// <summary>
		/// The bounding sphere of this queued meshset.
		/// Note that it is a standard public field so
		/// that it can be passed by reference.
		/// </summary>
		public BoundingSphere BoundingSphere;

		public readonly float Distance;
		public readonly FlowControl FlowControl;

		public AlphaSortMeshset(Renderer renderer, Camera camera, NJS_MODEL model, NJS_MESHSET set)
		{
			Model       = model;
			Set         = set;
			FlowControl = renderer.FlowControl;
			Transform   = MatrixStack.Peek();

			BoundingSphere = Set.GetWorldSpaceBoundingSphere();
			BoundingBox = Set.GetWorldSpaceBoundingBox();

			Distance = (BoundingSphere.Center - camera.Position).LengthSquared();
		}
	}
}