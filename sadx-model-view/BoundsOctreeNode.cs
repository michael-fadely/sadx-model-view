using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using SharpDX;

// A node in a BoundsOctree
// Copyright 2014 Nition, BSD licence (see LICENCE file). http://nition.co
namespace sadx_model_view
{
	public class BoundsOctreeNode<T>
	{
		/// <summary>
		/// If there are already numObjectsAllowed in a node, we split it into children.
		/// A generally good number seems to be something around 8-15.
		/// </summary>
		const int numObjectsAllowed = 8;

		/// <summary>
		/// Centre of this node
		/// </summary>
		public Vector3 Center { get; private set; }

		/// <summary>
		/// Length of this node if it has a looseness of 1.0.
		/// </summary>
		public float BaseLength { get; private set; }

		/// <summary>
		/// Looseness value for this node
		/// </summary>
		float looseness;

		/// <summary>
		// Minimum size for a node in this octree
		/// </summary>
		float minimumSize;

		/// <summary>
		/// Actual length of sides, taking the looseness value into account
		/// </summary>
		float looseLength;

		/// <summary>
		/// Bounding box that represents this node
		/// </summary>
		BoundingBox bounds;

		/// <summary>
		// Objects in this node
		/// </summary>
		readonly List<OctreeObject> objects = new List<OctreeObject>();

		/// <summary>
		/// Child nodes, if any
		/// </summary>
		BoundsOctreeNode<T>[] children;

		/// <summary>
		/// Bounds of potential children in this node. These are actual size (with looseness taken into account), not base size.
		/// </summary>
		BoundingBox[] childBounds;

		/// <summary>
		/// An object in the octree
		/// </summary>
		class OctreeObject
		{
			public T Object;
			public BoundingBox Bounds;
		}

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="baseLength">Length of this node, not taking looseness into account.</param>
		/// <param name="minSizeVal">Minimum size of nodes in this octree.</param>
		/// <param name="loosenessVal">Multiplier for <paramref name="baseLength"/> to get the actual size.</param>
		/// <param name="centerVal">Centre position of this node.</param>
		public BoundsOctreeNode(float baseLength, float minSizeVal, float loosenessVal, Vector3 centerVal)
		{
			SetValues(baseLength, minSizeVal, loosenessVal, centerVal);
		}

		// #### PUBLIC METHODS ####

		/// <summary>
		/// Add an object.
		/// </summary>
		/// <param name="obj">Object to add.</param>
		/// <param name="objBounds">3D bounding box around the object.</param>
		/// <returns><value>true</value> if the object fits entirely within this node.</returns>
		public bool Add(T obj, BoundingBox objBounds)
		{
			if (!Encapsulates(bounds, objBounds))
			{
				return false;
			}

			SubAdd(obj, objBounds);
			return true;
		}

		/// <summary>
		/// Remove an object. Makes the assumption that the object only exists once in the tree.
		/// </summary>
		/// <param name="obj">Object to remove.</param>
		/// <returns><value>true</value> if the object was removed successfully.</returns>
		public bool Remove(T obj)
		{
			var removed = false;

			for (int i = 0; i < objects.Count; i++)
			{
				if (objects[i].Object.Equals(obj))
				{
					removed = objects.Remove(objects[i]);
					break;
				}
			}

			if (!removed && children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					removed = children[i].Remove(obj);

					if (removed)
					{
						break;
					}
				}
			}

			if (removed && children != null)
			{
				// Check if we should merge nodes now that we've removed an item
				if (ShouldMerge())
				{
					Merge();
				}
			}

			return removed;
		}

		/// <summary>
		/// Removes the specified object at the given position. Makes the assumption that the object only exists once in the tree.
		/// </summary>
		/// <param name="obj">Object to remove.</param>
		/// <param name="objBounds">3D bounding box around the object.</param>
		/// <returns><value>true</value> if the object was removed successfully.</returns>
		public bool Remove(T obj, BoundingBox objBounds)
		{
			if (!Encapsulates(bounds, objBounds))
			{
				return false;
			}

			return SubRemove(obj, objBounds);
		}

