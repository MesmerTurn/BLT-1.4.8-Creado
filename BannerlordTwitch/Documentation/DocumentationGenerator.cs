using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BannerlordTwitch.Util;
using HarmonyLib;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Tableaus;
using Path = System.IO.Path;

namespace BannerlordTwitch
{
    [HarmonyPatch]
    public class DocumentationGenerator : IDocumentationGenerator
    {
        private int anchor;
        private readonly List<string> toc = new();
        private readonly List<string> content = new();

        private static readonly string CSSFileName = "Bannerlord-Twitch-Documentation.css";
        private static string CSSFullPath => Path.Combine(Path.GetDirectoryName(typeof(DocumentationGenerator).Assembly.Location) ?? ".", "..", "..", CSSFileName);

        // Moves the page's existing top-level sections (matched by their existing CSS
        // classes, unchanged) into 4 tab panels and wires up the tab buttons.
        // .commands/.rewards/.class-config/.perks-config -> Hero
        // .upgrade-system-wrapper -> Clans and Kingdoms
        // .common-config -> General Settings
        // .campaign-map-wrapper -> Campaign Map
        private const string TabScript = @"
<script>
(function () {
    var groups = {
        'tab-hero': ['.commands', '.rewards', '.class-config', '.perks-config'],
        'tab-clans': ['.upgrade-system-wrapper'],
        'tab-settings': ['.common-config'],
        'tab-map': ['.campaign-map-wrapper']
    };
    var contentEl = document.querySelector('.content');
    if (!contentEl) return;
    var toc = contentEl.querySelector('.toc-container');
    var anchor = toc || contentEl.querySelector('.tab-bar');
    if (!anchor) return;

    var panels = {};
    Object.keys(groups).forEach(function (tabId) {
        var panel = document.createElement('div');
        panel.className = 'tab-panel';
        panel.id = tabId;
        panels[tabId] = panel;
        anchor.parentNode.insertBefore(panel, anchor.nextSibling);
    });

    Object.keys(groups).forEach(function (tabId) {
        groups[tabId].forEach(function (sel) {
            var el = contentEl.querySelector(sel);
            if (el) panels[tabId].appendChild(el);
        });
    });

    panels['tab-hero'].classList.add('active');

    var buttons = contentEl.querySelectorAll('.tab-button');
    buttons.forEach(function (btn) {
        btn.addEventListener('click', function () {
            buttons.forEach(function (b) { b.classList.remove('active'); });
            Object.keys(panels).forEach(function (id) { panels[id].classList.remove('active'); });
            btn.classList.add('active');
            panels[btn.getAttribute('data-tab')].classList.add('active');
        });
    });
})();
</script>";

        public async Task Document(IDocumentable documentable)
        {
            // Make sure previous image writes are all complete or aborted
            await WaitForPendingImagesAsync();
            await MainThreadSync.RunWaitAsync(() => documentable.GenerateDocumentation(this));
        }

        public static string DocumentationRootDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord",
            "Configs", "BLT-documentation");

        public static string DocumentationPath => Path.Combine(DocumentationRootDir, "index.html");

        public async Task SaveAsync(string title, string introduction, bool addTOC = true)
        {
            // Wait for image writes first
            await WaitForPendingImagesAsync();

            await MainThreadSync.RunWaitAsync(() =>
            {
                if (addTOC)
                {
                    toc.InsertRange(0, new[]
                    {
                        "<div class=\"toc-container\">",
                        "<h2 class=\"toc-title\">Table of Contents</h2>"
                    });
                    toc.Add("</div>");
                    content.InsertRange(0, toc);
                }

                // Top-level nav: Hero / Clans and Kingdoms / General Settings / Campaign Map.
                // Sits between the intro paragraph and the Table of Contents. Groups the
                // page's existing top-level sections (identified by their existing CSS
                // classes - no change to how/when those sections are generated) into 4
                // panels via a small script at the end of the page, since the sections are
                // written to `content` sequentially and flat, not as a tree Claude can
                // re-parent from C# alone.
                content.InsertRange(0, new[]
                {
                    "<div class=\"tab-bar\">",
                    "<button class=\"tab-button active\" data-tab=\"tab-hero\" type=\"button\">Hero</button>",
                    "<button class=\"tab-button\" data-tab=\"tab-clans\" type=\"button\">Clans and Kingdoms</button>",
                    "<button class=\"tab-button\" data-tab=\"tab-settings\" type=\"button\">General Settings</button>",
                    "<button class=\"tab-button\" data-tab=\"tab-map\" type=\"button\">Campaign Map</button>",
                    "</div>"
                });

                content.InsertRange(0, new[]
                {
                    "<!DOCTYPE html><html>",
                    "<head>",
                    "<meta charset=\"utf-8\"/>",
                    "<link rel=\"stylesheet\" href=\"style.css\">",
                    "</head>",
                    "<body>",
                    "<div class=\"content\">",
                    $"<h1>{title}</h1>",
                    $"<p>{introduction}</p>"
                });

                content.Add(TabScript);
                content.Add("</div></html></body>");

                try
                {
                    Directory.CreateDirectory(DocumentationRootDir);
                    File.WriteAllLines(DocumentationPath, content);
                    string targetCSSFilePath = Path.Combine(DocumentationRootDir, "style.css");
                    if (File.Exists(targetCSSFilePath))
                        File.Delete(targetCSSFilePath);
                    File.Copy(CSSFullPath, targetCSSFilePath);
                }
                catch (Exception e)
                {
                    Log.Error($"Couldn't write documentation: {e.Message}");
                }
            });
        }

