#region REFS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.ComponentModel;
using LeagueSharp;
using LeagueSharp.SDK.Core;
using LeagueSharp.SDK.Core.Enumerations;
using LeagueSharp.SDK.Core.UI.IMenu;
using LeagueSharp.SDK.Core.UI.IMenu.Values;
using LeagueSharp.SDK.Core.UI.IMenu.Abstracts;
using LeagueSharp.SDK.Core.Events;
using LeagueSharp.SDK.Core.Wrappers;
using LeagueSharp.SDK.Core.Wrappers.Spell;
using LeagueSharp.SDK.Core.IDrawing;
using LeagueSharp.SDK.Core.Extensions;
using LeagueSharp.SDK.Core.Extensions.SharpDX;
using LeagueSharp.SDK.Core.Utils;
using LeagueSharp.SDK.Core.Math.Prediction;
using SharpDX;
using SharpDX.Direct3D9;
using Collision = LeagueSharp.SDK.Core.Math.Collision;
using ColorBGRA = SharpDX.ColorBGRA;
using Color = System.Drawing.Color;
using Menu = LeagueSharp.SDK.Core.UI.IMenu.Menu;
using Spell = LeagueSharp.SDK.Core.Wrappers.Spell;
#endregion

namespace Kalima {
    internal class Kalista {
        #region GAME LOAD
        static Dictionary<Vector3, Vector3> jumpPos;
        static readonly Obj_AI_Hero Player = ObjectManager.Player;
        static IEnumerable<Obj_AI_Hero> Heroes { get { return GameObjects.Heroes; } }

        #region Menu decs...
        static Menu kalimenu;
        static Menu kalm { get { return Kalista.kalimenu; } }
        static AMenuComponent MJungleClear { get { return kalm["JungleClear"]; } }
        static AMenuComponent Mharass { get { return kalm["Harass"]; } }
        static AMenuComponent MLaneClear { get { return kalm["LaneClear"]; } }
        static AMenuComponent Mmisc { get { return kalm["Misc"]; } }
        static AMenuComponent Mtimers { get { return kalm["Misc"]["TimersM"]; } }
        static AMenuComponent Mbal { get { return kalm["Balista"]; } }
        static AMenuComponent MbalChamps { get { return kalm["Balista"]["TargetSelector"]; } }
        static AMenuComponent MbalDraw { get { return kalm["Balista"]["Drawings"]; } }
        static float Manapercent { get { return Player.Mana / Player.MaxMana * 100; } }
        #endregion

        static Spell Q, W, E, R;
        static Obj_AI_Hero soulmate;//store the soulbound friend..
        static float soulmateRange = 1250f;
        static int MyLevel = 0;

        static void Game_OnGameLoad(object sender, EventArgs e) {
            if (Player.ChampionName != "Kalista") { return; }
            Q = new Spell(SpellSlot.Q, 1150f);
            Q.SetSkillshot(0.25f, 40f, 1700f, true, SkillshotType.SkillshotLine);
            W = new Spell(SpellSlot.W, 5200f);
            E = new Spell(SpellSlot.E, 1200f);
            R = new Spell(SpellSlot.R, 1200f);

            menuload();
            Game.OnUpdate += Game_OnUpdate;
//            Drawing.OnDraw += Drawing_OnDraw;
            Drawing.OnDraw += DraWing.Drawing_OnDraw;
            Obj_AI_Hero.OnProcessSpellCast += Event_OnProcessSpellCast;
            //            Orbwalking.OnNonKillableMinion += Event_OnNonKillableMinion;
//            Obj_AI_Hero.OnBuffAdd += Event_OnBuffAdd;
//            FillPositions();
        }

        static void Main(string[] args) { Load.OnLoad += Game_OnGameLoad; }
        #endregion

        #region MENU

