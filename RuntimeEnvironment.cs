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
		/// <summary>
		/// class that allows you to schedule jobs with the runtime. refer to the code example for details.
		/// <para>See the Example.cs file for basic information on the usage</para>
		/// </summary>
		/// <seealso cref="Job"/>
        /// <seealso cref="Command"/>
		/// <seealso cref="Tick(string, UpdateType, bool)"/>
		public partial class RuntimeEnvironment
		{
			#region vars
			#region const vars
			const int maxinterval = int.MaxValue - 1;
			const int MaxStoredEvents = 3;
			const double MaxTolerableRuntime = 10;
			public const string SaveStringBegin = "RTENV";
			public const string SaveStringEnd = "VNETR";
			public const char SaveJobSeparator = '\u2194';
			public const char SaveInfoSeparator = ' ';
			const int RTLinesMax = 10000;
			private readonly List<string> ForbiddenJobNames = new List<string>() { "all" }; //strings that are used for some internal commands in the place of jobnames
			private readonly List<string> ForbiddenCommands = new List<string>() { "toggle", "run", "frequency", "evaluate", "reset" }; //commands that are already provided by the environment

			private readonly Dictionary<int, UpdateFrequency> intervalToFrequency = new Dictionary<int, UpdateFrequency>()
			{
				{ 1, UpdateFrequency.Update1 },
				{ 10, UpdateFrequency.Update10 },
				{ 100, UpdateFrequency.Update100 }
			};
			private readonly Dictionary<UpdateFrequency, string> FrequencyToString = new Dictionary<UpdateFrequency, string>()
			{
				{ UpdateFrequency.None, "off"},
				{ UpdateFrequency.Once, "onc"},
				{ UpdateFrequency.Update1, "  1"},
				{ UpdateFrequency.Update10, " 10"},
				{ UpdateFrequency.Update100, "100"},
				{ UpdateFrequency.Update1 | UpdateFrequency.Once, "  1+1"},
				{ UpdateFrequency.Update10 | UpdateFrequency.Once, " 10+1"},
				{ UpdateFrequency.Update100 | UpdateFrequency.Once, "100+1"},
				{ UpdateFrequency.Update1 | UpdateFrequency.Update10 | UpdateFrequency.Once, " 11+1" },
				{ UpdateFrequency.Update1 | UpdateFrequency.Update100 | UpdateFrequency.Once, "101+1" }
			};
			#endregion const vars
			#region readonly vars
			private readonly Dictionary<string, Job> Jobs;
			private Dictionary<string, IEnumerator<bool>> RunningJobs = new Dictionary<string, IEnumerator<bool>>();
			private readonly List<string> JobNames;
			private readonly bool AllowToggle = true;
			private readonly bool AllowFrequencyChange = true;
			private readonly bool EchoState;
			private readonly bool DisplayState;
			private readonly IMyTextSurface PBscreen;
			private readonly IMyTextSurface PBkeyboard;

			private readonly MyCommandLine CommandLine = new MyCommandLine();

			private readonly Dictionary<string, Command> Commands;
			private readonly UpdateType KnownCommandUpdateTypes;

			public MyGridProgram ThisProgram { get; }
			#endregion readonly vars
			#region control vars
			public bool Online { get; private set; } = false; //whether the environment is currently online
			private int CurrentTick = 0; //the current (continous) tick
			private int SymbolTick = 0; //the current tick for the symbol output
			private int interval = maxinterval; //the current min Job requeue interval
			public int CurrentTickrate { get; private set; } //the current update frequency
			private int FastTick = 0; //number of consequtive fast ticks
			private int FastTickMax = 0; //max number of consequtive fast ticks
			private bool firstrun = true;
            private string LastRunJobs = "";
			private bool StoreRT = false;
			private int RTLines = 0;

			public double ContinousTime { get; private set; } = 0;
			public double TimeSinceLastCall { get; private set; } = 0;
			public double LastRuntime { get; private set; } = 0;
			public double MaxRunTime { get; private set; } = 0;
			private RunningAverage _AverageRuntime = new RunningAverage();

			private int commanded = 0;

			private List<string> EvaluateJobList = new List<string>();
			private bool EvaluationMode = false;
			private bool EvaluationDone = true;
			private int EvaluatingState = -1;
			private Dictionary<string, JobRuntimeInfo> AverageJobRuntimes = new Dictionary<string, JobRuntimeInfo>();

			private CachedObject<List<string>> SystemInfoList;
			private CachedObject<List<string>> JobInfoList;
			private Dictionary<int, CachedObject<string>> StatsStrings = new Dictionary<int, CachedObject<string>>();

			public List<string> LastEvents { get; private set; } = new List<string>();
			#endregion control vars
			

			
			#endregion vars

			#region public functions
			/// <summary>
			/// Ctor. call this in your Program Ctor.
			/// </summary>
			/// <param name="_ThisProgram">"<c>this</c>" to hand over a reference to the calling Program</param>
			/// <param name="_Jobs">a dict mapping from a job name to an <c>Job</c>. Mandatory, otherwise this entire class is useless</param>
			/// <param name="_Commands">a dict mapping from a string to an Command. Not mandatory</param>
			/// <param name="_EchoState">whether the enviromnment should echo its state each run.</param>
			/// <param name="_DisplayState">whether the environment should display its state onscreen.</param>
			public RuntimeEnvironment(
				MyGridProgram _ThisProgram,
				Dictionary<string, Job> _Jobs,
				Dictionary<string, Command> _Commands = null,
				bool _EchoState = false,
				bool _DisplayState = false
			)
			{
				ThisProgram = _ThisProgram;
				CurrentTickrate = RateNeededForInterval(interval);
				EchoState = _EchoState;
				DisplayState = _DisplayState;
				if (DisplayState)
				{
					PBscreen = ThisProgram.Me.GetSurface(0);
					PBscreen.ContentType = ContentType.TEXT_AND_IMAGE;
					PBscreen.WriteText("", false);
					PBscreen.Font = "Monospace";
					PBkeyboard = ThisProgram.Me.GetSurface(1);
					PBkeyboard.ContentType = ContentType.TEXT_AND_IMAGE;
					PBkeyboard.WriteText("", false);
					PBkeyboard.Font = "Monospace";
					PBkeyboard.FontSize = 4.5f;
				}

				Output(things:"Creating RuntimeEnvironment...");

				Jobs = _Jobs;
				Output(EndLine: false, things: "  registering jobs...");
				if( !Jobs.Any() )
				{
					Output(EndLine: true, things: " ERROR");
					throw new Exception("No Jobs provided!");
				}
				else
				{ Output(EndLine: true, things: ""); }
				foreach (var job in Jobs)
				{
                    Output(EndLine: false, things: "    " + job.Key);
                    if (ForbiddenJobNames.Any(x => x == job.Key))
					{
                        Output(things: " ERROR");
                        Echo("forbidden job key \"", job.Key, "\" encountered.");
						throw new Exception("forbidden job key \"" + job.Key + "\" encountered.");
					}
                    else
                    { Output(things: " OK"); }

					AverageJobRuntimes.Add(job.Key, new JobRuntimeInfo());

					job.Value.RequeueInterval = SanitizeInterval(job.Value.RequeueInterval);
					AllowFrequencyChange |= job.Value.AllowFrequencyChange;
					AllowToggle |= job.Value.AllowToggle;

					RunningJobs.Add(job.Key, null);
				}
				JobNames = Jobs.Keys.ToList();

				if (_Commands == null)
				{ Commands = new Dictionary<string, Command>(); }
				else
				{ Commands = _Commands; }

				Output(EndLine: false, things: "  registering commands..." );
				if(!Commands.Any())
				{ Output(EndLine: true, things: " None"); }
				else
				{ Output(EndLine: true, things: ""); }
				foreach (var command in Commands.Keys)
				{
                    Output(EndLine: false,things: "    " + command);
                    if (ForbiddenCommands.Any(x => x == command))
					{
                        Output(things: " ERROR");
						Echo("forbidden command key \"", command, "\" encountered.");
                        throw new Exception("forbidden command key \"" + command + "\" encountered.");
					}
                    else
                    { Output(things: " OK"); }
				}

				Commands.Add("run", new Command(CMD_run, 1, UpdateType.Trigger | UpdateType.Terminal));
				Commands.Add("evaluate", new Command(CMD_evaluate, 1, UpdateType.Terminal));
				if (AllowToggle)
				{ Commands.Add("toggle", new Command(CMD_toggle, 0, UpdateType.Trigger | UpdateType.Terminal)); }
				if (AllowFrequencyChange)
				{ Commands.Add("frequency", new Command(CMD_freq, 1)); }
				Commands.Add("reset", new Command(CMD_reset, 0, UpdateType.Trigger | UpdateType.Terminal));

				foreach (var command in Commands.Values)
				{ KnownCommandUpdateTypes |= command.UpdateType; }

				Output(EndLine: false, things: "  building caches..." );
				SystemInfoList = new CachedObject<List<string>>(BuildSystemInfoList);
				JobInfoList = new CachedObject<List<string>>(BuildJobInfoList);
				StatsStrings[0] = new CachedObject<string>(() => BuildStatsString(0));
				StatsStrings[1] = new CachedObject<string>(() => BuildStatsString(1));
				StatsStrings[2] = new CachedObject<string>(() => BuildStatsString(2));
				StatsStrings[3] = new CachedObject<string>(() => BuildStatsString(3));
				StatsStrings[-1] = new CachedObject<string>(() => BuildStatsString(-1));
				StatsStrings[-2] = new CachedObject<string>(() => BuildStatsString(-2));
				StatsStrings[-3] = new CachedObject<string>(() => BuildStatsString(-3));
				Output(EndLine: true, things: "Done!");

                if (DisplayState)
				{
					var textsize = ThisProgram.Me.GetSurface(0).MeasureStringInPixels( new StringBuilder(StatsString(-2)), "Monospace", 1f);
					var screensize = ThisProgram.Me.GetSurface(0).SurfaceSize;
					var xscale = screensize[0] / textsize[0];
					var yscale = screensize[1] / textsize[1];
					ThisProgram.Me.GetSurface(0).FontSize = Math.Min(xscale, yscale);
					ThisProgram.Me.GetSurface(0).Font = "Monospace";
				}

				Output(things: "Done Creating RuntimeEnvironment");
			}

			/// <summary>
			/// Loads the activity state and interval for jobs. <para>Should only be called once in the <c>Program()</c> ctor.</para>
			/// </summary>
			/// <param name="saveString">The <c>Storage</c> string that the PB API provides</param>
			/// <returns>Success. If true, there were no errors during loading.</returns>
            /// <seealso cref="GetSaveString"/>
			public bool LoadFromString( string saveString )
			{
                Output(EndLine:false, things: "Loading..." );
				if (saveString == null || saveString.Length < SaveStringBegin.Length + SaveStringEnd.Length)
				{ Output(EndLine: true, things: "invalid: too short"); return false; }

				int pStart = saveString.IndexOf(SaveStringBegin);
				int pEnd = saveString.LastIndexOf(SaveJobSeparator + SaveStringEnd);
				
				if ( pStart < 0 || pEnd <= 0 )
				{ Output(true, "invalid: coudlnt find tags ( got ",pStart,",",pEnd,")"); return false; }

				pStart += SaveStringBegin.Length;

				string[] states = saveString.Substring(pStart, pEnd - pStart).Split(SaveJobSeparator);

				foreach( string jobstring in states )
				{
					string[] info = jobstring.Split(SaveInfoSeparator);
					int i = 0;
					bool active = false;
                    if ( JobNames.Contains(info[0]) && int.TryParse(info[1], out i ) && bool.TryParse(info[2], out active ) )
                    {
                        Jobs[info[0]].RequeueInterval = i;
                        Jobs[info[0]].active = active;
                    }
                    else
                    {
                        Output(EndLine: true, things: new object[]{"failed at stored info for", info[0], "\n\tEither an unknown job or corrputed data."} );
                        return false;
                    }
				}
                Output(EndLine: true, things: "Done!" );

                UpdateOnline();
				UpdateInterval();

				return true;
			}

			/// <summary>
			/// returns the activity and interval of all jobs encoded in a string that can be used in the <c>Save()</c> function.
			/// <para>Should only be called once in the programs <c>Save()</c> functions</para>
			/// </summary>
			/// <returns>the state of the environment encoded in a string</returns>
            /// <seealso cref="LoadFromString(string)"/>
			public string GetSaveString()
			{
				string saveString = SaveStringBegin;
				foreach( var job in Jobs )
				{
					saveString += job.Key + SaveInfoSeparator + job.Value.RequeueInterval.ToString() + SaveInfoSeparator + job.Value.active.ToString() + SaveJobSeparator;
				}
				return saveString + SaveStringEnd;
			}
			
			/// <summary>
			/// This should be called ONCE in main(). Advances the internal state and runs all jobs that would happen on that tick.
			/// </summary>
			/// <param name="args">the <c>string</c> that <c>Main(string args, UpdateType)</c> gets handed</param>
			/// <param name="updateType">the <c>UpdateType</c> that <c>Main(string args, UpdateType)</c> gets handed</param>
			/// <param name="execute">if false, only the state will be advanced, commands parsed, but no jobs will be excuted</param>
			/// <see cref="RuntimeEnvironment"/>
			public void Tick(string args, UpdateType updateType, bool execute = true)
			{
				if( ( updateType & KnownCommandUpdateTypes) != 0 )
				{ execute &= ParseArgs(args); commanded = 2; }

				if ( (Online && execute ) || EvaluationMode )
				{
					if (!EvaluationMode && CurrentTick % interval == 0)
					{
						foreach (var job in Jobs)
						{
							if (job.Value.active && CurrentTick % job.Value.RequeueInterval == 0)
							{ TryQueueJob(job.Key); }
						}
					}

					ProcessRunningJobs();

					bool hasstates = false;
					foreach (var name in JobNames)
					{
						if ( EvaluationMode || (!Jobs[name].lazy && RunningJobs[name] != null )  )
						{
							hasstates = true;
							++FastTick;
							if(FastTick>FastTickMax)
							{ ++FastTickMax; }
							if ((ThisProgram.Runtime.UpdateFrequency & UpdateFrequency.Update1) == 0 )
							{ ThisProgram.Runtime.UpdateFrequency |= UpdateFrequency.Once; }
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
				LastRuntime = ThisProgram.Runtime.LastRunTimeMs;
				if (commanded == 0)
				{ _AverageRuntime.AddValue( LastRuntime ); }
				TimeSinceLastCall = ThisProgram.Runtime.TimeSinceLastRun.TotalSeconds * 1000 + LastRuntime;
				ContinousTime += TimeSinceLastCall;

				if ( StoreRT )
				{
					WriteOut(ThisProgram.Me.GetSurface(1), null, RTLines++ < RTLinesMax, false, true, LastRuntime.ToString());
					if (RTLines == RTLinesMax)
					{ RTLines = 0; }
				}

				if (EvaluationMode)
				{
					if ( EvaluatingState < 0 && RunningJobs.Values.All(x => x == null) )
					{
						TryQueueJob(EvaluateJobList[0]);
						EvaluatingState = 0;
						EvaluationDone = false;
					}
					else if (EvaluatingState > -1)
					{
						if (EvaluationDone)
						{
							AverageJobRuntimes[EvaluateJobList[0]].Update();
							EvaluateJobList.RemoveAt(0);
							EvaluationMode = EvaluateJobList.Any();
							EvaluatingState = -1;
							StatsStrings[3].Invalidate();
							UpdateOnline();
						}
						else
						{
							AverageJobRuntimes[EvaluateJobList[0]].AddParse(EvaluatingState, LastRuntime);
							++EvaluatingState;
							EvaluationDone = RunningJobs[EvaluateJobList[0]] == null;
						}
					}
				}

				if ( !firstrun && LastRuntime > MaxRunTime )
				{
					if( commanded == 0 )
					{ MaxRunTime = LastRuntime; }

					if( commanded != 0 && (LastRuntime > MaxTolerableRuntime || LastRuntime > MaxRunTime * 3 ) )
					{ SaveEvent(string.Format("Command{0} took {1:0.}ms", execute && RunningJobs.Values.Any(x => x != null)?"+Jobs:("+LastRunJobs+")":"" ,MaxRunTime)); }
					else if( LastRuntime > MaxTolerableRuntime || LastRuntime > MaxRunTime * 1.3 )
					{ SaveEvent(string.Format("Jobs: ({0}) took {1:0.}ms", LastRunJobs, MaxRunTime)); }
				}
				else if ( CurrentTick > 10 )
				{ firstrun = false; }

				if (EchoState)
				{ Echo(StatsString(-1)); }
				if (DisplayState && CurrentTick % 10 == 0 )
				{
					WriteOut(PBscreen, l_surf: null, append: false, EchoOnFail: false, things:StatsString(-2));
					WriteOut(PBkeyboard, things: TickString() + "\n" + TickString() + "\n" + TickString());
				}

				SystemInfoList.Invalidate();
				JobInfoList.Invalidate();
				for(int i = -3; i < 4; ++i )
				{ StatsStrings[i].Invalidate(); }

				commanded -= commanded > 0 ? 1 : 0;
			}

			/// <summary>
			/// toggles the entire env if no args given, toggles name if name given, sets name if name and state given
			/// </summary>
			/// <param name="name">name of the Job</param>
			/// <param name="state">targetstate. 1 for On, 0 for Off, -1 for toggle</param>
			public void TrySetActive(string name = null, int state = -1)
			{
				if (string.IsNullOrEmpty(name))
				{
					if ( state == -1)
					{
						Online = !Online;
						Echo(Online ? "starting..." : "pausing...");
					}
				}
				else if (JobNames.Contains(name) && Jobs[name].AllowToggle )
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

			/// <summary>
			/// Sets the execution interval for a job. The intervall will be sanitized to multiples of the appropriate PB update frequency Only call this if you want to do something special from somewhere else
			/// </summary>
			/// <param name="newinterval">new interval. Will be sanatized</param>
			/// <param name="name">name of the Job. If empty all jobs will be set</param>
			public void TrySetInterval(int newinterval, string name = null)
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
				CommandLine.TryParse(args);

				if ( CommandLine.Argument(0) == null )
				{ return true; }

				bool valid = false;
				bool result = true;
				if ( Commands.Keys.Any(x => x == CommandLine.Argument(0)) )
				{
					if ( Commands[CommandLine.Argument(0)].MinumumArguments < CommandLine.ArgumentCount )
					{
						result = Commands[CommandLine.Argument(0)].Action(CommandLine);
						valid = true;
					}
				}

				SaveEvent( "Command:" + (valid?"":"invalid:") + "\"" + args + "\"");

				return result;
			}

			private void SaveEvent( string s )
			{
				LastEvents.Insert(0, s);
				if(LastEvents.Count > MaxStoredEvents)
				{ LastEvents.RemoveAt(LastEvents.Count-1); }
			}

			private void TryQueueJob(string name)
			{
				if (JobNames.Contains(name) && RunningJobs[name] == null)
				{
					RunningJobs[name] = Jobs[name].Action();
					Online = true;
				}
			}

			public void TryRegisterEvaluation(string name)
			{
				if(JobNames.Contains(name))
				{
					EvaluationMode = true;
					EvaluateJobList.Add(name);
				}
			}

			private void ProcessRunningJobs()
			{
                LastRunJobs = "";
				foreach( var job in JobNames )
				{
					if( RunningJobs[job] != null )
					{
						if( !RunningJobs[job].MoveNext() )
						{
							RunningJobs[job].Dispose();
							RunningJobs[job] = null;
						}
                        LastRunJobs += job + ",";
					}
				}
				LastRunJobs = LastRunJobs.Substring(0, Math.Max(LastRunJobs.Length-1,0));
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
				SetUpdateFrequency();
			}

			private void UpdateInterval( int newinterval = maxinterval )
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
			{ CurrentTick -= CurrentTick % CurrentTickrate; }

			private void SetUpdateFrequency()
			{
				if( Online )
				{
					ThisProgram.Runtime.UpdateFrequency = 
						FastTick>0 && ((ThisProgram.Runtime.UpdateFrequency & UpdateFrequency.Update1) == 0) ? UpdateFrequency.Once :
						intervalToFrequency[CurrentTickrate];
				}
				else
				{ ThisProgram.Runtime.UpdateFrequency = UpdateFrequency.None; }
			}

            private void Output( bool EndLine = true, params object[] things)
			{
				var text = ListToString(things);
				if (EchoState)
				{ Echo(text); }
				if (DisplayState)
				{ WriteOut(ThisProgram.Me.GetSurface(0), l_surf: null, append: true, EchoOnFail: false, EndLine: EndLine, things: text); }
			}
			#endregion private functions

			#region command functions
			private bool CMD_toggle(MyCommandLine commandLine)
			{
				if( commandLine.ArgumentCount == 1 )
				{ TrySetActive(); }
				else if( commandLine.ArgumentCount == 2 )
				{
					if (commandLine.Argument(1) == "all")
					{
						foreach (var name in JobNames)
						{ TrySetActive(name); }
					}
					else
					{
						if( JobNames.Contains(commandLine.Argument(1)) )
						{ TrySetActive(commandLine.Argument(1)); }
					}
				}
				else if (commandLine.ArgumentCount == 3 )
				{
					int state = commandLine.Argument(2) == "" ? -1 :
						commandLine.Argument(2) == "off" ? 0 :
						commandLine.Argument(2) == "on" ? 1 :
						-2;

					if (state != -2)
					{
						if(commandLine.Argument(1) == "all")
						{
							foreach (var name in JobNames)
							{ TrySetActive(name, state); }
						}
						else
						{ TrySetActive(commandLine.Argument(1), state); }
					}
				}
				return true;
			}

			private bool CMD_run(MyCommandLine commandLine)
			{
				if(commandLine.ArgumentCount == 1)
				{ return true; }
				else
				{ TryQueueJob(commandLine.Argument(1)); return true; }
			}

			private bool CMD_freq(MyCommandLine commandLine)
			{
				int i;
				if (commandLine.ArgumentCount > 1 && int.TryParse(commandLine.Argument(1), out i) )
				{ TrySetInterval( i ); }
				else if(commandLine.ArgumentCount > 2 && int.TryParse(commandLine.Argument(2), out i) )
				{
					if (commandLine.Argument(1) == "all")
					{ TrySetInterval(i); }
					else
					{ TrySetInterval(i, commandLine.Argument(1)); }
				}
				return true;
			}

			private bool CMD_evaluate(MyCommandLine commandLine)
			{
				if( commandLine.Argument(1) == "all" )
				{
					foreach( var x in JobNames )
					{ TryRegisterEvaluation(x); }
				}
				else
				{ TryRegisterEvaluation(commandLine.Argument(1)); }

				return true;
			}

			private bool CMD_reset(MyCommandLine commandLine)
			{
				string name = commandLine.Argument(1);
				if ( name == "all")
				{
					foreach (var x in JobNames)
					{ AverageJobRuntimes[x] = new JobRuntimeInfo(); }
				}
				else if ( Jobs.ContainsKey( name ) )
				{ AverageJobRuntimes[name] = new JobRuntimeInfo(); }

				StatsStrings[3].Invalidate();
				StatsStrings[-3].Invalidate();

				return true;
			}
			#endregion command functions

			#region StringHelper
			/// <summary>
			/// a string that will be different every CurrentTick so you can tell the program is still working.
			/// </summary>
			/// <returns>a string that will be different every CurrentTick so you can tell the program is still working.</returns>
			public string TickString()
			{
				if (!Online)
				{ return "--PAUSED--"; }

				int i = SymbolTick % 11;
				return new string('-', i) + "|" + new string('-', 11 - i);
			}

			static public string ListToString(params object[] things)
			{
				if (things.Length == 1 && things[0] is string)
				{ return things[0] as string; }
				else
				{
					const string separator = " ";
					string s = "";
					foreach (var p in things)
					{
						if (p is string)
						{ s += p + separator; }
						else if (p is IEnumerable)
						{
							foreach (var x in p as IList)
							{ s += x.ToString() + separator; }
						}
						else
						{ s += p.ToString() + separator; }
					}
					return s;
				}
			}

			private List<string> BuildSystemInfoList()
			{
				const string fmtstringLong = "{0,-7} {1,4:0.#}";
				const string fmtstringShort = "{0,-4} {1,7:0.}";

				return new List<string>()
				{
					string.Format( fmtstringShort, "Freq",
						FrequencyToString.Keys.Contains(ThisProgram.Runtime.UpdateFrequency) ?
						FrequencyToString[ThisProgram.Runtime.UpdateFrequency] :
						"???"
					),
					string.Format( fmtstringShort, "Tick", CurrentTick),
					string.Format( fmtstringLong, "fast", FastTick.ToString() + "/" + FastTickMax.ToString() ),
					string.Format( fmtstringLong, "Elapsed", (int)TimeSinceLastCall),
					string.Format( fmtstringLong, " max RT", MaxRunTime),
					string.Format( fmtstringLong, " avg RT", _AverageRuntime.Average ),
					string.Format( fmtstringLong, "last RT", LastRuntime)
				};
			}

			private List<string> BuildJobInfoList()
			{
				const string fmtstring = "{0,-6} {1,4:0} {2,2}";

				var res = new List<string>()
				{ string.Format(fmtstring, "name", "freq", "act"), "---------------" };
				foreach (var job in Jobs)
				{
					res.Add(string.Format(fmtstring, job.Key.Substring(0, Math.Min(job.Key.Length, 6)), job.Value.RequeueInterval, (job.Value.active ? (RunningJobs[job.Key] == null ? "-" : "+") : " ")));
				}
				return res;
			}

			private string BuildStatsString( int which = -1 )
			{
				string res = "";
				switch (which)
				{
					case 0:
					{
						res = "___ System ___";
						foreach (var x in SystemInfoList.Value)
						{ res += "\n" + x; }
						break;
					}
					case 1:
					{
						res = "____ Jobs ____";
						foreach (var x in JobInfoList.Value)
						{ res += "\n" + x; }
						break;
					}
					case 2:
					{
						res = "Last Events:";
						foreach( var x in LastEvents )
						{ res += "\n" + x; }
						break;
					}
					case 3:
					{
						const string fmtstringJob = "\n{0,-6} | {1,1}";
						const string fmtstringTime = " | {0,3:0.0} | {1,3:0.0} | {2,-13}";
						res = "Runtimes [ms]:" + string.Format(fmtstringJob + " | {2,3} | {3,3} | {4,-13} ", "job", "N", "avg", "tot", "stages" );

						foreach ( var job in JobNames)
						{
							res += string.Format(fmtstringJob, job.Substring(0, Math.Min(job.Length, 6)), AverageJobRuntimes[job].Total.N);

							if (AverageJobRuntimes[job].Any)
							{
								string tmp = "";
								foreach (var t in AverageJobRuntimes[job].StateInfo )
								{ tmp += string.Format("{0:0.0} ", t.Average); }
								res += string.Format(fmtstringTime, AverageJobRuntimes[job].Average.Average, AverageJobRuntimes[job].Total.Average, tmp );
							}
							else
							{ res += string.Format(fmtstringTime, " ? ", " ? ", " ? "); }
						}
						break;
					}
					case -1:
					{
						res = StatsStrings[0].Value + "\n" + StatsStrings[1].Value + "\n" + StatsStrings[2].Value + "\n" + StatsStrings[3].Value + "\n";
						break;
					}
					case -2:
					{
						var sys = SystemInfoList.Value;
						var job = JobInfoList.Value;
						res = string.Format("{0,10} {1,1}", "", Online ? "Online" : "Offline") + "\n";
						res += string.Format("{0,-12} | {1,-1}", "   System", "    Jobs");
						for (int i = 0; i < Math.Max(sys.Count, job.Count); ++i)
						{ res += string.Format("\n{0,-12} | {1,-1}", i < sys.Count ? sys[i] : "", i < job.Count ? job[i] : ""); }
						res += "\n-------------------------------\n" + StatsStrings[2].Value;
						break;
					}
					case -3:
					{
						res = StatsStrings[-2].Value + "\n-------------------------------\n" + StatsStrings[3].Value;
						break;
					}
					default:
						break;
				}
				return res;
			}

			/// <summary>
			/// a string containing information about the current state of the <c>RuntimeEnvironment</c>
			/// </summary>
			/// <param name="which">which block you want, 0 for system, 1 for jobs, -1 for compact display (monospace LCD), -2 for list (terminal/log)</param>
			/// <returns>the string you wanted</returns>
			public string StatsString(int which = -1)
			{ return StatsStrings[which].Value; }
			#endregion StringHelper

			#region helpers
			/// <summary>
			/// builds a space separated string from all arguments and Echos it. Expands enumerable types, except for string.
			/// </summary>
			/// <param name="things">params object array of what you want to echo. objects should have a ToString() method</param>
			public void Echo(params object[] things)
			{ ThisProgram.Echo( ListToString(things) ); }

			/// <summary>
			/// Writes the object array to the given surfaces. All other arguments are optional
			/// </summary>
			/// <param name="surf">a single surface to write to</param>
			/// <param name="l_surf">a list of surfaces to write to</param>
			/// <param name="append">whether to append to the text already on that surface</param>
			/// <param name="EchoOnFail">whether to Echo if writing to the displays false</param>
			/// <param name="things">the <c>params object[]</c> of stuff to write</param>
			public void WriteOut(IMyTextSurface surf = null, List<IMyTextSurface> l_surf = null, bool append = false, bool EchoOnFail = false, bool EndLine = true, params object[] things )
			{
				var badgroup = l_surf == null || !l_surf.Any();
				string text = ListToString(things) + (EndLine ? "\n" : "");
				foreach (var thing in things)
				{
					if (surf != null)
					{ surf.WriteText(text, append); }
					if (!badgroup)
					{
						foreach (var s in l_surf)
						{ if( s != null ) s.WriteText(text, append); }
					}
					if (EchoOnFail && surf == null && badgroup)
					{ ThisProgram.Echo(text); }
				}
			}

			/// <summary>
			/// Gets all block of a <typeparamref name="Tfind"/> fulfilling <paramref name="conditional"/>. Then constructs a <typeparamref name="Treturn"/> using the static constructor <paramref name="Constructor"/> and returns a list of the results;
			/// </summary>
			/// <typeparam name="Tfind">The type to find</typeparam>
			/// <typeparam name="Treturn">the type to return</typeparam>
			/// <param name="Constructor">a function creating a <typeparamref name="Treturn"/> from a <typeparamref name="Tfind"/></param>
			/// <param name="conditional">an aditional constraint the <typeparamref name="Tfind"/> must match</param>
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