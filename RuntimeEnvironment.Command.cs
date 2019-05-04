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
			/// The data structure for a Command
			/// </summary>
			/// <see cref="Command.Command(Func{MyCommandLine, bool}, int, UpdateType)"/>
			/// <seealso cref="Tick(string, UpdateType, bool)"/>
			public class Command
			{
				public readonly Func<MyCommandLine, bool> Action;
				public readonly int MinumumArguments;
				public readonly UpdateType UpdateType;

				/// <summary>
				/// Constructs a job object
				/// <para>Your Command function will get handed a MyCommandLine argument, which has parsed the argument <c>RuntimeEnvironment.Tick(..)</c> got. Note that the first argument will be the command itself</para>
				/// </summary>
				/// <param name="_Action">The function to be called when the command is encoutnered. Gets a already populated <c>MyCommandLine</c>, which contains the arguments Tick() got</param>
				/// <param name="_MinumumArguments">minumum of ADDITIONAL arguments this command needs. Note: not sanitzed for whitespace or empty additional commands</param>
				/// <param name="_UpdateType">update type the command will be run on. defaults to manually clicking the "run" button</param>
				/// <seealso cref="Tick(string, UpdateType, bool)"/>
				public Command(Func<MyCommandLine, bool> _Action, int _MinumumArguments = 0, UpdateType _UpdateType = UpdateType.Terminal)
				{
					Action = _Action;
					MinumumArguments = _MinumumArguments;
					UpdateType = _UpdateType;
				}
			}
		}
	}
}
