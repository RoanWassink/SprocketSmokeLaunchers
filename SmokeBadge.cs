namespace SprocketSmokeLaunchers;
// Authored normalized smoke silhouette in the upper-right corner; no game pixels in source.
public static class SmokeBadge
{
    public static (float shade,float alpha) Pixel(float x,float y)
    {
        float d=Math.Min(Math.Min(Circle(x,y,.72f,.72f,.105f),Circle(x,y,.81f,.77f,.115f)),
            Math.Min(Circle(x,y,.76f,.86f,.085f),Circle(x,y,.88f,.84f,.077f)));
        float alpha=Math.Clamp((.013f-d)/.008f,0,1);
        float shade=d>-.014f ? .13f : .76f+.15f*Math.Clamp((y-.65f)/.3f,0,1);
        return(shade,alpha);
    }
    private static float Circle(float x,float y,float cx,float cy,float r) => MathF.Sqrt((x-cx)*(x-cx)+(y-cy)*(y-cy))-r;
}
