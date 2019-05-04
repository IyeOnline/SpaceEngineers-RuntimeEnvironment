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
		public class RunningAverage
		{
			public double Value { get; private set; } = 0;
			public int N { get; private set; } = 0;

			public RunningAverage()
			{ }
			public RunningAverage(double _Value, int _N = 1)
			{ N = _N; Value = _Value; }
			public RunningAverage(List<double> l)
			{ N = l.Count; Value = l.Average(); }

			public void AddValue(double value)
			{ Value = (value + N * Value) / ++N; }

			public void Set(double _Value, int _N = 1)
			{ N = _N; Value = _Value; }

			public void Reset()
			{ Value = 0; N = 0; }
		}
	}
}
