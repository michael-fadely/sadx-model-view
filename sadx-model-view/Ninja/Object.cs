using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using sadx_model_view.Extensions;
using sadx_model_view.Interfaces;

using SharpDX;

namespace sadx_model_view.Ninja
{
	[Flags]
	internal enum NJD_EVAL : uint
	{
		/// <summary>
		/// Ignore translation.
		/// </summary>
		UNIT_POS = 1 << 0,

		/// <summary>
		/// Ignore rotation.
		/// </summary>
		UNIT_ANG = 1 << 1,

		/// <summary>
		/// Ignore scale.
		/// </summary>
		UNIT_SCL = 1 << 2,

		/// <summary>
		/// Don't draw this model.
		/// </summary>
		HIDE = 1 << 3,

		/// <summary>
		/// Terminate tracing children. (Don't draw children)
		/// </summary>
		BREAK = 1 << 4,

		/// <summary>
		/// Use ZXY rotation.
		/// </summary>
		ZXY_ANG = 1 << 5,

		/// <summary>
		/// Skip animation.
		/// </summary>
		SKIP = 1 << 6,

		/// <summary>
		/// Unknown.
		/// </summary>
		SHAPE_SKIP = 1 << 7,

		/// <summary>
		/// Unknown.
		/// </summary>
		CLIP = 1 << 8,

		/// <summary>
		/// Unknown.
		/// </summary>
		MODIFIER = 1 << 9
	}

	public class ObjectTriangles
	{
		public ObjectTriangles(NJS_OBJECT @object, List<Triangle> triangles)
		{
			this.Object    = @object;
			this.Triangles = triangles;
		}

		public NJS_OBJECT     Object    { get; set; }
		public List<Triangle> Triangles { get; set; }
	}

	/// <summary>
	/// <para>An object in the world which has a model, position, angle, and scale.</para>
	/// See also:
	/// <seealso cref="NJS_MODEL"/>
	/// </summary>
	public class NJS_OBJECT : IDisposable,
	                          IInvalidatable,
	                          IEnumerable<NJS_OBJECT>
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static int SizeInBytes => 0x34;

		/// <summary>
		/// Constructs <see cref="NJS_OBJECT"/>, its children, and all of its available members from a file.
		/// </summary>
		/// <param name="stream">A stream containing the data.</param>
		public NJS_OBJECT(Stream stream)
		{
			byte[] buffer = new byte[SizeInBytes];
			stream.Read(buffer, 0, buffer.Length);

			evalflags = BitConverter.ToUInt32(buffer, 0);

			Position.X = BitConverter.ToSingle(buffer, 8);
			Position.Y = BitConverter.ToSingle(buffer, 8 + 4);
			Position.Z = BitConverter.ToSingle(buffer, 8 + 8);

			Angle.X = BitConverter.ToInt32(buffer, 0x14);
			Angle.Y = BitConverter.ToInt32(buffer, 0x14 + 4);
			Angle.Z = BitConverter.ToInt32(buffer, 0x14 + 8);

			Scale.X = BitConverter.ToSingle(buffer, 0x20);
			Scale.Y = BitConverter.ToSingle(buffer, 0x20 + 4);
			Scale.Z = BitConverter.ToSingle(buffer, 0x20 + 8);

			uint modelOffset   = BitConverter.ToUInt32(buffer, 0x04);
			uint childOffset   = BitConverter.ToUInt32(buffer, 0x2C);
			uint siblingOffset = BitConverter.ToUInt32(buffer, 0x30);

			long position = stream.Position;

			if (modelOffset != 0)
			{
				Model = ModelCache.FromStream(stream, modelOffset);
			}

			if (childOffset != 0)
			{
				Child = ObjectCache.FromStream(stream, childOffset);
			}

			if (siblingOffset != 0)
			{
				Sibling = ObjectCache.FromStream(stream, siblingOffset);
			}

			stream.Position = position;
			IsInvalid = true;
		}

		/// <summary>
		/// Copy constructor.
		/// This is a shallow copy; it will not copy children or siblings.
		/// </summary>
		/// <param name="obj">Object to copy from.</param>
		public NJS_OBJECT(NJS_OBJECT obj)
		{
			evalflags = obj.evalflags;
			Model     = obj.Model != null ? new NJS_MODEL(obj.Model) : null;
			Position  = obj.Position;
			Angle     = obj.Angle;
			Scale     = obj.Scale;
			Child     = obj.Child;
			Sibling   = obj.Sibling;
			IsInvalid = true;
		}

		public void Dispose()
		{
			DisposableExtensions.DisposeAndNullify(ref Model);
			DisposableExtensions.DisposeAndNullify(ref Child);
			DisposableExtensions.DisposeAndNullify(ref Sibling);
		}

		public uint       evalflags; /* evalation flags              */
		public NJS_MODEL? Model;     /* model data pointer           */
		public Vector3    Position;  /* translation                  */
		public Rotation3  Angle;     /* rotation                     */
		public Vector3    Scale;     /* scaling                      */

		public NJS_OBJECT? Child;
		public NJS_OBJECT? Sibling;

