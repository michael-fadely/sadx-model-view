using System;
using System.IO;
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
		/// <param name="file">A file stream containing the data.</param>
		public NJS_OBJECT(Stream file)
		{
			var buffer = new byte[SizeInBytes];
			file.Read(buffer, 0, buffer.Length);

			evalflags = BitConverter.ToUInt32(buffer, 0);

			pos.X = BitConverter.ToSingle(buffer, 8);
			pos.Y = BitConverter.ToSingle(buffer, 8 + 4);
			pos.Z = BitConverter.ToSingle(buffer, 8 + 8);

			ang.X = BitConverter.ToInt32(buffer, 0x14);
			ang.Y = BitConverter.ToInt32(buffer, 0x14 + 4);
			ang.Z = BitConverter.ToInt32(buffer, 0x14 + 8);

			scl.X = BitConverter.ToSingle(buffer, 0x20);
			scl.Y = BitConverter.ToSingle(buffer, 0x20 + 4);
			scl.Z = BitConverter.ToSingle(buffer, 0x20 + 8);

			var model_ptr = BitConverter.ToUInt32(buffer, 0x04);
			var child_ptr = BitConverter.ToUInt32(buffer, 0x2C);
			var sibling_ptr = BitConverter.ToUInt32(buffer, 0x30);

			var position = file.Position;

			if (model_ptr != 0)
			{
				file.Position = model_ptr;
				model = new NJS_MODEL(file);
			}

			if (child_ptr != 0)
			{
				file.Position = child_ptr;
				child = new NJS_OBJECT(file);
			}

			if (sibling_ptr != 0)
			{
				file.Position = sibling_ptr;
				sibling = new NJS_OBJECT(file);
			}

			file.Position = position;
		}

		public uint evalflags;     /* evalation flags              */
		public NJS_MODEL model;      /* model data pointer           */
		public Vector3 pos;       /* translation                  */
		public Rotation3 ang;        /* rotation                     */
		public Vector3 scl;       /* scaling                      */
		public NJS_OBJECT child;     /* child object                 */
		public NJS_OBJECT sibling;   /* sibling object               */

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
			model?.CommitVertexBuffer(device);
			child?.CommitVertexBuffer(device);
			sibling?.CommitVertexBuffer(device);
		}

		public void Draw(Device device)
		{
			MatrixStack.Push();

			if (!IgnoreTranslation)
			{
				MatrixStack.Translate(ref pos);
			}

			if (!IgnoreRotation)
			{
				MatrixStack.Rotate(ref ang, UseZXYRotation);
			}

			if (!IgnoreScale)
			{
				MatrixStack.Scale(ref scl);
			}

			MatrixStack.SetTransform(device);

			if (!SkipDraw)
			{
				model?.Draw(device);
			}

			if (!SkipChildren)
			{
				child?.Draw(device);
			}

			MatrixStack.Pop();
			sibling?.Draw(device);
		}

		public void CalculateRadius()
		{
			Radius = model?.r ?? 0.0f;

			if (child != null)
			{
				child.CalculateRadius();
				Radius = Math.Max(Radius, child.Radius);
			}

			if (sibling != null)
			{
				sibling.CalculateRadius();
				Radius = Math.Max(Radius, sibling.Radius);
			}
		}

		public void Dispose()
		{
			model?.Dispose();
		}
	}
}
