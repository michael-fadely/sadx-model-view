using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using sadx_model_view.Extensions.SharpDX.Mathematics.Collision;
using SharpDX;

// A node in a BoundsOctree
// Copyright 2014 Nition, BSD licence (see LICENCE file). http://nition.co
namespace sadx_model_view
{
	public struct RayCollisionResult<T>
	{
		public T Collider;
		public RayHit Hit;

		public RayCollisionResult(T collider, RayHit hit)
		{
			Collider = collider;
			Hit      = hit;
		}
	}

	public class BoundsOctreeNode<T>
	{
		/// <summary>
		/// If there are already numObjectsAllowed in a node, we split it into children.
		/// A generally good number seems to be something around 8-15.
		/// </summary>
		private const int NumObjectsAllowed = 16; // TODO: configurable

		/// <summary>
		/// Center of this node
		/// </summary>
		public Vector3 Center { get; private set; }

		/// <summary>
		/// Length of this node if it has a looseness of 1.0.
		/// </summary>
		public float BaseLength { get; private set; }

		/// <summary>
		/// Looseness value for this node
		/// </summary>
		private float _looseness;

		/// <summary>
		/// Minimum size for a node in this octree
		/// </summary>
		private float _minimumSize;

		/// <summary>
		/// Actual length of sides, taking the looseness value into account
		/// </summary>
		private float _looseLength;

		/// <summary>
		/// Bounding box that represents this node
		/// </summary>
		private BoundingBox _bounds;

		/// <summary>
		/// Objects in this node
		/// </summary>
		private readonly List<OctreeObject> _objects = new List<OctreeObject>();

		/// <summary>
		/// Child nodes, if any
		/// </summary>
		private BoundsOctreeNode<T>[]? _children;

		/// <summary>
		/// Bounds of potential children in this node. These are actual size (with looseness taken into account), not base size.
		/// </summary>
		private BoundingBox[]? _childBounds;

		/// <summary>
		/// An object in the octree
		/// </summary>
		private class OctreeObject
		{
			public readonly T           Object;
			public          BoundingBox Bounds;

			public OctreeObject(T o, BoundingBox bounds)
			{
				Object = o;
				Bounds = bounds;
			}
		}

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="baseLength">Length of this node, not taking looseness into account.</param>
		/// <param name="minSizeVal">Minimum size of nodes in this octree.</param>
		/// <param name="loosenessVal">Multiplier for <paramref name="baseLength"/> to get the actual size.</param>
		/// <param name="centerVal">Center position of this node.</param>
		public BoundsOctreeNode(float baseLength, float minSizeVal, float loosenessVal, Vector3 centerVal)
		{
			SetValues(baseLength, minSizeVal, loosenessVal, in centerVal);
		}

		// #### PUBLIC METHODS ####

		/// <summary>
		/// Add an object.
		/// </summary>
		/// <param name="obj">Object to add.</param>
		/// <param name="objBounds">3D bounding box around the object.</param>
		/// <returns><value>true</value> if the object fits entirely within this node.</returns>
		public bool Add(T obj, in BoundingBox objBounds)
		{
			if (!Encapsulates(_bounds, objBounds))
			{
				return false;
			}

			SubAdd(obj, in objBounds);
			return true;
		}

		/// <summary>
		/// Remove an object. Makes the assumption that the object only exists once in the tree.
		/// </summary>
		/// <param name="obj">Object to remove.</param>
		/// <returns><value>true</value> if the object was removed successfully.</returns>
		public bool Remove(T obj)
		{
			bool removed = false;

			for (int i = 0; i < _objects.Count; i++)
			{
				if (Equals(_objects[i].Object, obj))
				{
					removed = _objects.Remove(_objects[i]);
					break;
				}
			}

			if (!removed && _children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					removed = _children[i].Remove(obj);

					if (removed)
					{
						break;
					}
				}
			}

			if (removed && _children != null)
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
		public bool Remove(T obj, in BoundingBox objBounds)
		{
			if (!Encapsulates(_bounds, objBounds))
			{
				return false;
			}

			return SubRemove(obj, in objBounds);
		}

