namespace SprocketSmokeLaunchers;
// Game-independent state: spending is monotonic, including repeated model builds,
// toggles, physics transitions and old-state loads into a live instance.
public sealed class AmmoState
{
    public bool Spent { get; private set; }
    public bool TrySpend() { if (Spent) return false; Spent = true; return true; }
    public string Save() => Spent ? "1:1" : "1:0";
    public void Load(string? value)
    {
        if (value == null) return; // old/new definition without addon state
        // Unknown/corrupt state fails closed; never turn it into free ammunition.
        Spent |= value != "1:0";
    }
}
public static class SmokeRules
{
    public const int Tubes = 3, MaxClouds = 48, PuffsPerCloud = 16, MaxBanks = 128;
    public const int MaxVolleyBanks = MaxClouds / Tubes;
    public const float Speed = 15, CloudSeconds = 25, MaxFlight = 5;
    public const float BankMass = 18, BankCost = 120, PuffSize = 7;
    public static float Azimuth(int tube)
    {
        if (tube < 0 || tube >= Tubes) throw new ArgumentOutOfRangeException(nameof(tube));
        return TriLauncherGeometry.Azimuth(tube);
    }
    public static bool CanVolley(int banks, int freeClouds) => banks > 0 && banks <= MaxVolleyBanks && freeClouds >= banks * Tubes;
    public static float Opacity(float age) => !float.IsFinite(age) || age < 0 ? 0 : Math.Clamp(age / 2, 0, 1) * Math.Clamp((CloudSeconds - age) / 5, 0, 1) * .82f;
}
