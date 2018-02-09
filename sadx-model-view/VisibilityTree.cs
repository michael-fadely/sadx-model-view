using System.Collections.Generic;
using System.Diagnostics;
using sadx_model_view.Ninja;
using sadx_model_view.SA1;
using SharpDX;

namespace sadx_model_view
{
	class VisibilityTree
	{
		BoundsOctree<MeshsetQueueElementBase> tree;

		public bool Empty => tree.Count == 0;

		public VisibilityTree(LandTable landTable)
		{
			BoundingSphere bounds = default;

			foreach (var col in landTable.ColList)
			{
				CalculateBounds(col.Object, ref bounds);
			}

			Debug.Assert(MatrixStack.Empty);
			Create(in bounds);
		}

		public VisibilityTree(NJS_OBJECT @object)
		{
			BoundingSphere bounds = default;
			CalculateBounds(@object, ref bounds);
			Create(in bounds);
		}

		public void Add(LandTable landTable, Renderer renderer)
		{
			foreach (var col in landTable.ColList)
			{
				// HACK:
				if ((col.Flags & ColFlags.Visible) != 0)
				{
					Add(col.Object, renderer);
				}
			}
		}

		public void Add(NJS_OBJECT @object, Renderer renderer)
		{
			foreach (NJS_OBJECT o in @object)
			{
				if (o.Model != null)
				{
					foreach (var set in o.Model.meshsets)
					{
						tree.Add(new MeshsetQueueElementBase(renderer, o, o.Model, set), set.GetWorldSpaceBoundingBox());
					}
				}
			}
		}

		public List<MeshsetQueueElementBase> GetVisible(Camera camera)
		{
			var frustum = camera.Frustum;
			return GetVisible(in frustum);
		}

		public List<MeshsetQueueElementBase> GetVisible(in BoundingFrustum frustum)
		{
			List<MeshsetQueueElementBase> result = new List<MeshsetQueueElementBase>();
			tree.GetColliding(result, frustum);
			return result;
		}

		static void CalculateBounds(NJS_OBJECT @object, ref BoundingSphere bounds)
		{
			foreach (NJS_OBJECT o in @object)
			{
				if (o.Model == null)
				{
					continue;
				}

				foreach (var set in o.Model.meshsets)
				{
					bounds = BoundingSphere.Merge(bounds, set.GetWorldSpaceBoundingSphere());
				}
			}
		}

		void Create(in BoundingSphere bounds)
		{
			tree = new BoundsOctree<MeshsetQueueElementBase>(bounds, 1.0f, 1.0f);
		}
	}
}
