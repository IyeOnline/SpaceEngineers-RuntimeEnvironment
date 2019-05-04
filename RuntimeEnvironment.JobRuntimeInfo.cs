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
				public RunningAverage Sum { get; private set; } = new RunningAverage();
				public bool Any { get; private set; }

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
					Average.Set(StateInfo.Average(x => x.Value), StateInfo[0].N);
					Sum.Set(StateInfo.Sum(x => x.Value), StateInfo[0].N);
				}
			}
		}
	}
}