        static void menuload() {
            kalimenu = new Menu("Kalimá", Player.ChampionName, true);
            Bootstrap.Init(new string[] { });
            var haraM = kalimenu.Add(new Menu("Harass", "Harass"));
            var LaneM = kalimenu.Add(new Menu("LaneClear", "LaneClear"));
            var JungM = kalimenu.Add(new Menu("JungleClear", "Jungleclear"));
            var MiscM = kalimenu.Add(new Menu("Misc", "Misc"));
            var DrawM = kalimenu.Add(new Menu("Drawing", "Drawing"));

            haraM.Add(new MenuBool("harassQ", "Use Q", true));
            haraM.Add(new MenuSlider("harassQchance", "Q cast if Chance of hit is:", 4, 1, 4));
            haraM.Add(new MenuSlider("harassmanaminQ", "Q requires % mana", 60, 0, 100));
            haraM.Add(new MenuBool("harassuseE", "Use E", true));
            haraM.Add(new MenuBool("harassEoutOfRange", "Use E when out of range", true));
            haraM.Add(new MenuSlider("harassE", "when being able to kill X minions and E champion", 1, 1, 10));
            haraM.Add(new MenuSlider("harassEminhealth", "E req minion % health to prevent E cooldown", 10, 1, 50));
            haraM.Add(new MenuSlider("harassmanaminE", "E requires % mana", 30, 0, 100));
            haraM.Add(new MenuBool("harassActive", "Active", true));

            JungM.Add(new MenuBool("jungleclearQ", "Use Q", false));
            JungM.Add(new MenuBool("jungleclearE", "Use E", true));
            JungM.Add(new MenuSlider("jungleclearmana", "E requires % mana", 20, 0, 100));
            JungM.Add(new MenuBool("bardragsteal", "Steal dragon/baron", true));
            JungM.Add(new MenuBool("jungleclearQbd", "Use Q on dragon/baron?", true));
            JungM.Add(new MenuBool("jungleActive", "Active", true));

            LaneM.Add(new MenuBool("laneclearQ", "Use Q", true));
            LaneM.Add(new MenuSlider("laneclearQcast", "Q cast if minions >= X", 2, 2, 10));
            LaneM.Add(new MenuSlider("laneclearmanaminQ", "Q requires % mana", 65, 0, 100));
            LaneM.Add(new MenuBool("laneclearE", "Use E", true));
            LaneM.Add(new MenuSlider("laneclearEcast", "E cast if minions >= X (min value)", 2, 0, 10));
            LaneM.Add(new MenuSlider("laneclearEcastincr", "Increase number by Level (decimal):", 1, 0, 4));
            LaneM.Add(new MenuSlider("laneclearEminhealth", "E req minion % health to prevent E cooldown", 10, 1, 50));
            LaneM.Add(new MenuSlider("laneclearmanaminE", "E requires % mana", 30, 0, 100));
            LaneM.Add(new MenuBool("laneclearbigminionsE", "E when it can kill siege/super minions", true));
            LaneM.Add(new MenuBool("laneclearlasthit", "E when non-killable by AA", true));

            MiscM.Add(new MenuBool("AutoLevel", "Auto Level Skills", true));
            MiscM.Add(new MenuBool("autoW", "Auto W", true));
            MiscM.Add(new MenuSlider("autowenemyclose", "Dont Send W with an enemy in X Range:", 2000, 0, 5000));
            MiscM.Add(new MenuBool("killsteal", "Kill Steal", true));
            MiscM.Add(new MenuBool("savesoulbound", "Save Soulbound (With R)", true));
            MiscM.Add(new MenuSlider("savesoulboundat", "Save when health < %", 25, 0, 100));
            MiscM.Add(new MenuBool("fleeKey", "Flee Toggle", false));

            var TimersM = MiscM.Add(new Menu("TimersM", "Timer Limits"));
            TimersM.Add(new MenuSlider("onupdateT", "OnUpdate Timer (max times per second)", 30, 1, 100));
            TimersM.Add(new MenuSlider("ondrawT", "OnDraw Timer (max times per second)", 30, 1, 500));


            DrawM.Add(new MenuColor("drawAA", "Auto Attack Range", ColorBGRA.FromRgba(207207023)));
            DrawM.Add(new MenuColor("drawjumpspots", "Jump Spots", ColorBGRA.FromRgba(000000255)));
            DrawM.Add(new MenuColor("drawQ", "Q Range", ColorBGRA.FromRgba(043255000)));
            DrawM.Add(new MenuColor("drawW", "W Range", ColorBGRA.FromRgba(000000255)));
            DrawM.Add(new MenuColor("drawE", "E Range", ColorBGRA.FromRgba(057138204)));
            DrawM.Add(new MenuColor("drawR", "R Range", ColorBGRA.FromRgba(019154161)));
            //marksman...
            DrawM.Add(new MenuColor("drawEdmg", "Draw E dmg HPbar", ColorBGRA.FromRgba(100149237)));
            DrawM.Add(new MenuColor("drawEspearsneeded", "Draw E# Spears Needed", ColorBGRA.FromRgba(255140000)));
            DrawM.Add(new MenuBool("drawsoulmatelink", "Draw Link Signal", true));
            DrawM.Add(new MenuBool("drawcoords", "Draw Map Coords", false));

            var balista = kalimenu.Add(new Menu("Balista", "Balista", false));

            var targetselect = balista.Add(new Menu("TargetSelector", "Target Selector"));
            var champselect = balista.Add(new Menu("Drawings", "Drawings"));
            champselect.Add(new MenuBool("drawminrange", "Min Range", false));
            champselect.Add(new MenuBool("drawmaxrange", "Max Range", false));
            champselect.Add(new MenuBool("lineformat", "Line Range", true));
            foreach (var enemy in HeroManager.Enemies.FindAll(x => x.IsEnemy)) {
                targetselect.Add(new MenuBool("target" + enemy.ChampionName, enemy.ChampionName));
            }
            balista.Add(new MenuSlider("balistaminrange", "Min Range", 600, 500, 1400));
            balista.Add(new MenuSlider("balistamaxrange", "Max Range", 1350, 500, 1400));
            balista.Add(new MenuSlider("balistenemyamaxrange", "Enemy Max Range", 2000, 500, 2400));
            balista.Add(new MenuBool("balistaActive", "Active", true));

            kalimenu.Attach();
        }
        #endregion

