using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AssetClassID assetClass)
            {
                var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                if (assets == null)
                    return null;

                if ((int)assetClass < 0)
                {
                    return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-mono-behaviour.png");
                }

                switch (assetClass)
                {
                    case AssetClassID.Animation: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-animation.png");
                    case AssetClassID.AnimationClip: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-animation-clip.png");
                    case AssetClassID.Animator: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-animator.png");
                    case AssetClassID.AnimatorController: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-animator-controller.png");
                    case AssetClassID.AnimatorOverrideController: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-animator-override-controller.png");
                    case AssetClassID.AudioClip: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-audio-clip.png");
                    case AssetClassID.AudioListener: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-audio-listener.png");
                    case AssetClassID.AudioMixer: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-audio-mixer.png");
                    case AssetClassID.AudioMixerGroup: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-audio-mixer-group.png");
                    case AssetClassID.AudioSource: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-audio-source.png");
                    case AssetClassID.Avatar: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-avatar.png");
                    case AssetClassID.BillboardAsset: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-billboard.png");
                    case AssetClassID.BillboardRenderer: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-billboard-renderer.png");
                    case AssetClassID.BoxCollider: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-box-collider.png");
                    case AssetClassID.Camera: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-camera.png");
                    case AssetClassID.Canvas: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-canvas.png");
                    case AssetClassID.CanvasGroup: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-canvas-group.png");
                    case AssetClassID.CanvasRenderer: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-canvas-renderer.png");
                    case AssetClassID.CapsuleCollider: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-capsule-collider.png");
                    case AssetClassID.CapsuleCollider2D: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-capsule-collider.png");
                    case AssetClassID.ComputeShader: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-compute-shader.png");
                    case AssetClassID.Cubemap: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-cubemap.png");
                    case AssetClassID.Flare: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-flare.png");
                    case AssetClassID.FlareLayer: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-flare-layer.png");
                    case AssetClassID.Font: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-font.png");
                    case AssetClassID.GameObject: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-game-object.png");
                    case AssetClassID.Light: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-light.png");
                    case AssetClassID.LightmapSettings: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-lightmap-settings.png");
                    case AssetClassID.LODGroup: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-lod-group.png");
                    case AssetClassID.Material: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-material.png");
                    case AssetClassID.Mesh: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-mesh.png");
                    case AssetClassID.MeshCollider: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-mesh-collider.png");
                    case AssetClassID.MeshFilter: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-mesh-filter.png");
                    case AssetClassID.MeshRenderer: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-mesh-renderer.png");
                    case AssetClassID.MonoBehaviour: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-mono-behaviour.png");
                    case AssetClassID.MonoScript: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-mono-script.png");
                    case AssetClassID.NavMeshSettings: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-nav-mesh-settings.png");
                    case AssetClassID.ParticleSystem: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-particle-system.png");
                    case AssetClassID.ParticleSystemRenderer: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-particle-system-renderer.png");
                    case AssetClassID.RectTransform: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-rect-transform.png");
                    case AssetClassID.ReflectionProbe: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-reflection-probe.png");
                    case AssetClassID.Rigidbody: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-rigidbody.png");
                    case AssetClassID.Shader: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-shader.png");
                    case AssetClassID.ShaderVariantCollection: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-shader-collection.png");
                    case AssetClassID.Sprite: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-sprite.png");
                    case AssetClassID.SpriteRenderer: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-sprite-renderer.png");
                    case AssetClassID.Terrain: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-terrain.png");
                    case AssetClassID.TerrainCollider: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-terrain-collider.png");
                    case AssetClassID.Texture2D: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-texture2d.png");
                    case AssetClassID.Texture3D: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-texture2d.png");
                    case AssetClassID.Transform: return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-transform.png");
                    default:
                        return GetBitmap(assets, "UABEAvalonia/Assets/Icons/asset-unknown.png");
                }
            }

            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        private Bitmap GetBitmap(IAssetLoader loader, string path)
        {
            return new Bitmap(loader.Open(new Uri($"avares://{path}")));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
