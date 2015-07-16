#region REFS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;
using LeagueSharp;
using LeagueSharp.SDK.Core;
using LeagueSharp.SDK.Core.Enumerations;
using LeagueSharp.SDK.Core.UI.IMenu;
using LeagueSharp.SDK.Core.UI.IMenu.Values;
using LeagueSharp.SDK.Core.UI.IMenu.Abstracts;
using LeagueSharp.SDK.Core.Events;
using LeagueSharp.SDK.Core.IDrawing;
using LeagueSharp.SDK.Core.Extensions;
using LeagueSharp.SDK.Core.Extensions.SharpDX;
using LeagueSharp.SDK.Core.Utils;
using LeagueSharp.SDK.Core.Math.Prediction;
using LeagueSharp.SDK.Core.Wrappers;
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
            Orbwalker.OnAction += Event_OnAction;
//            Obj_AI_Hero.OnBuffAdd += Event_OnBuffAdd;
//            FillPositions();
        }
        [STAThread]//STAT (windows.forms att warning fix)
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
            MiscM.Add(new MenuKeyBind("fleeKey", "Flee Toggle", Keys.T,KeyBindType.Toggle));

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

            var blitzskarneringame = HeroManager.Allies.Find(x => x.CharData.BaseSkinName == "Blitzcrank" || x.CharData.BaseSkinName == "Skarner" || x.CharData.BaseSkinName == "TahmKench");
            if (blitzskarneringame != null) {
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
            }

            kalimenu.Attach();
        }
        #endregion

        #region EVENT GAME ON UPDATE
        static float? onupdatetimers;
        static void Game_OnUpdate(EventArgs args) {
            if (Player.IsDead) { return; }

            if (onupdatetimers != null) {
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

            if (Mharass["harassActive"].GetValue<MenuBool>().Value) {
                harass();
            }

            if (MJungleClear["jungleActive"].GetValue<MenuBool>().Value) {
                Jungleclear();
            }

            if (Mmisc["autoW"].GetValue<MenuBool>().Value) {
                AutoW();
            }

            if (Mmisc["fleeKey"].GetValue<MenuKeyBind>().Active) {
//                ShowjumpsandFlee();
            }

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

        static void harass() {
            var lqmana = Mharass["harassmanaminQ"].GetValue<MenuSlider>().Value;
            var lemana = Mharass["harassmanaminE"].GetValue<MenuSlider>().Value;
            var minmana = lqmana;
            var mymana = Manapercent;
            if (lemana < minmana) { minmana = lemana; }
            if (mymana < minmana) { return; }//quick check to return if less than minmana...

            if (Mharass["harassQ"].GetValue<MenuBool>().Value && mymana > lqmana && Q.IsReady(1) && !Player.IsDashing()) {
                var enemies = HeroManager.Enemies.FindAll(h =>
                    h.IsValidTarget(Q.Range) && Q.CanCast(h) &&
                    ((Q.GetPrediction(h).Hitchance >= gethitchanceQ) ||
                    (Q.GetPrediction(h).Hitchance == HitChance.Collision)));
                if (enemies != null) {
                    foreach (var enemy in enemies) {
                        switch (Q.GetPrediction(enemy).Hitchance) {
                            case HitChance.Collision:
                                var collide = Q.GetPrediction(enemy).CollisionObjects;
                                var dontbother = 0;
                                foreach (var thing in collide) {
                                    if (thing.Health > GetQDamage(thing)) { dontbother = 1; }
                                }
                                if (dontbother == 0) {
                                    Q.Cast(enemy);
                                }
                                break;
                            default:
                                Q.Cast(enemy);
                                break;
                        }
                    }
                }
            }

            if (!Mharass["harassuseE"].GetValue<MenuBool>().Value) { return; }
            if (mymana < lemana || !E.IsReady()) { return; }

            if (Mharass["harassE"].GetValue<MenuSlider>().Value >= 1 && Mharass["harassEoutOfRange"].GetValue<MenuBool>().Value) {
                var minhealth = Mharass["harassEminhealth"].GetValue<MenuSlider>().Value;//readability/future usage
                //use R.range instead of E.range so it can harass ".outofrange" as long as E is castable
                var Minions = GameObjects.EnemyMinions.Where(x => Player.Distance(x) < R.Range && (x.Health + (x.HPRegenRate / 2)) < GetEDamage(x) && ECanCast(x) && x.HealthPercent >= minhealth);
                if (Minions != null && Minions.Count() >= Mharass["harassE"].GetValue<MenuSlider>().Value) {
                    var enemy = HeroManager.Enemies.Find(x => ECanCast(x));
                    if (enemy != null) { ECast(); }
                }
            }
        }

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

        #region JUNGLE CLEAR

        static void Jungleclear() {
            var mymana = Manapercent;
            if (mymana < MJungleClear["jungleclearmana"].GetValue<MenuSlider>().Value) { return; }

            //baron / dragon
            if (MJungleClear["bardragsteal"].GetValue<MenuBool>().Value) {
                var bigkahuna = ObjectManager.Get<Obj_AI_Minion>().Find(x => (ECanCast(x) || Q.CanCast(x)) && (x.CharData.BaseSkinName.ToLower().Contains("dragon") || x.CharData.BaseSkinName.ToLower().Contains("baron")));
                if (bigkahuna != null) {
                    var kahunaE = GetEDamage(bigkahuna);
                    var kahunaQ = GetQDamage(bigkahuna);
                    var kahunahealth = (bigkahuna.Health + (bigkahuna.HPRegenRate / 2));
                    if (ECanCast(bigkahuna) && kahunahealth < kahunaE) {ECast();}
                    if (Q.CanCast(bigkahuna) && kahunahealth < kahunaQ) {Q.Cast(bigkahuna);}
                    //check for q+e combo..and Qit..if it lands next jungclear will Eit
                    if (kahunahealth < (kahunaE+kahunaQ)) {Q.Cast(bigkahuna);}
                }
            }
            //other minions in jungle...
            var jungleinside = ObjectManager.Get<Obj_AI_Minion>().FirstOrDefault(x => Player.Distance(x) < E.Range && x.Team == GameObjectTeam.Neutral);
            if (MJungleClear["jungleclearQ"].GetValue<MenuBool>().Value) {
                if (Q.CanCast(jungleinside)) { Q.Cast(jungleinside); }
            }
            if (MJungleClear["jungleclearE"].GetValue<MenuBool>().Value) {
                if (ECanCast(jungleinside) && (jungleinside.Health + (jungleinside.HPRegenRate / 2)) <= GetEDamage(jungleinside)) { ECast(); }
            }
        }
        #endregion

        #region LANECLEAR

        static void laneclear() {
            if (!E.IsReady() && !Q.IsReady()) { return; }
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
            //from l# since 
            double dmg = new[] { 10, 70, 130, 190, 250 }[Q.Level -1] + Player.BaseAttackDamage + Player.FlatPhysicalDamageMod;
            return (float)Player.CalculateDamage(target,DamageType.Magical,dmg);
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

        //credits to xcsoft for this function
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

        #region AUTO W (Sentinel stuff)
        static readonly List<mysentinels> _mysentinels = new List<mysentinels>();
        internal class mysentinels {
            public string Name;
            public Vector3 Position;
            public mysentinels(string name, Vector3 position) {
                Name = name;
                Position = position;
            }
        }
        static int? sentinelcloserthan(Vector3 position, int distance) {
            foreach (var xxxXxxx in ObjectManager.Get<AttackableUnit>().Where(obj => obj.Name.Contains("RobotBuddy"))) {
                if (Vector3.Distance(position, xxxXxxx.Position) < distance) { return 1; }
            }
            return 0;
        }
        static void fillsentinels() {
            _mysentinels.Clear();
            foreach (var xxxXxxx in ObjectManager.Get<AttackableUnit>().Where(obj => obj.Name.Contains("RobotBuddy"))) {
                _mysentinels.Add(new mysentinels("RobotBuddy", xxxXxxx.Position));
            }
            //add the camps where to send sentinels to...
            _mysentinels.Add(new mysentinels("Blue Camp Blue Buff", new Vector3(3864f,7822f,51.8922f)));
            _mysentinels.Add(new mysentinels("Blue Camp Red Buff", new Vector3(7716f,4070f,54.10854f)));
            _mysentinels.Add(new mysentinels("Red Camp Blue Buff", new Vector3(10800f,7010f,51.7226f)));
            _mysentinels.Add(new mysentinels("Red Camp Red Buff", new Vector3(6948f,10642f,55.99818f)));
            _mysentinels.Add(new mysentinels("Dragon", new Vector3(9728f,4328f,-71.2406f)));
            _mysentinels.Add(new mysentinels("Baron", new Vector3(5002f,10480f,-71.2406f)));
            _mysentinels.Add(new mysentinels("Mid Bot River", new Vector3(8370f, 6176f, -71.2406f)));
            //add river mid bush here...
            //_mysentinels.Add(new mysentinels("RiverTop", (Vector3)SummonersRift.Bushes.);
        }
        static float? autoWtimers;
        static void AutoW() {
            var useW = Mmisc["autoW"].GetValue<MenuBool>().Value;
            if (useW && W.IsReady()) {
                if (autoWtimers != null) {
                    if ((Game.ClockTime - autoWtimers) > 2) {
                        autoWtimers = null;
                    } else { return; }
                }
                var closestenemy = HeroManager.Enemies.Find(x => Player.ServerPosition.Distance(x.ServerPosition) < Mmisc["autowenemyclose"].GetValue<MenuSlider>().Value);
                if (closestenemy != null) { return; }
                if ((Player.ManaPercent < 50) || Player.IsDashing() || Player.IsWindingUp || Player.InFountain()) { return; }
                fillsentinels();

                Random rnd = new Random();
                var sentineldestinations = _mysentinels.Where(s => !s.Name.Contains("RobotBuddy")).OrderBy(s => rnd.Next()).ToList();
                foreach (var destinations in sentineldestinations) {
                    var distancefromme = Vector3.Distance(Player.Position, destinations.Position);
                    if (sentinelcloserthan(destinations.Position, 1500) == 0 && distancefromme < W.Range) {
                        autoWtimers = Game.ClockTime;
                        W.Cast(destinations.Position);
                        DraWing.drawtext("sendingbug", 3, Drawing.Width * 0.45f, Drawing.Height * 0.90f, Color.PapayaWhip, "Sending bug to: " + destinations.Name);
                        return;
                    }
                }
            }
        }

        #endregion

        #region MISC EVENTS
        static void Event_OnAction(object sender, Orbwalker.OrbwalkerActionArgs e) {if (e.Type == OrbwalkerType.NonKillableMinion) {Event_OnNonKillableMinion((Obj_AI_Minion)e.Target);}}
        static void Event_OnNonKillableMinion(Obj_AI_Minion minion) {
            var minionX = minion;
            if (!MLaneClear["laneclearE"].GetValue<MenuBool>().Value || !E.IsReady() || !ECanCast(minionX)) { return; }
            if (Manapercent < MLaneClear["laneclearmanaminE"].GetValue<MenuSlider>().Value) { return; }
            if (MLaneClear["laneclearlasthit"].GetValue<MenuBool>().Value) {
                var minhealth = MLaneClear["laneclearEminhealth"].GetValue<MenuSlider>().Value;
                if (minionX.Health <= GetEDamage(minionX) && minionX.HealthPercent >= minhealth) {
                    ECast();
                }
            }
        }

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

    #region HeroManager Temp
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