		public float Radius { get; private set; }

		public bool IgnoreTranslation
		{
			get => (evalflags & (uint)NJD_EVAL.UNIT_POS) != 0;
			set
			{
				if (value)
				{
					evalflags |= (uint)NJD_EVAL.UNIT_POS;
				}
				else
				{
					evalflags &= (uint)~NJD_EVAL.UNIT_POS;
				}
			}
		}

		public bool IgnoreRotation
		{
			get => (evalflags & (uint)NJD_EVAL.UNIT_ANG) != 0;
			set
			{
				if (value)
				{
					evalflags |= (uint)NJD_EVAL.UNIT_ANG;
				}
				else
				{
					evalflags &= (uint)~NJD_EVAL.UNIT_ANG;
				}
			}
		}

		public bool IgnoreScale
		{
			get => (evalflags & (uint)NJD_EVAL.UNIT_SCL) != 0;
			set
			{
				if (value)
				{
					evalflags |= (uint)NJD_EVAL.UNIT_SCL;
				}
				else
				{
					evalflags &= (uint)~NJD_EVAL.UNIT_SCL;
				}
			}
		}

		public bool SkipDraw
		{
			get => (evalflags & (uint)NJD_EVAL.HIDE) != 0;
			set
			{
				if (value)
				{
					evalflags |= (uint)NJD_EVAL.HIDE;
				}
				else
				{
					evalflags &= (uint)~NJD_EVAL.HIDE;
				}
			}
		}

		public bool SkipChildren
		{
			get => (evalflags & (uint)NJD_EVAL.BREAK) != 0;
			set
			{
				if (value)
				{
					evalflags |= (uint)NJD_EVAL.BREAK;
				}
				else
				{
					evalflags &= (uint)~NJD_EVAL.BREAK;
				}
			}
		}

		public bool UseZXYRotation
		{
			get => (evalflags & (uint)NJD_EVAL.ZXY_ANG) != 0;
			set
			{
				if (value)
				{
					evalflags |= (uint)NJD_EVAL.ZXY_ANG;
				}
				else
				{
					evalflags &= (uint)~NJD_EVAL.ZXY_ANG;
				}
			}
		}

		/// <summary>
		/// Applies this object's transform to the top of the matrix stack.
		/// </summary>
		public void PushTransform()
		{
			if (!IgnoreTranslation)
			{
				MatrixStack.Translate(in Position);
			}

			if (!IgnoreRotation)
			{
				MatrixStack.Rotate(in Angle, UseZXYRotation);
			}

			if (!IgnoreScale)
			{
				MatrixStack.Scale(in Scale);
			}
		}

		public void CommitVertexBuffer(Renderer device)
		{
			Model?.CommitVertexBuffer(device);
			Child?.CommitVertexBuffer(device);
			Sibling?.CommitVertexBuffer(device);
		}

		public void CalculateRadius()
		{
			Radius = Model?.r ?? 0.0f;

			if (Child != null)
			{
				Child.CalculateRadius();
				Radius = MathF.Max(Radius, Child.Radius);
			}

			if (Sibling != null)
			{
				Sibling.CalculateRadius();
				Radius = MathF.Max(Radius, Sibling.Radius);
			}
		}

		public static NJS_OBJECT? Copy(NJS_OBJECT? @object, bool copyChildren, bool copySiblings)
		{
			if (@object == null)
			{
				return null;
			}

			var obj = new NJS_OBJECT(@object);

			if (copyChildren)
			{
				obj.Child = Copy(obj.Child, true, copySiblings);
			}

			if (copySiblings)
			{
				obj.Sibling = Copy(obj.Sibling, copyChildren, true);
			}

			return obj;
		}

		public ObjectTriangles GetTriangles()
		{
			if (MatrixStack.Empty)
			{
				throw new Exception("oops");
			}

			var triangles = new List<Triangle>();
			Model?.GetTriangles(triangles);
			return new ObjectTriangles(this, triangles);
		}

		private bool _isInvalid;

		public bool IsInvalid
		{
			get => _isInvalid;
			set
			{
				_isInvalid = value;

				if (Model != null && Model.IsInvalid != value)
				{
					Model.IsInvalid = value;
				}

				if (Child != null && Child.IsInvalid != value)
				{
					Child.IsInvalid = value;
				}

				if (Sibling != null && Sibling.IsInvalid != value)
				{
					Sibling.IsInvalid = value;
				}
			}
		}

		public IEnumerator<NJS_OBJECT> GetEnumerator()
		{
			return GetEnumerator(this);
		}

		// TODO: rename
		private static IEnumerator<NJS_OBJECT> GetEnumerator(NJS_OBJECT o)
		{
			NJS_OBJECT? @object = o;

			do
			{
				MatrixStack.Push();
				@object.PushTransform();

				yield return @object;

				if (@object.Child != null)
				{
					foreach (NJS_OBJECT? c in @object.Child)
					{
						yield return c;
					}
				}

				MatrixStack.Pop();
				@object = @object.Sibling;
			} while (@object != null);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