        // public void SavePdf()
        // {
        //     //var cssData = PdfGenerator.ParseStyleSheet(File.ReadAllText(CSSFullPath));
        //     Save();
        //     
        //     var pdf = PdfGenerator.GeneratePdf(string.Join("\n", docs), 
        //         new PdfGenerateConfig
        //         {
        //             //PageSize = PageSize.A0,
        //             ManualPageSize = XSize.FromSize(new (1000, 4000))
        //         },
        //         //cssData: cssData,
        //         stylesheetLoad: (sender, args) =>
        //         {
        //             args.SetStyleSheetData = PdfGenerator.ParseStyleSheet(
        //                 File.ReadAllText(Path.Combine(DocumentationRootDir, args.Src))
        //                 );
        //         }, 
        //         imageLoad: (sender, args) =>
        //         {
        //             args.Callback(Path.Combine(DocumentationRootDir, args.Src));
        //         });
        //
        //     pdf.Save(Path.Combine(DocumentationRootDir, "blt-docs.pdf"));
        // }

        // private static string LinkToAnchor(string text, string anchorTag = null)
        //     => $"<a href=\"#{text}{anchorTag ?? ""}\">{text}</a>";
        //
        // private static string MakeAnchor(string text, string anchorTag = null)
        //     => $"<a name=\"{text}{anchorTag ?? ""}\">{text}</a>";

        private IDocumentationGenerator ScopedTag(string tag, string css, Action content)
        {
            this.content.Add(css != null ? $"<{tag} class=\"{css}\">" : $"<{tag}>");
            content();
            this.content.Add($"</{tag}>");
            return this;
        }

        private IDocumentationGenerator Tag(string tag, string css, string content)
        {
            this.content.Add(
                css != null
                    ? $"<{tag} class={css}>{content}</{tag}>"
                    : $"<{tag}>{content}</{tag}>"
                );
            return this;
        }

        public IDocumentationGenerator Div(string css, Action content) => ScopedTag("div", css, content);
        public IDocumentationGenerator Div(Action content) => Div(null, content);

        public IDocumentationGenerator Details(string css, Action content) => ScopedTag("details", css, content);
        public IDocumentationGenerator Details(Action content) => Details(null, content);

        public IDocumentationGenerator Summary(string css, Action content) => ScopedTag("summary", css, content);
        public IDocumentationGenerator Summary(Action content) => Summary(null, content);
        public IDocumentationGenerator Summary(string css, string content) => Tag("summary", css, content);
        public IDocumentationGenerator Summary(string content) => Summary(null, content);

        public IDocumentationGenerator H1(string css, string content)
        {
            toc.Add($"<a href=\"#{++anchor}\"><h1 class=\"toc-h1\">{content}</h1></a>");
            MakeAnchor($"{anchor}", "");
            return Tag("h1", css, content);
        }

        public IDocumentationGenerator H1(string content) => H1(null, content);

        public IDocumentationGenerator H2(string css, string content)
        {
            toc.Add($"<a href=\"#{++anchor}\"><h2 class=\"toc-h2\">{content}</h2></a>");
            MakeAnchor($"{anchor}", "");
            return Tag("h2", css, content);
        }

