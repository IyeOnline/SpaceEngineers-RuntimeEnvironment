using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
	partial class Program
	{
		public class CachedObject<T>
		{
			public bool Good { get; private set; } = false;
			private readonly Func<T> Setter;
			private T Data;

			public CachedObject(Func<T> _Setter)
			{ Setter = _Setter; }

			public T Get()
			{ if (!Good) Data = Setter(); Good = true; return Data; }

			public void Invalidate()
			{ Good = false; }
		}
	}
}
