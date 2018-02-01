using sadx_model_view.Ninja;
using SharpDX;
using System.Collections.Generic;

// TODO: opaque instancing

namespace sadx_model_view
{
	class MeshsetQueueElement
	{
		public NJS_MODEL      Model          { get; }
		public NJS_MESHSET    Set            { get; }
		public Matrix         Transform      { get; }
		public BoundingSphere BoundingSphere { get; }
		public bool           Transparent    { get; }

		public readonly float       Distance;
		public readonly FlowControl FlowControl;

		public MeshsetQueueElement(Renderer renderer, Camera camera, NJS_MODEL model, NJS_MESHSET set)
		{
			Model       = model;
			Set         = set;
			FlowControl = renderer.FlowControl;
			Transform   = MatrixStack.Peek();

			ushort matId = set.MaterialId;
			var    mats  = model.mats;

			Transparent = matId < mats.Count && (mats[matId].attrflags & NJD_FLAG.UseAlpha) != 0;

			BoundingSphere = Set.GetWorldSpaceBoundingSphere();
			Distance = (BoundingSphere.Center - camera.Position).LengthSquared();
		}
	}

	class MeshsetTree
	{
		private readonly List<MeshsetQueueElement> opaqueSets = new List<MeshsetQueueElement>();
		private readonly List<MeshsetQueueElement> alphaSets = new List<MeshsetQueueElement>();

		public IEnumerable<MeshsetQueueElement> OpaqueSets => opaqueSets;
		public IEnumerable<MeshsetQueueElement> AlphaSets => alphaSets;

		public void Clear()
		{
			opaqueSets.Clear();
			alphaSets.Clear();
		}

		public void SortOpaque()
		{
			opaqueSets.Sort((a, b) =>
			{
				if (a.Set == b.Set)
				{
					return 0;
				}

				if (a.Distance > b.Distance)
				{
					return 1;
				}

				if (a.Distance < b.Distance)
				{
					return -1;
				}

				return 0;
			});
		}

		public void SortAlpha()
		{
			alphaSets.Sort((a, b) =>
			{
				if (a.Distance > b.Distance)
				{
					return -1;
				}

				if (a.Distance < b.Distance)
				{
					return 1;
				}

				return 0;
			});
		}

		public void Enqueue(Renderer renderer, Camera camera, NJS_MODEL model, NJS_MESHSET set)
		{
			BoundingBox bounds = set.GetWorldSpaceBoundingBox();

			if (!camera.Frustum.Intersects(ref bounds))
			{
				return;
			}

			var element = new MeshsetQueueElement(renderer, camera, model, set);

			if (element.Transparent)
			{
				alphaSets.Add(element);
			}
			else
			{
				opaqueSets.Add(element);
			}
		}
	}
}