        #region EVENT GAME ON UPDATE
        static float? onupdatetimers;
        static void Game_OnUpdate(EventArgs args) {
            if (Player.IsDead) { return; }

            if (onupdatetimers != null) {
                //var onupdatet = kalm["Misc"]["TimersM"]["onupdateT"].GetValue<MenuSlider>().Value;
                var onupdatet = Mtimers["onupdateT"].GetValue<MenuSlider>().Value;
                if ((Game.ClockTime - onupdatetimers) > (1 / onupdatet)) {
                    onupdatetimers = null;
                } else { return; }
            }

            if (Mmisc["killsteal"].GetValue<MenuBool>().Value) {
                Killsteal();
            }
            if (Player.Level >= MyLevel) {Event_OnLevelUp();}
            if (Player.IsRecalling()) { return; }

            switch (Orbwalker.ActiveMode) {
                case OrbwalkerMode.LaneClear:
                    laneclear();
                    break;
                case OrbwalkerMode.LastHit:
                    break;
                case OrbwalkerMode.Hybrid:
                    break;
            }
            onupdatetimers = Game.ClockTime;
        }
        #endregion

        #region HARASS

        static void Killsteal() {
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(h => Q.CanCast(h) || ECanCast(h))) {
                if (hasundyingbuff(enemy)) { continue; }
                var edmg = GetEDamage(enemy);
                var enemyhealth = enemy.Health;
                var enemyregen = enemy.HPRegenRate / 2;
                if (((enemyhealth + enemyregen) <= edmg) && ECanCast(enemy) && !hasundyingbuff(enemy)) { ECast(); return; }
                if (Q.GetPrediction(enemy).Hitchance >= HitChance.High && Q.CanCast(enemy)) {
                    var qdamage = GetQDamage(enemy);
                    if ((qdamage + edmg) >= (enemyhealth + enemyregen)) {
                        Q.Cast(enemy);
                        return;
                    }
                }
            }
        }

