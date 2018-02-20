using sadx_model_view.Ninja;
using SharpDX;
using System.Collections.Generic;

// TODO: opaque instancing

namespace sadx_model_view
{
	public class MeshsetQueueElementBase
	{
		public NJS_OBJECT     Object         { get; }
		public NJS_MODEL      Model          { get; }
		public NJS_MESHSET    Set            { get; }
		public Matrix         Transform      { get; }
		public BoundingBox    BoundingBox    { get; }
		public BoundingSphere BoundingSphere { get; }
		public bool           Transparent    { get; }

		public readonly FlowControl FlowControl;

		public MeshsetQueueElementBase(Renderer renderer, NJS_OBJECT @object, NJS_MODEL model, NJS_MESHSET set)
		{
			Object      = @object;
			Model       = model;
			Set         = set;
			FlowControl = renderer.FlowControl;
			Transform   = MatrixStack.Peek();

			ushort matId = set.MaterialId;
			List<NJS_MATERIAL> mats = model.mats;

			Transparent = matId < mats.Count && (mats[matId].attrflags & NJD_FLAG.UseAlpha) != 0;

			BoundingBox = Set.GetWorldSpaceBoundingBox();
			BoundingSphere = Set.GetWorldSpaceBoundingSphere();
		}

		public MeshsetQueueElementBase(MeshsetQueueElementBase b)
		{
			Object         = b.Object;
			Model          = b.Model;
			Set            = b.Set;
			Transform      = b.Transform;
			BoundingBox    = b.BoundingBox;
			BoundingSphere = b.BoundingSphere;
			Transparent    = b.Transparent;
			FlowControl    = b.FlowControl;
		}
	}

	public class MeshsetQueueElement : MeshsetQueueElementBase
	{
		public readonly float Distance;

		public MeshsetQueueElement(Renderer renderer, Camera camera, NJS_OBJECT @object, NJS_MODEL model, NJS_MESHSET set) : base(renderer, @object, model, set)
		{
			Distance = (BoundingSphere.Center - camera.Position).LengthSquared();
		}

		public MeshsetQueueElement(MeshsetQueueElementBase b, Camera camera) : base(b)
		{
			Distance = (BoundingSphere.Center - camera.Position).LengthSquared();
		}
	}

	public class MeshsetQueue
	{
		readonly List<MeshsetQueueElement> opaqueSets = new List<MeshsetQueueElement>();
		readonly List<MeshsetQueueElement> alphaSets  = new List<MeshsetQueueElement>();

		public IReadOnlyList<MeshsetQueueElement> OpaqueSets => opaqueSets;
		public IReadOnlyList<MeshsetQueueElement> AlphaSets  => alphaSets;

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

		public void Enqueue(Renderer renderer, Camera camera, NJS_OBJECT @object, NJS_MODEL model, NJS_MESHSET set)
		{
			Enqueue(new MeshsetQueueElement(renderer, camera, @object, model, set));
		}

		public void Enqueue(MeshsetQueueElement element)
		{
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