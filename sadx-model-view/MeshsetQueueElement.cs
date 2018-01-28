using System;
using System.Collections.Generic;
using sadx_model_view.Ninja;
using SharpDX;

namespace sadx_model_view
{
	class MeshsetQueueElement
	{
		public MeshsetQueueElement Previous, Next;

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

		public void InsertNext(MeshsetQueueElement e)
		{
			if (!(e.Next is null && e.Previous is null))
			{
				throw new Exception("Element must be an orphan.");
			}

			var next = Next;

			// my next element is now e
			Next = e;

			// e's previous element (presumed null) is now me
			e.Previous = this;

			// e's next element is now my next element
			e.Next = next;

			// if my next element isn't null, its previous element is now e
			if (next != null)
			{
				next.Previous = e;
			}
		}
	}

	class MeshsetTree
	{
		private MeshsetQueueElement opaqueRoot, opaqueTop, alphaRoot;

		public IEnumerable<MeshsetQueueElement> OpaqueSets
		{
			get
			{
				if (opaqueRoot is null)
				{
					yield break;
				}

				foreach (var e in opaqueRoot.Enumerate())
				{
					yield return e;
				}
			}
		}

		public IEnumerable<MeshsetQueueElement> AlphaSets
		{
			get
			{
				if (alphaRoot is null)
				{
					yield break;
				}

				foreach (var e in alphaRoot.Enumerate())
				{
					yield return e;
				}
			}
		}

		public void Clear()
		{
			opaqueRoot = null;
			opaqueTop = null;
			alphaRoot = null;
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
				if (alphaRoot is null)
				{
					alphaRoot = element;
					return;
				}

				if (element.Distance > alphaRoot.Distance)
				{
					element.Next = alphaRoot;
					alphaRoot.Previous = element;
					alphaRoot = element;
					return;
				}

				MeshsetQueueElement target = alphaRoot;

				for (MeshsetQueueElement e = target.Next; !(e is null) && e.Distance > element.Distance; e = e.Next)
				{
					target = e;
				}

				target?.InsertNext(element);
				return;
			}

			// TODO: sort in reverse (nearest first), by texture, flags, etc

			if (opaqueRoot is null)
			{
				opaqueRoot = element;
				opaqueTop = element;
				return;
			}

			opaqueTop.InsertNext(element);
			opaqueTop = element;
		}
	}
}