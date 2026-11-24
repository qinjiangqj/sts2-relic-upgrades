using Godot;
using MegaCrit.Sts2.Core.Models;

namespace AaaCode.RelicForge;

internal static class RelicOverlayArt
{
    private static readonly Dictionary<string, ImageTexture> Icons = new(StringComparer.Ordinal);

    internal static string? Resolve(string size, string overlayPath, RelicModel original)
    {
        if (size == "outline")
            return original.IconOutline?.ResourcePath;

        lock (Icons)
        {
            if (Icons.TryGetValue(overlayPath, out var cached))
                return cached.ResourcePath;

            var source = size == "detail" ? original.BigIcon : original.Icon;
            if (source is null || !ResourceLoader.Exists(overlayPath, "Texture2D"))
                return source?.ResourcePath;

            var overlay = ResourceLoader.Load<Texture2D>(overlayPath);
            using var overlayImage = CopyPixels(overlay);
            using var image = ReadIcon(source);
            if (image is null || overlayImage is null || image.GetSize() != overlayImage.GetSize())
                return source.ResourcePath;

            image.BlendRect(overlayImage, new Rect2I(Vector2I.Zero, overlayImage.GetSize()), Vector2I.Zero);

            var icon = ImageTexture.CreateFromImage(image);
            icon.TakeOverPath(overlayPath.Replace("/art/", "/composed/", StringComparison.Ordinal));
            Icons.Add(overlayPath, icon);
            return icon.ResourcePath;
        }
    }

    private static Image? ReadIcon(Texture2D texture)
    {
        if (texture is not AtlasTexture atlas)
            return CopyPixels(texture);

        using var atlasImage = CopyPixels(atlas.Atlas);
        if (atlasImage is null)
            return null;

        var icon = Image.CreateEmpty(texture.GetWidth(), texture.GetHeight(), false, Image.Format.Rgba8);
        icon.BlitRect(atlasImage, (Rect2I)atlas.Region, (Vector2I)atlas.Margin.Position);
        return icon;
    }

    private static Image? CopyPixels(Texture2D? texture)
    {
        using var source = texture?.GetImage();
        if (source is null || source.IsEmpty())
            return null;

        var image = Image.CreateFromData(source.GetWidth(), source.GetHeight(), source.HasMipmaps(), source.GetFormat(), source.GetData());
        if (image.IsCompressed() && image.Decompress() != Error.Ok)
        {
            image.Dispose();
            return null;
        }
        image.Convert(Image.Format.Rgba8);
        return image;
    }
}
