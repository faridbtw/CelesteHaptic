using System;
using System.Collections.Generic;
namespace CelesteHaptic {
    public enum Effect { Pulse, Dash, Impact, Death, Climb, Glide, Collect, Environment }
    // Independent of game APIs so the same synthesis is exercised by the test harness.
    public sealed class Synth {
        sealed class Voice { public Effect Kind; public int Position, End; public float Strength; }
        readonly List<Voice> voices = new List<Voice>();
        readonly object gate = new object();
        public int VoiceCount { get { lock(gate) return voices.Count; } }
        public void Clear() { lock(gate) voices.Clear(); }
        public void Add(Effect kind, float strength, float seconds) {
            if (float.IsNaN(strength) || float.IsInfinity(strength) || float.IsNaN(seconds) || float.IsInfinity(seconds) || strength <= 0 || seconds <= 0) return;
            int frames = (int)(Math.Min(4, seconds) * 48000);
            if (frames < 1) return;
            lock(gate) {
                if (kind == Effect.Climb || kind == Effect.Glide || kind == Effect.Environment) {
                    foreach(var v in voices) if(v.Kind == kind) {
                        v.End = Math.Max(v.End, v.Position + frames); v.Strength = Math.Min(1, strength); return;
                    }
                }
                if (voices.Count >= 24) voices.RemoveAt(0);
                voices.Add(new Voice { Kind=kind, Strength=Math.Min(1,strength), End=frames });
            }
        }
        public void Render(float[] output, float gain) {
            Array.Clear(output,0,output.Length);
            if(float.IsNaN(gain) || float.IsInfinity(gain)) gain=0;
            gain=Math.Max(0, Math.Min(2,gain));
            lock(gate) {
                foreach(var v in voices) {
                    for(int f=0; f<output.Length/2 && v.Position<v.End; f++,v.Position++) {
                        double t=v.Position/48000.0, duration=v.End/48000.0;
                        double envelope=Math.Min(1,t/.003)*Math.Min(1,(duration-t)/.012);
                        double l=0,r=0;
                        switch(v.Kind) {
                            case Effect.Dash:
                                double sweep=2*Math.PI*(220*t-120*t*t);
                                l=Math.Sin(sweep)*Math.Exp(-t*13); r=Math.Sin(sweep+Math.Sin(t*24)*.6)*Math.Exp(-t*10); break;
                            case Effect.Impact:
                                l=Math.Sin(2*Math.PI*75*t)*Math.Exp(-t*17); r=.75*Math.Sin(2*Math.PI*95*t)*Math.Exp(-t*20); break;
                            case Effect.Death:
                                l=Math.Sin(2*Math.PI*62*t)*Math.Exp(-t*5); r=Math.Sin(2*Math.PI*88*t)*Math.Exp(-t*6); break;
                            case Effect.Climb:
                                l=.55*Math.Sin(2*Math.PI*160*t)*(0.65+.35*Math.Cos(2*Math.PI*23*t)); r=.45*Math.Sin(2*Math.PI*185*t); break;
                            case Effect.Glide:
                                l=.65*Math.Sin(2*Math.PI*145*t)*(0.8+.2*Math.Sin(2*Math.PI*8*t)); r=.65*Math.Sin(2*Math.PI*175*t)*(0.8+.2*Math.Cos(2*Math.PI*8*t)); break;
                            case Effect.Collect:
                                double spacing=Math.Min(.075,duration*.42), width=Math.Min(.025,duration*.23);
                                l=Math.Sin(2*Math.PI*240*t)*Math.Exp(-Math.Pow((t-.012)/width,2));
                                r=Math.Sin(2*Math.PI*290*t)*Math.Exp(-Math.Pow((t-spacing)/width,2)); break;
                            case Effect.Environment:
                                l=.7*Math.Sin(2*Math.PI*72*t)*(.7+.3*Math.Sin(2*Math.PI*11*t)); r=.7*Math.Sin(2*Math.PI*103*t)*(.7+.3*Math.Cos(2*Math.PI*13*t)); break;
                            default:
                                l=Math.Sin(2*Math.PI*125*t)*Math.Exp(-t*7); r=Math.Sin(2*Math.PI*155*t)*Math.Exp(-t*7); break;
                        }
                        output[f*2]+=(float)(l*envelope*v.Strength*gain*.8);
                        output[f*2+1]+=(float)(r*envelope*v.Strength*gain*.8);
                    }
                }
                voices.RemoveAll(v=>v.Position>=v.End);
            }
            for(int i=0;i<output.Length;i++) output[i]=Math.Max(-.98f,Math.Min(.98f,output[i]));
        }
    }
}
