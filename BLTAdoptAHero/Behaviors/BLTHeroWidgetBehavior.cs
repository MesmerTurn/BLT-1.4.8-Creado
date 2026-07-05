using System;
using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Util;
using BLTAdoptAHero.UI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.InputSystem;
using TaleWorlds.CampaignSystem.TournamentGames;
using SandBox.Tournaments.MissionLogics;
using SandBox.ViewModelCollection.Missions.NameMarker;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Mission.NameMarker;

namespace BLTAdoptAHero
{
    public static class BLTExternalStats
    {
        // Filled by MakeBltGreatAgain at module load; null until then.
        public static System.Func<Hero, int> CompanionCount;
        public static System.Func<Hero, float> AdrenalineFraction;   // 0..1
        public static System.Func<Hero, int> DamageDealt;
        public static System.Func<Hero, float> PowerFraction;        // 0..1 (optional, may stay null)
        public static System.Func<Hero, float> ResurrectFraction;    // 0..1 cooldown progress (optional)
        public static System.Func<Hero, int> ResurrectSeconds;       // seconds remaining (optional)

        public static int Companions(Hero h)      => CompanionCount   != null && h != null ? SafeI(() => CompanionCount(h)) : 0;
        public static float Adrenaline(Hero h)     => AdrenalineFraction != null && h != null ? SafeF(() => AdrenalineFraction(h)) : 0f;
        public static int Damage(Hero h)           => DamageDealt      != null && h != null ? SafeI(() => DamageDealt(h)) : 0;
        public static float Power(Hero h)          => PowerFraction    != null && h != null ? SafeF(() => PowerFraction(h)) : 0f;
        public static float Resurrect(Hero h)      => ResurrectFraction!= null && h != null ? SafeF(() => ResurrectFraction(h)) : 0f;
        public static int ResurrectSecs(Hero h)    => ResurrectSeconds != null && h != null ? SafeI(() => ResurrectSeconds(h)) : 0;
        private static int SafeI(System.Func<int> f){ try { return f(); } catch { return 0; } }
        private static float SafeF(System.Func<float> f){ try { return f(); } catch { return 0f; } }
    }

    [DefaultView]
    public class HeroWidgetMissionView : MissionView
    {
        private GauntletLayer _layer;
        private HeroWidgetVM _vm;
        private GauntletMovieIdentifier _gauntletMovie;
        private Camera _camera;
        private readonly Dictionary<Hero, HeroIconVM> _heroToVM = new();
        private bool _isInitialized = false;
        private readonly float configWidth = GlobalCommonConfig.Get().NametagWidth;
        private readonly float configHeight = GlobalCommonConfig.Get().NametagHeight;
        private readonly float configFontsize = GlobalCommonConfig.Get().NametagFontsize;
        private readonly InputKey configToggleKey = Enum.TryParse(GlobalCommonConfig.Get().NametagKey, out InputKey key) ? key : InputKey.H;
        private readonly bool configUseNewLayout = GlobalCommonConfig.Get().UseNewHeroBarLayout;
        private const int MaxLevelDots = 6;
        private bool _hideUI = false;

        //private readonly Dictionary<Hero, string> _tournamentTeamColorCache = new();

        public override void OnMissionScreenTick(float dt)
        {
            if (!GlobalCommonConfig.Get().NametagEnabled)
                return;

            if (Input.IsKeyReleased(configToggleKey))
            {
                _hideUI = !_hideUI;
            }

            var heroBehavior = Mission.Current?.GetMissionBehavior<BLTAdoptAHeroCommonMissionBehavior>();
            var combatMission = Mission.Current.CombatType;
            if (heroBehavior == null || MissionScreen == null || combatMission == Mission.MissionCombatType.NoCombat)
                return;
;

            if (!_isInitialized)
            {
                if (heroBehavior.activeHeroes.Count > 0)
                {
                    InitializeUI();
                    _isInitialized = true;
                }
            }
            else
            {
                UpdateHeroIcons(heroBehavior);
            }
        }       

        private void InitializeUI()
        {
            //Log.Trace("BLTAdoptAHero: Initializing UI.");
            this._vm = new HeroWidgetVM();
            this._layer = new GauntletLayer("BLTHeroWidgetLayer", 15, false);
            string movieName = configUseNewLayout ? "BLTHeroNametagV2" : "BLTHeroNametag";
            this._gauntletMovie = this._layer.LoadMovie(movieName, _vm);
            this.MissionScreen.AddLayer(_layer);
            //Log.Trace("BLTAdoptAHero: Layer added to MissionScreen.");
            //Log.Trace($"BLTAdoptAHero: Movie loaded. RootWidget is Null? {_gauntletMovie.RootWidget == null}");
            this._camera = MissionScreen.CombatCamera;
        }

