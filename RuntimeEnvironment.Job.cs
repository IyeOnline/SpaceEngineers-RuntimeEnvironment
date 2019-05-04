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
		public partial class RuntimeEnvironment
		{
			/// <summary>
			/// The data structure for a job
			/// </summary>
			/// <see cref="Job.Job(Func{IEnumerator{bool}}, int, bool, bool, bool, bool)"/>
			/// <seealso cref="Tick(string, UpdateType, bool)"/>
			public class Job
			{
				public readonly Func<IEnumerator<bool>> Action;
				public int RequeueInterval;
				public bool active;
				public readonly bool lazy;
				public readonly bool AllowToggle;
				public readonly bool AllowFrequencyChange;

				/// <summary>Construts a job object</summary>
				/// <param name="_Action">
				/// a statemachine
				/// <para>use <c>yield return true;</c> everytime you want it to wait for the next tick</para>
				/// </param>
				/// <param name="_RequeueInterval">
				/// interval between how often the job will be requeued. Note this is server ticks, not PB executes
				/// <para>NOTE: if this is smaller than the number of states the <paramref name="_Action"/> has, it will only be queued once the next interval is hit.</para>
				/// <para>Will be sanatized to a reasonable multiple of possible PB update Frequencies N*(1,10,100)</para>
				/// </param>
				/// <param name="_active">whether the job should be active from the start</param>
				/// <param name="_lazy">if true, your job will not switch the environment into fast tick mode, but instead space your job out</param>
				/// <param name="_AllowToggle">whether the user should be allowed to use the command "toggle" to turn this job on or off.</param>
				/// <param name="_AllowFrequencyChange">whether the user should be allowed to use the command "frequency" to change the requeue interval of this job.</param>
				/// <seealso cref="Tick(string, UpdateType, bool)"/>
				public Job(Func<IEnumerator<bool>> _Action, int _RequeueInterval, bool _active = true, bool _lazy = true, bool _AllowToggle = true, bool _AllowFrequencyChange = true)
				{
					Action = _Action;
					RequeueInterval = _RequeueInterval;
					active = _active;
					lazy = _lazy;
					AllowToggle = _AllowToggle;
					AllowFrequencyChange = _AllowFrequencyChange;
				}
			}
		}
	}
}
