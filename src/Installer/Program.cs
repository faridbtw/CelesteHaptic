using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
record InstallState(string OriginalHash,string PatchedHash,string Version,int RumbleHooks,int MotorHooks);
class Program {
    const string Version="1.0";
    static IEnumerable<TypeDefinition> Types(TypeDefinition t){yield return t;foreach(var c in t.NestedTypes)foreach(var x in Types(c))yield return x;}
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    static void AtomicWrite(string path,byte[] bytes){string tmp=path+".celestehaptic-tmp";try{File.WriteAllBytes(tmp,bytes);File.Move(tmp,path,true);}finally{if(File.Exists(tmp))File.Delete(tmp);}}
    static int Kind(string name){
        name=name.ToLowerInvariant();
        if(name.Contains("die(")||name.Contains("death"))return 3;
        if(name.Contains("dreamdash")||name.Contains("starfly")||name.Contains("feather"))return 5;
        if(name.Contains("dash"))return 1;
        if(name.Contains("climb"))return 4;
        if(name.Contains("refill")||name.Contains("strawberry")||name.Contains("heartgem")||name.Contains("cassette")||name.Contains("collect"))return 6;
        if(name.Contains("oncollide")||name.Contains("bounce")||name.Contains("spring")||name.Contains("hit"))return 2;
        if(name.Contains("block")||name.Contains("rumbletrigger")||name.Contains("quake")||name.Contains("shake"))return 7;
        return 0;
    }
    static (byte[] bytes,int rumble,int motors,List<string> sites) Patch(string original){
        var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(Path.GetDirectoryName(original)!);
        using var module=ModuleDefinition.ReadModule(original,new ReaderParameters{AssemblyResolver=resolver});
        var types=module.Types.SelectMany(Types).ToArray();
        if(module.AssemblyReferences.Any(r=>r.Name.Contains("Everest")||r.Name=="CelesteHaptic")||types.Any(t=>t.FullName.StartsWith("Celeste.Mod.")))
            throw new Exception("This installer supports vanilla Celeste. An existing mod loader or patch was found; game left unchanged.");
        var input=types.Single(t=>t.FullName=="Celeste.Input");
        var engine=types.Single(t=>t.FullName=="Monocle.Engine");
        if(input.Methods.Count(m=>m.Name=="Rumble"&&m.Parameters.Count==2)!=1 || input.Methods.Count(m=>m.Name=="RumbleSpecific"&&m.Parameters.Count==2)!=1)
            throw new Exception("Unsupported Celeste rumble API; game left unchanged.");
        var reference=new AssemblyNameReference("CelesteHaptic",new Version(1,0,0,0));module.AssemblyReferences.Add(reference);
        var hooks=new TypeReference("CelesteHaptic","Hooks",module,reference);
        MethodReference Hook(string name,TypeReference result,IEnumerable<TypeReference> parameters){
            var method=new MethodReference(name,result,hooks){HasThis=false};
            foreach(var p in parameters)method.Parameters.Add(new ParameterDefinition(p));return method;
        }
        var empty=Array.Empty<TypeReference>();
        void Inject(MethodDefinition method,string hook){
            method.Body.SimplifyMacros();var il=method.Body.GetILProcessor();
            il.InsertBefore(method.Body.Instructions[0],il.Create(OpCodes.Call,Hook(hook,module.TypeSystem.Void,empty)));method.Body.OptimizeMacros();
        }
        Inject(input.Methods.Single(m=>m.Name=="Initialize"&&m.Parameters.Count==0),"Start");
        Inject(engine.Methods.Single(m=>m.Name=="Update"&&m.Parameters.Count==1),"UpdateState");
        Inject(engine.Methods.Single(m=>m.Name=="OnExiting"&&m.Parameters.Count==2),"Shutdown");
        int count=0,motors=0;var sites=new List<string>();
        foreach(var type in types)foreach(var method in type.Methods.Where(m=>m.HasBody)){
            if(type==input)continue;
            var replacements=method.Body.Instructions.Where(i=>i.OpCode==OpCodes.Call&&i.Operand is MethodReference mr&&
                ((mr.DeclaringType.FullName=="Celeste.Input"&&(mr.Name=="Rumble"||mr.Name=="RumbleSpecific"))||
                (mr.DeclaringType.FullName=="Microsoft.Xna.Framework.Input.GamePad"&&mr.Name=="SetVibration"&&mr.Parameters.Count==3))).ToArray();
            if(replacements.Length==0)continue;
            method.Body.SimplifyMacros();var il=method.Body.GetILProcessor();
            foreach(var instruction in replacements){
                var old=(MethodReference)instruction.Operand;
                if(old.DeclaringType.FullName=="Celeste.Input"){
                    int kind=Kind(method.FullName);
                    var target=Hook(old.Name,module.TypeSystem.Void,old.Parameters.Select(p=>p.ParameterType).Append(module.TypeSystem.Int32));
                    // Retain the old instruction as the context load so branches cannot skip it.
                    instruction.OpCode=OpCodes.Ldc_I4;instruction.Operand=kind;
                    il.InsertAfter(instruction,il.Create(OpCodes.Call,target));
                    sites.Add(method.FullName+" -> "+kind);count++;
                }else{
                    instruction.Operand=Hook("SetVibration",old.ReturnType,old.Parameters.Select(p=>p.ParameterType));motors++;
                }
            }
            method.Body.OptimizeMacros();
        }
        if(count<20 || motors<1)throw new Exception("Too few expected hooks; unsupported game build. No changes made.");
        using var output=new MemoryStream();module.Write(output);return(output.ToArray(),count,motors,sites);
    }
    static void CheckClosed(){
        if(Process.GetProcessesByName("Celeste").Length>0)throw new Exception("Quit Celeste before installing or uninstalling.");
    }
    static int Main(string[] args){
        try{
            if(args.Length<2)throw new Exception("Usage: Installer install|uninstall|status GAME_FOLDER [PAYLOAD_FOLDER]");
            string command=args[0],game=Path.GetFullPath(args[1]);
            string resources=Path.Combine(game,"Celeste.app","Contents","Resources"),exe=Path.Combine(resources,"Celeste.exe");
            string home=Path.Combine(game,"CelesteHaptic"),statePath=Path.Combine(home,"install.json");
            if(!File.Exists(exe))throw new Exception("Celeste.exe was not found in "+resources);
            InstallState? state=File.Exists(statePath)?JsonSerializer.Deserialize<InstallState>(File.ReadAllText(statePath)):null;
            if(command=="status"){
                Console.WriteLine(state==null?"CelesteHaptic is not installed.":"CelesteHaptic "+state.Version+" — "+(Hash(exe)==state.PatchedHash?"installed, game hash verified.":"game changed since installation; reinstall after checking other mods."));return 0;
            }
            CheckClosed();
            if(command=="uninstall"){
                if(state==null){Console.WriteLine("Nothing to uninstall.");return 0;}
                string current=Hash(exe);
                if(current!=state.PatchedHash && current!=state.OriginalHash)throw new Exception("The game changed after installation. Refusing to overwrite it. Use Steam Verify files to restore vanilla Celeste; your backup remains in CelesteHaptic/backups.");
                string backup=Path.Combine(home,"backups","Celeste."+state.OriginalHash+".exe");
                if(!File.Exists(backup)||Hash(backup)!=state.OriginalHash)throw new Exception("Original backup missing or changed; game left unchanged.");
                AtomicWrite(exe,File.ReadAllBytes(backup));
                File.Delete(Path.Combine(resources,"CelesteHaptic.dll"));File.Delete(statePath);
                Console.WriteLine("Original Celeste restored and verified. Saves, settings, and backups retained.");return 0;
            }
            if(command!="install"||args.Length<3)throw new Exception("Install requires the payload folder.");
            string payload=Path.GetFullPath(args[2]);
            foreach(string f in new[]{"CelesteHaptic.dll","HapticsBridge"})if(!File.Exists(Path.Combine(payload,f)))throw new Exception("Missing payload: "+f);
            string original=exe;
            if(state!=null){
                string current=Hash(exe);
                if(current==state.PatchedHash)original=Path.Combine(home,"backups","Celeste."+state.OriginalHash+".exe");
                else if(current!=state.OriginalHash)throw new Exception("Game files changed after installation. Use Steam Verify files, then remove the old install.json to install against the verified game.");
                if(!File.Exists(original)||Hash(original)!=state.OriginalHash)throw new Exception("Original backup failed verification.");
            }
            string originalHash=Hash(original);
            var patch=Patch(original);
            string patchedHash=Convert.ToHexString(SHA256.HashData(patch.bytes)).ToLowerInvariant();
            Directory.CreateDirectory(Path.Combine(home,"backups"));
            string backupPath=Path.Combine(home,"backups","Celeste."+originalHash+".exe");
            if(File.Exists(backupPath)&&Hash(backupPath)!=originalHash)throw new Exception("Existing backup is corrupt. No changes made.");
            if(!File.Exists(backupPath))AtomicWrite(backupPath,File.ReadAllBytes(original));
            AtomicWrite(Path.Combine(resources,"CelesteHaptic.dll"),File.ReadAllBytes(Path.Combine(payload,"CelesteHaptic.dll")));
            AtomicWrite(Path.Combine(home,"HapticsBridge"),File.ReadAllBytes(Path.Combine(payload,"HapticsBridge")));
            if(!OperatingSystem.IsWindows())File.SetUnixFileMode(Path.Combine(home,"HapticsBridge"),UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute|UnixFileMode.GroupRead|UnixFileMode.GroupExecute|UnixFileMode.OtherRead|UnixFileMode.OtherExecute);
            string config=Path.Combine(home,"settings.ini");
            if(!File.Exists(config))File.WriteAllText(config,"# Restart Celeste after changing these settings.\nEnabled=true\n# 0.5 = gentle, 1.4 = default, 2.0 = maximum\nMasterGain=1.4\n");
            File.WriteAllLines(Path.Combine(home,"hook-map.txt"),patch.sites);
            var installed=new InstallState(originalHash,patchedHash,Version,patch.rumble,patch.motors);
            AtomicWrite(statePath,System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(installed,new JsonSerializerOptions{WriteIndented=true})));
            AtomicWrite(exe,patch.bytes);
            Console.WriteLine($"CelesteHaptic {Version} installed: {patch.rumble} rumble hooks, {patch.motors} motor guards. Original backed up. Launch Celeste normally from Steam.");return 0;
        }catch(Exception e){Console.Error.WriteLine("CelesteHaptic: "+e.Message);return 1;}
    }
}
