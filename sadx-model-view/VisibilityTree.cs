﻿using System.Collections.Generic;
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
			BoundingBox bounds = default;

			foreach (Col col in landTable.ColList)
			{
				CalculateBounds(col.Object, ref bounds);
			}

			Debug.Assert(MatrixStack.Empty);
			Create(in bounds);
		}

		public VisibilityTree(NJS_OBJECT @object)
		{
			BoundingBox bounds = default;
			CalculateBounds(@object, ref bounds);
			Create(in bounds);
		}

		public void Add(LandTable landTable, Renderer renderer)
		{
			foreach (Col col in landTable.ColList)
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
				if (o.Model == null)
				{
					continue;
				}

				foreach (NJS_MESHSET set in o.Model.meshsets)
				{
					var element = new MeshsetQueueElementBase(renderer, o, o.Model, set);
					tree.Add(element, element.BoundingBox);
				}
			}
		}

		public List<MeshsetQueueElementBase> GetVisible(Camera camera)
		{
			BoundingFrustum frustum = camera.Frustum;
			return GetVisible(in frustum);
		}

		public List<MeshsetQueueElementBase> GetVisible(in BoundingFrustum frustum)
		{
			var result = new List<MeshsetQueueElementBase>();
			tree.GetColliding(result, in frustum);
			return result;
		}

		static void CalculateBounds(NJS_OBJECT @object, ref BoundingBox bounds)
		{
			foreach (NJS_OBJECT o in @object)
			{
				if (o.Model == null)
				{
					continue;
				}

				foreach (NJS_MESHSET set in o.Model.meshsets)
				{
					bounds = BoundingBox.Merge(bounds, set.GetWorldSpaceBoundingBox());
				}
			}
		}

		void Create(in BoundingBox bounds)
		{
			tree = new BoundsOctree<MeshsetQueueElementBase>(bounds, 0.1f, 1.0f);
		}

		public IEnumerable<BoundingBox> GiveMeTheBounds() => tree.GiveMeTheBounds();
	}
}