        internal void UpdateHeroIcons(BLTAdoptAHeroCommonMissionBehavior heroBehavior)
        {
            bool inTournament = MissionHelpers.InTournament();
            if (!_isInitialized || _camera == null) return;

            var heroVMs = new List<(Hero hero, HeroIconVM vm, float dist)>();

            var heroTeamCache = new Dictionary<Hero, string>();
            foreach (var hero in heroBehavior.activeHeroes)
            {
                if (!_heroToVM.TryGetValue(hero, out var vm))
                {
                    vm = new HeroIconVM { HeroName = hero.FirstName?.Raw() ?? "" };
                    _vm.Heroes.Add(vm);
                    _heroToVM[hero] = vm;
                }

                // --- Populate stats ---
                var missionState = heroBehavior.GetMissionState(hero);
                int kills = missionState?.Kills ?? 0;
                int retinueKills = missionState?.RetinueKills ?? 0;
                int wonGold = missionState?.WonGold ?? 0;
                int wonXP = missionState?.WonXP ?? 0;

                var summonState = BLTSummonBehavior.Current?.GetHeroSummonState(hero);
                int retinueAlive = (summonState?.ActiveRetinue ?? 0) + (summonState?.ActiveRetinue2 ?? 0);
                int retinueDead = (summonState?.DeadRetinue ?? 0) + (summonState?.DeadRetinue2 ?? 0);

                vm.Kills = kills;
                vm.RetinueKills = retinueKills;
                vm.RetinueAlive = retinueAlive;
                vm.RetinueDead = retinueDead;
                vm.GoldText = Abbrev(wonGold);
                vm.XpText = Abbrev(wonXP);
                vm.ClassLevel = hero?.Level ?? 0; // native hero level, no longer used for dots (kept for potential future use)
                int equipTier = (BLTAdoptAHeroCampaignBehavior.Current?.GetEquipmentTier(hero) ?? -1) + 1; // convert 0-7 to 1-8; -1+1=0 if unavailable
                int filledDots = Math.Min(Math.Max(equipTier, 0), MaxLevelDots);
                vm.LevelDotsText = new string('●', filledDots) + new string('○', MaxLevelDots - filledDots);
                vm.LevelText = $"T{equipTier}";

                vm.Companions = BLTExternalStats.Companions(hero);
                vm.DamageText = Abbrev(BLTExternalStats.Damage(hero));
                vm.AdrenalineFraction = BLTExternalStats.Adrenaline(hero);
                vm.AdrenalineVisible = vm.AdrenalineFraction > 0f;
                vm.PowerFraction = BLTExternalStats.Power(hero);
                vm.ResurrectFraction = BLTExternalStats.Resurrect(hero);
                int rs = BLTExternalStats.ResurrectSecs(hero);
                vm.ResurrectVisible = vm.ResurrectFraction > 0f || rs > 0;
                vm.ResurrectText = vm.ResurrectFraction >= 1f ? "respawn" : (rs > 0 ? rs + "s" : "");

                vm.StatsLine = $"{vm.Kills} · R{vm.RetinueAlive} -{vm.RetinueDead} · C{vm.Companions} · {vm.GoldText} · {vm.XpText} · {vm.DamageText}";

                var agent = hero.GetAgent();
                if (agent != null && agent.IsActive())
                {
                    Vec3 globalPos = agent.Position;
                    globalPos.z += agent.GetEyeGlobalHeight() + 0.15f;

                    float x = 0f, y = 0f, z = 0f;
                    MBWindowManager.WorldToScreen(_camera, globalPos, ref x, ref y, ref z);

                    bool onScreen = z > 0f && x > 0f && y > 0f &&
                                    x < Screen.RealScreenResolutionWidth &&
                                    y < Screen.RealScreenResolutionHeight;

                    if (onScreen)
                    {
                        if (_hideUI)
                        {
                            vm.IsVisible = false;
                        }
                        else
                        {
                            float dist = agent.Position.Distance(_camera.Position);
                            if (dist < 350f)
                            {
                                float scale = MBMath.Lerp(1f, 0.6f, (dist - 25f) / 75f, 0.00f); //Min:25 Max:100
                                scale = MBMath.ClampFloat(scale, 0.5f, 1f);

                                vm.IsVisible = true;
                                float baseWidth = configUseNewLayout ? 190f : configWidth;
                                float baseHeight = configUseNewLayout ? 90f : configHeight;
                                vm.Width = baseWidth * scale;
                                vm.Height = baseHeight * scale;
                                vm.FontSize = Math.Max(15, (int)(configFontsize * scale));
                                vm.PositionX = x - vm.Width * 0.5f;
                                vm.PositionY = y - vm.Height * 0.5f - 5f;

                                vm.BarWidth = 8f * scale;
                                vm.BarMaxHeight = 50f * scale;
                                vm.ResurrectBarHeight = 6f * scale;
                                vm.ResurrectBarMaxWidth = (baseWidth - 20f) * scale;
                                vm.AdrenalineBarFillHeight = vm.BarMaxHeight * MBMath.ClampFloat(vm.AdrenalineFraction, 0f, 1f);
                                vm.PowerBarFillHeight = vm.BarMaxHeight * MBMath.ClampFloat(vm.PowerFraction, 0f, 1f);
                                vm.ResurrectBarFillWidth = vm.ResurrectBarMaxWidth * MBMath.ClampFloat(vm.ResurrectFraction, 0f, 1f);


                                heroVMs.Add((hero, vm, dist));

                                if (!heroTeamCache.ContainsKey(hero))
                                    heroTeamCache[hero] = inTournament
                                        ? GetTournamentTeamColor(hero)
                                        : BLTAdoptAHeroCommonMissionBehavior.IsHeroOnPlayerSide(hero)
                                            ? "#4EE04CF0"
                                            : "#ED1C24F0";

                            }
                            else
                            {
                                vm.IsVisible = false;
                            }

                        }
                    }
                    else
                    {
                        vm.IsVisible = false;
                    }
                }
                else
                {
                    _vm.Heroes.Remove(vm);
                    _heroToVM.Remove(hero);
                }
            }

            var sorted = heroVMs
                                .Where(h => h.vm.IsVisible)
                                .OrderBy(h => h.vm.PositionY)
                                .ToList();

            float minOverlapY = 4f;
            float paddingY = 2f;
            float slideFactor = 0.5f;

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                var anchor = sorted[i].vm;

                for (int j = i + 1; j < sorted.Count; j++)
                {
                    var farther = sorted[j].vm;
                    if (!farther.IsVisible) continue;

                    if (Math.Abs(farther.PositionY - (anchor.PositionY + anchor.Height)) > 50f)
                        break;

                    bool overlapX = farther.PositionX < anchor.PositionX + anchor.Width * 0.9f &&
                                    farther.PositionX + farther.Width * 0.9f > anchor.PositionX;
                    bool overlapY = farther.PositionY < anchor.PositionY + anchor.Height &&
                                    farther.PositionY + farther.Height > anchor.PositionY;

                    if (overlapX && overlapY)
                    {
                        float overlapAmountY = (anchor.PositionY + anchor.Height) - farther.PositionY;
                        if (overlapAmountY > minOverlapY)
                        {
                            farther.PositionY -= overlapAmountY * slideFactor + paddingY;
                        }
                    }
                }
            }