		/// <summary>
		/// Check if the specified bounds intersect with anything in the tree. See also: GetColliding.
		/// </summary>
		/// <param name="checkBounds">BoundingBox to check.</param>
		/// <returns><value>true</value> if there was a collision.</returns>
		public bool IsColliding(in BoundingBox checkBounds)
		{
			// Are the input bounds at least partially in this node?
			if (!bounds.Intersects(checkBounds))
			{
				return false;
			}

			// Check against any objects in this node
			for (var i = 0; i < objects.Count; i++)
			{
				OctreeObject o = objects[i];
				if (o.Bounds.Intersects(checkBounds))
				{
					return true;
				}
			}

			// Check children
			if (children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					if (children[i].IsColliding(in checkBounds))
					{
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Check if the specified ray intersects with anything in the tree. See also: GetColliding.
		/// </summary>
		/// <param name="checkRay">Ray to check.</param>
		/// <param name="maxDistance">Distance to check.</param>
		/// <returns><value>true</value> if there was a collision.</returns>
		public bool IsColliding(in Ray checkRay, float maxDistance = float.PositiveInfinity)
		{
			// Is the input ray at least partially in this node?
			if (!checkRay.Intersects(ref bounds, out float distance) || distance > maxDistance)
			{
				return false;
			}

			// Check against any objects in this node
			for (var i = 0; i < objects.Count; i++)
			{
				OctreeObject o = objects[i];
				if (checkRay.Intersects(ref o.Bounds, out distance) && distance <= maxDistance)
				{
					return true;
				}
			}

			// Check children
			if (children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					if (children[i].IsColliding(in checkRay, maxDistance))
					{
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Returns an array of objects that intersect with the specified bounds, if any. Otherwise returns an empty array. See also: IsColliding.
		/// </summary>
		/// <param name="checkBounds">BoundingBox to check. Passing by ref as it improves performance with structs.</param>
		/// <param name="result">List result.</param>
		/// <returns>Objects that intersect with the specified bounds.</returns>
		public void GetColliding(in BoundingBox checkBounds, List<T> result)
		{
			ContainmentType containment = bounds.Contains(checkBounds);

			switch (containment)
			{
				case ContainmentType.Disjoint:
					return;

				case ContainmentType.Contains:
					for (var i = 0; i < objects.Count; i++)
					{
						OctreeObject o = objects[i];
						result.Add(o.Object);
					}

					break;

				case ContainmentType.Intersects:
					for (var i = 0; i < objects.Count; i++)
					{
						OctreeObject o = objects[i];
						if (o.Bounds.Intersects(checkBounds))
						{
							result.Add(o.Object);
						}
					}

					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			// Check children
			if (children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					children[i].GetColliding(in checkBounds, result);
				}
			}
		}

		/// <summary>
		/// Returns an array of objects that intersect with the specified ray, if any. Otherwise returns an empty array. See also: IsColliding.
		/// </summary>
		/// <param name="checkRay">Ray to check. Passing by ref as it improves performance with structs.</param>
		/// <param name="maxDistance">Distance to check.</param>
		/// <param name="result">List result.</param>
		/// <returns>Objects that intersect with the specified ray.</returns>
		public void GetColliding(in Ray checkRay, List<T> result, float maxDistance = float.PositiveInfinity)
		{
			// Is the input ray at least partially in this node?
			if (!checkRay.Intersects(ref bounds, out float distance) || distance > maxDistance)
			{
				return;
			}

			// Check against any objects in this node
			for (var i = 0; i < objects.Count; i++)
			{
				OctreeObject o = objects[i];
				if (checkRay.Intersects(ref o.Bounds, out distance) && distance <= maxDistance)
				{
					result.Add(o.Object);
				}
			}

			// Check children
			if (children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					children[i].GetColliding(in checkRay, result, maxDistance);
				}
			}
		}

		/// <summary>
		/// Returns an array of objects that intersect with the specified bounds, if any. Otherwise returns an empty array. See also: IsColliding.
		/// </summary>
		/// <param name="frustum">Frustum to check. Passing by ref as it improves performance with structs.</param>
		/// <param name="result">List result.</param>
		/// <returns>Objects that intersect with the specified bounds.</returns>
		public void GetColliding(in BoundingFrustum frustum, List<T> result)
		{
			ContainmentType containment = frustum.Contains(ref bounds);

			switch (containment)
			{
				case ContainmentType.Disjoint:
					return;

				case ContainmentType.Contains:
					for (var i = 0; i < objects.Count; i++)
					{
						OctreeObject o = objects[i];
						result.Add(o.Object);
					}

					break;

				case ContainmentType.Intersects:
					// Check against any objects in this node
					for (var i = 0; i < objects.Count; i++)
					{
						OctreeObject o = objects[i];
						if (frustum.Intersects(ref o.Bounds))
						{
							result.Add(o.Object);
						}
					}

					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			// Check children
			if (children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					children[i].GetColliding(in frustum, result);
				}
			}
		}

		/// <summary>
		/// Set the 8 children of this octree.
		/// </summary>
		/// <param name="childOctrees">The 8 new child nodes.</param>
		public void SetChildren(BoundsOctreeNode<T>[] childOctrees)
		{
			if (childOctrees.Length != 8)
			{
				Debug.WriteLine("Child octree array must be length 8. Was length: " + childOctrees.Length);
				return;
			}

			children = childOctrees;
		}

		public BoundingBox GetBounds()
		{
			return bounds;
		}

		/*
		/// <summary>
		/// Draws node boundaries visually for debugging.
		/// Must be called from OnDrawGizmos externally. See also: DrawAllObjects.
		/// </summary>
		/// <param name="depth">Used for recurcive calls to this method.</param>
		public void DrawAllBounds(float depth = 0)
		{
			float tintVal = depth / 7; // Will eventually get values > 1. Color rounds to 1 automatically
			Gizmos.color = new Color(tintVal, 0, 1.0f - tintVal);

			BoundingBox thisBounds = new BoundingBox(Center, new Vector3(adjLength, adjLength, adjLength));
			Gizmos.DrawWireCube(thisBounds.center, thisBounds.size);

			if (children != null)
			{
				depth++;
				for (int i = 0; i < 8; i++)
				{
					children[i].DrawAllBounds(depth);
				}
			}
			Gizmos.color = Color.white;
		}
		*/

		/*
		/// <summary>
		/// Draws the bounds of all objects in the tree visually for debugging.
		/// Must be called from OnDrawGizmos externally. See also: DrawAllBounds.
		/// </summary>
		public void DrawAllObjects()
		{
			float tintVal = BaseLength / 20;
			Gizmos.color = new Color(0, 1.0f - tintVal, tintVal, 0.25f);

			foreach (OctreeObject obj in objects)
			{
				Gizmos.DrawCube(obj.BoundingBox.center, obj.BoundingBox.size);
			}

			if (children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					children[i].DrawAllObjects();
				}
			}

			Gizmos.color = Color.white;
		}
		*/

		/// <summary>
		/// We can shrink the octree if:
		/// - This node is >= double minLength in length
		/// - All objects in the root node are within one octant
		/// - This node doesn't have children, or does but 7/8 children are empty
		/// We can also shrink it if there are no objects left at all!
		/// </summary>
		/// <param name="minLength">Minimum dimensions of a node in this octree.</param>
		/// <returns>The new root, or the existing one if we didn't shrink.</returns>
		public BoundsOctreeNode<T> ShrinkIfPossible(float minLength)
		{
			if (BaseLength < 2 * minLength)
			{
				return this;
			}

			if (objects.Count == 0 && (children == null || children.Length == 0))
			{
				return this;
			}

			// Check objects in root
			int bestFit = -1;
			for (int i = 0; i < objects.Count; i++)
			{
				OctreeObject curObj = objects[i];
				int newBestFit = BestFitChild(curObj.Bounds);
				if (i == 0 || newBestFit == bestFit)
				{
					// In same octant as the other(s). Does it fit completely inside that octant?
					if (Encapsulates(childBounds[newBestFit], curObj.Bounds))
					{
						if (bestFit < 0)
						{
							bestFit = newBestFit;
						}
					}
					else
					{
						// Nope, so we can't reduce. Otherwise we continue
						return this;
					}
				}
				else
				{
					return this; // Can't reduce - objects fit in different octants
				}
			}

			// Check objects in children if there are any
			if (children != null)
			{
				bool childHadContent = false;
				for (int i = 0; i < children.Length; i++)
				{
					if (children[i].HasAnyObjects())
					{
						if (childHadContent)
						{
							return this; // Can't shrink - another child had content already
						}

						if (bestFit >= 0 && bestFit != i)
						{
							return this; // Can't reduce - objects in root are in a different octant to objects in child
						}

						childHadContent = true;
						bestFit = i;
					}
				}
			}

			// Can reduce
			if (children == null)
			{
				// We don't have any children, so just shrink this node to the new size
				// We already know that everything will still fit in it
				SetValues(BaseLength / 2, minimumSize, looseness, childBounds[bestFit].Center);
				return this;
			}

			// No objects in entire octree
			if (bestFit == -1)
			{
				return this;
			}

			// We have children. Use the appropriate child as the new root node
			return children[bestFit];
		}

		/*
	/// <summary>
	/// Get the total amount of objects in this node and all its children, grandchildren etc. Useful for debugging.
	/// </summary>
	/// <param name="startingNum">Used by recursive calls to add to the previous total.</param>
	/// <returns>Total objects in this node and its children, grandchildren etc.</returns>
	public int GetTotalObjects(int startingNum = 0) {
		int totalObjects = startingNum + objects.Count;
		if (children != null) {
			for (int i = 0; i < 8; i++) {
				totalObjects += children[i].GetTotalObjects();
			}
		}
		return totalObjects;
	}
	*/

		// #### PRIVATE METHODS ####

		/// <summary>
		/// Set values for this node.
		/// </summary>
		/// <param name="baseLength">Length of this node, not taking looseness into account.</param>
		/// <param name="minSize">Minimum size of nodes in this octree.</param>
		/// <param name="loosenessVal">Multiplier for <paramref name="baseLength"/> to get the actual size.</param>
		/// <param name="center">Centre position of this node.</param>
		void SetValues(float baseLength, float minSize, float loosenessVal, Vector3 center)
		{
			if (childBounds is null)
			{
				childBounds = new BoundingBox[8];
			}

			BaseLength  = baseLength;
			minimumSize = minSize;
			looseness   = loosenessVal;
			Center      = center;
			looseLength = looseness * BaseLength;

			// Create the bounding box.
			bounds = BoundingBox.FromSphere(new BoundingSphere(Center, looseLength / 2f));

			float quarter = BaseLength / 4f;

			// since we're using a bounding sphere for convenience, this is a radius
			// and we can therefore reuse quarter (wherease BaseLength is the length of
			// the side of the bounding box).
			float childSize = quarter * looseness;

			childBounds[0] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(-quarter, quarter, -quarter), childSize));
			childBounds[1] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(quarter, quarter, -quarter), childSize));
			childBounds[2] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(-quarter, quarter, quarter), childSize));
			childBounds[3] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(quarter, quarter, quarter), childSize));
			childBounds[4] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(-quarter, -quarter, -quarter), childSize));
			childBounds[5] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(quarter, -quarter, -quarter), childSize));
			childBounds[6] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(-quarter, -quarter, quarter), childSize));
			childBounds[7] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(quarter, -quarter, quarter), childSize));
		}

		/// <summary>
		/// Private counterpart to the public Add method.
		/// </summary>
		/// <param name="obj">Object to add.</param>
		/// <param name="box">3D bounding box around the object.</param>
		void SubAdd(T obj, BoundingBox box)
		{
			// We know it fits at this level if we've got this far
			// Just add if few objects are here, or children would be below min size
			if (objects.Count < numObjectsAllowed || BaseLength / 2f < minimumSize)
			{
				var newObj = new OctreeObject
				{
					Object = obj,
					Bounds = box
				};

				objects.Add(newObj);
				return;
			}

			// Fits at this level, but we can go deeper. Would it fit there?

			// Create the 8 children
			int bestFitChild;

			if (children is null)
			{
				Split();

				// Now that we have the new children, see if this node's existing objects would fit there
				for (int i = objects.Count - 1; i >= 0; i--)
				{
					OctreeObject existing = objects[i];
					// Find which child the object is closest to based on where the
					// object's center is located in relation to the octree's center.
					bestFitChild = BestFitChild(existing.Bounds);
					// Does it fit?
					if (Encapsulates(children[bestFitChild].bounds, existing.Bounds))
					{
						children[bestFitChild].SubAdd(existing.Object, existing.Bounds); // Go a level deeper
						objects.Remove(existing);                                        // Remove from here
					}
				}
			}

			// Now handle the new object we're adding now
			bestFitChild = BestFitChild(box);

			if (Encapsulates(children[bestFitChild].bounds, box))
			{
				children[bestFitChild].SubAdd(obj, box);
			}
			else
			{
				var newObj = new OctreeObject
				{
					Object = obj,
					Bounds = box
				};

				objects.Add(newObj);
			}
		}

		/// <summary>
		/// Private counterpart to the public <see cref="Remove(T, BoundingBox)"/> method.
		/// </summary>
		/// <param name="obj">Object to remove.</param>
		/// <param name="objBounds">3D bounding box around the object.</param>
		/// <returns><value>true</value> if the object was removed successfully.</returns>
		bool SubRemove(T obj, BoundingBox objBounds)
		{
			bool removed = false;

			for (int i = 0; i < objects.Count; i++)
			{
				if (objects[i].Object.Equals(obj))
				{
					removed = objects.Remove(objects[i]);
					break;
				}
			}

			if (!removed && children != null)
			{
				int bestFitChild = BestFitChild(objBounds);
				removed = children[bestFitChild].SubRemove(obj, objBounds);
			}

			if (removed && children != null)
			{
				// Check if we should merge nodes now that we've removed an item
				if (ShouldMerge())
				{
					Merge();
				}
			}

			return removed;
		}

		/// <summary>
		/// Splits the octree into eight children.
		/// </summary>
		void Split()
		{
			if (children is null)
			{
				children = new BoundsOctreeNode<T>[8];
			}

			float quarter   = BaseLength / 4f;
			float newLength = BaseLength / 2f;

			children[0] = new BoundsOctreeNode<T>(newLength, minimumSize, looseness, Center + new Vector3(-quarter, quarter,  -quarter));
			children[1] = new BoundsOctreeNode<T>(newLength, minimumSize, looseness, Center + new Vector3(quarter,  quarter,  -quarter));
			children[2] = new BoundsOctreeNode<T>(newLength, minimumSize, looseness, Center + new Vector3(-quarter, quarter,  quarter));
			children[3] = new BoundsOctreeNode<T>(newLength, minimumSize, looseness, Center + new Vector3(quarter,  quarter,  quarter));
			children[4] = new BoundsOctreeNode<T>(newLength, minimumSize, looseness, Center + new Vector3(-quarter, -quarter, -quarter));
			children[5] = new BoundsOctreeNode<T>(newLength, minimumSize, looseness, Center + new Vector3(quarter,  -quarter, -quarter));
			children[6] = new BoundsOctreeNode<T>(newLength, minimumSize, looseness, Center + new Vector3(-quarter, -quarter, quarter));
			children[7] = new BoundsOctreeNode<T>(newLength, minimumSize, looseness, Center + new Vector3(quarter,  -quarter, quarter));
		}

		/// <summary>
		/// Merge all children into this node - the opposite of Split.
		/// Note: We only have to check one level down since a merge will never happen if the children already have children,
		/// since THAT won't happen unless there are already too many objects to merge.
		/// </summary>
		void Merge()
		{
			// Note: We know children != null or we wouldn't be merging
			for (int i = 0; i < 8; i++)
			{
				BoundsOctreeNode<T> curChild = children[i];
				int numObjects = curChild.objects.Count;
				for (int j = numObjects - 1; j >= 0; j--)
				{
					OctreeObject curObj = curChild.objects[j];
					objects.Add(curObj);
				}
			}

			// Remove the child nodes (and the objects in them - they've been added elsewhere now)
			children = null;
		}

		/// <summary>
		/// Checks if outerBounds encapsulates innerBounds.
		/// </summary>
		/// <param name="outerBounds">Outer bounds.</param>
		/// <param name="innerBounds">Inner bounds.</param>
		/// <returns><value>true</value> if innerBounds is fully encapsulated by outerBounds.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static bool Encapsulates(BoundingBox outerBounds, BoundingBox innerBounds)
		{
			//return outerBounds.Contains(innerBounds.Minimum) && outerBounds.Contains(innerBounds.Maximum);
			return outerBounds.Contains(ref innerBounds) == ContainmentType.Contains;
		}

		/// <summary>
		/// Find which child node this object would be most likely to fit in.
		/// </summary>
		/// <param name="objBounds">The object's bounds.</param>
		/// <returns>One of the eight child octants.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		int BestFitChild(BoundingBox objBounds)
		{
			return (objBounds.Center.X <= Center.X ? 0 : 1) + (objBounds.Center.Y >= Center.Y ? 0 : 4) + (objBounds.Center.Z <= Center.Z ? 0 : 2);
		}

		/// <summary>
		/// Checks if there are few enough objects in this node and its children that the children should all be merged into this.
		/// </summary>
		/// <returns><value>true</value> there are less or the same abount of objects in this and its children than numObjectsAllowed.</returns>
		bool ShouldMerge()
		{
			int totalObjects = objects.Count;

			if (children != null)
			{
				for (var i = 0; i < children.Length; i++)
				{
					BoundsOctreeNode<T> child = children[i];
					if (child.children != null)
					{
						// If any of the *children* have children, there are definitely too many to merge,
						// or the child woudl have been merged already
						return false;
					}

					totalObjects += child.objects.Count;
				}
			}

			return totalObjects <= numObjectsAllowed;
		}

		/// <summary>
		/// Checks if this node or anything below it has something in it.
		/// </summary>
		/// <returns><value>true</value> if this node or any of its children, grandchildren etc have something in them</returns>
		public bool HasAnyObjects()
		{
			if (objects.Count > 0)
			{
				return true;
			}

			if (children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					if (children[i].HasAnyObjects())
					{
						return true;
					}
				}
			}

			return false;
		}

		public IEnumerable<BoundingBox> GiveMeTheBounds()
		{
			//if (objects.Count > 0)
			{
				yield return bounds;
			}

			if (children == null)
			{
				yield break;
			}

			for (var i = 0; i < children.Length; i++)
			{
				BoundsOctreeNode<T> child = children[i];

				foreach (BoundingBox b in child.GiveMeTheBounds())
				{
					yield return b;
				}
			}
		}
	}
}