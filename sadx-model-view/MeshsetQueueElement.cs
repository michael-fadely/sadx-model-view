using sadx_model_view.Ninja;
using SharpDX;

namespace sadx_model_view
{
	internal class MeshsetQueueElement
	{
		public NJS_MODEL Model { get; }
		public NJS_MESHSET Set { get; }
		public Matrix Transform { get; }

		public readonly float Distance;
		public readonly FlowControl FlowControl;

		public MeshsetQueueElement(Renderer renderer, Camera camera, NJS_MODEL model, NJS_MESHSET set)
		{
			Model       = model;
			Set         = set;
			FlowControl = renderer.FlowControl;
			Transform   = MatrixStack.Peek();

			BoundingSphere sphere = Set.GetWorldSpaceBoundingSphere();
			Distance = (sphere.Center - camera.Position).LengthSquared();
		}
	}
}