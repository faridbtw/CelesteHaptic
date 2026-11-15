using System;
using System.Reflection;
using System.Threading;
using Celeste;
using CelesteHaptic;
class RuntimeTests {
    static void Main(){
        Settings.Instance=(Settings)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Settings));
        Settings.Instance.Rumble=RumbleAmount.On;
        Hooks.Start();
        var field=typeof(Hooks).GetField("connected",BindingFlags.NonPublic|BindingFlags.Static);
        for(int i=0;i<100 && !(bool)field.GetValue(null);i++)Thread.Sleep(100);
        if(!(bool)field.GetValue(null)){Hooks.Shutdown();throw new Exception("Actual runtime did not connect to the physical controller");}
        foreach(Effect e in Enum.GetValues(typeof(Effect))){
            Hooks.Rumble(RumbleStrength.Medium,RumbleLength.Short,(int)e);
            Thread.Sleep(180);
        }
        Hooks.Shutdown();
        Console.WriteLine("PASS: adapter auto-start, real native transport, eight generated effects, shutdown.");
    }
}
