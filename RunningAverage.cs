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
			public double Sum { get; private set; } = 0;
			public double Average { get { return Sum / N; } }
			public int N { get; private set; } = 0;

			public RunningAverage()
			{ }
			public RunningAverage(double _Sum, int _N = 1)
			{ N = _N; Sum = _Sum; }
			public RunningAverage(List<double> l)
			{ N = l.Count; Sum = l.Sum(); }

			public void AddValue(double value)
			{ Sum += value; ++N; }

			public void Set(double _Sum, int _N = 1)
			{ N = _N; Sum = _Sum; }

			public void Reset()
			{ Sum = 0; N = 0; }
		}
	}
}
