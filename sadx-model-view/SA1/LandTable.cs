using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using sadx_model_view.Extensions;
using sadx_model_view.Ninja;

namespace sadx_model_view.SA1
{
	[Flags]
	public enum LandTableFlags
	{
		Animation   = 0x1,
		TextureList = 0x2,
		TextureName = 0x8,
	}

	public class LandTable : IDisposable
	{
		public static int SizeInBytes => 0x24;

		public short          COLCount  => (short)ColList.Count;
		public short          AnimCount => (short)AnimData.Count;
		public LandTableFlags Flags;
		public float          Unknown_1;

		public readonly List<Col> ColList;
		public readonly List<GeoAnimData> AnimData;

		public string      TexName;
		public NJS_TEXLIST TexList;
		public int         Unknown_2;
		public int         Unknown_3;

		public uint TexListPointer;

		public LandTable(Stream stream)
		{
			ColList = new List<Col>();
			AnimData = new List<GeoAnimData>();
			TexList = null;

			var buffer = new byte[SizeInBytes];
			stream.Read(buffer, 0, buffer.Length);

			short col_count = BitConverter.ToInt16(buffer, 0x00);
			short anim_count = BitConverter.ToInt16(buffer, 0x02);

			Flags = (LandTableFlags)BitConverter.ToInt32(buffer, 0x04);
			Unknown_1 = BitConverter.ToSingle(buffer, 0x08);

			uint col_ptr    = BitConverter.ToUInt32(buffer, 0x0C);
			uint anim_ptr   = BitConverter.ToUInt32(buffer, 0x10);
			uint name_ptr   = BitConverter.ToUInt32(buffer, 0x14);
			TexListPointer = BitConverter.ToUInt32(buffer, 0x18);

			Unknown_2 = BitConverter.ToInt32(buffer, 0x1C);
			Unknown_3 = BitConverter.ToInt32(buffer, 0x20);

			long position = stream.Position;

			if (col_count > 0 && col_ptr > 0)
			{
				stream.Position = col_ptr;
				for (int i = 0; i < col_count; i++)
				{
					ColList.Add(new Col(stream));
				}
			}

			if (anim_count > 0 && anim_ptr > 0)
			{
				stream.Position = anim_ptr;
				for (int i = 0; i < anim_count; i++)
				{
					AnimData.Add(new GeoAnimData(stream));
				}
			}

			if (name_ptr > 0)
			{
				var str = new byte[255];
				stream.Position = name_ptr;
				TexName = Encoding.UTF8.GetString(str, 0, stream.ReadString(ref str));
			}

			stream.Position = position;
		}

		public LandTable()
		{
			Flags     = 0;
			Unknown_1 = 0.0f;
			ColList   = new List<Col>();
			AnimData  = new List<GeoAnimData>();
			TexName   = string.Empty;
			TexList   = null;
			Unknown_2 = 0;
			Unknown_3 = 0;
		}

		public IEnumerable<ObjectTriangles> GetTriangles()
		{
			foreach (Col c in ColList)
			{
				// HACK: since we're only displaying visible COL elements, only get triangles for visible
				if ((c.Flags & ColFlags.Visible) == 0)
				{
					continue;
				}

				foreach (ObjectTriangles pair in c.GetTriangles())
				{
					yield return pair;
				}
			}
		}

		public void CommitVertexBuffer(Renderer device)
		{
			foreach (Col c in ColList)
			{
				c.CommitVertexBuffer(device);
			}
		}

		public void Dispose()
		{
			foreach (Col col in ColList)
			{
				col?.Dispose();
			}

			ColList.Clear();

			foreach (GeoAnimData anim in AnimData)
			{
				anim?.Dispose();
			}

			AnimData.Clear();
		}
	}
}
