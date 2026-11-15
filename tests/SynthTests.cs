using System;
using System.IO;
using CelesteHaptic;
class Tests {
    static void Check(bool condition,string reason){if(!condition)throw new Exception(reason);}
    static double Energy(float[] a){double sum=0;foreach(float x in a){Check(!float.IsNaN(x)&&!float.IsInfinity(x)&&Math.Abs(x)<=.981,"invalid/unsafe sample");sum+=x*x;}return sum;}
    static void Main(){
        var block=new float[960];var s=new Synth();
        s.Render(block,1);Check(Energy(block)==0,"idle must be silent");
        foreach(Effect kind in Enum.GetValues(typeof(Effect))){
            s.Clear();s.Add(kind,1,.25f);double energy=0,difference=0;
            for(int b=0;b<30;b++){s.Render(block,1);energy+=Energy(block);for(int f=0;f<480;f++)difference+=Math.Abs(block[f*2]-block[f*2+1]);}
            Check(energy>1&&difference>1,"effect lacks signal/stereo: "+kind);Check(s.VoiceCount==0,"effect did not expire");
            s.Render(block,1);Check(Energy(block)==0,"tail leaked");
        }
        var full=new float[960];var half=new float[960];
        s.Add(Effect.Impact,1,.1f);s.Render(full,1);s.Clear();s.Add(Effect.Impact,.5f,.1f);s.Render(half,1);
        for(int i=0;i<960;i++)Check(Math.Abs(full[i]*.5-half[i])<.00001,"half setting not linear");
        s.Clear();for(int i=0;i<120;i++){s.Add(Effect.Climb,.1f,.1f);s.Render(block,1);Check(s.VoiceCount==1,"continuous effect stacked");}
        for(int i=0;i<15;i++)s.Render(block,1);Check(s.VoiceCount==0,"continuous effect stuck");
        for(int i=0;i<100;i++)s.Add(Effect.Death,1,1);Check(s.VoiceCount==24,"unbounded voices");s.Render(block,2);Energy(block);
        s.Clear();s.Add(Effect.Pulse,float.NaN,1);s.Add(Effect.Pulse,1,float.PositiveInfinity);s.Add(Effect.Pulse,-1,1);s.Add(Effect.Pulse,1,-1);Check(s.VoiceCount==0,"invalid event accepted");
        s.Add(Effect.Pulse,1,.1f);s.Render(block,float.NaN);Check(Energy(block)==0,"invalid gain emitted samples");
        Console.WriteLine("PASS: all eight effects, stereo, silence, expiry, half intensity, repeated events, voice limits, invalid input, peak limits.");
    }
}
