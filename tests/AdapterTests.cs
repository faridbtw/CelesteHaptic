using System;
using System.Reflection;
using Celeste;
using CelesteHaptic;
class AdapterTests {
    static readonly BindingFlags Flags=BindingFlags.Static|BindingFlags.NonPublic;
    static void Set(string name,object value){typeof(Hooks).GetField(name,Flags).SetValue(null,value);}
    static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    static float[] Render(Synth mixer){var block=new float[960];mixer.Render(block,1);return block;}
    static void Main(){
        Settings.Instance=(Settings)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Settings));Settings.Instance.Rumble=RumbleAmount.On;
        Set("connected",true);Set("allowed",true);
        var mixer=(Synth)typeof(Hooks).GetField("Mixer",Flags).GetValue(null);
        Hooks.Rumble(RumbleStrength.Strong,RumbleLength.Short,(int)Effect.Dash);
        Check(mixer.VoiceCount==1,"game enum hook did not queue effect");
        var full=Render(mixer);mixer.Clear();
        Settings.Instance.Rumble=RumbleAmount.Half;
        Hooks.Rumble(RumbleStrength.Strong,RumbleLength.Short,(int)Effect.Dash);
        var half=Render(mixer);
        for(int i=0;i<full.Length;i++)Check(Math.Abs(full[i]*.5-half[i])<.00001,"Half game setting ignored");
        mixer.Clear();Settings.Instance.Rumble=RumbleAmount.Off;
        Hooks.Rumble(RumbleStrength.Strong,RumbleLength.Short,(int)Effect.Dash);
        Check(mixer.VoiceCount==0,"Off game setting ignored");
        Settings.Instance.Rumble=RumbleAmount.On;
        Hooks.RumbleSpecific(.4f,.2f,(int)Effect.Collect);Check(mixer.VoiceCount==1,"specific float hook failed");mixer.Clear();
        Hooks.Rumble(RumbleStrength.Climb,RumbleLength.Short,(int)Effect.Dash);
        Hooks.Rumble(RumbleStrength.Climb,RumbleLength.Short,(int)Effect.Dash);
        Check(mixer.VoiceCount==1,"Climb enum not merged");mixer.Clear();
        // Off lets the original game's fallback execute without hardware initialization.
        Settings.Instance.Rumble=RumbleAmount.Off;Set("connected",false);
        Hooks.Rumble(RumbleStrength.Light,RumbleLength.Short,0);Hooks.RumbleSpecific(.2f,.1f,0);
        Check(mixer.VoiceCount==0,"disconnected path queued effects");
        Console.WriteLine("PASS: real game enums, Half/Off settings, specific rumble, climb merging, disconnected fallback.");
    }
}
