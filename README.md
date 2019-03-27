# Space Engineers Runtime Environment
Always wanted to be able to schedule parts of your script over multiple ticks, but didnt want to spend the time implementing a state machine? 

Always wanted to run your script only every other tick, but not in line with the allowed update frequencies?

Always wanted to allow your script users to use console commands, but were too lazy to implement a rudimentary arg parser? Always wanted your Main() function to be reduced to just one line?

Then fear not, this project is for you!

# When to use it
**Don't** use it if you dont *need* any of its features. A simple script checking the state of your batteries really doesnt need this. 

**Don't** use it just to (try to) increase the performance of your script. If you need performance improvements that bad, you have other issues.

**Do** use it when you want to space out the executions of parts of your script, but dont want to worry about controlling all of that.

# How to use it properly
1. Setup [Malware](https://github.com/malware-dev)'s amazing [Development Kit for SE](https://github.com/malware-dev/MDK-SE/ "MDK-SE")

2. Setup your VS project. Refer to [Malware's guide](https://github.com/malware-dev/MDK-SE/wiki/Getting-Started)

3. Add the Runtime Environment to your project. There is two ways to do that
    * As a subroject. Make sure to not accidentally include the Example as a source. That would be bad. You can just ignore it in VS.
    * By simply adding the source file to your project

4. Take a look at the [example](https://github.com/IyeOnline/SpaceEngineers-RuntimeEnvironment/blob/master/Example/Program.cs) for a quick overview. Check out the [wiki](https://github.com/IyeOnline/SpaceEngineers-RuntimeEnvironment/wiki) for more in depth expalnations

# How to use it improperly
In the case you want to use this, but really dont want to use Malwares amazing development kit, or just dont want to use VS at all, you can still use this framework. 
You just need to copy and paste the right parts of source code into the right places.
