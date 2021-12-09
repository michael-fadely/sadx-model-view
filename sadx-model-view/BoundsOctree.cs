﻿using System.Collections.Generic;
using System.Runtime.CompilerServices;
using sadx_model_view.Extensions;
using SharpDX;

// A Dynamic, Loose Octree for storing any objects that can be described with AABB bounds
// See also: PointOctree, where objects are stored as single points and some code can be simplified
// Octree:	An octree is a tree data structure which divides 3D space into smaller partitions (nodes)
//			and places objects into the appropriate nodes. This allows fast access to objects
//			in an area of interest without having to check every object.
// Dynamic: The octree grows or shrinks as required when objects as added or removed
//			It also splits and merges nodes as appropriate. There is no maximum depth.
//			Nodes have a constant - numObjectsAllowed - which sets the amount of items allowed in a node before it splits.
// Loose:	The octree's nodes can be larger than 1/2 their parent's length and width, so they overlap to some extent.
//			This can alleviate the problem of even tiny objects ending up in large nodes if they're near boundaries.
//			A looseness value of 1.0 will make it a "normal" octree.
// T:		The content of the octree can be anything, since the bounds data is supplied separately.

// Originally written for my game Scraps (http://www.scrapsgame.com) but intended to be general-purpose.
// Copyright 2014 Nition, BSD licence (see LICENCE file). http://nition.co
// Unity-based, but could be adapted to work in pure C#

// Note: For loops are often used here since in some cases (e.g. the IsColliding method)
// they actually give much better performance than using Foreach, even in the compiled build.
// Using a LINQ expression is worse again than Foreach.
namespace sadx_model_view
{
	public class BoundsOctree<T>
	{
		// The total amount of objects currently in the tree
		public int Count { get; private set; }

		// Root node of the octree
		private BoundsOctreeNode<T> _rootNode;

		// Should be a value between 1 and 2. A multiplier for the base size of a node.
		// 1.0 is a "normal" octree, while values > 1 have overlap
		private readonly float _looseness;

		// Size that the octree was on creation
		private readonly float _initialSize;

		// Minimum side length that a node can be - essentially an alternative to having a max depth
		private readonly float _minSize;

#if UNITY_EDITOR
		// For collision visualisation. Automatically removed in builds.
		const int numCollisionsToSave = 4;
		readonly Queue<BoundingBox> lastBoundsCollisionChecks = new Queue<BoundingBox>();
		readonly Queue<Ray> lastRayCollisionChecks = new Queue<Ray>();
#endif

		/// <summary>
		/// Constructor for the bounds octree.
		/// </summary>
		/// <param name="bounds">The size of the initial node. The octree will never shrink smaller than this.</param>
		/// <param name="minNodeSize">Nodes will stop splitting if the new nodes would be smaller than this.</param>
		/// <param name="loosenessVal">Clamped between 1 and 2. Values > 1 let nodes overlap.</param>
		public BoundsOctree(BoundingSphere bounds, float minNodeSize, float loosenessVal) : this(bounds.Radius * 2f, bounds.Center, minNodeSize, loosenessVal)
		{
		}

		/// <summary>
		/// Constructor for the bounds octree.
		/// </summary>
		/// <param name="bounds">
		/// The size of the initial node.
		/// Note that the bounding box will be made uniform (maximum dimension).
		/// The octree will never shrink smaller than this.
		/// </param>
		/// <param name="minNodeSize">Nodes will stop splitting if the new nodes would be smaller than this.</param>
		/// <param name="loosenessVal">Clamped between 1 and 2. Values > 1 let nodes overlap.</param>
		public BoundsOctree(BoundingBox bounds, float minNodeSize, float loosenessVal) : this(BoundingSphere.FromBox(bounds), minNodeSize, loosenessVal)
		{
		}

