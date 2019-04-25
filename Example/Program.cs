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
	partial class Program : MyGridProgram
	{
		/// <summary>
		/// Your runtime environment. Construction is done in the Program ctor
		/// </summary>
		RuntimeEnvironment Env;

		/// <summary>
		/// the function that will be our job
		/// <para>
		/// Note its return type. This is a statemachine, following Malwares guide on this: 
		/// https://github.com/malware-dev/MDK-SE/wiki/Coroutines---Run-operations-over-multiple-ticks
		/// </para>
		/// long story short, it will "pause" the execution of the job everytime a "yield return true"
		/// </summary>
		public IEnumerator<bool> MyJobFunction()
		{
            Echo("-+-+-");
            yield return true; //the job will be "paused" here until the next tick
            Echo("+-+-+");
            yield return false; //a "yield return false;" will end the job right away. Here it is pointless as the end of the function will be reached.
            //this section of code would never be reached.
            //however, you do not need to end with "yield return false;"
            //once the end of the function body is reached, the job will be done in any case.
        }

        /// <summary>
        /// the function that will be run when we give the command
        /// </summary>
        /// <param name="commandline">A <c>MyCommandLine</c> object, that contains the parsed arguments that Main/Tick got.
        /// <para>See https://github.com/malware-dev/MDK-SE/wiki/VRage.Game.ModAPI.Ingame.Utilities.MyCommandLine </para>
        /// </param>
        /// <returns>whether the script should still execute jobs in this tick</returns>
        public bool MyCommandFunction(MyCommandLine commandline)
		{
			//this is your command functions body. Note that because its decladed in the body of the Program class, it has access to the RuntimeEnvironemnt and can use its members
			Env.Echo("there were", commandline.ArgumentCount, "arguments:");    //This version of echo is capable of expanding argument lists
			foreach (var x in commandline.Items)
			{ Env.Echo(x); }
			return true; //If you return true, the Command will still process any active jobs in the Tick
		}

		/// <summary>
		/// the programs constructor. This is where you want to do all the setup for your runtime environment, preferably all your code.
		/// </summary>
		Program()
		{
			// create a dictionary mapping from a jobs name to job objects
			// the interval will get sanitized to multiples of possible programmable block update frequencies
			var jobDict = new Dictionary<string, RuntimeEnvironment.Job>()
				{ {
					"jobname",						// the name your job uses. used for output.
					new RuntimeEnvironment.Job(
						_Action: MyJobFunction,		// the function to be called when your job is active. Needs to be "state machine compatible"
						_RequeueInterval: 22,		// after how many ticks function will get executed again. this gets sanatized to multiples of 1,10 or 100
						_active: false,				// is your job active from the start?
						_lazy:false,				// if true, your job will not switch the Environment into a fast tick mode (assuming it isnt already at updatefrequency 1)
						_AllowToggle: true,			// is the user allowed to toggle your job off, using the "toggle" command?
						_AllowFrequencyChange: true // is the user allowed to change the frequency of your job using the "frequency" command?
					)
				} };

			// create a dictionary mapping from a commands name (i.e. the part before the first space, if any) to command object
			var commandDict = new Dictionary<string, RuntimeEnvironment.Command>()
				{ {
					"command",								//the string that should be the first part of your command. no spaces allowed
					new RuntimeEnvironment.Command(
						_Action : MyCommandFunction,		// the function to be called when the string is encounted
						_MinumumArguments : 0,				// the minimum number of arguments (apart from itself) your command expectes. 0 is the default
						_UpdateType : UpdateType.Terminal	// the update type your command will be run on. defaults to a user pressing the run button
					)
				} };

			// create the Environment with a reference to the Program
			Env = new RuntimeEnvironment(
				_ThisProgram: this,     //you need to hand over a reference to the calling program so the environment can control frequencies
				_Jobs: jobDict,         //the dictionary you created above
				_Commands: commandDict, //OPTIONAL: the command dict you created above
				_EchoState : true,      //OPTIONAL, default false: whether the enviroment should display its state in the terminal every tick
				_DisplayState: true     //OPTIONAL, default false: whether the enviroment should display its state on the running PBs screen every tick
			);

            //the environment can load its state from the PBs internal Storage String, assuming you did save it.
            Echo("test");
            Env.LoadFromString(Storage);
		}

        /// <summary>
        /// this function is called everytime the world is saved. The <c>Storage</c> string allows you to save the state of your program. The environment provides a way to save and load its state
        /// </summary>
        /// <see cref="RuntimeEnvironment.LoadFromString(string)"/>
        /// <see cref="RuntimeEnvironment.GetSaveString()"/>
        void Save()
        {
            Storage = "some stuff you want to save" + Env.GetSaveString() + "more stuff you want to save"; // The environment does guard its own storage, so you dont have to worry about it getting corrupted.
        }

		/// <summary>
		/// your main function. ideally when using the environment you dont want it to look any different from this.
		/// </summary>
		/// <param name="args">the string the pb gets when run. This will get split by spaces inside of <c>Tick()</c></param>
		/// <param name="updateType"></param>
		void Main(string args, UpdateType updateType)
		{
			// this calls for command and job execution. DO NOT CALL THIS MORE THAN ONCE per call of Main()
			Env.Tick(args, updateType);
		}
	}
}