		/// <summary>
		/// Check if the specified bounds intersect with anything in the tree. See also: GetColliding.
		/// </summary>
		/// <param name="checkBounds">BoundingBox to check.</param>
		/// <returns><value>true</value> if there was a collision.</returns>
		public bool IsColliding(in BoundingBox checkBounds)
		{
			// Are the input bounds at least partially in this node?
			if (!_bounds.Intersects(checkBounds))
			{
				return false;
			}

			// Check against any objects in this node
			foreach (OctreeObject o in _objects)
			{
				if (o.Bounds.Intersects(checkBounds))
				{
					return true;
				}
			}

			// Check children
			if (_children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					if (_children[i].IsColliding(in checkBounds))
					{
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Check if the specified bounds intersect with anything in the tree. See also: GetColliding.
		/// </summary>
		/// <param name="checkBounds">BoundingSphere to check.</param>
		/// <returns><value>true</value> if there was a collision.</returns>
		public bool IsColliding(in BoundingSphere checkBounds)
		{
			// Are the input bounds at least partially in this node?
			if (!_bounds.Intersects(checkBounds))
			{
				return false;
			}

			// Check against any objects in this node
			foreach (OctreeObject o in _objects)
			{
				if (o.Bounds.Intersects(checkBounds))
				{
					return true;
				}
			}

			// Check children
			if (_children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					if (_children[i].IsColliding(in checkBounds))
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
			if (!checkRay.Intersects(ref _bounds, out float distance) || distance > maxDistance)
			{
				return false;
			}

			// Check against any objects in this node
			foreach (OctreeObject o in _objects)
			{
				if (checkRay.Intersects(ref o.Bounds, out distance) && distance <= maxDistance)
				{
					return true;
				}
			}

			// Check children
			if (_children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					if (_children[i].IsColliding(in checkRay, maxDistance))
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void GetColliding(in BoundingBox checkBounds, List<T> result)
		{
			GetCollidingImpl(in checkBounds, result, false);
		}

		private void GetCollidingImpl(in BoundingBox checkBounds, List<T> result, bool contains)
		{
			ContainmentType containment = contains ? ContainmentType.Contains : _bounds.Contains(checkBounds);

			switch (containment)
			{
				case ContainmentType.Disjoint:
					return;

				case ContainmentType.Contains:
					foreach (OctreeObject o in _objects)
					{
						result.Add(o.Object);
					}

					contains = true;
					break;

				case ContainmentType.Intersects:
					foreach (OctreeObject o in _objects)
					{
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
			if (_children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					_children[i].GetCollidingImpl(in checkBounds, result, contains);
				}
			}
		}

		/// <summary>
		/// Returns an array of objects that intersect with the specified bounds, if any. Otherwise returns an empty array. See also: IsColliding.
		/// </summary>
		/// <param name="checkBounds">BoundingSphere to check. Passing by ref as it improves performance with structs.</param>
		/// <param name="result">List result.</param>
		/// <returns>Objects that intersect with the specified bounds.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void GetColliding(in BoundingSphere checkBounds, List<T> result)
		{
			GetCollidingImpl(in checkBounds, result, false);
		}

		private void GetCollidingImpl(in BoundingSphere checkBounds, List<T> result, bool contains)
		{
			ContainmentType containment = contains ? ContainmentType.Contains : _bounds.Contains(checkBounds);

			switch (containment)
			{
				case ContainmentType.Disjoint:
					return;

				case ContainmentType.Contains:
					foreach (OctreeObject o in _objects)
					{
						result.Add(o.Object);
					}

					contains = true;
					break;

				case ContainmentType.Intersects:
					foreach (OctreeObject o in _objects)
					{
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
			if (_children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					_children[i].GetCollidingImpl(in checkBounds, result, contains);
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
		public void GetColliding(in Ray checkRay, List<RayCollisionResult<T>> result, float maxDistance = float.PositiveInfinity)
		{
			// Is the input ray at least partially in this node?
			if (!checkRay.Intersects(ref _bounds, out RayHit hit) || hit.Distance > maxDistance)
			{
				return;
			}

			// Check against any objects in this node
			foreach (OctreeObject o in _objects)
			{
				if (checkRay.Intersects(ref o.Bounds, out hit) && hit.Distance <= maxDistance)
				{
					result.Add(new RayCollisionResult<T>(o.Object, hit));
				}
			}

			// Check children
			if (_children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					_children[i].GetColliding(in checkRay, result, maxDistance);
				}
			}
		}

		/// <summary>
		/// Returns an array of objects that intersect with the specified bounds, if any. Otherwise returns an empty array. See also: IsColliding.
		/// </summary>
		/// <param name="frustum">Frustum to check. Passing by ref as it improves performance with structs.</param>
		/// <param name="result">List result.</param>
		/// <returns>Objects that intersect with the specified bounds.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void GetColliding(in BoundingFrustum frustum, List<T> result)
		{
			GetCollidingImpl(in frustum, result, false);
		}

		private void GetCollidingImpl(in BoundingFrustum frustum, List<T> result, bool contains)
		{
			ContainmentType containment = contains ? ContainmentType.Contains : frustum.Contains(ref _bounds);

			switch (containment)
			{
				case ContainmentType.Disjoint:
					return;

				case ContainmentType.Contains:
					foreach (OctreeObject o in _objects)
					{
						result.Add(o.Object);
					}

					contains = true;
					break;

				case ContainmentType.Intersects:
					// Check against any objects in this node
					foreach (OctreeObject o in _objects)
					{
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
			if (_children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					_children[i].GetCollidingImpl(in frustum, result, contains);
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

			_children = childOctrees;
		}

		public BoundingBox GetBounds()
		{
			return _bounds;
		}

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

			if (_objects.Count == 0 && (_children == null || _children.Length == 0))
			{
				return this;
			}

			if (_childBounds is null)
			{
				throw new InvalidOperationException();
			}

			// Check objects in root
			int bestFit = -1;
			for (int i = 0; i < _objects.Count; i++)
			{
				OctreeObject curObj = _objects[i];
				int newBestFit = BestFitChild(curObj.Bounds);
				if (i == 0 || newBestFit == bestFit)
				{
					// In same octant as the other(s). Does it fit completely inside that octant?
					if (Encapsulates(_childBounds[newBestFit], curObj.Bounds))
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
			if (_children != null)
			{
				bool childHadContent = false;
				for (int i = 0; i < _children.Length; i++)
				{
					if (_children[i].HasAnyObjects())
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
			if (_children == null)
			{
				// We don't have any children, so just shrink this node to the new size
				// We already know that everything will still fit in it
				Vector3 c = _childBounds[bestFit].Center;
				SetValues(BaseLength / 2, _minimumSize, _looseness, in c);
				return this;
			}

			// No objects in entire octree
			if (bestFit == -1)
			{
				return this;
			}

			// We have children. Use the appropriate child as the new root node
			return _children[bestFit];
		}

		// #### PRIVATE METHODS ####

		/// <summary>
		/// Set values for this node.
		/// </summary>
		/// <param name="baseLength">Length of this node, not taking looseness into account.</param>
		/// <param name="minSize">Minimum size of nodes in this octree.</param>
		/// <param name="loosenessVal">Multiplier for <paramref name="baseLength"/> to get the actual size.</param>
		/// <param name="center">Center position of this node.</param>
		private void SetValues(float baseLength, float minSize, float loosenessVal, in Vector3 center)
		{
			if (_childBounds is null)
			{
				_childBounds = new BoundingBox[8];
			}

			BaseLength   = baseLength;
			_minimumSize = minSize;
			_looseness   = loosenessVal;
			Center       = center;
			_looseLength = _looseness * BaseLength;

			// Create the bounding box.
			_bounds = BoundingBox.FromSphere(new BoundingSphere(Center, _looseLength / 2f));

			float quarter = BaseLength / 4f;

			// since we're using a bounding sphere for convenience, this is a radius
			// and we can therefore reuse quarter (wherease BaseLength is the length of
			// the side of the bounding box).
			float childSize = quarter * _looseness;

			_childBounds[0] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(-quarter, quarter, -quarter), childSize));
			_childBounds[1] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(quarter, quarter, -quarter), childSize));
			_childBounds[2] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(-quarter, quarter, quarter), childSize));
			_childBounds[3] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(quarter, quarter, quarter), childSize));
			_childBounds[4] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(-quarter, -quarter, -quarter), childSize));
			_childBounds[5] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(quarter, -quarter, -quarter), childSize));
			_childBounds[6] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(-quarter, -quarter, quarter), childSize));
			_childBounds[7] = BoundingBox.FromSphere(new BoundingSphere(Center + new Vector3(quarter, -quarter, quarter), childSize));
		}

		/// <summary>
		/// Private counterpart to the public Add method.
		/// </summary>
		/// <param name="obj">Object to add.</param>
		/// <param name="box">3D bounding box around the object.</param>
		private void SubAdd(T obj, in BoundingBox box)
		{
			// We know it fits at this level if we've got this far
			// Just add if few objects are here, or children would be below min size
			if (_objects.Count < NumObjectsAllowed || BaseLength / 2f < _minimumSize)
			{
				var newObj = new OctreeObject(obj, _bounds);
				_objects.Add(newObj);
				return;
			}

			// Fits at this level, but we can go deeper. Would it fit there?

			// Create the 8 children
			int bestFitChild;

			if (_children is null)
			{
				Split();

				// Now that we have the new children, see if this node's existing objects would fit there
				for (int i = _objects.Count - 1; i >= 0; i--)
				{
					OctreeObject existing = _objects[i];
					// Find which child the object is closest to based on where the
					// object's center is located in relation to the octree's center.
					bestFitChild = BestFitChild(existing.Bounds);
					// Does it fit?
					if (Encapsulates(_children![bestFitChild]._bounds, existing.Bounds))
					{
						BoundingBox b = existing.Bounds;
						_children[bestFitChild].SubAdd(existing.Object, in b); // Go a level deeper
						_objects.Remove(existing);                             // Remove from here
					}
				}
			}

			// Now handle the new object we're adding now
			bestFitChild = BestFitChild(box);

			if (Encapsulates(_children![bestFitChild]._bounds, box))
			{
				_children[bestFitChild].SubAdd(obj, in box);
			}
			else
			{
				var newObj = new OctreeObject(obj, _bounds);
				_objects.Add(newObj);
			}
		}

		/// <summary>
		/// Private counterpart to the public <see cref="Remove(T, in BoundingBox)"/> method.
		/// </summary>
		/// <param name="obj">Object to remove.</param>
		/// <param name="objBounds">3D bounding box around the object.</param>
		/// <returns><value>true</value> if the object was removed successfully.</returns>
		private bool SubRemove(T obj, in BoundingBox objBounds)
		{
			bool removed = false;

			for (int i = 0; i < _objects.Count; i++)
			{
				if (Equals(_objects[i].Object, obj))
				{
					removed = _objects.Remove(_objects[i]);
					break;
				}
			}

			if (!removed && _children != null)
			{
				int bestFitChild = BestFitChild(objBounds);
				removed = _children[bestFitChild].SubRemove(obj, in objBounds);
			}

			if (removed && _children != null)
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
		private void Split()
		{
			if (_children is null)
			{
				_children = new BoundsOctreeNode<T>[8];
			}

			float quarter   = BaseLength / 4f;
			float newLength = BaseLength / 2f;

			_children[0] = new BoundsOctreeNode<T>(newLength, _minimumSize, _looseness, Center + new Vector3(-quarter, quarter,  -quarter));
			_children[1] = new BoundsOctreeNode<T>(newLength, _minimumSize, _looseness, Center + new Vector3(quarter,  quarter,  -quarter));
			_children[2] = new BoundsOctreeNode<T>(newLength, _minimumSize, _looseness, Center + new Vector3(-quarter, quarter,  quarter));
			_children[3] = new BoundsOctreeNode<T>(newLength, _minimumSize, _looseness, Center + new Vector3(quarter,  quarter,  quarter));
			_children[4] = new BoundsOctreeNode<T>(newLength, _minimumSize, _looseness, Center + new Vector3(-quarter, -quarter, -quarter));
			_children[5] = new BoundsOctreeNode<T>(newLength, _minimumSize, _looseness, Center + new Vector3(quarter,  -quarter, -quarter));
			_children[6] = new BoundsOctreeNode<T>(newLength, _minimumSize, _looseness, Center + new Vector3(-quarter, -quarter, quarter));
			_children[7] = new BoundsOctreeNode<T>(newLength, _minimumSize, _looseness, Center + new Vector3(quarter,  -quarter, quarter));
		}

		/// <summary>
		/// Merge all children into this node - the opposite of Split.
		/// Note: We only have to check one level down since a merge will never happen if the children already have children,
		/// since THAT won't happen unless there are already too many objects to merge.
		/// </summary>
		private void Merge()
		{
			// Note: We know children != null or we wouldn't be merging
			for (int i = 0; i < 8; i++)
			{
				BoundsOctreeNode<T> curChild = _children![i];
				int numObjects = curChild._objects.Count;
				for (int j = numObjects - 1; j >= 0; j--)
				{
					OctreeObject curObj = curChild._objects[j];
					_objects.Add(curObj);
				}
			}

			// Remove the child nodes (and the objects in them - they've been added elsewhere now)
			_children = null;
		}

		/// <summary>
		/// Checks if outerBounds encapsulates innerBounds.
		/// </summary>
		/// <param name="outerBounds">Outer bounds.</param>
		/// <param name="innerBounds">Inner bounds.</param>
		/// <returns><value>true</value> if innerBounds is fully encapsulated by outerBounds.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool Encapsulates(BoundingBox outerBounds, BoundingBox innerBounds)
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
		private int BestFitChild(BoundingBox objBounds)
		{
			return (objBounds.Center.X <= Center.X ? 0 : 1) + (objBounds.Center.Y >= Center.Y ? 0 : 4) + (objBounds.Center.Z <= Center.Z ? 0 : 2);
		}

		/// <summary>
		/// Checks if there are few enough objects in this node and its children that the children should all be merged into this.
		/// </summary>
		/// <returns><value>true</value> there are less or the same abount of objects in this and its children than numObjectsAllowed.</returns>
		private bool ShouldMerge()
		{
			int totalObjects = _objects.Count;

			if (_children != null)
			{
				foreach (BoundsOctreeNode<T> child in _children)
				{
					if (child._children != null)
					{
						// If any of the *children* have children, there are definitely too many to merge,
						// or the child woudl have been merged already
						return false;
					}

					totalObjects += child._objects.Count;
				}
			}

			return totalObjects <= NumObjectsAllowed;
		}

		/// <summary>
		/// Checks if this node or anything below it has something in it.
		/// </summary>
		/// <returns><value>true</value> if this node or any of its children, grandchildren etc have something in them</returns>
		public bool HasAnyObjects()
		{
			if (_objects.Count > 0)
			{
				return true;
			}

			if (_children != null)
			{
				for (int i = 0; i < 8; i++)
				{
					if (_children[i].HasAnyObjects())
					{
						return true;
					}
				}
			}

			return false;
		}

		public IEnumerable<BoundingBox> GiveMeTheBounds()
		{
			if (_objects.Count > 0)
			{
				yield return _bounds;
			}

			if (_children == null)
			{
				yield break;
			}

			foreach (BoundsOctreeNode<T> child in _children)
			{
				foreach (BoundingBox b in child.GiveMeTheBounds())
				{
					yield return b;
				}
			}
		}
	}
}