        #endregion

        #region LANECLEAR

        static void laneclear() {
            if (Player.Spellbook.IsCastingSpell || (!E.IsReady() && !Q.IsReady())) { return; }
            var lqmana = MLaneClear["laneclearmanaminQ"].GetValue<MenuSlider>().Value;
            var lemana = MLaneClear["laneclearmanaminE"].GetValue<MenuSlider>().Value;
            var minmana = lqmana;
            var mymana = Manapercent;
            if (lemana < minmana) { minmana = lemana; }
            if (mymana < minmana) { return; }

            var Minions = ObjectManager.Get<Obj_AI_Minion>().Where(x => x.IsEnemy &&
                ((ECanCast(x) && x.Health < GetEDamage(x)) ||
                (Q.CanCast(x) && x.Health < GetQDamage(x) && Q.GetPrediction(x).Hitchance == HitChance.Collision))
                );
            if (Minions == null) { return; }

            if (MLaneClear["laneclearQ"].GetValue<MenuBool>().Value && Q.IsReady() && mymana >= lqmana && !Player.IsDashing()) {
                var laneclearQCast = MLaneClear["laneclearQcast"].GetValue<MenuSlider>().Value;
                var minionsQ = Minions.Find(x =>
                    x.Health < GetQDamage(x) &&
                    Q_GetCollisionMinions(Player, Player.ServerPosition.Extend(x.ServerPosition, Q.Range)).Count >= MLaneClear["laneclearQcast"].GetValue<MenuSlider>().Value &&
                    Q_GetCollisionMinions(Player, Player.ServerPosition.Extend(x.ServerPosition, Q.Range)).All(xx => xx.Health < GetQDamage(xx)));
                if (minionsQ != null) {
                    Q.Cast(minionsQ);
                }
            }

            if (MLaneClear["laneclearE"].GetValue<MenuBool>().Value && E.IsReady() && mymana >= lemana && !Player.IsDashing()) {
                var minhealth = MLaneClear["laneclearEminhealth"].GetValue<MenuSlider>().Value;
                var minionsE = Minions.Where(x => (x.Health + (x.HPRegenRate / 2)) < GetEDamage(x) && ECanCast(x) && x.HealthPercent >= minhealth);
                double laneclearE = MLaneClear["laneclearEcast"].GetValue<MenuSlider>().Value;
                double incrementE = MLaneClear["laneclearEcastincr"].GetValue<MenuSlider>().Value;
                if (minionsE != null && minionsE.Count() >= Math.Round(laneclearE + (Player.Level * (incrementE / 10)))) {
                    ECast();
                } else if (minionsE != null && MLaneClear["laneclearbigminionsE"].GetValue<MenuBool>().Value) { //kill siege/super minions when it can E
                    var bigminion = minionsE.Find(x => x.CharData.BaseSkinName.ToLower().Contains("siege") || x.CharData.BaseSkinName.ToLower().Contains("super"));
                    if (bigminion != null) { ECast(); }
                }
            }
        }
        #endregion

        #region MISC FUNCTIONS

        static float GetQDamage(Obj_AI_Base target) {
            var baseDamage = new[] { 10, 70, 130, 190, 250 };
            var bd = baseDamage[Q.Level - 1];
            var additionalBaseDamage = new[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f };
            var abd = additionalBaseDamage[Q.Level - 1];
            var realtotalad = Player.TotalAttackDamage * 0.9;//remove 10% until its fixed in l# to reflect the new 0.9 AD changes

            var totalDamage = bd + (abd * realtotalad);
            totalDamage = 100 / (100 + (target.Armor * Player.PercentArmorPenetrationMod) -
                Player.FlatArmorPenetrationMod) * totalDamage;

            return (float)totalDamage;            
        }

