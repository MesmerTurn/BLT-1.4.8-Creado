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
            // 2026-08-12: was Max(10_000, count*500) - with a large class/item roster that cap
            // could reach 30-50s+, and every render that genuinely stalls burns nearly the whole
            // cap on every single doc generation. Tightened per-image budget and poll interval so
            // a stalled render gives up sooner while a normal one (which exits early via
            // pendingImages.IsEmpty, long before hitting the cap) is unaffected.
            int maxWaitMs = Math.Max(4_000, pendingImages.Count * 150);
            int elapsedMs = 0;
            while (!pendingImages.IsEmpty && elapsedMs < maxWaitMs)
            {
                await Task.Delay(50);
                elapsedMs += 50;
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

        // One distinct color per branch (cycled by branch index) so the constellation reads as
        // several different star-groups fanning out, not one monochrome gold web. Locked nodes
        // ignore this and stay muted gray regardless of branch - color = "you have this."
        private static readonly string[] BranchColors =
        {
            "#ffd700", // gold
            "#66e0ff", // cyan
            "#ff6bcb", // pink
            "#7cff6b", // green
            "#ff9d42", // orange
            "#a78bfa", // violet
            "#ff5c5c", // red
            "#5ca8ff", // blue
            "#e0ff5c", // lime
            "#ff8ccf", // rose
            "#5cffe0", // teal
            "#ffb85c", // amber
            "#c084fc", // fuchsia
            "#4ade80", // emerald
            "#fb7185", // coral
            "#38bdf8", // sky
            "#facc15", // yellow
            "#f472b6", // magenta
        };

        public static string PerkBranchColor(int branchIndex) => BranchColors[((branchIndex % BranchColors.Length) + BranchColors.Length) % BranchColors.Length];

        // A representative glyph per branch, matched by keyword against the branch name - this
        // file (core BannerlordTwitch.dll) can't take a hard reference on any specific addon's
        // branch list, so icons are guessed generically rather than looked up from a fixed table
        // keyed by an addon-specific enum. Falls back to a plain star for anything unmatched.
        public static string PerkBranchIcon(string branchName)
        {
            string n = (branchName ?? "").ToLowerInvariant();
            if (n.Contains("capstone") || n.Contains("duelist") || n.Contains("juggernaut") || n.Contains("reaper") || n.Contains("marksman")) return "⚔"; // crossed swords
            if (n.Contains("hp")) return "♥"; // heart
            if (n.Contains("ignore armor")) return "†"; // dagger
            if (n.Contains("cut through")) return "⛨"; // black cross on shield
            if (n.Contains("damage")) return "⚔"; // crossed swords
            if (n.Contains("evade")) return "↯"; // zigzag arrow
            if (n.Contains("regen")) return "✚"; // heavy greek cross
            if (n.Contains("berserk")) return "♨"; // hot springs (flame-ish glyph with broad font support)
            if (n.Contains("shrug")) return "⛨"; // shield
            if (n.Contains("aoe")) return "✹"; // burst star
            if (n.Contains("cleave")) return "⚒"; // hammer and pick
            if (n.Contains("loot")) return "♦"; // diamond
            if (n.Contains("swap")) return "⇄"; // left-right arrows
            if (n.Contains("speed")) return "↪"; // arrow hook (motion)
            if (n.Contains("accuracy")) return "◎"; // bullseye
            if (n.Contains("mounted")) return "♞"; // chess knight (closest broadly-supported "horse" glyph)
            if (n.Contains("two-handed")) return "⚔";
            if (n.Contains("polearm")) return "↑";
            if (n.Contains("thrown")) return "→";
            if (n.Contains("one-handed")) return "⚔";
            return "★"; // star
        }

        // Lattice layout node: sits in a horizontal lane (see PerksSection), so the label is
        // placed directly below - no rotation needed, lanes are spaced far enough apart
        // vertically that a horizontal label never collides with the lane above or below it.
        public IDocumentationGenerator PerkNode(float x, float y, string icon, int number, string name, bool unlocked, string branchColor, bool isRoot = false)
        {
            string color = unlocked ? branchColor : "#8a7aa0";
            string glow = unlocked ? $"0 0 8px {color}, 0 0 16px #6b46c1" : "none";
            string opacity = unlocked ? "1" : "0.5";
            // Root perks (branch entry point) render as a bigger "anchor" node - the rest are
            // smaller, same visual weighting the reference skill-tree uses for its class-root icon.
            float size = isRoot ? 30f : 22f;
            // Deterministic per-node stagger so the twinkle animation (see .perk-star CSS) doesn't
            // pulse every node in lockstep - purely cosmetic, derived from position so it's stable
            // across regenerations of the same catalog.
            float delay = ((x * 13f + y * 7f) % 40f) / 10f;

            return Div(() =>
            {
                P($"<div class=\"perk-star{(isRoot ? " perk-star-root" : "")}\" style=\"position:absolute; left:{x}px; top:{y}px;" +
                  "transform:translate(-50%,-50%);" +
                  $"width:{size}px; height:{size}px; border-radius:50%; background:rgba(13,6,32,0.85);" +
                  $"border:2px solid {color}; box-shadow:{glow}; opacity:{opacity};" +
                  $"animation-delay:{delay}s; display:flex; align-items:center; justify-content:center;" +
                  $"font-size:{size * 0.55f}px; line-height:1; color:{color};\">{icon}</div>");

                P($"<div style=\"position:absolute; left:{x}px; top:{y - (size / 2f) - 14f}px;" +
                  "transform:translate(-50%,0); font-size:10px; font-family:Georgia,serif;" +
                  $"color:{color}; text-shadow:0 0 4px #6b46c1; opacity:{opacity};\">{number}</div>");

                P($"<div style=\"position:absolute; left:{x}px; top:{y + (size / 2f) + 6f}px;" +
                  "transform:translate(-50%,0); font-size:10px; font-family:Georgia,serif;" +
                  $"color:{color}; opacity:{opacity}; white-space:nowrap;\">{name}</div>");
            });
        }

        public IDocumentationGenerator PerkLine(float x1, float y1, float x2, float y2, string color = "#d4af37")
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            float angle = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);

            return Div(() =>
            {
                P($"<div style=\"position:absolute; left:{x1}px; top:{y1}px;" +
                  $"width:{length}px; height:1px;" +
                  $"background:linear-gradient(90deg, {color}, transparent);" +
                  "opacity:0.75;" +
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
            IEnumerable<(string BranchName, IEnumerable<(string Key, string DisplayName, float BonusPerRank, IEnumerable<string> RequiredPerkKeys, string RequirementText)> Perks)> branches,
            Func<string, int> getRank)
        {
            // Lattice layout, one horizontal lane per branch, ranks running left to right from a
            // shared left-side "hub" - matching an actual reference skill-tree screenshot the
            // user supplied (2026-08-12): several class trees fanning rightward from one origin,
            // circular icon nodes, branch name at the end of its own row. Replaces an earlier
            // radial "starburst" layout (branches as spokes from a center point) that the user
            // said still "doesn't look appetizing" despite being structurally different from the
            // parallel-column layout before it - the reference image is the actual target now,
            // not a guess at what "constellation" should mean.
            //
            // A branch counts as a "hybrid capstone lane" (positioned between its two source
            // lanes instead of getting its own row) when it has exactly one perk with 2+ entries
            // in RequiredPerkKeys - detected generically here rather than by name, since this file
            // (core BannerlordTwitch.dll) can't hard-reference any specific addon's capstone keys.
            const float LaneSpacing = 90f;     // vertical distance between adjacent branch lanes
            const float NodeSpacing = 130f;    // horizontal distance between ranks in a lane
            const float OriginX = 110f;        // left starting point for rank 1 of each lane
            const float OriginY = 70f;         // top margin before the first lane
            const float CapstoneExtraX = 90f;  // extra horizontal offset for capstone nodes past their sources' rightmost rank

            var branchList = branches.ToList();
            var normalLanes = new List<int>();
            var capstoneLanes = new List<int>();
            for (int i = 0; i < branchList.Count; i++)
            {
                var perksList = branchList[i].Perks.ToList();
                bool isCapstone = perksList.Count == 1 && (perksList[0].RequiredPerkKeys?.Count() ?? 0) >= 2;
                (isCapstone ? capstoneLanes : normalLanes).Add(i);
            }

            int laneCount = normalLanes.Count;
            int maxRanks = normalLanes.Count > 0 ? normalLanes.Max(i => branchList[i].Perks.Count()) : 0;
            float canvasWidth = OriginX + Math.Max(0, maxRanks - 1) * NodeSpacing + CapstoneExtraX + 260f;
            float canvasHeight = OriginY + Math.Max(0, laneCount - 1) * LaneSpacing + 60f;

            return Div(() =>
            {
                H2("Perks");

                var coords = new Dictionary<string, (float x, float y)>();
                var keyBranchIndex = new Dictionary<string, int>();
                var laneY = new float[branchList.Count];

                for (int laneIndex = 0; laneIndex < normalLanes.Count; laneIndex++)
                {
                    int col = normalLanes[laneIndex];
                    float y = OriginY + laneIndex * LaneSpacing;
                    laneY[col] = y;
                    var ordered = branchList[col].Perks.ToList();
                    for (int row = 0; row < ordered.Count; row++)
                    {
                        float x = OriginX + row * NodeSpacing;
                        coords[ordered[row].Key] = (x, y);
                        keyBranchIndex[ordered[row].Key] = col;
                    }
                }

                // Capstones sit past the rightmost normal rank, at the vertical midpoint between
                // the lanes their two requirements come from - visually "between" the two branches
                // they combine, with lines converging into it from both sides.
                foreach (int col in capstoneLanes)
                {
                    var perk = branchList[col].Perks.First();
                    var reqYs = new List<float>();
                    float maxReqX = OriginX;
                    foreach (var reqKey in perk.RequiredPerkKeys)
                    {
                        if (coords.TryGetValue(reqKey, out var reqPos))
                        {
                            reqYs.Add(reqPos.y);
                            maxReqX = Math.Max(maxReqX, reqPos.x);
                        }
                    }
                    float capY = reqYs.Count > 0 ? reqYs.Average() : OriginY;
                    float capX = maxReqX + CapstoneExtraX;
                    laneY[col] = capY;
                    coords[perk.Key] = (capX, capY);
                    keyBranchIndex[perk.Key] = col;
                }

                Div("perk-scroll", () =>
                {
                    Div("perk-constellation", () =>
                    {
                        // Spacer in normal flow so this position:relative container actually
                        // reserves canvasWidth x canvasHeight - its real children below are all
                        // position:absolute and (correctly) don't otherwise contribute to its size.
                        P($"<div style=\"position:relative; width:{canvasWidth}px; height:{canvasHeight}px;\"></div>");

                        // Branch label sits right after that lane's last node (normal lanes) or
                        // right after the capstone node itself.
                        for (int laneIndex = 0; laneIndex < normalLanes.Count; laneIndex++)
                        {
                            int col = normalLanes[laneIndex];
                            var ordered = branchList[col].Perks.ToList();
                            float labelX = OriginX + Math.Max(0, ordered.Count - 1) * NodeSpacing + 34f;
                            float y = laneY[col];
                            string color = PerkBranchColor(col);
                            P($"<div style=\"position:absolute; left:{labelX}px; top:{y}px;" +
                              "transform:translate(0,-50%); font-size:13px; font-family:Georgia,serif; font-weight:bold;" +
                              $"color:{color}; text-shadow:0 0 4px #6b46c1; opacity:1; white-space:nowrap;\">{branchList[col].BranchName}</div>");
                        }
                        foreach (int col in capstoneLanes)
                        {
                            var perk = branchList[col].Perks.First();
                            var pos = coords[perk.Key];
                            string color = PerkBranchColor(col);
                            P($"<div style=\"position:absolute; left:{pos.x + 22f}px; top:{pos.y}px;" +
                              "transform:translate(0,-50%); font-size:13px; font-family:Georgia,serif; font-weight:bold;" +
                              $"color:{color}; text-shadow:0 0 4px #6b46c1; opacity:1; white-space:nowrap;\">{branchList[col].BranchName}</div>");
                        }

                        foreach (var branch in branchList)
                        {
                            foreach (var p in branch.Perks)
                            {
                                foreach (var reqKey in p.RequiredPerkKeys ?? Array.Empty<string>())
                                {
                                    if (string.IsNullOrEmpty(reqKey)) continue;
                                    if (!coords.TryGetValue(reqKey, out var prev) || !coords.TryGetValue(p.Key, out var cur)) continue;
                                    string lineColor = keyBranchIndex.TryGetValue(reqKey, out var srcBranch) ? PerkBranchColor(srcBranch) : "#d4af37";
                                    PerkLine(prev.x, prev.y, cur.x, cur.y, lineColor);
                                }
                            }
                        }
                        for (int laneIndex = 0; laneIndex < normalLanes.Count; laneIndex++)
                        {
                            int col = normalLanes[laneIndex];
                            string branchColor = PerkBranchColor(col);
                            string icon = PerkBranchIcon(branchList[col].BranchName);
                            int num = 1;
                            foreach (var p in branchList[col].Perks)
                            {
                                var c = coords[p.Key];
                                bool unlocked = getRank(p.Key) > 0;
                                PerkNode(c.x, c.y, icon, num, p.DisplayName, unlocked, branchColor, isRoot: num == 1);
                                num++;
                            }
                        }
                        foreach (int col in capstoneLanes)
                        {
                            var perk = branchList[col].Perks.First();
                            var c = coords[perk.Key];
                            bool unlocked = getRank(perk.Key) > 0;
                            string branchColor = PerkBranchColor(col);
                            string icon = PerkBranchIcon(branchList[col].BranchName);
                            PerkNode(c.x, c.y, icon, 1, perk.DisplayName, unlocked, branchColor, isRoot: false);
                        }
                    });
                });

                Table(() =>
                {
                    TR(() => { TH("Branch"); TH("#"); TH("Perk"); TH("Per Rank"); TH("Requires"); });
                    foreach (var branch in branchList)
                    {
                        int legendNum = 1;
                        foreach (var p in branch.Perks)
                        {
                            TR(() =>
                            {
                                TD(branch.BranchName);
                                TD(legendNum.ToString());
                                TD(p.DisplayName);
                                TD($"+{p.BonusPerRank * 100:0.#}%");
                                TD(string.IsNullOrEmpty(p.RequirementText) ? "-" : p.RequirementText);
                            });
                            legendNum++;
                        }
                    }
                });
            });
        }
    }
}