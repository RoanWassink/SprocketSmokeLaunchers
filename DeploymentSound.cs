namespace SprocketSmokeLaunchers;
// Authored grenade ignition pop/pressure release, no recorded or third-party assets.
public static class DeploymentSound
{
    public const float Duration=.48f;
    public static float[] CreateSamples()
    {
        var samples=new float[(int)(LaunchSound.Frequency*Duration)]; uint rng=0xB017C0DE;
        double low=0;
        for(int i=0;i<samples.Length;i++)
        {
            double t=(double)i/LaunchSound.Frequency;
            rng^=rng<<13; rng^=rng>>17; rng^=rng<<5;
            double noise=2*rng/(double)uint.MaxValue-1; low+=.18*(noise-low);
            double attack=Math.Min(1,t/.0008);
            double crack=(noise-low)*Math.Exp(-t*100);
            double body=Math.Sin(2*Math.PI*(92*t-65*t*t))*Math.Exp(-t*24);
            double gas=low*Math.Exp(-t*13);
            double value=attack*(.65*crack+.48*body+.65*gas);
            samples[i]=(float)(.82*Math.Tanh(value*2)*Math.Clamp((Duration-t)/.035,0,1));
        }
        return samples;
    }
}
public sealed class DeploymentCue
{
    private bool fired;
    public void Reset() => fired=false;
    public bool TryDeploy() { if(fired) return false; fired=true; return true; }
}
