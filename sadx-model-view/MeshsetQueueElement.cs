using System.Collections.Generic;
using System.Linq;
using sadx_model_view.Ninja;
using SharpDX;

namespace sadx_model_view
{
	class MeshsetQueueElement
	{
		public MeshsetQueueElement Next;

		public NJS_MODEL   Model       { get; }
		public NJS_MESHSET Set         { get; }
		public Matrix      Transform   { get; }
		public bool        Transparent { get; }

		public readonly float       Distance;
		public readonly FlowControl FlowControl;

		public MeshsetQueueElement(Renderer renderer, Camera camera, NJS_MODEL model, NJS_MESHSET set)
		{
			Model       = model;
			Set         = set;
			FlowControl = renderer.FlowControl;
			Transform   = MatrixStack.Peek();

			ushort matId = set.MaterialId;
			var mats = model.mats;

			Transparent = matId < mats.Count && (mats[matId].attrflags & NJD_FLAG.UseAlpha) != 0;

			BoundingSphere sphere = Set.GetWorldSpaceBoundingSphere();
			Distance = (sphere.Center - camera.Position).LengthSquared();
		}

		public IEnumerable<MeshsetQueueElement> Enumerate()
		{
			for (var e = this; !(e is null); e = e.Next)
			{
				yield return e;
			}
		}
	}

	class MeshsetTree
	{
		private MeshsetQueueElement OpaqueRoot, OpaqueTop, AlphaRoot;

		public IEnumerable<MeshsetQueueElement> OpaqueSets
		{
			get
			{
				if (OpaqueRoot is null)
				{
					yield break;
				}

				foreach (var e in OpaqueRoot.Enumerate())
				{
					yield return e;
				}
			}
		}

		public IEnumerable<MeshsetQueueElement> AlphaSets
		{
			get
			{
				if (AlphaRoot is null)
				{
					yield break;
				}

				foreach (var e in AlphaRoot.Enumerate())
				{
					yield return e;
				}
			}
		}

		public void Clear()
		{
			OpaqueRoot = null;
			OpaqueTop = null;
			AlphaRoot = null;
		}

		public void Enqueue(Renderer renderer, Camera camera, NJS_MODEL model, NJS_MESHSET set)
		{
			var bounds = set.GetWorldSpaceBoundingBox();

			if (!camera.Frustum.Intersects(ref bounds))
			{
				return;
			}

			var element = new MeshsetQueueElement(renderer, camera, model, set);

			if (element.Transparent)
			{
				if (AlphaRoot is null)
				{
					AlphaRoot = element;
					return;
				}

				if (element.Distance > AlphaRoot.Distance)
				{
					element.Next = AlphaRoot;
					AlphaRoot = element;
					return;
				}

				MeshsetQueueElement last = AlphaRoot;

				for (MeshsetQueueElement e = AlphaRoot; !(e is null) && e.Distance > element.Distance; e = e.Next)
				{
					last = e;
				}

				var next = last.Next;
				last.Next = element;
				element.Next = next;

				return;
			}

			// TODO: sort in reverse (nearest first), by texture, flags, etc

			if (OpaqueRoot is null)
			{
				OpaqueRoot = element;
				OpaqueTop = element;
				return;
			}

			OpaqueTop.Next = element;
			OpaqueTop = element;
		}
	}
}