		/// <summary>
		/// Constructor for the bounds octree.
		/// </summary>
		/// <param name="initialWorldSize">Size of the sides of the initial node. The octree will never shrink smaller than this.</param>
		/// <param name="initialWorldPos">Position of the center of the initial node.</param>
		/// <param name="minNodeSize">Nodes will stop splitting if the new nodes would be smaller than this.</param>
		/// <param name="loosenessVal">Clamped between 1 and 2. Values > 1 let nodes overlap.</param>
		public BoundsOctree(float initialWorldSize, Vector3 initialWorldPos, float minNodeSize, float loosenessVal)
		{
			if (minNodeSize > initialWorldSize)
			{
				minNodeSize = initialWorldSize;
			}

			Count        = 0;
			_initialSize = initialWorldSize;
			_minSize     = minNodeSize;
			_looseness   = loosenessVal.Clamp(1.0f, 2.0f);
			_rootNode    = new BoundsOctreeNode<T>(_initialSize, _minSize, _looseness, initialWorldPos);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IEnumerable<BoundingBox> GiveMeTheBounds()
		{
			return _rootNode.GiveMeTheBounds();
		}

		/// <summary>
		/// Add an object.
		/// </summary>
		/// <param name="obj">Object to add.</param>
		/// <param name="objBounds">3D bounding box around the object.</param>
		public void Add(T obj, BoundingBox objBounds)
		{
			// Add object or expand the octree until it can be added
			while (!_rootNode.Add(obj, in objBounds))
			{
				Grow(objBounds.Center - _rootNode.Center);
			}

			Count++;
		}

		/// <summary>
		/// Remove an object. Makes the assumption that the object only exists once in the tree.
		/// </summary>
		/// <param name="obj">Object to remove.</param>
		/// <returns><value>true</value> if the object was removed successfully.</returns>
		public bool Remove(T obj)
		{
			bool removed = _rootNode.Remove(obj);

			// See if we can shrink the octree down now that we've removed the item
			if (removed)
			{
				Count--;
				Shrink();
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
			bool removed = _rootNode.Remove(obj, in objBounds);

			// See if we can shrink the octree down now that we've removed the item
			if (removed)
			{
				Count--;
				Shrink();
			}

			return removed;
		}

		/// <summary>
		/// Check if the specified bounds intersect with anything in the tree. See also: GetColliding.
		/// </summary>
		/// <param name="checkBounds">bounds to check.</param>
		/// <returns><value>true</value> if there was a collision.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsColliding(in BoundingBox checkBounds)
		{
			return _rootNode.IsColliding(in checkBounds);
		}

		/// <summary>
		/// Check if the specified bounds intersect with anything in the tree. See also: GetColliding.
		/// </summary>
		/// <param name="checkBounds">bounds to check.</param>
		/// <returns><value>true</value> if there was a collision.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsColliding(in BoundingSphere checkBounds)
		{
			return _rootNode.IsColliding(in checkBounds);
		}

		/// <summary>
		/// Check if the specified ray intersects with anything in the tree. See also: GetColliding.
		/// </summary>
		/// <param name="checkRay">ray to check.</param>
		/// <param name="maxDistance">distance to check.</param>
		/// <returns><value>true</value> if there was a collision.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsColliding(in Ray checkRay, float maxDistance)
		{
			return _rootNode.IsColliding(in checkRay, maxDistance);
		}

		/// <summary>
		/// Returns an array of objects that intersect with the specified bounds, if any. Otherwise returns an empty array. See also: IsColliding.
		/// </summary>
		/// <param name="collidingWith">list to store intersections.</param>
		/// <param name="checkBounds">bounds to check.</param>
		/// <returns>Objects that intersect with the specified bounds.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void GetColliding(List<T> collidingWith, in BoundingBox checkBounds)
		{
			_rootNode.GetColliding(in checkBounds, collidingWith);
		}

		/// <summary>
		/// Returns an array of objects that intersect with the specified bounds, if any. Otherwise returns an empty array. See also: IsColliding.
		/// </summary>
		/// <param name="collidingWith">list to store intersections.</param>
		/// <param name="checkBounds">bounds to check.</param>
		/// <returns>Objects that intersect with the specified bounds.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void GetColliding(List<T> collidingWith, in BoundingSphere checkBounds)
		{
			_rootNode.GetColliding(in checkBounds, collidingWith);
		}

		/// <summary>
		/// Returns an array of objects that intersect with the specified ray, if any. Otherwise returns an empty array. See also: IsColliding.
		/// </summary>
		/// <param name="collidingWith">list to store intersections.</param>
		/// <param name="checkRay">ray to check.</param>
		/// <param name="maxDistance">distance to check.</param>
		/// <returns>Objects that intersect with the specified ray.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void GetColliding(List<RayCollisionResult<T>> collidingWith, in Ray checkRay, float maxDistance = float.PositiveInfinity)
		{
			_rootNode.GetColliding(in checkRay, collidingWith, maxDistance);
		}

		/// <summary>
		/// Returns an array of objects that intersect with the specified bounds, if any. Otherwise returns an empty array. See also: IsColliding.
		/// </summary>
		/// <param name="collidingWith">list to store intersections.</param>
		/// <param name="frustum">Frustum to check.</param>
		/// <returns>Objects that intersect with the specified bounds.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void GetColliding(List<T> collidingWith, in BoundingFrustum frustum)
		{
			_rootNode.GetColliding(in frustum, collidingWith);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public BoundingBox GetMaxBounds()
		{
			return _rootNode.GetBounds();
		}

		// #### PRIVATE METHODS ####

		/// <summary>
		/// Grow the octree to fit in all objects.
		/// </summary>
		/// <param name="direction">Direction to grow.</param>
		private void Grow(Vector3 direction)
		{
			BoundsOctreeNode<T> oldRoot = _rootNode;

			int     xDirection = direction.X >= 0 ? 1 : -1;
			int     yDirection = direction.Y >= 0 ? 1 : -1;
			int     zDirection = direction.Z >= 0 ? 1 : -1;
			float   half       = _rootNode.BaseLength / 2;
			float   newLength  = _rootNode.BaseLength * 2;
			Vector3 newCenter  = _rootNode.Center + new Vector3(xDirection * half, yDirection * half, zDirection * half);

			// Create a new, bigger octree root node
			_rootNode = new BoundsOctreeNode<T>(newLength, _minSize, _looseness, newCenter);

			if (oldRoot.HasAnyObjects())
			{
				// Create 7 new octree children to go with the old root as children of the new root
				int rootPos = GetRootPosIndex(xDirection, yDirection, zDirection);
				var children = new BoundsOctreeNode<T>[8];

				for (int i = 0; i < 8; i++)
				{
					if (i == rootPos)
					{
						children[i] = oldRoot;
					}
					else
					{
						xDirection  = i % 2 == 0 ? -1 : 1;
						yDirection  = i > 3 ? -1 : 1;
						zDirection  = i < 2 || i > 3 && i < 6 ? -1 : 1;
						children[i] = new BoundsOctreeNode<T>(_rootNode.BaseLength, _minSize, _looseness, newCenter + new Vector3(xDirection * half, yDirection * half, zDirection * half));
					}
				}

				// Attach the new children to the new root node
				_rootNode.SetChildren(children);
			}
		}

		/// <summary>
		/// Shrink the octree if possible, else leave it the same.
		/// </summary>
		private void Shrink()
		{
			_rootNode = _rootNode.ShrinkIfPossible(_initialSize);
		}

		/// <summary>
		/// Used when growing the octree. Works out where the old root node would fit inside a new, larger root node.
		/// </summary>
		/// <param name="xDir">X direction of growth. 1 or -1.</param>
		/// <param name="yDir">Y direction of growth. 1 or -1.</param>
		/// <param name="zDir">Z direction of growth. 1 or -1.</param>
		/// <returns>Octant where the root node should be.</returns>
		private static int GetRootPosIndex(int xDir, int yDir, int zDir)
		{
			int result = xDir > 0 ? 1 : 0;

			if (yDir < 0)
			{ 
				result += 4;
			}

			if (zDir > 0)
			{
				result += 2;
			}

			return result;
		}
	}
}