using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
	partial class Program
	{
		public partial class RuntimeEnvironment
		{
			private class JobRuntimeInfo
			{
				public List<RunningAverage> StateInfo { get; private set; } = new List<RunningAverage>();
				public RunningAverage Average { get; private set; } = new RunningAverage();
				public RunningAverage Total { get; private set; } = new RunningAverage();
				public bool Any { get; private set; } = false;

				public int N { get { return StateInfo[0].N; } }

				public void AddParse(int stage, double time)
				{
					Any = true;
					if (stage >= StateInfo.Count)
					{ StateInfo.Add(new RunningAverage(time)); }
					else
					{ StateInfo[stage].AddValue(time); }
				}

				public void Update()
				{
					Average.Set(StateInfo.Average(x => x.Average), N);
					Total.Set(StateInfo.Sum(x => x.Average), N);
				}
			}
		}
	}
}