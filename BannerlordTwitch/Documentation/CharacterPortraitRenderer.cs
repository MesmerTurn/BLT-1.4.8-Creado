using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using BannerlordTwitch.Util;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;
using TaleWorlds.ScreenSystem;
using Path = System.IO.Path;

namespace BannerlordTwitch.Documentation
{
    // Standalone reuse of the character-portrait tableau rendering pipeline debugged in
    // DocumentationGenerator.cs on 2026-08-17 (root-caused there: dead TableauCacheManager call,
    // wrong texture-unwrap type, wrong SaveToFile bool arg, wrong file-existence check path,
    // GetChild(0) instead of FindChildrenWithType). Extracted here so other features (the Hero
    // Appearance Gallery) can render a single character portrait on demand without depending on
    // DocumentationGenerator's queue/instance state. DocumentationGenerator itself is untouched -
    // it keeps using its own already-working code path.
    public static class CharacterPortraitRenderer
    {
        // Renders a single character portrait and writes it to absoluteOutputPngPath.
        // Must be called from a context where awaiting is fine (not directly on the game's main
        // thread) - internally it hops onto the main thread via MainThreadSync.RunWaitAsync for
        // every engine call, exactly like the proven DocumentationGenerator code.
        // Returns true on success, false on any failure (already logged internally).
        public static async Task<bool> RenderAsync(CharacterCode cc, string absoluteOutputPngPath, string logContext)
        {
            GauntletLayer layer = null;
            ImageIdentifierWidget widget = null;
            string tempRelativeName = $"blt_gallery_{Guid.NewGuid():N}.png";
            try
            {
                await MainThreadSync.RunWaitAsync(() =>
                {
                    var vm = new CharacterImageIdentifierVM(cc);
                    layer = new GauntletLayer("BLTGalleryCharacterTableauLayer", 200, false);
                    var movieId = layer.LoadMovie("BLTItemTableauCapture", vm);
                    ScreenManager.TopScreen?.AddLayer(layer);
                    widget = movieId?.Movie?.RootWidget?.GetChild(0) as ImageIdentifierWidget;
                });

                if (widget == null)
                {
                    Log.Error($"CharacterPortraitRenderer: ImageIdentifierWidget not found in the BLTItemTableauCapture prefab for '{logContext}' - broken link.");
                    return false;
                }

                Texture captured = null;
                for (int i = 0; i < 80 && captured == null; i++)
                {
                    await Task.Delay(50);
                    await MainThreadSync.RunWaitAsync(() =>
                    {
                        var current = widget.Texture;
                        if (current != null
                            && current.PlatformTexture is EngineTexture engineTexture)
                        {
                            captured = engineTexture.Texture;
                        }
                    });
                }

                if (captured == null)
                {
                    Log.Error($"CharacterPortraitRenderer: tableau for '{logContext}' never rendered a texture in time.");
                    return false;
                }

                return SaveTexture(captured, tempRelativeName, absoluteOutputPngPath, logContext);
            }
            catch (Exception ex)
            {
                Log.Exception("CharacterPortraitRenderer.RenderAsync", ex);
                return false;
            }
            finally
            {
                if (layer != null)
                {
                    await MainThreadSync.RunWaitAsync(() =>
                    {
                        try { ScreenManager.TopScreen?.RemoveLayer(layer); }
                        catch (Exception ex) { Log.Exception("CharacterPortraitRenderer: RemoveLayer", ex); }
                    });
                }
            }
        }

        // Same relative-path/channel-swap handling as DocumentationGenerator.TextureComplete -
        // Texture.SaveToFile resolves relative paths against the engine's own bin folder
        // (AppDomain.CurrentDomain.BaseDirectory), not this process's CWD, and the raw engine
        // export has red/blue channels swapped.
        private static bool SaveTexture(Texture texture, string tempRelativeName, string absoluteOutputPngPath, string logContext)
        {
            try
            {
                string enginePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, tempRelativeName);
                texture.TransformRenderTargetToResource(tempRelativeName);
                texture.SaveToFile(tempRelativeName, true);

                for (int i = 0; i < 100 && !File.Exists(enginePath); i++)
                {
                    System.Threading.Thread.Sleep(100);
                }

                if (!File.Exists(enginePath))
                {
                    Log.Error($"CharacterPortraitRenderer: couldn't export image for '{logContext}'.");
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPngPath) ?? ".");
                if (File.Exists(absoluteOutputPngPath))
                {
                    File.Delete(absoluteOutputPngPath);
                }

                using (var bitmap = new Bitmap(enginePath))
                {
                    var corrected = SwapRedAndBlueChannels(bitmap);
                    corrected.Save(absoluteOutputPngPath);
                }

                File.Delete(enginePath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception("CharacterPortraitRenderer.SaveTexture", ex);
                return false;
            }
        }

        private static Bitmap SwapRedAndBlueChannels(Bitmap bitmap)
        {
            var imageAttr = new ImageAttributes();
            imageAttr.SetColorMatrix(new(
                new[]
                {
                    new[] {0.0F, 0.0F, 1.0F, 0.0F, 0.0F},
                    new[] {0.0F, 1.0F, 0.0F, 0.0F, 0.0F},
                    new[] {1.0F, 0.0F, 0.0F, 0.0F, 0.0F},
                    new[] {0.0F, 0.0F, 0.0F, 1.0F, 0.0F},
                    new[] {0.0F, 0.0F, 0.0F, 0.0F, 1.0F}
                }
            ));
            var temp = new Bitmap(bitmap.Width, bitmap.Height);
            var pixel = GraphicsUnit.Pixel;
            using var g = Graphics.FromImage(temp);
            g.DrawImage(bitmap, Rectangle.Round(bitmap.GetBounds(ref pixel)), 0, 0,
                bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, imageAttr);
            return temp;
        }
    }
}
