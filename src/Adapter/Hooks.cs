using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Celeste;
using Monocle;
[assembly:System.Reflection.AssemblyVersion("1.0.0.0")]
namespace CelesteHaptic {
    public static class Hooks {
        [System.Runtime.InteropServices.DllImport("libc",EntryPoint="kill")]
        static extern int SendSignal(int pid,int signal);
        const int Port=48113;
        static readonly Synth Mixer=new Synth();
        static readonly object LogGate=new object();
        static readonly float[] Strengths={.15f,.4f,1f,.05f}, Lengths={.1f,.25f,.5f,1f,2f};
        static volatile bool connected, stopping, allowed;
        static bool started, enabled=true;
        static float masterGain=1.4f;
        static string directory, logPath;
        static Thread worker;
        static Process bridge;
        static TcpClient socket;
        static int events;
        public static void Start() {
            if(started) return; started=true;
            try {
                string resources=Path.GetDirectoryName(typeof(Hooks).Assembly.Location);
                directory=Path.GetFullPath(Path.Combine(resources,"../../../CelesteHaptic"));
                Directory.CreateDirectory(directory);
                logPath=Path.Combine(directory,"CelesteHaptic.log");
                File.WriteAllText(logPath,"CelesteHaptic 1.0 — procedural DualSense haptics\n");
                string config=Path.Combine(directory,"settings.ini");
                if(File.Exists(config)) foreach(string raw in File.ReadAllLines(config)) {
                    string line=raw.Trim(); if(line.StartsWith("#"))continue;
                    string[] kv=line.Split('='); if(kv.Length!=2)continue;
                    if(kv[0].Trim()=="Enabled") bool.TryParse(kv[1].Trim(),out enabled);
                    float g;
                    if(kv[0].Trim()=="MasterGain" && float.TryParse(kv[1],NumberStyles.Float,CultureInfo.InvariantCulture,out g) && !float.IsNaN(g) && !float.IsInfinity(g)) masterGain=Math.Max(0,Math.Min(2,g));
                }
                AppDomain.CurrentDomain.ProcessExit+=(s,e)=>Shutdown();
                if(!enabled){Log("Disabled in settings.ini; ordinary rumble retained.");return;}
                allowed=true;
                worker=new Thread(Run){IsBackground=true,Name="CelesteHaptic"}; worker.Start();
                Log("Hooks initialized; native bridge starts automatically. Port "+Port);
            } catch(Exception e) { enabled=false; Log("Startup failed; ordinary rumble retained: "+e); }
        }
        public static void UpdateState() {
            if(!started)Start();
            try {
                var level=Engine.Scene as Level;
                allowed=enabled && Engine.Instance!=null && Engine.Instance.IsActive && !MInput.Disabled && (level==null || !level.Paused) && Settings.Instance!=null && Settings.Instance.Rumble!=RumbleAmount.Off;
                if(!allowed)Mixer.Clear();
            } catch {allowed=false;}
        }
        public static void Rumble(RumbleStrength strength,RumbleLength length,int context) {
            int s=(int)strength,l=(int)length;
            if(s<0||s>=Strengths.Length||l<0||l>=Lengths.Length) {Input.Rumble(strength,length);return;}
            if(!TryPlay(Strengths[s],Lengths[l],s==3?(int)Effect.Climb:context)) Input.Rumble(strength,length);
        }
        public static void RumbleSpecific(float strength,float time,int context) {
            if(!TryPlay(strength,time,context)) Input.RumbleSpecific(strength,time);
        }
        static bool TryPlay(float strength,float time,int context) {
            if(!enabled||!allowed||!connected||Settings.Instance==null||Settings.Instance.Rumble==RumbleAmount.Off) return false;
            try {
                float scale=Settings.Instance.Rumble==RumbleAmount.Half?.5f:1f;
                Mixer.Add((Effect)context,strength*scale,time);
                if(Interlocked.Increment(ref events)<=16)Log("Gameplay effect: "+(Effect)context+" strength="+strength.ToString(CultureInfo.InvariantCulture)+" seconds="+time.ToString(CultureInfo.InvariantCulture));
                return true;
            } catch(Exception e){Log("Effect failed; using rumble: "+e.Message);return false;}
        }
        // Suppress SDL motor reports only while we own the haptic connection.
        public static bool SetVibration(PlayerIndex player,float left,float right) {
            if(enabled && allowed && connected) return true;
            return GamePad.SetVibration(player,left,right);
        }
        static void Log(string text) {
            try { lock(LogGate) if(logPath!=null) File.AppendAllText(logPath,DateTime.Now.ToString("HH:mm:ss ")+text+"\n"); } catch{}
        }
        static void StartBridge() {
            if(bridge!=null && !bridge.HasExited)return;
            string exe=Path.Combine(directory,"HapticsBridge");
            if(!File.Exists(exe))throw new FileNotFoundException("HapticsBridge missing",exe);
            var info=new ProcessStartInfo(exe,"--port "+Port+" --parent-pid "+Process.GetCurrentProcess().Id){UseShellExecute=false,WorkingDirectory=directory};
            foreach(string key in new ArrayList(info.EnvironmentVariables.Keys))
                if(key.StartsWith("DYLD_")||key.StartsWith("LD_")||key.StartsWith("DOORSTOP_"))info.EnvironmentVariables.Remove(key);
            bridge=Process.Start(info); Log("Started native companion.");
        }
        static void Run() {
            var samples=new float[960];var bytes=new byte[3840];
            string lastError=null;
            while(!stopping) {
                TcpClient client=null;
                try {
                    StartBridge();
                    client=new TcpClient();socket=client;client.NoDelay=true;client.SendTimeout=1000;
                    client.Connect("127.0.0.1",Port);
                    var stream=client.GetStream(); Mixer.Clear();connected=true;lastError=null;Log("DualSense transport connected.");
                    var clock=Stopwatch.StartNew();long blocks=0;
                    while(!stopping) {
                        if(!allowed)Mixer.Clear();
                        Mixer.Render(samples,allowed?masterGain:0);
                        Buffer.BlockCopy(samples,0,bytes,0,bytes.Length);stream.Write(bytes,0,bytes.Length);
                        long due=++blocks*10,remaining=due-clock.ElapsedMilliseconds;
                        if(remaining>0)Thread.Sleep((int)Math.Min(remaining,10));
                        // Never burst a backlog of stale effects after the process is suspended.
                        if(clock.ElapsedMilliseconds-due>50){clock.Restart();blocks=0;Mixer.Clear();}
                    }
                }catch(Exception e){
                    if(!stopping && lastError!=e.Message){Log("Transport unavailable; ordinary rumble until reconnect: "+e.Message);lastError=e.Message;}
                }finally{connected=false;Mixer.Clear();if(client!=null)client.Close();socket=null;}
                for(int i=0;i<10 && !stopping;i++)Thread.Sleep(100);
            }
        }
        public static void Shutdown() {
            stopping=true;connected=false;Mixer.Clear();
            try{if(socket!=null)socket.Close();}catch{}
            try{if(worker!=null && Thread.CurrentThread!=worker)worker.Join(1200);}catch{}
            try{if(bridge!=null&&!bridge.HasExited){if(SendSignal(bridge.Id,15)!=0)bridge.Kill();bridge.WaitForExit(1500);}}catch{}
        }
    }
}