        static float GetEDamage(Obj_AI_Base target, int spears = 0) {
            var stacks = target.GetBuffCount("kalistaexpungemarker");
            if (spears > 0) { stacks = spears; }
            if (stacks == 0) { return 1; }

            var baseDamage = new[] { 20, 30, 40, 50, 60 };
            var bd = baseDamage[E.Level - 1];
            var additionalBaseDamage = new[] { 0.6f, 0.6f, 0.6f, 0.6f, 0.6f };
            var abd = additionalBaseDamage[E.Level - 1];

            var spearDamage = new[] { 10, 14, 19, 25, 32 };
            var sd = spearDamage[E.Level - 1];
            var additionalSpearDamage = new[] { 0.20f, 0.225f, 0.25f, 0.275f, 0.30f };
            var asd = additionalSpearDamage[E.Level - 1];
            double realtotalad = Player.TotalAttackDamage * 0.9;//remove 10% until its fixed in l# to reflect the new 0.9 AD changes
            //            double realtotalad = Player.TotalAttackDamage;
            double playertotalad = realtotalad;

            if (target is Obj_AI_Hero) {
                if (Player.Masteries.Any()) {
                    //Martial Mastery
                    if (Player.Masteries.Any(m => m.Page == MasteryPage.Offense && m.Id == 98 && m.Points == 1)) {
                        playertotalad = playertotalad + 4;
                    }
                    //brute force
                    var brute = Player.Masteries.First(m => m.Page == MasteryPage.Offense && m.Id == 82);
                    if (brute != null && brute.Points >= 1) {
                        switch (brute.Points) {
                            case 1:
                                playertotalad = playertotalad + (Player.Level * 0.22);
                                break;
                            case 2:
                                playertotalad = playertotalad + (Player.Level * 0.39);
                                break;
                            case 3:
                                playertotalad = playertotalad + (Player.Level * 0.55);
                                break;
                        }
                    }
                }
            }

            double totalDamage = bd + abd * realtotalad + (stacks - 1) * (sd + asd * playertotalad);

            totalDamage = 100 / (100 + (target.Armor * Player.PercentArmorPenetrationMod) -
                Player.FlatArmorPenetrationMod) * totalDamage;

            if (target is Obj_AI_Hero) {
                if (Player.Masteries.Any()) {
                    //double edged sword
                    if (Player.Masteries.Any(m => m.Page == MasteryPage.Offense && m.Id == 65 && m.Points == 1)) {
                        totalDamage = totalDamage * 1.015;
                    }
                    //havoc
                    if (Player.Masteries.Any(m => m.Page == MasteryPage.Offense && m.Id == 146 && m.Points == 1)) {
                        totalDamage = totalDamage * 1.03;
                    }
                    //spell weaving
                    if (Player.Masteries.Any(m => m.Page == MasteryPage.Offense && m.Id == 97 && m.Points == 1)) {
                        if (stacks < 3) {
                            totalDamage = totalDamage * (stacks * 1.01);
                        } else { totalDamage = totalDamage * 1.03; }
                    }
                    //executioner
                    var mastery = Player.Masteries.Find(m => m.Page == MasteryPage.Offense && m.Id == 100);
                    if (mastery != null && mastery.Points >= 1 &&
                        target.Health / target.MaxHealth <= 0.05d + 0.15d * mastery.Points) {
                        totalDamage = totalDamage * 1.05;
                    }
                }
            }
            return (float)totalDamage;
        }

        static List<Obj_AI_Base> Q_GetCollisionMinions(Obj_AI_Hero source, Vector3 targetposition) {
            var input = new PredictionInput {
                Unit = source,
                Radius = Q.Width,
                Delay = Q.Delay,
                Speed = Q.Speed,
            };

            input.CollisionObjects = CollisionableObjects.Minions;
            return Collision.GetCollision(new List<Vector3> { targetposition }, input).OrderBy(obj => obj.Distance(source)).ToList();
        }

