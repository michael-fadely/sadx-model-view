using System;
using System.IO;
using System.Linq;
using SharpDX;
using SharpDX.Direct3D9;

namespace sadx_model_view.Ninja
{
	[Flags]
	enum NJD_EVAL : uint
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

	/// <summary>
	/// <para>An object in the world which has a model, position, angle, and scale.</para>
	/// See also:
	/// <seealso cref="NJS_MODEL"/>
	/// </summary>
	public class NJS_OBJECT : IDisposable
	{
		/// <summary>
		/// Native structure size in bytes.
		/// </summary>
		public static int SizeInBytes => 0x34;

		/// <summary>
		/// Constructs <see cref="NJS_OBJECT"/>, its children, and all of its available members from a file.
		/// </summary>
		/// <param name="stream">A stream containing the data.</param>
		/// <param name="parent">A parent object.</param>
		/// <param name="previousSibling">A previous sibling object.</param>
		public NJS_OBJECT(Stream stream, NJS_OBJECT parent = null, NJS_OBJECT previousSibling = null)
		{
			Parent          = parent;
			PreviousSibling = previousSibling;

			var buffer = new byte[SizeInBytes];
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

			var model_ptr = BitConverter.ToUInt32(buffer, 0x04);
			var child_ptr = BitConverter.ToUInt32(buffer, 0x2C);
			var sibling_ptr = BitConverter.ToUInt32(buffer, 0x30);

			var position = stream.Position;

			if (model_ptr != 0)
			{
				stream.Position = model_ptr;
				Model = new NJS_MODEL(stream);
			}

			if (child_ptr != 0)
			{
				Child = ObjectCache.FromStream(stream, child_ptr, this, previousSibling);
			}

			if (sibling_ptr != 0)
			{
				Sibling = ObjectCache.FromStream(stream, sibling_ptr, parent, this);
			}

			stream.Position = position;
		}

		/// <summary>
		/// Copy constructor.
		/// This is a shallow copy; it will not copy children or siblings.
		/// </summary>
		/// <param name="obj">Object to copy from.</param>
		public NJS_OBJECT(NJS_OBJECT obj)
		{
			evalflags       = obj.evalflags;
			Model           = obj.Model != null ? new NJS_MODEL(obj.Model) : null;
			Position        = obj.Position;
			Angle           = obj.Angle;
			Scale           = obj.Scale;
			Parent          = obj.Parent;
			Child           = obj.Child;
			PreviousSibling = obj.PreviousSibling;
			Sibling         = obj.Sibling;
		}

		~NJS_OBJECT()
		{
			Dispose();
		}

		public void Dispose()
		{
			Model?.Dispose();
		}

		public uint evalflags;     /* evalation flags              */
		public NJS_MODEL Model;    /* model data pointer           */
		public Vector3 Position;   /* translation                  */
		public Rotation3 Angle;    /* rotation                     */
		public Vector3 Scale;      /* scaling                      */

		/// <summary>
		/// (Extension) Parent of this child object.
		/// </summary>
		public NJS_OBJECT Parent;
		public NJS_OBJECT Child;
		/// <summary>
		/// (Extension) Last sibling of this sibling object.
		/// </summary>
		public NJS_OBJECT PreviousSibling;
		public NJS_OBJECT Sibling;

		public float Radius { get; private set; }

		public bool IgnoreTranslation
		{
			get { return ((NJD_EVAL)evalflags).HasFlag(NJD_EVAL.UNIT_POS); }
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
			get { return ((NJD_EVAL)evalflags).HasFlag(NJD_EVAL.UNIT_ANG); }
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
			get { return ((NJD_EVAL)evalflags).HasFlag(NJD_EVAL.UNIT_SCL); }
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
			get { return ((NJD_EVAL)evalflags).HasFlag(NJD_EVAL.HIDE); }
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
			get { return ((NJD_EVAL)evalflags).HasFlag(NJD_EVAL.BREAK); }
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
			get { return ((NJD_EVAL)evalflags).HasFlag(NJD_EVAL.ZXY_ANG); }
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

		public void CommitVertexBuffer(Device device)
		{
			Model?.CommitVertexBuffer(device);
			Child?.CommitVertexBuffer(device);
			Sibling?.CommitVertexBuffer(device);
		}

		public void Draw(Device device)
		{
			MatrixStack.Push();

			if (!IgnoreTranslation)
			{
				MatrixStack.Translate(ref Position);
			}

			if (!IgnoreRotation)
			{
				MatrixStack.Rotate(ref Angle, UseZXYRotation);
			}

			if (!IgnoreScale)
			{
				MatrixStack.Scale(ref Scale);
			}

			MatrixStack.SetTransform(device);

			if (!SkipDraw)
			{
				Model?.Draw(device);
			}

			if (!SkipChildren)
			{
				Child?.Draw(device);
			}

			MatrixStack.Pop();
			Sibling?.Draw(device);
		}

		public void CalculateRadius()
		{
			Radius = Model?.r ?? 0.0f;

			if (Child != null)
			{
				Child.CalculateRadius();
				Radius = Math.Max(Radius, Child.Radius);
			}

			if (Sibling != null)
			{
				Sibling.CalculateRadius();
				Radius = Math.Max(Radius, Sibling.Radius);
			}
		}

		public void Sort()
		{
			Sort(this);
		}

		public static NJS_OBJECT Copy(NJS_OBJECT @object, bool copyChildren, bool copySiblings)
		{
			if (@object == null)
				return null;

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

		private static void Filter(NJS_OBJECT obj, bool useAlpha)
		{
			// TODO: sort transparent mesh by radius/intersections
			if (obj.Model != null && obj.Model.nbMat > 0)
			{
				NJS_MODEL model = obj.Model;

				var filtered = model.meshsets.Where(x => model.mats[x.MaterialId].attrflags.HasFlag(NJD_FLAG.UseAlpha) == useAlpha).ToList();

				if (filtered.Count == 0)
				{
					model.Dispose();
					obj.Model = null;
					obj.SkipDraw = true;
				}
				else if (filtered.Count != model.nbMeshset)
				{
					foreach (NJS_MESHSET m in model.meshsets
						.Where(x => model.mats[x.MaterialId].attrflags.HasFlag(NJD_FLAG.UseAlpha) != useAlpha))
					{
						m.Dispose();
					}

					model.meshsets.Clear();
					model.meshsets = filtered;
					model.nbMeshset = (ushort)filtered.Count;
				}
			}

			if (obj.Child != null)
			{
				Filter(obj.Child, useAlpha);
			}

			if (obj.Sibling != null)
			{
				Filter(obj.Sibling, useAlpha);
			}
		}

		/// <summary>
		/// Sorts an object and its siblings/children to ensure opaque objects are rendered first, and transparent last.
		/// Particularly useful for Dreamcast models.
		/// </summary>
		/// <param name="obj">The object to sort.</param>
		private static void Sort(NJS_OBJECT obj)
		{
			// first let's make a deep copy of this object...
			var copy = Copy(obj, true, true);

			Filter(obj, false);
			Filter(copy, true);

			NJS_OBJECT last;

			if (obj.Sibling == null)
			{
				last = obj;
			}
			else
			{
				last = obj.Sibling;
				while (last.Sibling != null)
				{
					last = last.Sibling;
				}
			}

			last.Sibling = copy;
		}
	}
}
