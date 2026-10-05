namespace SprocketSmokeLaunchers;
// Authored deterministic short three-pulse plop/FLOENK cue; no recorded/game audio.
public static class LaunchSound
{
    public const int Frequency=22050;
    public const float Duration=.28f;
    public const float OutputGain=2.8f; // +5.42 dB over 0.2.1, preserves saved volume and mute.
    public static float[] CreateSamples()
    {
        var samples=new float[(int)(Frequency*Duration)]; uint rng=0x51A0BEEF;
        double mean=0;
        for(int i=0;i<samples.Length;i++)
        {
            double t=(double)i/Frequency,value=0;
            for(int shot=0;shot<3;shot++)
            {
                double age=t-shot*.035;
                if(age<0) continue;
                rng^=rng<<13; rng^=rng>>17; rng^=rng<<5;
                double noise=(rng/(double)uint.MaxValue)*2-1;
                double attack=Math.Min(1,age/.0018);
                double body=Math.Sin(2*Math.PI*(155*age-150*age*age))*Math.Exp(-age*30);
                double metal=Math.Sin(2*Math.PI*490*age)*Math.Exp(-age*62);
                double air=noise*Math.Exp(-age*115);
                value+=attack*(.36*body+.07*metal+.16*air);
            }
            value=Math.Tanh(value)*.65;
            value*=Math.Clamp((Duration-t)/.025,0,1);
            samples[i]=(float)value; mean+=value;
        }
        mean/=samples.Length;
        // Remove DC with a window preserving silence at both ends.
        for(int i=0;i<samples.Length;i++) samples[i]-=(float)(mean*Math.Sin(Math.PI*i/(samples.Length-1)));
        for(int i=0;i<samples.Length;i++) samples[i]*=OutputGain;
        return samples;
    }
}
