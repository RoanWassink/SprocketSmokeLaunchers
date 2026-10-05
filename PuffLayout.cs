namespace SprocketSmokeLaunchers;
// Persistent bounded layout; shared by rendering and managed regression checks.
public static class PuffLayout
{
    public static System.Numerics.Vector3 Center(int index,float age)
    {
        if(index<0 || index>=SmokeRules.PuffsPerCloud) throw new ArgumentOutOfRangeException(nameof(index));
        if(!float.IsFinite(age) || age<0 || age>SmokeRules.CloudSeconds) throw new ArgumentOutOfRangeException(nameof(age));
        float angle=index*2.39996323f, radius=2.6f*MathF.Sqrt((index+.5f)/SmokeRules.PuffsPerCloud);
        float x=MathF.Cos(angle)*radius,z=MathF.Sin(angle)*radius;
        return new(x*(1+.035f*age),((index%4)-1.5f)*1.1f+.07f*age,z*(1+.035f*age));
    }
    public static float Size(int index) => SmokeRules.PuffSize*(.75f+.25f*(index%5)/4);
    public static System.Numerics.Vector3 Corner(int index,int corner,float age,System.Numerics.Vector3 right,System.Numerics.Vector3 up)
    {
        if(corner<0 || corner>3) throw new ArgumentOutOfRangeException(nameof(corner));
        float half=Size(index)*.5f;
        return Center(index,age)+right*(corner is 1 or 2 ? half : -half)+up*(corner>=2 ? half : -half);
    }
}