        //idea from hellsing
        static bool hasundyingbuff(Obj_AI_Base target) {
            var hasbuff = HeroManager.Enemies.Find(a =>
                target.CharData.BaseSkinName == a.CharData.BaseSkinName && a.Buffs.Any(b =>
                    b.Name.ToLower().Contains("undying rage") ||
                    b.Name.ToLower().Contains("chrono shift") ||
                    b.Name.ToLower().Contains("judicatorintervention") ||
                    b.Name.ToLower().Contains("poppyditarget")));
            if (hasbuff != null) { return true; }
            return false;
        }

        //prevent double E's which put E on cooldown
        static float? ecasttimer;
        static void ECast() {
            if (ecasttimer != null) {
                if ((Game.ClockTime - ecasttimer) > 0.500) {//wait 500ms before using E again
                    ecasttimer = null;
                } else { return; }
            }
            ecasttimer = Game.ClockTime;
            E.Cast();
        }

        static bool ECanCast(Obj_AI_Base target) {
            if (!E.IsReady() || !E.CanCast(target) || hasundyingbuff(target)) { return false; }
            var cancast = false;
            if (ecasttimer != null) {
                if ((Game.ClockTime - ecasttimer) > 0.500) {//check with e's timer
                    ecasttimer = null;
                    cancast = true;
                } else { return false; }
            } else { cancast = true; }
            if (cancast) { return true; }
            return false;
        }

        static HitChance gethitchanceQ { get { return hitchanceQ(); } }
        static HitChance hitchanceQ() {
            switch (Mharass["harassQchance"].GetValue<MenuSlider>().Value) {
                case 1:
                    return HitChance.Low;
                case 2:
                    return HitChance.Medium;
                case 3:
                    return HitChance.High;
                case 4:
                    return HitChance.VeryHigh;
                default:
                    return HitChance.High;
            }
        }

        #endregion

        #region MISC EVENTS