            // --- Step 4: Apply cached colors ---
            foreach (var (hero, vm, dist) in heroVMs)
            {
                if (heroTeamCache.TryGetValue(hero, out var color))
                    vm.Color = color;
            }

            // --- Step 5: Remove inactive heroes ---
            var toRemove = _heroToVM.Keys.Except(heroBehavior.activeHeroes).ToList();
            foreach (var hero in toRemove)
            {
                _vm.Heroes.Remove(_heroToVM[hero]);
                _heroToVM.Remove(hero);
            }
        }

        private static string Abbrev(int n)
        {
            if (n >= 1000)
                return (n / 1000.0).ToString("0.#") + "K";
            return n.ToString();
        }

        private string GetTournamentTeamColor(Hero hero)
        {
            if (!MissionHelpers.InTournament() || hero == null)
                return "#FFFFFFF0";

            //if (_tournamentTeamColorCache.TryGetValue(hero, out var cachedColor))
            //    return cachedColor;

            // Otherwise scan and cache
            var agents = Mission.Current?.Agents;
            if (agents == null) return "#FFFFFFF0";

            foreach (var agent in agents)
            {
                var agentHero = agent.GetAdoptedHero();
                if (agentHero == hero)
                {
                    int teamIndex = agent.Team?.TeamIndex ?? -1;
                    string[] teamColors = { "#0000FFF0", "#FF0000F0", "#00FF00F0", "#FFFF00F0" };
                    string color = (teamIndex >= 0 && teamIndex < teamColors.Length)
                        ? teamColors[teamIndex]
                        : "#FFFFFFF0";

                    //_tournamentTeamColorCache[hero] = color;
                    //Log.Trace(_tournamentTeamColorCache.Values.Count.ToString());
                    return color;
                }
            }

            return "#FFFFFFF0";
        }

