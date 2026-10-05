using System.Numerics;
namespace SprocketSmokeLaunchers;
// Minimal measured endpoints/axes, not game mesh data. Native asset remains in-game.
public static class TriLauncherGeometry
{
    public const string MeshGuid="4155910536a20744f92326d954d869fe";
    public const string MaterialGuid="244fb3087922ee7428d51ec7ebe33800";
    public const float MuzzleClearance=.015f;
    public static Vector3 Mouth(int tube) => tube switch
    {
        0 => new(.26336518f,.15495215f,.08494430f),
        1 => new(.34717497f,.05435029f,.11635075f),
        2 => new(.28577831f,.19914119f,-.07292999f),
        _ => throw new ArgumentOutOfRangeException(nameof(tube))
    };
    public static Vector3 Direction(int tube) => tube switch
    {
        0 => Vector3.Normalize(new(.11736293f,.68301296f,.72091556f)),
        1 => Vector3.Normalize(new(.35355368f,.70710683f,.61237216f)),
        2 => Vector3.Normalize(new(.56565046f,.68301278f,.46209648f)),
        _ => throw new ArgumentOutOfRangeException(nameof(tube))
    };
    public static Vector3 Muzzle(int tube) => Mouth(tube)+Direction(tube)*MuzzleClearance;
    public static float Azimuth(int tube) { var d=Direction(tube); return MathF.Atan2(d.X,d.Z)*180/MathF.PI; }
    public static float Elevation(int tube) => MathF.Asin(Direction(tube).Y)*180/MathF.PI;
}