        public IDocumentationGenerator H2(string content) => H2(null, content);

        public IDocumentationGenerator H3(string css, string content)
        {
            toc.Add($"<a href=\"#{++anchor}\"><h3 class=\"toc-h3\">{content}</h3></a>");
            MakeAnchor($"{anchor}", "");
            return Tag("h3", css, content);
        }

        public IDocumentationGenerator H3(string content) => H3(null, content);

        public IDocumentationGenerator Table(string css, Action content, bool collapsible = true, string summary = "")
        {
            if (!collapsible)
                return ScopedTag("table", css, content);

            return Details(() =>
            {
                Summary(summary);
                ScopedTag("table", css, content);
            });
        }
        public IDocumentationGenerator Table(Action content, bool collapsible = true, string summary = "")
        {
            return Table(null, content, collapsible, summary);
        }

        public IDocumentationGenerator TR(string css, Action content) => ScopedTag("tr", css, content);
        public IDocumentationGenerator TR(Action content) => TR(null, content);
        public IDocumentationGenerator TR(string css, string content) => Tag("tr", css, content);
        public IDocumentationGenerator TR(string content) => TR(null, content);

        public IDocumentationGenerator TH(string css, Action content) => ScopedTag("th", css, content);
        public IDocumentationGenerator TH(Action content) => TH(null, content);
        public IDocumentationGenerator TH(string css, string content) => Tag("th", css, content);
        public IDocumentationGenerator TH(string content) => TH(null, content);

        public IDocumentationGenerator TD(string css, Action content) => ScopedTag("td", css, content);
        public IDocumentationGenerator TD(Action content) => TD(null, content);
        public IDocumentationGenerator TD(string css, string content) => Tag("td", css, content);
        public IDocumentationGenerator TD(string content) => TD(null, content);

        public IDocumentationGenerator P(string css, string content) => Tag("p", css, content);
        public IDocumentationGenerator P(string content) => P(null, content);

        public IDocumentationGenerator Br()
        {
            content.Add("<br>");
            return this;
        }

        private int imageId;
        private readonly ConcurrentDictionary<string, object> pendingImages = new();

        private async Task WaitForPendingImagesAsync()
        {
            // With a large class/power roster this can be hundreds of tableau renders - the old
            // 10 second cap gave up long before they finished, silently dropping the rest (leaving
            // broken <img> links in the generated docs). Scale the wait with how many images are
            // actually still pending instead of a fixed short timeout, with a generous hard cap.
            int maxWaitMs = Math.Max(10_000, pendingImages.Count * 500);
            int elapsedMs = 0;
            while (!pendingImages.IsEmpty && elapsedMs < maxWaitMs)
            {
                await Task.Delay(100);
                elapsedMs += 100;
            }

            if (!pendingImages.IsEmpty)
            {
                Log.Error($"DocumentationGenerator: {pendingImages.Count} image(s) still not rendered after {maxWaitMs}ms, giving up on them (they will be broken links in the output).");
            }

            pendingImages.Clear();
        }

        public IDocumentationGenerator Img(ItemObject item) => Img(null, item);
        public IDocumentationGenerator Img(string css, ItemObject item)
        {
            string localPath = AddImage(css, item.Name.ToString());
            try
            {
                if (File.Exists(localPath))
                    File.Delete(localPath);
            }
            catch
            {
                // ignored
            }
            pendingImages.TryAdd(localPath, null);

#if e159 || e1510
            TableauCacheManager.Current.BeginCreateItemTexture(item, 
                texture => TextureComplete(item.Name.ToString(), localPath, texture));
#else
            //TableauCacheManager.Current.BeginCreateItemTexture(item,
            //    Hero.MainHero.ClanBanner.Serialize(),
            //    texture => TextureComplete(item.Name.ToString(), localPath, texture));
#endif
            return this;
        }

        public IDocumentationGenerator Img(CharacterCode cc, string altText) => Img(null, cc, altText);
        public IDocumentationGenerator Img(string css, CharacterCode cc, string altText)
        {
            string localPath = AddImage(css, altText);
            try
            {
                if (File.Exists(localPath))
                    File.Delete(localPath);
            }
            catch
            {
                // ignored
            }
            pendingImages.TryAdd(localPath, null);

            overrideRenderSettings = camera =>
            {
                //camera.SetViewVolume(false, -500, 500, 0, 1000, -500, 500);
                camera.Position -= camera.Direction * 1.2f;
                camera.Position -= Vec3.Up * 0.6f;
                camera.SetFovHorizontal(camera.GetFovHorizontal(), 120f / 256f, 0.1f, 1000f);
                return (120, 256);
            };
            //TableauCacheManager.Current.BeginCreateCharacterTexture(cc,
            //    texture => TextureComplete(altText, localPath, texture), true);
            return this;
        }

