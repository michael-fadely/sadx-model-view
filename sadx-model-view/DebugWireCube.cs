using SharpDX;

namespace sadx_model_view
{
	public struct DebugWireCube
	{
		public static int SizeInBytes => DebugLine.SizeInBytes * 12;

		public readonly DebugLine[] Lines;

		public DebugWireCube(BoundingBox box, Color4 color)
		{
			Vector3[] corners = box.GetCorners();

			Lines = new DebugLine[]
			{
				// first plane
				new(new DebugPoint(corners[0], color), new DebugPoint(corners[1], color)),
				new(new DebugPoint(corners[1], color), new DebugPoint(corners[2], color)),
				new(new DebugPoint(corners[2], color), new DebugPoint(corners[3], color)),
				new(new DebugPoint(corners[3], color), new DebugPoint(corners[0], color)),

				// second plane
				new(new DebugPoint(corners[4], color), new DebugPoint(corners[5], color)),
				new(new DebugPoint(corners[5], color), new DebugPoint(corners[6], color)),
				new(new DebugPoint(corners[6], color), new DebugPoint(corners[7], color)),
				new(new DebugPoint(corners[7], color), new DebugPoint(corners[4], color)),

				// last two (four lines)
				new(new DebugPoint(corners[0], color), new DebugPoint(corners[4], color)),
				new(new DebugPoint(corners[1], color), new DebugPoint(corners[5], color)),
				new(new DebugPoint(corners[2], color), new DebugPoint(corners[6], color)),
				new(new DebugPoint(corners[3], color), new DebugPoint(corners[7], color)),
			};
		}
	}
}
