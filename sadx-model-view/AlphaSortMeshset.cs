using sadx_model_view.Ninja;
using SharpDX;

namespace sadx_model_view
{
	internal class AlphaSortMeshset
	{
		public NJS_MODEL Model { get; }
		public NJS_MESHSET Set { get; }
		public Matrix Transform { get; }
		public readonly float Depth;
		public readonly FlowControl FlowControl;

		public AlphaSortMeshset(Renderer renderer, Camera camera, NJS_MODEL model, NJS_MESHSET set)
		{
			Model       = model;
			Set         = set;
			FlowControl = renderer.FlowControl;
			Transform   = MatrixStack.Peek();

			var center = (Vector3)Vector3.Transform(Set.Center, Transform);
			Depth = (center - camera.Position).Length() - set.Radius;
		}
	}
}