        public IDocumentationGenerator MakeAnchor(string tag, Action content)
        {
            this.content.Add($"<a name=\"{tag}\">");
            content();
            this.content.Add("</a>");
            return this;
        }

        public IDocumentationGenerator MakeAnchor(string tag, string content)
        {
            this.content.Add($"<a name=\"{tag}\">{content}</a>");
            return this;
        }

        public IDocumentationGenerator LinkToAnchor(string tag, Action content)
        {
            this.content.Add($"<a href=\"#{tag}\">");
            content();
            this.content.Add("</a>");
            return this;
        }

        public IDocumentationGenerator LinkToAnchor(string tag, string content)
        {
            this.content.Add($"<a href=\"#{tag}\">{content}</a>");
            return this;
        }

        private Bitmap SwapRedAndBlueChannels(Bitmap bitmap)
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

        private async void TextureComplete(string name, string localPath, Texture texture)
        {
            try
            {
                string path = Path.Combine(DocumentationRootDir, localPath);
                texture.TransformRenderTargetToResource(localPath);
                texture.SaveToFile(localPath, false);
                for (int i = 0; i < 100 && !File.Exists(localPath); i++)
                {
                    await Task.Delay(100);
                }

                if (File.Exists(localPath))
                {
                    Directory.CreateDirectory(DocumentationRootDir);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    // Scoped to make sure it gets closed and disposed
                    using (var bitmap = new Bitmap(localPath))
                    {
                        var corrected = SwapRedAndBlueChannels(bitmap);
                        corrected.Save(path);
                    }

                    File.Delete(localPath);
                }
                else
                {
                    Log.Error($"Couldn't export image for {name} to {localPath}");
                }
            }
            catch (Exception e)
            {
                Log.Exception("Img", e);
            }

            pendingImages.TryRemove(localPath, out _);
        }

        private string AddImage(string css, string name)
        {
            string localPath = $"blt_img_{++imageId}.png";
            if (File.Exists(localPath))
                File.Delete(localPath);
            content.Add(css == null
                ? $"<img src=\"{localPath}\" alt=\"{name}\">"
                : $"<img class=\"{css}\" src=\"{localPath}\" alt=\"{name}\">");
            return localPath;
        }

        private static Func<Camera, (int, int)> overrideRenderSettings;

        //[HarmonyPatch(typeof(ThumbnailRenderRequest), nameof(ThumbnailRenderRequest.CreateForCachedEntityWithoutTexture)), HarmonyPrefix, UsedImplicitly]
        //private static void CreateForCachedEntityWithoutTexture(Camera camera, ref int width, ref int height)
        //{
        //    if (overrideRenderSettings != null)
        //    {
        //        (width, height) = overrideRenderSettings(camera);
        //        overrideRenderSettings = null;
        //    }
        //}

