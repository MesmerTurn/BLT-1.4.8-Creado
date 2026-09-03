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
    // Sibling of CharacterPortraitRenderer.cs (2026-08-17) - identical pipeline, backed by
    // ItemImageIdentifierVM instead of CharacterImageIdentifierVM, for rendering individual
    // equipment slot icons (weapon/armor/shield/mount) next to a hero's portrait in the Hero
    // Appearance Gallery. Same proven tableau-rendering mechanism debugged today in
    // DocumentationGenerator.cs.
    public static class ItemPortraitRenderer
    {
        public static async Task<bool> RenderAsync(ItemObject item, string absoluteOutputPngPath, string logContext)
        {
            GauntletLayer layer = null;
            ImageIdentifierWidget widget = null;
            string tempRelativeName = $"blt_gallery_item_{Guid.NewGuid():N}.png";
            try
            {
                await MainThreadSync.RunWaitAsync(() =>
                {
                    var vm = new ItemImageIdentifierVM(item, "");
                    layer = new GauntletLayer("BLTGalleryItemTableauLayer", 200, false);
                    var movieId = layer.LoadMovie("BLTItemTableauCapture", vm);
                    ScreenManager.TopScreen?.AddLayer(layer);
                    widget = movieId?.Movie?.RootWidget?.GetChild(0) as ImageIdentifierWidget;
                });

                if (widget == null)
                {
                    Log.Error($"ItemPortraitRenderer: ImageIdentifierWidget not found in the BLTItemTableauCapture prefab for '{logContext}' - broken link.");
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
                    Log.Error($"ItemPortraitRenderer: tableau for '{logContext}' never rendered a texture in time.");
                    return false;
                }

                return SaveTexture(captured, tempRelativeName, absoluteOutputPngPath, logContext);
            }
            catch (Exception ex)
            {
                Log.Exception("ItemPortraitRenderer.RenderAsync", ex);
                return false;
            }
            finally
            {
                if (layer != null)
                {
                    await MainThreadSync.RunWaitAsync(() =>
                    {
                        try { ScreenManager.TopScreen?.RemoveLayer(layer); }
                        catch (Exception ex) { Log.Exception("ItemPortraitRenderer: RemoveLayer", ex); }
                    });
                }
            }
        }

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
                    Log.Error($"ItemPortraitRenderer: couldn't export image for '{logContext}'.");
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
                Log.Exception("ItemPortraitRenderer.SaveTexture", ex);
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
