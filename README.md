# Space Engineers Runtime Environment
Always wanted to be able to schedule parts of your script over multiple ticks, but didnt want to spend the time implementing a state machine? 

Always wanted to run your script only every other tick, but not in line with the allowed update frequencies?

Always wanted to allow your script users to use console commands, but were too lazy to implement a rudimentary arg parser? 

Always wanted your `Main()` function to be reduced to just one line?

Then fear not, this project is for you!

# What it does for you
* Gives you the ability to call functions in your code only on multiples of those allowed by `UpdateFrequency`. It will select the `UpdateFrequency` accordingly.

* Allows you to easily run [state machines](https://github.com/malware-dev/MDK-SE/wiki/Coroutines---Run-operations-over-multiple-ticks).

* Gives your script user access to commands to control the frequency and activity of your jobs, if you choose to allow that.

All in all, it should help you reduce the performance impact of your script.

# When to use it
**Do** use it when you want to space out the executions of parts of your script, but dont want to worry about controlling all of that.

**Don't** use it if you dont *need* any of its features. A simple script checking the state of your batteries really doesnt need this. You can of course still use it, e.g. if you plan on expaning your script to something sizable, but you wont really get any advantage from it in those early cases.

# How to use it properly
1. Setup [Malware](https://github.com/malware-dev)'s amazing [Development Kit for SE](https://github.com/malware-dev/MDK-SE/ "MDK-SE")

2. Setup your VS project. Refer to [Malware's guide](https://github.com/malware-dev/MDK-SE/wiki/Getting-Started)

3. Add the Runtime Environment to your project. There is two ways to do that:
    * As a shared project. The VS files for the shared project are included in the repo. Again, Malware has a [guide](https://github.com/malware-dev/MDK-SE/wiki/Mixin-Projects) on this. <br> Just note that you want to add an existing item instead of a new shared project. Select the `SpaceEngineers-RuntimeEnvironment.shproj` file.
    * By simply adding the all the source files to your project

4. Use it in your `Program.cs`. Take a look at the [example program](https://github.com/IyeOnline/SpaceEngineers-RuntimeEnvironment/blob/master/Example/Program.cs) for a quick overview, or check out the [wiki](https://github.com/IyeOnline/SpaceEngineers-RuntimeEnvironment/wiki) for more in depth expalnations.

# How to use it improperly
In the case you want to use this, but really dont want to use Malwares amazing development kit, or just dont want to use VS at all, you can still use this framework. 
You just need to copy and paste the right parts of source code into the right places. I will leave the ugly details to you.
)

# Feature requests & bug reports
If you have a good idea what this lib could need, or just had throw an exception in your face, open up an [issue](https://github.com/IyeOnline/SpaceEngineers-RuntimeEnvironment/issues).
Please provide a minimal "working" example of your problem, or a pseudocode draft of your suggestion.