        //
        // private static GameEntity CreateCharacterBaseEntityPostfix(
        //     CharacterCode characterCode,
        //     Scene scene,
        //     ref Camera camera,
        //     bool isBig)
        // {
        //     
        // }
        // public static void BeginCreateCharacterTexture(CharacterCode characterCode, Action<Texture> setAction, bool isBig)
        // {
        //     if (MBObjectManager.Instance == null)
        //         return;
        //
        //     characterCode.BodyProperties = new (
        //         new (
        //             (int) characterCode.BodyProperties.Age, 
        //             (int) characterCode.BodyProperties.Weight, 
        //             (int) characterCode.BodyProperties.Build), 
        //         characterCode.BodyProperties.StaticProperties);
        //     string str = characterCode.CreateNewCodeString() + (isBig ? "1" : "0") + "_blt";
        //     Texture texture;
        //
        //     var _characterVisuals = (ThumbnailCache) AccessTools.Field(
        //         typeof(TableauCacheManager), "_characterVisuals").GetValue(TableauCacheManager.Current);
        //     var _renderCallbacks = (Dictionary<string, TableauCacheManager.RenderDetails>) AccessTools.Field(
        //         typeof(TableauCacheManager), "_renderCallbacks").GetValue(TableauCacheManager.Current);
        //     if (_characterVisuals.GetValue(str, out texture))
        //     {
        //         if (this._renderCallbacks.ContainsKey(str))
        //             this._renderCallbacks[str].Actions.Add(setAction);
        //         else if (setAction != null)
        //             setAction(texture);
        //         _characterVisuals.AddReference(str);
        //     }
        //     else
        //     {
        //         Camera camera = (Camera) null;
        //         int index = isBig ? 0 : 4;
        //         GameEntity characterBaseEntity = this.CreateCharacterBaseEntity(characterCode,
        //             BannerlordTableauManager.TableauCharacterScenes[index], ref camera, isBig);
        //         GameEntity entity = this.FillEntityWithPose(characterCode, characterBaseEntity,
        //             BannerlordTableauManager.TableauCharacterScenes[index]);
        //         int width = 256;
        //         int height = isBig ? 120 : 184;
        //         this._thumbnailCreatorView.RegisterEntityWithoutTexture(
        //             BannerlordTableauManager.TableauCharacterScenes[index], camera, entity, width, height,
        //             this.characterTableauGPUAllocationIndex, str,
        //             "character_tableau_" + this._characterCount.ToString());
        //         ++this._characterCount;
        //         _characterVisuals.Add(str, (Texture) null);
        //         _characterVisuals.AddReference(str);
        //         if (!this._renderCallbacks.ContainsKey(str))
        //             this._renderCallbacks.Add(str, new TableauCacheManager.RenderDetails(new List<Action<Texture>>()));
        //         this._renderCallbacks[str].Actions.Add(setAction);
        //     }
        // }
        public IDocumentationGenerator MapLabel(float x, float y, string name, string type, string kingdomId, Func<string, string> getFillColor, Func<string, string> getBorderColor)
        {
            // Determine shape
            string shapeStyle = type switch
            {
                "Castle" => "border-radius:0%;",  // square
                "Town" => "border-radius:50%;",   // circle
                _ => "border-radius:25%;"         // rounded default
            };

            string fillColor = "#000080";
            string borderColor = "#000000";

            if (!string.IsNullOrEmpty(kingdomId))
            {
                // Get fill and border colors
                if (getFillColor != null)
                {
                    string c = getFillColor(kingdomId);
                    if (!string.IsNullOrEmpty(c))
                        fillColor = c.StartsWith("#") ? c : "#" + c;
                }

                if (getBorderColor != null)
                {
                    string c = getBorderColor(kingdomId);
                    if (!string.IsNullOrEmpty(c))
                        borderColor = c.StartsWith("#") ? c : "#" + c;
                }
            }                             

            string size = "12px";

            return Div(() =>
            {
                // Marker
                P($"<div style=\"position:absolute; left:{x}px; top:{y}px;" +
                  "transform:translate(-50%,-50%);" +
                  $"width:{size}; height:{size}; background:{fillColor}; {shapeStyle};" +
                  $"border:1px solid {borderColor}; box-shadow:1px 1px 2px rgba(0,0,0,0.5);\"></div>");

                // Name label slightly below marker
                P($"<div style=\"position:absolute; left:{x}px; top:{y + 8}px;" +
                  "transform:translate(-50%,0); font-size:10px; font-weight:bold;" +
                  "text-shadow:1px 1px 2px #000; white-space:nowrap;\">" +
                  $"{name}</div>");
            });
        }

        public IDocumentationGenerator MapSegment(float x1, float y1, float x2, float y2)
        {
            string color = "#2b5d87"; float thickness = 2f;
            float dx = x2 - x1;
            float dy = y2 - y1;

            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            float angle = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);

