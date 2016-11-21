using System;
using System.Collections.Generic;
using System.IO;
using sadx_model_view.Ninja;
using System.Text;
using SharpDX.Direct3D9;

namespace sadx_model_view.SA1
{
	[Flags]
	enum LandTableFlags
	{
		Animation   = 0x1,
		TextureList = 0x2,
		TextureName = 0x8,
	}

	class LandTable : IDisposable
	{
		public static int SizeInBytes => 0x24;

		public short COLCount => (short)COL.Count;
		public short AnimCount => (short)AnimData.Count;
		public LandTableFlags Flags;
		public float Unknown_1;
		public readonly List<Col> COL;
		public readonly List<GeoAnimData> AnimData;
		public string TexName;
		public NJS_TEXLIST TexList;
		public int Unknown_2;
		public int Unknown_3;

		public uint TexListPointer;

		public LandTable(Stream stream)
		{
			COL = new List<Col>();
			AnimData = new List<GeoAnimData>();
			TexList = null;

			var buffer = new byte[SizeInBytes];
			stream.Read(buffer, 0, buffer.Length);

			var col_count = BitConverter.ToInt16(buffer, 0x00);
			var anim_count = BitConverter.ToInt16(buffer, 0x02);

			Flags = (LandTableFlags)BitConverter.ToInt32(buffer, 0x04);
			Unknown_1 = BitConverter.ToSingle(buffer, 0x08);

			var col_ptr    = BitConverter.ToUInt32(buffer, 0x0C);
			var anim_ptr   = BitConverter.ToUInt32(buffer, 0x10);
			var name_ptr   = BitConverter.ToUInt32(buffer, 0x14);
			TexListPointer = BitConverter.ToUInt32(buffer, 0x18);

			Unknown_2 = BitConverter.ToInt32(buffer, 0x1C);
			Unknown_3 = BitConverter.ToInt32(buffer, 0x20);

			var position = stream.Position;

			if (col_count > 0 && col_ptr > 0)
			{
				stream.Position = col_ptr;
				for (var i = 0; i < col_count; i++)
				{
					COL.Add(new Col(stream));
				}
			}

			if (anim_count > 0 && anim_ptr > 0)
			{
				stream.Position = anim_ptr;
				for (var i = 0; i < anim_count; i++)
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
			COL       = new List<Col>();
			AnimData  = new List<GeoAnimData>();
			TexName   = string.Empty;
			TexList   = null;
			Unknown_2 = 0;
			Unknown_3 = 0;
		}

		~LandTable()
		{
			Dispose();
		}

		public void Dispose()
		{
			foreach (var c in COL)
			{
				c.Dispose();
			}

			COL.Clear();
		}

		public void CommitVertexBuffer(Device device)
		{
			foreach (var c in COL)
			{
				c.CommitVertexBuffer(device);
			}
		}

		public void Draw(Device device, ref Camera camera)
		{
			foreach (var c in COL)
			{
				c.Draw(device, ref camera);
			}
		}

		public void Sort()
		{
			foreach (var c in COL)
			{
				c.Sort();
			}
		}
	}
}
