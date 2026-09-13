using UnityEngine;

namespace ShipSimulator.Visuals
{
    public static class NavigationLightSectors
    {
        public static bool IsVisible(float bearingDegrees, float centerDegrees, float arcDegrees)
            => Mathf.Abs(Mathf.DeltaAngle(centerDegrees, bearingDegrees)) <= arcDegrees * 0.5f;

        public static Cubemap CreateCookie(float center, float arc)
        {
            const int size = 32;
            var cube = new Cubemap(size, TextureFormat.RGBA32, false) { name = "Navigation light sector" };
            for (int face = 0; face < 6; face++)
            {
                var colors = new Color[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2 - 1;
                    float v = (y + 0.5f) / size * 2 - 1;
                    Vector3 d = face == 0 ? new Vector3(1,-v,-u) : face == 1 ? new Vector3(-1,-v,u) :
                        face == 2 ? new Vector3(u,1,v) : face == 3 ? new Vector3(u,-1,-v) :
                        face == 4 ? new Vector3(u,-v,1) : new Vector3(-u,-v,-1);
                    float bearing = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    colors[y * size + x] = IsVisible(bearing, center, arc) ? Color.white : Color.clear;
                }
                cube.SetPixels(colors, (CubemapFace)face);
            }
            cube.Apply();
            return cube;
        }
    }
}
