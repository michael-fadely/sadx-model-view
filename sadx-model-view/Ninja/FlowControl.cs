namespace sadx_model_view.Ninja
{
	internal static class FlowControl
	{
		public static bool UseMaterialFlags;

		// Equivalent to: _nj_constant_attr_and_
		public static NJD_FLAG AndFlags {get; private set; }
		// Equivalent to: _nj_constant_attr_or_
		public static NJD_FLAG OrFlags { get; private set; }

		static FlowControl()
		{
			Reset();
		}

		public static void Reset()
		{
			AndFlags = (NJD_FLAG)0xFFFFFFFF;
			OrFlags = 0;
			UseMaterialFlags = false;
		}

		public static void Add(NJD_FLAG and, NJD_FLAG or)
		{
			AndFlags |= and;
			OrFlags |= or;
		}

		public static void Remove(NJD_FLAG and, NJD_FLAG or)
		{
			AndFlags &= ~and;
			OrFlags &= ~or;
		}

		public static void Set(NJD_FLAG and, NJD_FLAG or)
		{
			AndFlags = and;
			OrFlags = or;
		}

		public static NJD_FLAG Apply(NJD_FLAG flags)
		{
			return UseMaterialFlags ? OrFlags | (AndFlags & flags) : flags;
		}
	}
}
