using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.RestSite;

namespace AaaCode.RelicForge;

[HarmonyPatch(typeof(RestSiteOption), nameof(RestSiteOption.Icon), MethodType.Getter)]
internal static class ForgeRestSiteArt
{
    private const string FrameSource = "res://images/ui/rest_site/option_heal.png";
    private static Texture2D? _framedIcon;

    private static void Postfix(RestSiteOption __instance, ref Texture2D __result)
    {
        if (__instance is ForgeRestSiteOption)
            __result = _framedIcon ??= FrameIcon(__result);
    }

    private static Texture2D FrameIcon(Texture2D artwork)
    {
        if (!ResourceLoader.Exists(FrameSource) ||
            ResourceLoader.Load<Texture2D>(FrameSource)?.GetImage() is not { } frame)
            return artwork;

        using var frameImage = frame;
        using var image = artwork.GetImage();
        if (image is null || frame.IsEmpty() || (frame.IsCompressed() && frame.Decompress() != Error.Ok))
            return artwork;

        var width = image.GetWidth();
        var height = image.GetHeight();
        frame.Convert(Image.Format.Rgba8);
        frame.Resize(width, height, Image.Interpolation.Bilinear);
        image.Convert(Image.Format.Rgba8);

        // Use the installed game's silhouette and painted rim. Keep only the outer band.
        const int rimWidth = 22;
        for (int y = 0; y < height; y++)
        {
            int verticalInset = Math.Min(y, height - 1 - y);
            for (int x = 0; x < width; x++)
            {
                int inset = Math.Min(Math.Min(x, width - 1 - x), verticalInset);
                if (inset >= rimWidth)
                    continue;

                Color sample = frame.GetPixel(x, y);
                Color tint = Color.FromHsv(0.46f, 0.47f, sample.G * 0.84f, sample.A);
                Color color = tint.Lerp(image.GetPixel(x, y), Mathf.SmoothStep(13f, rimWidth, inset));
                image.SetPixel(x, y, color);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
