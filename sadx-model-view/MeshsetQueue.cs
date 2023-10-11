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

		public readonly MaterialFlagOverrideManager MaterialFlagOverride;

		public MeshsetQueueElementBase(Renderer renderer, NJS_OBJECT @object, NJS_MODEL model, NJS_MESHSET set)
		{
			Object      = @object;
			Model       = model;
			Set         = set;
			MaterialFlagOverride = renderer.MaterialFlagOverride;
			Transform   = MatrixStack.Peek();

			ushort matId = set.MaterialId;
			List<NJS_MATERIAL> mats = model.mats;

			Transparent = matId < mats.Count && (mats[matId].attrflags & NJD_FLAG.UseAlpha) != 0;

			BoundingBox = Set.GetWorldSpaceBoundingBox();
			BoundingSphere = BoundingSphere.FromBox(BoundingBox);
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
			MaterialFlagOverride    = b.MaterialFlagOverride;
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
		private readonly List<MeshsetQueueElement> _opaqueSets = new();
		private readonly List<MeshsetQueueElement> _alphaSets  = new();

		public IReadOnlyList<MeshsetQueueElement> OpaqueSets => _opaqueSets;
		public IReadOnlyList<MeshsetQueueElement> AlphaSets  => _alphaSets;

		public void Clear()
		{
			_opaqueSets.Clear();
			_alphaSets.Clear();
		}

		/// <summary>
		/// Sort opaque geometry by nearest to furthest.
		/// </summary>
		public void SortOpaque()
		{
			_opaqueSets.Sort((a, b) =>
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

		/// <summary>
		/// Sort transparent geometry by furthest to nearest.
		/// </summary>
		public void SortAlpha()
		{
			_alphaSets.Sort((a, b) =>
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
				_alphaSets.Add(element);
			}
			else
			{
				_opaqueSets.Add(element);
			}
		}
	}
}