            return Div(() =>
            {
                P($"<div style=\"position:absolute;" +
                  $"left:{x1}px;" +
                  $"top:{y1}px;" +
                  $"width:{length}px;" +
                  $"height:{thickness}px;" +
                  $"background:{color};" +
                  "transform-origin:0 50%;" +
                  $"transform:rotate({angle}deg);" +
                  "box-shadow:0 0 2px rgba(0,0,0,0.4);" +
                  "\"></div>");
            });
        }

        // ════════════════════════════════════════════════════════════════
        //  PERKS — "Constellation" rendering (2026-08-11 perk system design)
        //  Same absolutely-positioned-div approach as MapLabel/MapSegment above,
        //  not real SVG - matching this file's own established pattern.
        // ════════════════════════════════════════════════════════════════

        public IDocumentationGenerator PerkNode(float x, float y, int number, string name, bool unlocked)
        {
            string glow = unlocked ? "0 0 8px #ffd700, 0 0 16px #6b46c1" : "none";
            string color = unlocked ? "#ffd700" : "#8a7aa0";
            string opacity = unlocked ? "1" : "0.5";
            // Circled-digit unicode block starts at U+2460 (①) for 1, covers up to 20 (⑳).
            string numberGlyph = (number >= 1 && number <= 20) ? char.ConvertFromUtf32(0x2460 + number - 1) : number.ToString();

            return Div(() =>
            {
                P($"<div style=\"position:absolute; left:{x}px; top:{y}px;" +
                  "transform:translate(-50%,-50%);" +
                  $"width:14px; height:14px; border-radius:50%; background:{color};" +
                  $"box-shadow:{glow}; opacity:{opacity};\"></div>");

                P($"<div style=\"position:absolute; left:{x}px; top:{y - 18}px;" +
                  "transform:translate(-50%,0); font-size:13px; font-family:Georgia,serif;" +
                  $"color:{color}; text-shadow:0 0 4px #6b46c1; opacity:{opacity};\">" +
                  $"{numberGlyph}</div>");

                P($"<div style=\"position:absolute; left:{x}px; top:{y + 12}px;" +
                  "transform:translate(-50%,0); font-size:10px; font-family:Georgia,serif;" +
                  $"color:{color}; opacity:{opacity}; white-space:nowrap;\">{name}</div>");
            });
        }

        public IDocumentationGenerator PerkLine(float x1, float y1, float x2, float y2)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            float angle = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);

            return Div(() =>
            {
                P($"<div style=\"position:absolute; left:{x1}px; top:{y1}px;" +
                  $"width:{length}px; height:1px; background:#d4af37; opacity:0.6;" +
                  "transform-origin:0 50%;" +
                  $"transform:rotate({angle}deg);\"></div>");
            });
        }

        // Renders one branch's constellation + numbered legend table. Decoupled from any specific
        // perk-addon type (MakeBltGreatAgain.dll is optional and this is core BannerlordTwitch.dll,
        // which must not take a hard reference on an addon) - perks are passed as primitive tuples,
        // the same way MapLabel/MapSegment take primitive floats/strings rather than domain
        // objects. The caller (an addon, via reflection - see Settings.GenerateDocumentation)
        // supplies the ordered perk list for one branch and a rank-lookup delegate.
        public IDocumentationGenerator PerksSection(
            IEnumerable<(string BranchName, IEnumerable<(string Key, string DisplayName, float BonusPerRank, string RequiredPerkKey, string RequirementText)> Perks)> branches,
            Func<string, int> getRank)
        {
            return Div(() =>
            {
                H2("Perks");
                foreach (var branch in branches)
                {
                    H3(branch.BranchName);
                    var ordered = branch.Perks.ToList();
                    var coords = new Dictionary<string, (float x, float y)>();
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        coords[ordered[i].Key] = (40f + i * 60f, 40f + (i % 2 == 0 ? 0f : 30f));
                    }

                    Div("perk-constellation", () =>
                    {
                        foreach (var p in ordered)
                        {
                            if (!string.IsNullOrEmpty(p.RequiredPerkKey) && coords.TryGetValue(p.RequiredPerkKey, out var prev) && coords.TryGetValue(p.Key, out var cur))
                            {
                                PerkLine(prev.x, prev.y, cur.x, cur.y);
                            }
                        }
                        int num = 1;
                        foreach (var p in ordered)
                        {
                            var c = coords[p.Key];
                            bool unlocked = getRank(p.Key) > 0;
                            PerkNode(c.x, c.y, num, p.DisplayName, unlocked);
                            num++;
                        }
                    });

                    Table(() =>
                    {
                        TR(() => { TH("#"); TH("Perk"); TH("Per Rank"); TH("Requires"); });
                        int legendNum = 1;
                        foreach (var p in ordered)
                        {
                            TR(() =>
                            {
                                TD(legendNum.ToString());
                                TD(p.DisplayName);
                                TD($"+{p.BonusPerRank * 100:0.#}%");
                                TD(string.IsNullOrEmpty(p.RequirementText) ? "-" : p.RequirementText);
                            });
                            legendNum++;
                        }
                    });
                }
            });
        }
    }
}