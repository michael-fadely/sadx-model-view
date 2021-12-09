using System;
using System.Collections.Generic;
using System.IO;

using sadx_model_view.Extensions;
using sadx_model_view.Ninja;

using SharpDX;

namespace sadx_model_view.SA1
{
	[Flags]
	public enum ColFlags : uint
	{
		Solid          = 0x1,
		Water          = 0x2,
		NoFriction     = 0x4,
		NoAccel        = 0x8,
		Unknown2       = 0x10,
		UseSkyDrawDist = 0x20,
		NoLandingA     = 0x40,
		IncAccel       = 0x80,
		Dig            = 0x100,
		Unknown5       = 0x200,
		Unknown6       = 0x400,
		Unknown7       = 0x800,
		NoClimb        = 0x1000,
		Unknown8       = 0x2000,
		Stairs         = 0x4000,
		Unknown10      = 0x8000,
		Hurt           = 0x10000,
		Unknown11      = 0x20000,
		Unknown12      = 0x40000,
		Unknown13      = 0x80000,
		Footprints     = 0x100000,
		NoLandingB     = 0x200000,
		WaterNoAlpha   = 0x400000,
		Unknown16      = 0x800000,
		NoAlphaSortPls = 0x1000000,
		AlphaSortThing = 0x2000000,
		UvManipulation = 0x4000000,
		Unknown20      = 0x8000000,
		UseRotation    = 0x10000000,
		Unknown22      = 0x20000000,
		Unknown23      = 0x40000000,
		Visible        = 0x80000000
	}

	/// <summary>
	/// A class defining a model in a <see cref="LandTable"/>.
	/// </summary>
	public class Col : IDisposable
	{
		public static int SizeInBytes => 0x24;

		public Vector3 Center;
		public float   Radius;

		// ReSharper disable once NotAccessedField.Local
		private int pad_a;
		// ReSharper disable once NotAccessedField.Local
		private int pad_b;

		public NJS_OBJECT? Object;
		// ReSharper disable once NotAccessedField.Local
		private int      anonymous_6;
		public  ColFlags Flags;

		/// <summary>
		/// Constructs a Col object from a stream.
		/// </summary>
		/// <param name="stream">The stream containing the Col data.</param>
		public Col(Stream stream)
		{
			byte[] buffer = new byte[SizeInBytes];
			stream.Read(buffer, 0, buffer.Length);
			long position = stream.Position;

			Center = Util.VectorFromStream(in buffer, 0x00);
			Radius = BitConverter.ToSingle(buffer, 0x04);
			pad_a  = BitConverter.ToInt32(buffer, 0x10);
			pad_b  = BitConverter.ToInt32(buffer, 0x14);

			uint object_ptr = BitConverter.ToUInt32(buffer, 0x18);

			if (object_ptr > 0)
			{
				Object = ObjectCache.FromStream(stream, object_ptr);
			}

			anonymous_6 = BitConverter.ToInt32(buffer, 0x1C);
			Flags       = (ColFlags)BitConverter.ToUInt32(buffer, 0x20);

			stream.Position = position;
		}

		public Col()
		{
			Center      = Vector3.Zero;
			Radius      = 0.0f;
			pad_a       = 0;
			pad_b       = 0;
			Object      = null;
			anonymous_6 = 0;
			Flags       = 0;
		}

		public void Dispose()
		{
			DisposableExtensions.DisposeAndNullify(ref Object);
		}

		public void CommitVertexBuffer(Renderer device)
		{
			Object?.CommitVertexBuffer(device);
		}

		public IEnumerable<ObjectTriangles> GetTriangles()
		{
			if (Object == null)
			{
				yield break;
			}

			foreach (NJS_OBJECT o in Object)
			{
				yield return o.GetTriangles();
			}
		}
	}
}
