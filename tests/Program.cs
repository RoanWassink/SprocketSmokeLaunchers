using SprocketSmokeLaunchers;
int checks=0;
void Check(bool value,string name) { checks++; if(!value) throw new Exception(name); }
// Runtime policy: once per genuine combat, with shared spent tickets across copies/save reloads.
var ledger=new SessionLedger(); var bankAmmo=new SessionAmmo();
Check(!bankAmmo.TrySpend(ledger),"editor cannot spend");
Check(bankAmmo.Save(ledger)=="1:0","editor blueprint carries loaded preparation");
for(int sessionIndex=0;sessionIndex<100;sessionIndex++) {
 string id=sessionIndex.ToString("x32"); Check(ledger.Begin(id),"new Play begins once");
 bankAmmo.Load(ledger,"1:1"); Check(!bankAmmo.IsSpent(ledger),"legacy spent design starts new combat loaded");
 var beforeShot=bankAmmo.Save(ledger); var copy=new SessionAmmo(); copy.Load(ledger,beforeShot);
 Check(bankAmmo.TrySpend(ledger),"one three-grenade bank salvo per combat");
 Check(!copy.TrySpend(ledger),"unspent snapshot copy shares subsequently spent ticket");
 for(int repeatIndex=0;repeatIndex<100;repeatIndex++) {
  Check(!ledger.Begin(id),"repeated enter/update cannot reset active combat");
  bankAmmo.Load(ledger,beforeShot); Check(!bankAmmo.TrySpend(ledger),"same-session pre-shot reload cannot refill");
  bankAmmo.Load(ledger,"1:0"); Check(bankAmmo.IsSpent(ledger),"legacy loaded-state import cannot refill existing bank");
 }
 var spentCopy=new SessionAmmo(); spentCopy.Load(ledger,bankAmmo.Save(ledger)); Check(!spentCopy.TrySpend(ledger),"spent save copy remains spent");
 ledger.End(); Check(bankAmmo.Save(ledger)=="1:0","return editor cleans persistent design ammo");
}
ledger.Begin("abcdef0123456789abcdef0123456789");
for(int n=0;n<SessionLedger.MaxTickets;n++) { var newBank=new SessionAmmo(); newBank.Load(ledger,"1:0"); Check(newBank.TrySpend(ledger),"new banks bounded session budget"); }
var overBudget=new SessionAmmo(); Check(!overBudget.TrySpend(ledger),"release or arbitrary legacy-spawn abuse cannot allocate beyond session ticket cap");
ledger.End(); ledger.Begin("fedcba9876543210fedcba9876543210");
foreach(var malformed in new[]{"garbage","2:bad:1:0","3:unknown","2:fedcba9876543210fedcba9876543210:999999:0"}) {
 var invalid=new SessionAmmo(); invalid.Load(ledger,malformed); Check(!invalid.TrySpend(ledger),"invalid/forged same-session ticket fails closed");
}
var previousCombat=new SessionAmmo(); Check(previousCombat.TrySpend(ledger),"previous combat bank spends"); var previousSpent=previousCombat.Save(ledger);
ledger.End(); ledger.Begin("00112233445566778899aabbccddeeff");
var newCombat=new SessionAmmo(); newCombat.Load(ledger,previousSpent); Check(newCombat.TrySpend(ledger),"previous session spent v2 save starts new combat loaded");
newCombat.Load(ledger,previousSpent); Check(!newCombat.TrySpend(ledger),"previous-session import cannot refill current live spent bank");
var burst=DeploymentSound.CreateSamples(); var burstAgain=DeploymentSound.CreateSamples();
Check(burst.Length==(int)(22050*.48f),"distinct burst duration");
for(int i=0;i<burst.Length;i++) Check(float.IsFinite(burst[i]) && Math.Abs(burst[i])<.83 && burst[i]==burstAgain[i],"authored burst finite unclipped deterministic");
Check(burst[0]==0 && Math.Abs(burst[^1])<.001 && burst.Select(x=>(double)x*x).Average()>.0001,"burst silent ends and audible energy");
var cue=new DeploymentCue();
for(int reused=0;reused<1000;reused++) { cue.Reset(); Check(cue.TryDeploy(),"one cue at deployment per reused grenade slot"); for(int puff=0;puff<16;puff++) Check(!cue.TryDeploy(),"never repeat cue per smoke puff"); }
float badgeCoverage=0;
for(int y=0;y<128;y++) for(int x=0;x<128;x++) {
 var pixel=SmokeBadge.Pixel((x+.5f)/128,(y+.5f)/128);
 Check(float.IsFinite(pixel.alpha) && pixel.alpha>=0 && pixel.alpha<=1 && pixel.shade>=0 && pixel.shade<=1,"badge finite valid pixel");
 if(pixel.alpha>0) badgeCoverage++;
 if(x<64 || y<64) Check(pixel.alpha==0,"badge leaves most native launcher icon untouched");
}
Check(badgeCoverage/(128*128)> .04f && badgeCoverage/(128*128)<.15f,"recognizable bounded smoke badge coverage");
var ammo=new AmmoState(); Check(!ammo.Spent,"new loaded bank"); Check(ammo.TrySpend(),"first salvo");
for(int i=0;i<10000;i++) { Check(!ammo.TrySpend(),"held/repeated press cannot rearm"); ammo.Load("1:0"); Check(ammo.Spent,"old-state load cannot refill live bank"); }
Check(ammo.Save()=="1:1","spent saved"); var saved=new AmmoState(); saved.Load(ammo.Save()); Check(!saved.TrySpend(),"save/reload empty remains empty");
var loaded=new AmmoState(); loaded.Load("1:0"); Check(loaded.TrySpend(),"new spawn of prepared unspent state");
foreach(var bad in new[]{"","2:0","1:-1","1:2","garbage","1:1"}) { var a=new AmmoState(); a.Load(bad); Check(!a.TrySpend(),"bad/future states fail closed"); }
var legacy=new AmmoState(); legacy.Load(null); Check(legacy.TrySpend(),"new/legacy definition missing addon state");
Check(SmokeRules.Tubes==3,"exact native tube count");
for(int i=0;i<3;i++) {
 var d=TriLauncherGeometry.Direction(i); Check(Math.Abs(d.Length()-1)<.00001,"unit tube axis");
 Check(System.Numerics.Vector3.Distance(TriLauncherGeometry.Muzzle(i),TriLauncherGeometry.Mouth(i)+d*.015f)<.000001,"muzzle beyond native lip");
 Check(TriLauncherGeometry.Elevation(i)>43 && TriLauncherGeometry.Elevation(i)<45.01,"native elevation");
 foreach(var scale in new[]{new System.Numerics.Vector3(1,1,1),new System.Numerics.Vector3(-1,1,1),new System.Numerics.Vector3(1,1,-1)}) {
  var mirrored=System.Numerics.Vector3.Normalize(d*scale); Check(Math.Abs((mirrored*15).Length()-15)<.00001,"mirroring preserves speed");
  Check(Math.Abs(mirrored.Y-d.Y)<.00001,"mirroring preserves elevation");
 }
}
Check(Math.Abs(TriLauncherGeometry.Azimuth(1)-30)<.001 && Math.Abs(TriLauncherGeometry.Azimuth(0)-9.24647)<.001 && Math.Abs(TriLauncherGeometry.Azimuth(2)-50.75360)<.001,"native measured axes");
for(int banks=0;banks<=128;banks++) for(int slots=0;slots<=48;slots++)
    Check(SmokeRules.CanVolley(banks,slots)==(banks>0 && banks<=16 && slots>=banks*3),"all-or-none capacity");