        public override void OnRemoveBehavior()
        {
            _heroToVM.Clear();
            //_tournamentTeamColorCache.Clear();
            _vm?.Heroes.Clear();

            if (_layer != null && MissionScreen != null)
                MissionScreen.RemoveLayer(_layer);

            _layer = null;
            _vm = null;
            _camera = null;
            base.OnRemoveBehavior();
        }
    }

    public class HeroWidgetVM : ViewModel
    {
        [DataSourceProperty]
        public MBBindingList<HeroIconVM> Heroes { get; } = new();
    }

    public class HeroIconVM : ViewModel
    {
        private string _heroName;
        private bool _isVisible;
        private float _positionX;
        private float _positionY;
        private string _color;
        private float _width;
        private float _height;
        private int _fontSize;

        [DataSourceProperty]
        public string HeroName
        {
            get => _heroName;
            set { if (_heroName != value) { _heroName = value; OnPropertyChanged(nameof(HeroName)); } }
        }

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); } }
        }

        [DataSourceProperty]
        public float PositionX
        {
            get => _positionX;
            set { if (_positionX != value) { _positionX = value; OnPropertyChanged(nameof(PositionX)); } }
        }

        [DataSourceProperty]
        public float PositionY
        {
            get => _positionY;
            set { if (_positionY != value) { _positionY = value; OnPropertyChanged(nameof(PositionY)); } }
        }

        [DataSourceProperty]
        public string Color
        {
            get => _color;
            set { if (_color != value) { _color = value; OnPropertyChanged(nameof(Color)); } }
        }

        [DataSourceProperty]
        public float Width
        {
            get => _width;
            set { if (_width != value) { _width = value; OnPropertyChanged(nameof(Width)); } }
        }

        [DataSourceProperty]
        public float Height
        {
            get => _height;
            set { if (_height != value) { _height = value; OnPropertyChanged(nameof(Height)); } }
        }

        [DataSourceProperty]
        public int FontSize
        {
            get => _fontSize;
            set { if (_fontSize != value) { _fontSize = value; OnPropertyChanged(nameof(FontSize)); } }
        }

        private int _kills;
        [DataSourceProperty]
        public int Kills
        {
            get => _kills;
            set { if (_kills != value) { _kills = value; OnPropertyChanged(nameof(Kills)); } }
        }

        private int _retinueKills;
        [DataSourceProperty]
        public int RetinueKills
        {
            get => _retinueKills;
            set { if (_retinueKills != value) { _retinueKills = value; OnPropertyChanged(nameof(RetinueKills)); } }
        }

        private int _retinueAlive;
        [DataSourceProperty]
        public int RetinueAlive
        {
            get => _retinueAlive;
            set { if (_retinueAlive != value) { _retinueAlive = value; OnPropertyChanged(nameof(RetinueAlive)); } }
        }

        private int _retinueDead;
        [DataSourceProperty]
        public int RetinueDead
        {
            get => _retinueDead;
            set { if (_retinueDead != value) { _retinueDead = value; OnPropertyChanged(nameof(RetinueDead)); } }
        }

        private int _companions;
        [DataSourceProperty]
        public int Companions
        {
            get => _companions;
            set { if (_companions != value) { _companions = value; OnPropertyChanged(nameof(Companions)); } }
        }

        private string _goldText;
        [DataSourceProperty]
        public string GoldText
        {
            get => _goldText;
            set { if (_goldText != value) { _goldText = value; OnPropertyChanged(nameof(GoldText)); } }
        }

        private string _xpText;
        [DataSourceProperty]
        public string XpText
        {
            get => _xpText;
            set { if (_xpText != value) { _xpText = value; OnPropertyChanged(nameof(XpText)); } }
        }

        private string _damageText;
        [DataSourceProperty]
        public string DamageText
        {
            get => _damageText;
            set { if (_damageText != value) { _damageText = value; OnPropertyChanged(nameof(DamageText)); } }
        }

        private int _classLevel;
        [DataSourceProperty]
        public int ClassLevel
        {
            get => _classLevel;
            set { if (_classLevel != value) { _classLevel = value; OnPropertyChanged(nameof(ClassLevel)); } }
        }

        private float _adrenalineFraction;
        [DataSourceProperty]
        public float AdrenalineFraction
        {
            get => _adrenalineFraction;
            set { if (_adrenalineFraction != value) { _adrenalineFraction = value; OnPropertyChanged(nameof(AdrenalineFraction)); } }
        }

        private float _powerFraction;
        [DataSourceProperty]
        public float PowerFraction
        {
            get => _powerFraction;
            set { if (_powerFraction != value) { _powerFraction = value; OnPropertyChanged(nameof(PowerFraction)); } }
        }

        private float _resurrectFraction;
        [DataSourceProperty]
        public float ResurrectFraction
        {
            get => _resurrectFraction;
            set { if (_resurrectFraction != value) { _resurrectFraction = value; OnPropertyChanged(nameof(ResurrectFraction)); } }
        }

        private string _resurrectText;
        [DataSourceProperty]
        public string ResurrectText
        {
            get => _resurrectText;
            set { if (_resurrectText != value) { _resurrectText = value; OnPropertyChanged(nameof(ResurrectText)); } }
        }

        private bool _adrenalineVisible;
        [DataSourceProperty]
        public bool AdrenalineVisible
        {
            get => _adrenalineVisible;
            set { if (_adrenalineVisible != value) { _adrenalineVisible = value; OnPropertyChanged(nameof(AdrenalineVisible)); } }
        }

        private bool _resurrectVisible;
        [DataSourceProperty]
        public bool ResurrectVisible
        {
            get => _resurrectVisible;
            set { if (_resurrectVisible != value) { _resurrectVisible = value; OnPropertyChanged(nameof(ResurrectVisible)); } }
        }

        private string _statsLine;
        [DataSourceProperty]
        public string StatsLine
        {
            get => _statsLine;
            set { if (_statsLine != value) { _statsLine = value; OnPropertyChanged(nameof(StatsLine)); } }
        }

        private string _levelDotsText;
        [DataSourceProperty]
        public string LevelDotsText
        {
            get => _levelDotsText;
            set { if (_levelDotsText != value) { _levelDotsText = value; OnPropertyChanged(nameof(LevelDotsText)); } }
        }

        private string _levelText;
        [DataSourceProperty]
        public string LevelText
        {
            get => _levelText;
            set { if (_levelText != value) { _levelText = value; OnPropertyChanged(nameof(LevelText)); } }
        }

        private float _barWidth;
        [DataSourceProperty]
        public float BarWidth
        {
            get => _barWidth;
            set { if (_barWidth != value) { _barWidth = value; OnPropertyChanged(nameof(BarWidth)); } }
        }

        private float _barMaxHeight;
        [DataSourceProperty]
        public float BarMaxHeight
        {
            get => _barMaxHeight;
            set { if (_barMaxHeight != value) { _barMaxHeight = value; OnPropertyChanged(nameof(BarMaxHeight)); } }
        }

        private float _adrenalineBarFillHeight;
        [DataSourceProperty]
        public float AdrenalineBarFillHeight
        {
            get => _adrenalineBarFillHeight;
            set { if (_adrenalineBarFillHeight != value) { _adrenalineBarFillHeight = value; OnPropertyChanged(nameof(AdrenalineBarFillHeight)); } }
        }

        private float _powerBarFillHeight;
        [DataSourceProperty]
        public float PowerBarFillHeight
        {
            get => _powerBarFillHeight;
            set { if (_powerBarFillHeight != value) { _powerBarFillHeight = value; OnPropertyChanged(nameof(PowerBarFillHeight)); } }
        }

        private float _resurrectBarHeight;
        [DataSourceProperty]
        public float ResurrectBarHeight
        {
            get => _resurrectBarHeight;
            set { if (_resurrectBarHeight != value) { _resurrectBarHeight = value; OnPropertyChanged(nameof(ResurrectBarHeight)); } }
        }

        private float _resurrectBarMaxWidth;
        [DataSourceProperty]
        public float ResurrectBarMaxWidth
        {
            get => _resurrectBarMaxWidth;
            set { if (_resurrectBarMaxWidth != value) { _resurrectBarMaxWidth = value; OnPropertyChanged(nameof(ResurrectBarMaxWidth)); } }
        }

        private float _resurrectBarFillWidth;
        [DataSourceProperty]
        public float ResurrectBarFillWidth
        {
            get => _resurrectBarFillWidth;
            set { if (_resurrectBarFillWidth != value) { _resurrectBarFillWidth = value; OnPropertyChanged(nameof(ResurrectBarFillWidth)); } }
        }
    }
}