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
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
	partial class Program
	{
		/// <summary>
		/// class that allows you to schedule jobs with the runtime. refer to the code example for details.
		/// <para>See the Example.cs file for basic information on the usage</para>
		/// </summary>
		/// <seealso cref="Job"/>
		/// <seealso cref="CurrentTick(string, UpdateType, bool)"/>
		public class RuntimeEnvironment
		{
			const int maxinterval = int.MaxValue;
			private readonly List<string> ForbiddenJobNames = new List<string>(){ "all" }; //strings that are used for some internal commands in the place of jobnames
			private readonly List<string> ForbiddenCommands = new List<string>(){ "toggle", "run", "frequency" }; //commands that are already provided by the environment

			public bool Online { get; private set; } = false;
			private int CurrentTick = 0;
			private int SymbolTick = 0;
			private int interval = maxinterval;
			public int CurrentTickrate { get; private set; }
			private int FastTick = 0;
			private int FastTickMax = 0;

			public double ContinousTime { get; private set; } = 0;
			public double TimeSinceLastCall { get; private set; } = 0;
			public double LastRuntime { get; private set; } = 0;

			private readonly Dictionary<string, Job> Jobs;
			private Dictionary<string, IEnumerator<bool>> RunningJobs = new Dictionary<string, IEnumerator<bool>>();
			private readonly List<string> JobNames;
			private readonly bool AllowToggle = true;
			private readonly bool AllowFrequencyChange = true;

			private readonly bool EchoState;

			private readonly Dictionary<string, Command> Commands;
			private readonly UpdateType KnownCommandUpdateTypes;

			public MyGridProgram ThisProgram { get; }

			private static readonly Dictionary<int, UpdateFrequency> intervalToFrequency = new Dictionary<int, UpdateFrequency>()
			{
				{ 1, UpdateFrequency.Update1 },
				{ 10, UpdateFrequency.Update10 },
				{ 100, UpdateFrequency.Update100 }
			};

			private static readonly Dictionary<UpdateFrequency, string> FrequencyToString = new Dictionary<UpdateFrequency, string>()
			{
				{ UpdateFrequency.None, "off"},
				{ UpdateFrequency.Once, "onc"},
				{ UpdateFrequency.Update1, "  1"},
				{ UpdateFrequency.Update10, " 10"},
				{ UpdateFrequency.Update100, "100"},
				{ UpdateFrequency.Update1 | UpdateFrequency.Once, "  1 + 1"},
				{ UpdateFrequency.Update10 | UpdateFrequency.Once, " 10 + 1"},
				{ UpdateFrequency.Update100 | UpdateFrequency.Once, "100 + 1"},
				{ UpdateFrequency.Update1 | UpdateFrequency.Update10 | UpdateFrequency.Once, " 11 + 1" },
				{ UpdateFrequency.Update1 | UpdateFrequency.Update100 | UpdateFrequency.Once, "101 + 1" }
			};

			#region classes
			/// <summary>
			/// The data structure for a job
			/// </summary>
			/// <see cref="Job(Action, int, bool)"/>
			/// <seealso cref="Tick(string, UpdateType, bool)"/>
			public class Job
			{
				public readonly Func<IEnumerator<bool>> Action;
				public int RequeueInterval;
				public bool active;
				public readonly bool AllowToggle;
				public readonly bool AllowFrequencyChange;

				///<summary>Construts a job object</summary>
				///<param name="_Action">
				///a statemachine
				///<para>use <c>yield return true;</c> everytime you want it to wait for the next tick</para>
				/// </param>
				///<param name="_RequeueInterval">
				///interval between how often the job will be requeued. Note this is server ticks, not PB executes
				///<para>NOTE: if this is smaller than the number of states the <paramref name="_Action"/> has, it will only be queued once the next interval is hit.</para>
				///<para>Will be sanatized to a reasonable multiple of possible PB update Frequencies N*(1,10,100)</para>
				///</param>
				///<param name="_active">whether the job should be active from the start</param>
				///<param name="_AllowToggle">whether the user should be allowed to use the command "toggle" to turn this job on or off.</param>
				///<param name="_AllowFrequencyChange">whether the user should be allowed to use the command "frequency" to change the requeue interval of this job.</param>
				///<seealso cref="Tick(string, UpdateType, bool)"/>
				public Job( Func<IEnumerator<bool>> _Action, int _RequeueInterval, bool _active, bool _AllowToggle = true, bool _AllowFrequencyChange = true )
				{
					Action = _Action;
					RequeueInterval = _RequeueInterval;
					active = _active;
					AllowToggle = _AllowToggle;
					AllowFrequencyChange = _AllowFrequencyChange;	
				}
			}

			/// <summary>
			/// The data structure for a Command
			/// </summary>
			/// <see cref="Command.Command(Func{string[], bool}, int, UpdateType)"/>
			/// <seealso cref="Tick(string, UpdateType, bool)"/>
			public class Command
			{
				public readonly Func<string[], bool> Action;
				public readonly int MinumumArguments;
				public readonly UpdateType UpdateType;

				/// <summary>
				/// Constructs a job object
				/// <para>Will get handed an string array which is the argument string <c>RuntimeEnvironment.Tick(..)</c> got. Note that the first argument will be the command itself</para>
				/// </summary>
				/// <param name="_Action">The function to be called when the command is encoutnered. Gets a <c>string[]</c> which is the space separated commands the PB got</param>
				/// <param name="_MinumumArguments">minumum of ADDITIONAL arguments this command needs. Note: not sanitzed for whitespace or empty additional commands</param>
				/// <param name="_UpdateType">update type the command will be run on. defaults to manually clicking the "run" button</param>
				/// <seealso cref="Tick(string, UpdateType, bool)"/>
				public Command( Func<string[], bool> _Action, int _MinumumArguments = 0, UpdateType _UpdateType = UpdateType.Terminal )
				{
					Action = _Action;
					MinumumArguments = _MinumumArguments;
					UpdateType = _UpdateType;
				}
			}
			#endregion classes

			#region public functions
			///<summary>
			///Ctor. call this in your Program Ctor.
			///</summary>
			///<param name="_ThisProgram"><c>this</c> to hand over a reference to the calling Program</param>
			///<param name="_Jobs">a dict mapping from a job name to an <c>Job</c>. Mandatory, otherwise this entire class is useless</param>
			///<param name="_Commands">a dict mapping from a string to an Command. Not mandatory</param>
			public RuntimeEnvironment(
				MyGridProgram _ThisProgram,
				Dictionary<string, Job> _Jobs,
				Dictionary<string, Command> _Commands = null,
				bool _EchoState = false
			)
			{
				ThisProgram = _ThisProgram;
				CurrentTickrate = RateNeededForInterval(interval);
				EchoState = _EchoState;
				
				if(EchoState)
				{ Echo("Creating RuntimeEnvironment..."); }
				
				Jobs = _Jobs;
				if (EchoState)
				{ Echo("registered", Jobs.Count, "jobs"); }
				foreach( var job in Jobs )
				{
					if (ForbiddenJobNames.Any( x => x == job.Key ))
					{
						Echo("forbidden job key \"", job.Key, "\" encountered.");
						throw new ArgumentException();
					}
					job.Value.RequeueInterval = SanitizeInterval(job.Value.RequeueInterval);
					AllowFrequencyChange &= job.Value.AllowFrequencyChange;
					AllowToggle &= job.Value.AllowToggle;

					RunningJobs.Add(job.Key, null);
					if(EchoState)
					{ Echo("   ", job.Key); }
				}
				JobNames = Jobs.Keys.ToList();
				
				if( _Commands == null)
				{ Commands = new Dictionary<string, Command>(); }
				else
				{ Commands = _Commands; }
				
				if(EchoState)
				{ Echo("registered", Commands.Count, "commands"); }
				foreach (var command in Commands.Keys)
				{
					if (ForbiddenCommands.Any(x => x == command))
					{
						Echo("forbidden command key \"", command, "\" encountered.");
						throw new ArgumentException();
					}
					if(EchoState)
					{ Echo("   ", command); }
					
				}
				
				if(AllowToggle)
				{ Commands.Add("toggle", new Command(CMD_toggle)); }
				if(AllowFrequencyChange)
				{ Commands.Add("frequency", new Command(CMD_freq)); }

				Commands.Add("run", new Command(CMD_run,1));
				
				
				foreach(var command in Commands.Values)
				{
					KnownCommandUpdateTypes |= command.UpdateType;
				}
				
				if(EchoState)
				{ Echo("Done Creating RuntimeEnvironment"); }
			}
			
			///<summary>
			///This should be called ONCE in main(). Advances the internal state and runs all jobs that would happen on that tick.
			///</summary>
			///<param name="args">the <c>string</c> that <c>Main(string args, UpdateType)</c> gets handed</param>
			///<param name="updateType">the <c>UpdateType</c> that <c>Main(string args, UpdateType)</c> gets handed</param>
			///<param name="execute">if false, only the state will be advanced but no jobs will be excuted</param>
			///<see cref="RuntimeEnvironment"/>
			public void Tick(string args, UpdateType updateType, bool execute = true)
			{
				if( (updateType & KnownCommandUpdateTypes) != 0 )
				{
					execute &= ParseArgs(args);
				}

				if (JobNames.Count > 0)
				{

					if (Online && execute)
					{
						if (CurrentTick % interval == 0)
						{
							foreach (var job in Jobs)
							{
								if (job.Value.active && CurrentTick % job.Value.RequeueInterval == 0)
								{
									TryQueueJob(job.Key);
								}
							}
						}

						ProcessRunningJobs();

						bool hasstates = false;
						foreach (var name in JobNames)
						{
							if (RunningJobs[name] != null)
							{
								++FastTick;
								if(FastTick>FastTickMax)
								{ ++FastTickMax; }
								hasstates = true;
								ThisProgram.Runtime.UpdateFrequency |= UpdateFrequency.Once;
								break;
							}
						}

						if (!hasstates)
						{
							FastTick = 0;
							SyncTick();
							UpdateOnline();
						}
					}

					CurrentTick += FastTick > 0 ? 1 : CurrentTickrate;
					++SymbolTick;
					LastRuntime = ThisProgram.Runtime.LastRunTimeMs / 1000;
					TimeSinceLastCall = ThisProgram.Runtime.TimeSinceLastRun.TotalSeconds + LastRuntime;
					ContinousTime += TimeSinceLastCall;

					if (EchoState)
					{
						ThisProgram.Echo(TickString());
						ThisProgram.Echo(StatsString());
					}
				}
			}

			///<summary>
			///toggles the entire env if no args given, toggles name if name given, sets name if name and state given
			///</summary>
			///<param name="name">name of the Job</param>
			///<param name="state">targetstate. 1 for On, 0 for Off, -1 for toggle</param>
			public void SetActive(string name = "", int state = -1)
			{
				if (string.IsNullOrEmpty(name) && state == -1)
				{
					Online = !Online;
					Echo(Online ? "starting..." : "pausing...");
				}
				else if (Jobs.ContainsKey(name) && Jobs[name].AllowToggle )
				{
					var active = state == -1 ? !Jobs[name].active : state != 0;
					Jobs[name].active = active;
					Online |= active;
				}
				if (Online && !Jobs.Values.Any(x => x.active))
				{
					Online = false;
					Echo("paused because there is no active job");
				}
				UpdateInterval();
			}

			///<summary>
			///Sets the execution interval for a job. The intervall will be sanitized to multiples of the appropriate PB update frequency Only call this if you want to do something special from somewhere else
			///</summary>
			///<param name="newinterval">new interval. Will be sanatized</param>
			///<param name="name">name of the Job. If empty all jobs will be set</param>
			public void SetInterval(int newinterval, string name = "")
			{
				newinterval = SanitizeInterval(newinterval);
				//update tickrate for jobs
				if (string.IsNullOrEmpty(name))
				{
					foreach (var jname in JobNames)
					{ if (Jobs[jname].AllowFrequencyChange) { Jobs[jname].RequeueInterval = newinterval; } }
				}
				else if (Jobs.ContainsKey(name) && Jobs[name].AllowFrequencyChange )
				{ Jobs[name].RequeueInterval = newinterval; }
				
				UpdateInterval(newinterval);
			}
			#endregion public functions

			#region private functions
			private bool ParseArgs(string args)
			{
				if (string.IsNullOrEmpty(args))
				{ return true; }

				var substrings = args.Split(' ');

				if ( Commands.Keys.Any(x => x == substrings[0]) )
				{
					if ( Commands[substrings[0]].MinumumArguments < substrings.Length )
					{
						return Commands[substrings[0]].Action(substrings);
					}
				}
				return true;
			}

			private void TryQueueJob(string name)
			{
				if (Jobs.ContainsKey(name))
				{
					if (RunningJobs[name] == null)
					{
						RunningJobs[name] = Jobs[name].Action();
						Online = true;
					}
				}
			}

			private void ProcessRunningJobs()
			{
				foreach( var job in JobNames )
				{
					if( RunningJobs[job] != null )
					{
						if( !RunningJobs[job].MoveNext() )
						{
							RunningJobs[job].Dispose();
							RunningJobs[job] = null;
						}
					}
				}
			}

			private static int SanitizeInterval(int interval)
			{
				var oom = Math.Floor(Math.Log10(interval+1));
				oom = oom < 3 ? oom : 2;
				var factor = Math.Round(interval / Math.Pow(10,oom));
				return (int)(factor * Math.Pow(10, oom));
			}

			private static int RateNeededForInterval(int interval)
			{
				var oom = Math.Floor(Math.Log10(interval + 1));
				int res = (int)Math.Pow(10, oom);
				return res<100?res:100;
			}

			private void UpdateOnline()
			{
				Online = Jobs.Values.Any(x => x.active);
				//ThisProgram.Runtime.UpdateFrequency = Online ? intervalToFrequency[RateNeededForInterval(CurrentTickrate - FastTick)] : UpdateFrequency.None;
				SetUpdateFrequency();
			}

			private void UpdateInterval( int newinterval = maxinterval)
			{
				foreach( var job in Jobs.Values )
				{
					if( job.active && newinterval > job.RequeueInterval)
					{ newinterval = job.RequeueInterval; }
				}

				SyncTick();
				interval = newinterval;
				CurrentTickrate = RateNeededForInterval(newinterval);
				SetUpdateFrequency();
			}

			private void SyncTick()
			{
				CurrentTick -= CurrentTick % CurrentTickrate;
			}

			private void SetUpdateFrequency()
			{
				if( Online )
				{
					ThisProgram.Runtime.UpdateFrequency = FastTick>0 ? UpdateFrequency.Once : intervalToFrequency[CurrentTickrate];
				}
				else
				{ ThisProgram.Runtime.UpdateFrequency = UpdateFrequency.None; }
			}

			#region commands
			private bool CMD_toggle(string[] args)
			{
				if( args.Length == 1 || string.IsNullOrWhiteSpace(args[1]) )
				{ SetActive(); }
				else if( args.Length == 2 || string.IsNullOrWhiteSpace(args[2]) )
				{
					if (args[1] == "all")
					{
						foreach (var name in JobNames)
						{ SetActive(name); }
					}
					else
					{
						if( JobNames.Contains(args[1]) && Jobs[args[1]].AllowToggle )
						{ SetActive(args[1]); }
					}
				}
				else if(args.Length == 3 )
				{
					int state = args[2] == "" ? -1 :
						args[2] == "off" ? 0 :
						args[2] == "on" ? 1 :
						-2;

					if (state != -2)
					{
						if( args[1] == "all")
						{
							foreach (var name in JobNames)
							{ SetActive(name, state); }
						}
						else
						{ SetActive(args[1], state); }
					}
				}
				return true;
			}

			private bool CMD_run(string[] args)
			{
				if(args.Length==1)
				{ return true; }
				else
				{ TryQueueJob(args[1]); return true; }
			}

			private bool CMD_freq(string[] args)
			{
				int i;
				if ( args.Length > 1 && int.TryParse(args[1], out i) )
				{ SetInterval( i ); }
				else if(args.Length > 2 && int.TryParse(args[2], out i) )
				{
					if (args[1] == "all")
					{ SetInterval(i); }
					else
					{ SetInterval(i, args[1]); }
				}

				return true;
			}
			#endregion commands

			#endregion private functions

			#region infostrings
			///<summary>
			///a string that will be different every CurrentTick so you can tell the program is still working.
			///</summary>
			///<returns>a string that will be different every CurrentTick so you can tell the program is still working.</returns>
			public string TickString()
			{
				if (!Online)
				{ return "--PAUSED--"; }
				switch (SymbolTick % 10)
				{
					case 0: return "|---------";
					case 1: return "-|--------";
					case 2: return "--|-------";
					case 3: return "---|------";
					case 4: return "----|-----";
					case 5: return "-----|----";
					case 6: return "------|---";
					case 7: return "-------|--";
					case 8: return "--------|-";
					case 9: return "---------|";
					default: return "the static analyzer is stupid";
				}
			}

			/// <summary>
			/// a string containing information about the current state of the <c>RuntimeEnvironment</c>
			/// </summary>
			/// <returns></returns>
			public string StatsString()
			{
				string res = "UpdateFrequency: " + (
					FrequencyToString.Keys.Contains(ThisProgram.Runtime.UpdateFrequency) ?
					FrequencyToString[ThisProgram.Runtime.UpdateFrequency]:
					"???" )
					+ "\n";
				//string res = "UpdateFrequency: " + FrequencyToString[ThisProgram.Runtime.UpdateFrequency] + "\n";
				res += "fast tick: " + FastTick.ToString() + " (" + FastTickMax.ToString() + ")\n";
				res += "a job every " + interval.ToString() + " serverticks\n";
				res += string.Format("since last call: {0:.###}s\n", TimeSinceLastCall);
				res += string.Format("last runtime: {0:.0###}s\n", LastRuntime);
				res += "Jobs:\n----------------\n";
				const string fmtstring = "{0,-8} {1,3:0} {2,3} {3,1}\n";

				res += string.Format(fmtstring, "name", "int", "Act", "?");
				foreach( var job in Jobs )
				{
					res += string.Format(fmtstring, job.Key, job.Value.RequeueInterval, (job.Value.active ? "On" : "Off"), RunningJobs[job.Key] == null ? "-":"+" );
				}
				return res;
			}
			#endregion inforstrings

			#region helpers
			///<summary>
			///builds a space separated string from all arguments and Echos it. Expands enumerable types, except for string.
			///</summary>
			///<param name="args">params object array of what you want to echo. objects should have a ToString() method</param>
			public void Echo(params object[] args)
			{
				const string separator = " ";
				string s = "";
				foreach (var p in args)
				{
					if( p is string )
					{
						s += p + separator;
					}
					else if ( p is IEnumerable )
					{
						foreach (var x in p as IList)
						{ s += x.ToString() + separator; }
					}
					else
					{
						s += p.ToString() + separator;
					}
				}
				ThisProgram.Echo(s);
			}

			///<summary>
			///Writes a string to displays. Will write to both display and group if given
			///</summary>
			///<param name="text">The text to write</param>
			///<param name="display">A singular display to write to</param>
			///<param name="group">A group of displays to write to</param>
			///<param name="append">whether to append to the display(s)</param>
			///<param name="EchoOnFail">whether to Echo <paramref name="text"/> if neither <paramref name="display"/> nor <paramref name="group"/> were valid/given</param>
			public void WriteOut<T>(string text, IMyTextPanel display = null, List<T> group = null, bool append = false, bool EchoOnFail = false)
				where T : IMyTerminalBlock
			{
				var badgroup = group == null && !group.Any();
				if (display != null)
				{
					display.WritePublicText(text, append);
				}
				if (!badgroup)
				{
					foreach (IMyTextPanel p in group)
					{
						p.WritePublicText(text, append);
					}
				}
				if (EchoOnFail && display == null && badgroup)
				{
					ThisProgram.Echo(text);
				}
			}

			/// <summary>
			/// Wrapper around <c>RuntimeEnvironment.ThisProgram.GridTerminalSystem.GetBlocksOfType<typeparamref name="T"/>(<paramref name="L"/>,<paramref name="conditional"/>)</c>
			/// </summary>
			/// <typeparam name="T">Block Type to fetch</typeparam>
			/// <param name="L">output List</param>
			/// <param name="conditional">filter</param>
			public void GetBlocksOfType<T>(List<T> L, Func<T, bool> conditional = null)
				where T : class, IMyTerminalBlock
			{
				ThisProgram.GridTerminalSystem.GetBlocksOfType(L, conditional);
			}

			/// <summary>
			/// Wrapper around <c>RuntimeEnvironment.ThisProgram.GridTerminalSystem.GetBlockGroups(<paramref name="L"/>,<paramref name="conditional"/>)</c>
			/// </summary>
			/// <param name="L">output List</param>
			/// <param name="contitional">filter</param>
			public void GetBlockGroups(List<IMyBlockGroup> L, Func<IMyBlockGroup, bool> contitional = null)
			{
				ThisProgram.GridTerminalSystem.GetBlockGroups(L, contitional);
			}

			/// <summary>
			/// Gets all block of a <typeparamref name="Tfind"/> fulfilling <paramref name="conditional"/>. Then constructs a <typeparamref name="Treturn"/> using the static constructor <paramref name="Constructor"/> and returns a list of the results;
			/// </summary>
			/// <typeparam name="Tfind">The type to find</typeparam>
			/// <typeparam name="Treturn">the type to return</typeparam>
			/// <param name="Constructor">a function creating a <typeparamref name="Treturn"/> from a <typeparamref name="Tfind"/></param>
			/// <param name="conditional">an aditional constraing the valid <typeparamref name="Tfind"/> must match</param>
			/// <returns>All objects matching</returns>
			public List<Treturn> FetchAndConstruct<Tfind, Treturn>( Func<Tfind, Treturn> Constructor, Func<Tfind, bool> conditional = null)
				where Tfind : class, IMyTerminalBlock
				where Treturn : class
			{
				var tmp = new List<Tfind>();
				ThisProgram.GridTerminalSystem.GetBlocksOfType(tmp, conditional);
				var res = new List<Treturn>();
				foreach (var x in tmp)
				{ res.Add(Constructor(x)); }

				return res;
			}
			#endregion helpers
		}
	}
}