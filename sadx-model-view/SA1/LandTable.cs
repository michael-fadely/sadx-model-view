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
		public static readonly int SizeInBytes = 0x24;

		public short          COLCount  => (short)ColList.Count;
		public short          AnimCount => (short)AnimData.Count;
		public LandTableFlags Flags;
		public float          Unknown_1;

		public readonly List<Col>         ColList;
		public readonly List<GeoAnimData> AnimData;

		public string       TexName;
		public NJS_TEXLIST? TexList;
		public int          Unknown_2;
		public int          Unknown_3;

		public uint TexListPointer;

		public LandTable(Stream stream)
		{
			TexList = null;

			byte[] buffer = new byte[SizeInBytes];
			stream.ReadExact(buffer);

			short colCount = BitConverter.ToInt16(buffer, 0x00);
			short animCount = BitConverter.ToInt16(buffer, 0x02);

			Flags = (LandTableFlags)BitConverter.ToInt32(buffer, 0x04);
			Unknown_1 = BitConverter.ToSingle(buffer, 0x08);

			uint colOffset  = BitConverter.ToUInt32(buffer, 0x0C);
			uint animOffset = BitConverter.ToUInt32(buffer, 0x10);
			uint nameOffset = BitConverter.ToUInt32(buffer, 0x14);
			TexListPointer  = BitConverter.ToUInt32(buffer, 0x18);

			Unknown_2 = BitConverter.ToInt32(buffer, 0x1C);
			Unknown_3 = BitConverter.ToInt32(buffer, 0x20);

			long position = stream.Position;

			if (colCount > 0 && colOffset > 0)
			{
				ColList = new List<Col>(capacity: colCount);

				stream.Position = colOffset;

				for (int i = 0; i < colCount; i++)
				{
					ColList.Add(new Col(stream));
				}
			}
			else
			{
				ColList = new List<Col>();
			}

			if (animCount > 0 && animOffset > 0)
			{
				AnimData = new List<GeoAnimData>(capacity: animCount);

				stream.Position = animOffset;

				for (int i = 0; i < animCount; i++)
				{
					AnimData.Add(new GeoAnimData(stream));
				}
			}
			else
			{
				AnimData = new List<GeoAnimData>();
			}

			if (nameOffset > 0)
			{
				byte[] str = new byte[255];
				stream.Position = nameOffset;
				TexName = Encoding.UTF8.GetString(str, 0, stream.ReadString(str));
			}
			else
			{
				TexName = string.Empty;
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
				col.Dispose();
			}

			ColList.Clear();

			foreach (GeoAnimData anim in AnimData)
			{
				anim.Dispose();
			}

			AnimData.Clear();
		}
	}
}