        static void Event_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args) {
            //3 if's for no checks later...
            if (soulmate == null || Player.IsDead || !R.IsReady() || Player.Spellbook.IsCastingSpell) { return; }
            if (!Mmisc["savesoulbound"].GetValue<MenuBool>().Value) { return; }
            if (sender.IsMe) {
                if (args.SData.Name == "KalistaExpungeWrapper") {
                    Orbwalker.ResetAutoAttackTimer(); //dont reset because it does double E's and puts E on cooldown
                }
            }
            if (sender.IsEnemy && sender.Distance(soulmate.ServerPosition) < 1500f) {
                if (soulmate.HealthPercent <= Mmisc["savesoulboundat"].GetValue<MenuSlider>().Value) {
                    R.Cast();
                }
            }
        }

        static void Event_OnLevelUp() {
            if (Mmisc["AutoLevel"].GetValue<MenuBool>().Value) {
                DraWing.drawtext("levelupspells", 3, Drawing.Width * 0.45f, Drawing.Height * 0.90f, Color.PapayaWhip, "Levelling up Spells");
                if (MyLevel == 0) {
                    Player.Spellbook.LevelSpell(SpellSlot.W);
                    MyLevel++;
                } else {
                    MyLevel++;
                    if (MyLevel == 2) { Player.Spellbook.LevelSpell(SpellSlot.Q); }
                    Player.Spellbook.LevelSpell(SpellSlot.R);
                    Player.Spellbook.LevelSpell(SpellSlot.E);
                    Player.Spellbook.LevelSpell(SpellSlot.Q);
                    Player.Spellbook.LevelSpell(SpellSlot.W);
                }
            }
        }

        #endregion

        #region MY DRAWING CLASS FOR TIMED CIRCLES/TEXT/LINE
        //drawing class for timed drawings (pls jodus add this feature in l# xD)
        internal class DraWing {
            private static readonly Obj_AI_Hero Player = ObjectManager.Player;
            public static void Drawing_OnDraw(EventArgs args) {
                var timerightnow = Game.ClockTime;
                //remove old items from lists
                drawtextlist.RemoveAll(x => timerightnow - x.Addedon > x.Timer);
                drawcirclelist.RemoveAll(x => timerightnow - x.Addedon > x.Timer);
                drawlinelist.RemoveAll(x => timerightnow - x.Addedon > x.Timer);
                //draw everything...
                if (drawtextlist.Count > 0) {
                    foreach (var x in drawtextlist) {
                        Drawing.DrawText(x.X, x.Y, x.Color, x.Format);
                    }
                }
                if (drawcirclelist.Count > 0) {
                    foreach (var x in drawcirclelist) {
                        Drawing.DrawCircle(x.Position, x.Radius, x.Color);
                        
                    }
                }
                if (drawlinelist.Count > 0) {
                    foreach (var x in drawlinelist) {
                        Drawing.DrawLine(x.X, x.Y, x.X2, x.Y2, x.Thickness, x.Color);
                    }
                }
            }

            private static List<Drawline> drawlinelist = new List<Drawline>();
            private class Drawline {
                public string Name { get; set; }
                public double Timer { get; set; }
                public float Addedon { get; set; }
                //here goes the function stuff...
                public float X { get; set; }
                public float Y { get; set; }
                public float X2 { get; set; }
                public float Y2 { get; set; }
                public float Thickness { get; set; }
                public Color Color { get; set; }
            }
            public static void drawline(string name, double timer, float x, float y, float x2, float y2, float thickness, Color color) {
                drawlinelist.RemoveAll(xXx => xXx.Name == name);
                drawlinelist.Add(new Drawline() { Name = name, Timer = timer, Addedon = Game.ClockTime, X = x, Y = y, X2 = x2, Y2 = y2, Thickness = thickness, Color = color });
                return;
            }

            private static List<Drawcircle> drawcirclelist = new List<Drawcircle>();
            private class Drawcircle {
                public string Name { get; set; }
                public double Timer { get; set; }
                public float Addedon { get; set; }
                public Vector3 Position { get; set; }
                public float Radius { get; set; }
                public Color Color { get; set; }
            }
            public static void drawcircle(string name, double timer, Vector3 position, float radius, Color color) {
                drawcirclelist.RemoveAll(x => x.Name == name);
                drawcirclelist.Add(new Drawcircle() { Name = name, Timer = timer, Addedon = Game.ClockTime, Position = position, Radius = radius, Color = color });
                return;
            }

            private static List<Drawtext> drawtextlist = new List<Drawtext>();
            private class Drawtext {
                public string Name { get; set; }
                public double Timer { get; set; }
                public float Addedon { get; set; }
                public float X { get; set; }
                public float Y { get; set; }
                public Color Color { get; set; }
                public string Format { get; set; }
            }
            public static void drawtext(string name, double timer, float X, float Y, Color color, string format) {
                drawtextlist.RemoveAll(x => x.Name == name);
                drawtextlist.Add(new Drawtext() { Name = name, Timer = timer, Addedon = Game.ClockTime, X = X, Y = Y, Color = color, Format = format });
                return;
            }
        }
        #endregion

        
    }
    

    
    #region HeroManager Temp...
    public class HeroManager {
        /// <summary>
        ///     A list containing all heroes in the current match
        /// </summary>
        public static List<Obj_AI_Hero> AllHeroes { get; private set; }
        /// <summary>
        ///     A list containing only ally heroes in the current match
        /// </summary>
        public static List<Obj_AI_Hero> Allies { get; private set; }
        /// <summary>
        ///     A list containing only enemy heroes in the current match
        /// </summary>
        public static List<Obj_AI_Hero> Enemies { get; private set; }

        static HeroManager() {
            if (Game.Mode == GameMode.Running) {
                Game_OnStart(new EventArgs());
            }
            Game.OnStart += Game_OnStart;
        }

        static void Game_OnStart(EventArgs args) {
            AllHeroes = ObjectManager.Get<Obj_AI_Hero>().ToList();
            Allies = AllHeroes.FindAll(o => o.IsAlly);
            Enemies = AllHeroes.FindAll(o => o.IsEnemy);
        }
    }
    #endregion
}