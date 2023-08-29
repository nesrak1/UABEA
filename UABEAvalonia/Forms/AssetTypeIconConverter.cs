using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HarfBuzzSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    public class AssetTypeIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is AssetClassID assetClass)
            {
                if ((int)assetClass < 0)
                {
                    return GetBitmap("UABEAvalonia/Assets/Icons/asset-mono-behaviour.png");
                }

                return assetClass switch
                {
                    AssetClassID.Animation => GetBitmap("UABEAvalonia/Assets/Icons/asset-animation.png"),
                    AssetClassID.AnimationClip => GetBitmap("UABEAvalonia/Assets/Icons/asset-animation-clip.png"),
                    AssetClassID.Animator => GetBitmap("UABEAvalonia/Assets/Icons/asset-animator.png"),
                    AssetClassID.AnimatorController => GetBitmap("UABEAvalonia/Assets/Icons/asset-animator-controller.png"),
                    AssetClassID.AnimatorOverrideController => GetBitmap("UABEAvalonia/Assets/Icons/asset-animator-override-controller.png"),
                    AssetClassID.AudioClip => GetBitmap("UABEAvalonia/Assets/Icons/asset-audio-clip.png"),
                    AssetClassID.AudioListener => GetBitmap("UABEAvalonia/Assets/Icons/asset-audio-listener.png"),
                    AssetClassID.AudioMixer => GetBitmap("UABEAvalonia/Assets/Icons/asset-audio-mixer.png"),
                    AssetClassID.AudioMixerGroup => GetBitmap("UABEAvalonia/Assets/Icons/asset-audio-mixer-group.png"),
                    AssetClassID.AudioSource => GetBitmap("UABEAvalonia/Assets/Icons/asset-audio-source.png"),
                    AssetClassID.Avatar => GetBitmap("UABEAvalonia/Assets/Icons/asset-avatar.png"),
                    AssetClassID.BillboardAsset => GetBitmap("UABEAvalonia/Assets/Icons/asset-billboard.png"),
                    AssetClassID.BillboardRenderer => GetBitmap("UABEAvalonia/Assets/Icons/asset-billboard-renderer.png"),
                    AssetClassID.BoxCollider => GetBitmap("UABEAvalonia/Assets/Icons/asset-box-collider.png"),
                    AssetClassID.Camera => GetBitmap("UABEAvalonia/Assets/Icons/asset-camera.png"),
                    AssetClassID.Canvas => GetBitmap("UABEAvalonia/Assets/Icons/asset-canvas.png"),
                    AssetClassID.CanvasGroup => GetBitmap("UABEAvalonia/Assets/Icons/asset-canvas-group.png"),
                    AssetClassID.CanvasRenderer => GetBitmap("UABEAvalonia/Assets/Icons/asset-canvas-renderer.png"),
                    AssetClassID.CapsuleCollider => GetBitmap("UABEAvalonia/Assets/Icons/asset-capsule-collider.png"),
                    AssetClassID.CapsuleCollider2D => GetBitmap("UABEAvalonia/Assets/Icons/asset-capsule-collider.png"),
                    AssetClassID.ComputeShader => GetBitmap("UABEAvalonia/Assets/Icons/asset-compute-shader.png"),
                    AssetClassID.Cubemap => GetBitmap("UABEAvalonia/Assets/Icons/asset-cubemap.png"),
                    AssetClassID.Flare => GetBitmap("UABEAvalonia/Assets/Icons/asset-flare.png"),
                    AssetClassID.FlareLayer => GetBitmap("UABEAvalonia/Assets/Icons/asset-flare-layer.png"),
                    AssetClassID.Font => GetBitmap("UABEAvalonia/Assets/Icons/asset-font.png"),
                    AssetClassID.GameObject => GetBitmap("UABEAvalonia/Assets/Icons/asset-game-object.png"),
                    AssetClassID.Light => GetBitmap("UABEAvalonia/Assets/Icons/asset-light.png"),
                    AssetClassID.LightmapSettings => GetBitmap("UABEAvalonia/Assets/Icons/asset-lightmap-settings.png"),
                    AssetClassID.LODGroup => GetBitmap("UABEAvalonia/Assets/Icons/asset-lod-group.png"),
                    AssetClassID.Material => GetBitmap("UABEAvalonia/Assets/Icons/asset-material.png"),
                    AssetClassID.Mesh => GetBitmap("UABEAvalonia/Assets/Icons/asset-mesh.png"),
                    AssetClassID.MeshCollider => GetBitmap("UABEAvalonia/Assets/Icons/asset-mesh-collider.png"),
                    AssetClassID.MeshFilter => GetBitmap("UABEAvalonia/Assets/Icons/asset-mesh-filter.png"),
                    AssetClassID.MeshRenderer => GetBitmap("UABEAvalonia/Assets/Icons/asset-mesh-renderer.png"),
                    AssetClassID.MonoBehaviour => GetBitmap("UABEAvalonia/Assets/Icons/asset-mono-behaviour.png"),
                    AssetClassID.MonoScript => GetBitmap("UABEAvalonia/Assets/Icons/asset-mono-script.png"),
                    AssetClassID.NavMeshSettings => GetBitmap("UABEAvalonia/Assets/Icons/asset-nav-mesh-settings.png"),
                    AssetClassID.ParticleSystem => GetBitmap("UABEAvalonia/Assets/Icons/asset-particle-system.png"),
                    AssetClassID.ParticleSystemRenderer => GetBitmap("UABEAvalonia/Assets/Icons/asset-particle-system-renderer.png"),
                    AssetClassID.RectTransform => GetBitmap("UABEAvalonia/Assets/Icons/asset-rect-transform.png"),
                    AssetClassID.ReflectionProbe => GetBitmap("UABEAvalonia/Assets/Icons/asset-reflection-probe.png"),
                    AssetClassID.Rigidbody => GetBitmap("UABEAvalonia/Assets/Icons/asset-rigidbody.png"),
                    AssetClassID.Shader => GetBitmap("UABEAvalonia/Assets/Icons/asset-shader.png"),
                    AssetClassID.ShaderVariantCollection => GetBitmap("UABEAvalonia/Assets/Icons/asset-shader-collection.png"),
                    AssetClassID.SkinnedMeshRenderer => GetBitmap("UABEAvalonia/Assets/Icons/asset-mesh-renderer.png"), // todo
                    AssetClassID.Sprite => GetBitmap("UABEAvalonia/Assets/Icons/asset-sprite.png"),
                    AssetClassID.SpriteRenderer => GetBitmap("UABEAvalonia/Assets/Icons/asset-sprite-renderer.png"),
                    AssetClassID.Terrain => GetBitmap("UABEAvalonia/Assets/Icons/asset-terrain.png"),
                    AssetClassID.TerrainCollider => GetBitmap("UABEAvalonia/Assets/Icons/asset-terrain-collider.png"),
                    AssetClassID.TextAsset => GetBitmap("UABEAvalonia/Assets/Icons/asset-text-asset.png"),
                    AssetClassID.Texture2D => GetBitmap("UABEAvalonia/Assets/Icons/asset-texture2d.png"),
                    AssetClassID.Texture3D => GetBitmap("UABEAvalonia/Assets/Icons/asset-texture2d.png"),
                    AssetClassID.Transform => GetBitmap("UABEAvalonia/Assets/Icons/asset-transform.png"),
                    _ => GetBitmap("UABEAvalonia/Assets/Icons/asset-unknown.png"),
                };
            }

            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }


        Dictionary<string, Bitmap> cache = new();

        private Bitmap GetBitmap(string path)
        {
            Bitmap? bitmap;
            if (cache.TryGetValue(path, out bitmap))
            {
                return bitmap;
            }
            else
            {
                bitmap = new Bitmap(AssetLoader.Open(new Uri($"avares://{path}")));
                cache[path] = bitmap;
                return bitmap;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