Check(SmokeRules.MaxVolleyBanks==16 && SmokeRules.CanVolley(9,27) && !SmokeRules.CanVolley(9,26),"nine-bank regression and exact capacity");
Check(SmokeRules.CanVolley(16,48) && !SmokeRules.CanVolley(17,48),"full sixteen-bank coherent volley bounded by existing pool");
for(int tube=0;tube<3;tube++) foreach(var yaw in new[]{0f,30f,90f,180f}) foreach(var mirror in new[]{1f,-1f}) foreach(var size in new[]{.5f,1f,1.25f}) {
 var q=System.Numerics.Quaternion.CreateFromYawPitchRoll(yaw*MathF.PI/180,.31f,-.17f); var scale=new System.Numerics.Vector3(mirror*size,size,size);
 var localAxis=TriLauncherGeometry.Direction(tube); var axis=System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Transform(localAxis*scale,q));
 var mouth=System.Numerics.Vector3.Transform(TriLauncherGeometry.Mouth(tube)*scale,q);
 var muzzle=System.Numerics.Vector3.Transform(TriLauncherGeometry.Muzzle(tube)*scale,q);
 Check(System.Numerics.Vector3.Dot(System.Numerics.Vector3.Normalize(muzzle-mouth),axis)>.99999f,"mirrored/rotated/scaled launch axis follows rendered tube lip");
 Check(Math.Abs((muzzle-mouth).Length()-.015f*size)<.00001f,"muzzle clearance follows native geometry scale");
}
Check(SmokeRules.MaxClouds*SmokeRules.PuffsPerCloud==768,"hard particle budget");
// Repeated spawn/volley cycles: full pool rejects a new volley without spending;
// after expiry a fresh prepared vehicle can use the budget again.
for(int cycle=0;cycle<500;cycle++)
{
    int active=24; var banks=Enumerable.Range(0,8).Select(_=>new AmmoState()).ToArray();
    Check(SmokeRules.CanVolley(banks.Length,48-active),"fresh pool accepts coherent eight-bank salvo");
    foreach(var b in banks) { Check(b.TrySpend(),"one spend per bank"); active+=3; }
    var waiting=new AmmoState(); if(SmokeRules.CanVolley(1,48-active)) waiting.TrySpend();
    Check(!waiting.Spent && active==48,"pool exhaustion does not consume pending ammo");
    active=0; Check(SmokeRules.CanVolley(1,48-active) && waiting.TrySpend(),"expiry frees capacity, one new salvo");
    foreach(var b in banks) Check(!b.TrySpend(),"expiry never refills original banks");
}
Check(SmokeRules.Opacity(0)==0 && SmokeRules.Opacity(25)==0,"opacity birth/end");
Check(SmokeRules.Opacity(2)>.8 && SmokeRules.Opacity(20)>.8,"full opacity window");
for(int i=0;i<=1000;i++) { float age=i*.04f; Check(SmokeRules.Opacity(age)>=0 && SmokeRules.Opacity(age)<=.82f,"opacity bounded"); }
Check(SmokeRules.Opacity(float.NaN)==0 && SmokeRules.Opacity(-1)==0,"invalid age safe");
for(int i=0;i<3;i++) { var d=TriLauncherGeometry.Direction(i); float flight=2*15*d.Y/9.81f; float range=15*MathF.Sqrt(1-d.Y*d.Y)*flight; Check(flight>2.08 && flight<2.17 && range>22.8 && range<23,"native flat ground reference"); }
Check(LaunchSound.OutputGain==2.8f,"requested louder cue without config migration");
var pcm=LaunchSound.CreateSamples(); var repeat=LaunchSound.CreateSamples();
Check(pcm.Length==(int)(22050*.28f),"short mono cue duration");
for(int i=0;i<pcm.Length;i++) Check(float.IsFinite(pcm[i]) && Math.Abs(pcm[i])<1 && pcm[i]==repeat[i],"authored PCM finite bounded deterministic");
Check(pcm[0]==0 && Math.Abs(pcm[^1])<.001 && pcm.Select(x=>(double)x*x).Average()>.0001,"cue silence edges and audible energy");
Check(SmokeRules.BankMass==18 && SmokeRules.BankCost==120,"loaded bank calibration");
for(int puff=0;puff<SmokeRules.PuffsPerCloud;puff++) for(int tick=0;tick<=250;tick++)
{
    float age=tick*.1f;
    var centre=PuffLayout.Center(puff,age);
    Check(float.IsFinite(centre.X) && float.IsFinite(centre.Y) && float.IsFinite(centre.Z),"all puff positions finite over full lifetime");
    foreach(var basis in new[]{(System.Numerics.Vector3.UnitX,System.Numerics.Vector3.UnitY),(System.Numerics.Vector3.UnitZ,System.Numerics.Vector3.UnitY),(-System.Numerics.Vector3.UnitX,System.Numerics.Vector3.UnitZ)})
    {
        var c0=PuffLayout.Corner(puff,0,age,basis.Item1,basis.Item2);
        var c1=PuffLayout.Corner(puff,1,age,basis.Item1,basis.Item2);
        var c2=PuffLayout.Corner(puff,2,age,basis.Item1,basis.Item2);
        var c3=PuffLayout.Corner(puff,3,age,basis.Item1,basis.Item2);
        Check(System.Numerics.Vector3.Distance((c0+c1+c2+c3)/4,centre)<.00001f,"camera rotation preserves puff centre");
        Check(Math.Abs(System.Numerics.Vector3.Distance(c0,c1)-PuffLayout.Size(puff))<.00001,"camera rotation preserves puff size");
        Check(Math.Abs(c0.X)<12 && Math.Abs(c0.Y-1)<12 && Math.Abs(c0.Z)<12 && Math.Abs(c2.X)<12 && Math.Abs(c2.Y-1)<12 && Math.Abs(c2.Z)<12,"fixed mesh culling bounds contain puffs for all tested cameras/ages");
    }
}
_ = PuffLayout.Corner(0,0,1,System.Numerics.Vector3.UnitX,System.Numerics.Vector3.UnitY);
long before=GC.GetAllocatedBytesForCurrentThread(); float checksum=0;
for(int i=0;i<100000;i++) checksum+=PuffLayout.Corner(i%16,i%4,12,System.Numerics.Vector3.UnitX,System.Numerics.Vector3.UnitY).X;
long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
Check(allocated==0 && float.IsFinite(checksum),"puff layout makes no managed heap allocation across 100000 frame calls");
Console.WriteLine($"Audio peak={pcm.Max(x=>Math.Abs(x)):F6}, RMS={Math.Sqrt(pcm.Select(x=>(double)x*x).Average()):F6}; cue gain 2.8 (+5.42 dB over 0.2.1). Saved source volume unchanged.\nPASS {checks} assertions: 100 combat epochs, duplicate/shared ticket/reload protections, 1024 ticket cap, procedural badge coverage, legacy ammo/save/fail-closed, coherent capacity and 500 repeated simulated volleys, geometry/flight/opacity/budgets. billboard camera geometry/bounds and zero-allocation layout. Native runtime tests remain pending.");

