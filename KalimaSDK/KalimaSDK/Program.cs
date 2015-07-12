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
using LeagueSharp.SDK.Core.Events;
using LeagueSharp.SDK.Core.Wrappers;
using LeagueSharp.SDK.Core.IDrawing;
using LeagueSharp.SDK.Core.Extensions;
using LeagueSharp.SDK.Core.Extensions.SharpDX;
using LeagueSharp.SDK.Core.Utils;
using SharpDX;
using SharpDX.Direct3D9;
using ColorBGRA = SharpDX.ColorBGRA;
using Color = System.Drawing.Color;
using Menu = LeagueSharp.SDK.Core.UI.IMenu.Menu;
using Collision = LeagueSharp.SDK.Core.Extensions;
using Spell = LeagueSharp.SDK.Core.Wrappers.Spell;
#endregion

namespace Kalima {
    internal class Kalista {
        #region GAME LOAD
        static Dictionary<Vector3, Vector3> jumpPos;
        static readonly Obj_AI_Hero Player = ObjectManager.Player;
        static IEnumerable<Obj_AI_Hero> Heroes { get { return GameObjects.Heroes; } }
        //        static Orbwalking.Orbwalker Orbwalker;
        static Menu kalimenu;
        static Menu kalm { get { return Kalista.kalimenu; } }
        static float Manapercent { get { return Player.Mana / Player.MaxMana * 100; } }

        static Spell Q, W, E, R;
        static Obj_AI_Hero soulmate;//store the soulbound friend..
        static float soulmateRange = 1250f;
        static int MyLevel = 0;

        static void Game_OnGameLoad(object sender, EventArgs e) {//"1 3 1 2 1 4 1 3 1 3 4 3 3 2 2 4 2 2";
            if (Player.ChampionName != "Kalista") { return; }
            Q = new Spell(SpellSlot.Q, 1150f);
            Q.SetSkillshot(0.25f, 40f, 1700f, true, SkillshotType.SkillshotLine);
            W = new Spell(SpellSlot.W, 5200f);
            E = new Spell(SpellSlot.E, 1200f);
            R = new Spell(SpellSlot.R, 1200f);

            menuload();
            Game.OnUpdate += Game_OnUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Drawing.OnDraw += DraWing.Drawing_OnDraw;
            Obj_AI_Hero.OnProcessSpellCast += Event_OnProcessSpellCast;
            //            Orbwalking.OnNonKillableMinion += Event_OnNonKillableMinion;
            Obj_AI_Hero.OnBuffAdd += Event_OnBuffAdd;
            FillPositions();
        }

        static void Main(string[] args) { Load.OnLoad += Game_OnGameLoad; }
        #endregion

        #region MENU

        static void menuload() {
            kalimenu = new Menu("Kalimá", Player.ChampionName, true);
            Bootstrap.Init(new string[] { });
            Menu haraM = kalimenu.Add(new Menu("Harass", "Harass"));
            Menu LaneM = kalimenu.Add(new Menu("Lane Clear", "LaneClear"));
            Menu JungM = kalimenu.Add(new Menu("Jungle Clear", "Jungleclear"));
            Menu MiscM = kalimenu.Add(new Menu("Misc", "Misc"));
            Menu DrawM = kalimenu.Add(new Menu("Drawing", "Drawing"));

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
            Menu TimersM = MiscM.Add(new Menu("Timer Limits", "Timer Limits"));
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
            var blitzskarneringame = HeroManager.Allies.Find(x => x.CharData.BaseSkinName == "Blitzcrank" || x.CharData.BaseSkinName == "Skarner");

            if (blitzskarneringame != null) {
                //check if its blitz or skarner the ally...
                string menuname;
                if (blitzskarneringame.CharData.BaseSkinName == "Blitzcrank") {
                    menuname = "Balista";
                } else { menuname = "Salista"; }

                Menu balista = kalimenu.Add(new Menu(menuname, menuname));

                Menu targetselect = balista.Add(new Menu("Target Selector", "Target Selector"));
                Menu champselect = balista.Add(new Menu("Drawings", "Drawings"));
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
            if (Player.IsDead || Player.IsRecalling()) { return; }

            if (onupdatetimers != null) {
                if ((Game.ClockTime - onupdatetimers) > (1 / kalm["onupdateT"].GetValue<MenuSlider>().Value)) {
                    onupdatetimers = null;
                } else { return; }
            }

            if (kalm["killsteal"].GetValue<MenuBool>().Value) {
                Killsteal();
            }
            if (Player.IsRecalling()) { return; }

            if (kalm["harassActive"].GetValue<MenuBool>().Value) {
                harass();
            }
            if (kalm["jungleActive"].GetValue<MenuBool>().Value) {
                Jungleclear();
            }
            if (kalm["autoW"].GetValue<MenuBool>().Value) {
                AutoW();
            }
            if (kalm["fleeKey"].GetValue<MenuBool>().Value) {
                ShowjumpsandFlee();
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
            if (Player.Level >= MyLevel) {
                Event_OnLevelUp();
            }
            onupdatetimers = Game.ClockTime;
        }
        #endregion

        #region HARASS

        static void harass() {
            var lqmana = kalm["harassmanaminQ"].GetValue<MenuSlider>().Value;
            var lemana = kalm["harassmanaminE"].GetValue<MenuSlider>().Value;
            var minmana = lqmana;
            var mymana = Manapercent;
            if (lemana < minmana) { minmana = lemana; }
            if (mymana < minmana) { return; }//quick check to return if less than minmana...

            if (kalm["harassQ"].GetValue<MenuBool>().Value && mymana > lqmana && Q.IsReady(1) && !Player.IsDashing()) {
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
                                    if (thing.Health < Player.GetSpellDamage(thing, SpellSlot.Q)) { dontbother = 1; }
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

            if (!kalm["harassuseE"].GetValue<MenuBool>().Value) { return; }
            if (mymana < lemana || !E.IsReady()) { return; }

            if (kalm["harassE"].GetValue<MenuSlider>().Value >= 1 && kalm["harassEoutOfRange"].GetValue<MenuBool>().Value) {
                var minhealth = kalm["harassEminhealth"].GetValue<MenuSlider>().Value;
                //use R.range instead of E.range so it can harass ".outofrange" as long as E is castable
                //                var Minions = MinionManager.GetMinions(Player.ServerPosition, R.Range, MinionTypes.All, MinionTeam.NotAlly).FindAll(x => (x.Health + (x.HPRegenRate / 2)) < GetEDamage(x) && ECanCast(x) && x.HealthPercent >= minhealth);
                var Minions = GameObjects.EnemyMinions.Where(x => x.Distance(Player) < R.Range && (x.Health + (x.HPRegenRate / 2)) < GetEDamage(x) && ECanCast(x) && x.HealthPercent >= minhealth);
                if (Minions != null && Minions.Count() >= kalm["harassE"].GetValue<MenuSlider>().Value) {
                    var enemy = HeroManager.Enemies.Find(x => ECanCast(x));
                    if (enemy != null) { ECast(); }
                }
            }
        }

        static HitChance gethitchanceQ { get { return hitchanceQ(); } }
        static HitChance hitchanceQ() {
            switch (kalm["harassQchance"].GetValue<MenuSlider>().Value) {
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

        //credits to xcsoft for this function
        /*        static List<Obj_AI_Base> Q_GetCollisionMinions(Obj_AI_Hero source, Vector3 targetposition) {
                    var input = new PredictionInput {
                        Unit = source,
                        Radius = Q.Width,
                        Delay = Q.Delay,
                        Speed = Q.Speed,
                    };

                    input.CollisionObjects[0] = CollisionableObjects.Minions;
                    return Collision.GetCollision(new List<Vector3> { targetposition }, input).OrderBy(obj => obj.Distance(source, false)).ToList();
                }*/

        static void Killsteal() {
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(h => Q.CanCast(h) || ECanCast(h))) {
                if (hasundyingbuff(enemy)) { continue; }
                var edmg = GetEDamage(enemy);
                var enemyhealth = enemy.Health;
                var enemyregen = enemy.HPRegenRate / 2;
                if (((enemyhealth + enemyregen) <= edmg) && ECanCast(enemy) && !hasundyingbuff(enemy)) { ECast(); return; }
                if (Q.GetPrediction(enemy).Hitchance >= HitChance.High && Q.CanCast(enemy)) {
                    var qdamage = Player.GetSpellDamage(enemy, SpellSlot.Q);
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
            if (mymana < kalm["jungleclearmana"].GetValue<MenuSlider>().Value) { return; }

            //baron / dragon
            if (kalm["bardragsteal"].GetValue<MenuBool>().Value) {
                var junglehugeE = GameObjects.Minions.Where(x => x.Distance(Player) < E.Range).FirstOrDefault(x => x.Health + (x.HPRegenRate / 2) <= GetEDamage(x) && (x.CharData.BaseSkinName.ToLower().Contains("dragon") || x.CharData.BaseSkinName.ToLower().Contains("baron")));
                if (junglehugeE != null && ECanCast(junglehugeE)) { ECast(); }//jungleclearQbd
                var junglehugeQ = GameObjects.Minions.Where(x => x.Distance(Player) < Q.Range).FirstOrDefault(x => x.Health + (x.HPRegenRate / 2) <= Player.GetSpellDamage(x, SpellSlot.Q) && (x.CharData.BaseSkinName.ToLower().Contains("dragon") || x.CharData.BaseSkinName.ToLower().Contains("baron")));
                if (junglehugeQ != null && Q.CanCast(junglehugeQ)) { Q.Cast(junglehugeQ); }
            }
            //other minions in jungle...
            var jungleinside = GameObjects.JungleLarge.Where(x => x.Distance(Player) < E.Range).FirstOrDefault();
            if (kalm["jungleclearQ"].GetValue<MenuBool>().Value) {
                if (Q.CanCast(jungleinside)) { Q.Cast(jungleinside); }
            }
            if (kalm["jungleclearE"].GetValue<MenuBool>().Value) {
                if (ECanCast(jungleinside) && (jungleinside.Health + (jungleinside.HPRegenRate / 2)) <= GetEDamage(jungleinside)) { ECast(); }
            }
        }
        #endregion

        #region LANECLEAR

        static void laneclear() {
            if (Player.Spellbook.IsCastingSpell || (!E.IsReady() && !Q.IsReady())) { return; }
            var lqmana = kalm["laneclearmanaminQ"].GetValue<MenuSlider>().Value;
            var lemana = kalm["laneclearmanaminE"].GetValue<MenuSlider>().Value;
            var minmana = lqmana;
            var mymana = Manapercent;
            if (lemana < minmana) { minmana = lemana; }
            if (mymana < minmana) { return; }

            var Minions = GameObjects.EnemyMinions.Where(x => x.Distance(Player) < E.Range && (x.Health < Player.GetSpellDamage(x, SpellSlot.Q) && Q.CanCast(x)) || (x.Health < GetEDamage(x) && ECanCast(x))).ToList();
            if (Minions.Count <= 0) { return; }

            if (kalm["laneclearQ"].GetValue<MenuBool>().Value && Q.IsReady() && mymana >= lqmana && !Player.IsDashing()) {
                //                var minionsQ = Minions.Find(x => x.Health < Player.GetSpellDamage(x, SpellSlot.Q) && Q_GetCollisionMinions(Player, Player.ServerPosition.Extend(x.ServerPosition, Q.Range)).Count >= kalm.GetValue<MenuSlider>("laneclearQcast").Value && Q_GetCollisionMinions(Player, Player.ServerPosition.Extend(x.ServerPosition, Q.Range)).All(xx => xx.Health < Player.GetSpellDamage(xx, SpellSlot.Q)));
                //                if (minionsQ != null) {Q.Cast(minionsQ);}
            }

            if (kalm["laneclearE"].GetValue<MenuBool>().Value && E.IsReady() && mymana >= lemana && !Player.IsDashing()) {
                var minhealth = kalm["laneclearEminhealth"].GetValue<MenuSlider>().Value;
                var minionsE = Minions.Where(x => (x.Health + (x.HPRegenRate / 2)) < GetEDamage(x) && ECanCast(x) && x.HealthPercent >= minhealth);
                double laneclearE = kalm["laneclearEcast"].GetValue<MenuSlider>().Value;
                double incrementE = kalm["laneclearEcastincr"].GetValue<MenuSlider>().Value;
                if (minionsE != null && minionsE.Count() >= Math.Round(laneclearE + (Player.Level * (incrementE / 10)))) {
                    ECast();
                } else if (minionsE != null && kalm["laneclearbigminionsE"].GetValue<MenuBool>().Value) { //kill siege/super minions when it can E
                    var bigminion = minionsE.Find(x => x.CharData.BaseSkinName.ToLower().Contains("siege") || x.CharData.BaseSkinName.ToLower().Contains("super"));
                    if (bigminion != null) { ECast(); }
                }
            }
        }
        #endregion

        #region MISC EVENTS

        static void Event_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args) {
            //3 if's for no checks later...
            if (soulmate == null || Player.IsDead || !R.IsReady() || Player.Spellbook.IsCastingSpell) { return; }
            if (!kalm["savesoulbound"].GetValue<MenuBool>().Value) { return; }
            if (sender.IsMe) {
                if (args.SData.Name == "KalistaExpungeWrapper") {
                    Orbwalker.ResetAutoAttackTimer();
                }
            }
            if (sender.IsEnemy && sender.Distance(soulmate.ServerPosition) < 1500f) {
                if (soulmate.HealthPercent <= kalm["savesoulboundat"].GetValue<MenuSlider>().Value) {
                    R.Cast();
                }
            }
        }

        static void Event_OnNonKillableMinion(AttackableUnit minion) {
            var minionX = (Obj_AI_Minion)minion;
            if (!kalm["laneclearE"].GetValue<MenuBool>().Value || !E.IsReady() || !ECanCast(minionX)) { return; }
            if (Manapercent < kalm["laneclearmanaminE"].GetValue<MenuSlider>().Value) { return; }
            if (kalm["laneclearlasthit"].GetValue<MenuBool>().Value) {
                var minhealth = kalm["laneclearEminhealth"].GetValue<MenuSlider>().Value;
                if (minionX.Health <= GetEDamage(minionX) && minionX.HealthPercent >= minhealth) {
                    ECast();
                }
            }
        }

        static void Event_OnBuffAdd(Obj_AI_Base sender, Obj_AI_BaseBuffAddEventArgs args) {
        }

        static void Event_OnLevelUp() {
            if (kalm["AutoLevel"].GetValue<MenuBool>().Value) {
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

        #region EVENT ON DRAW
        static float? ondrawtimers;
        static void Drawing_OnDraw(EventArgs args) {
            if (Player.IsDead) { return; }
            var curposition = Player.Position;
            var ondrawmenutimer = (1 / kalm["ondrawT"].GetValue<MenuSlider>().Value);
            if (ondrawtimers != null) {
                if ((Game.ClockTime - ondrawtimers) > ondrawmenutimer) {
                    ondrawtimers = null;
                } else { return; }
            }
            ondrawtimers = Game.ClockTime;

            var dAA = kalm.GetValue<MenuColor>("drawAA");
            var dQ = kalm.GetValue<MenuColor>("drawQ");
            var dW = kalm.GetValue<MenuColor>("drawW");
            var dE = kalm.GetValue<MenuColor>("drawE");
            var dR = kalm.GetValue<MenuColor>("drawR");
            var dj = kalm.GetValue<MenuColor>("drawjumpspots");
            if (dAA.Active) { DraWing.drawcircle("drawAA", 1, curposition, Player.AttackRange, Color.Blue); }
            if (Q.IsReady() && dQ.Active) { DraWing.drawcircle("drawQ", 1, curposition, Q.Range, Color.Teal); }
            if (W.IsReady() && dW.Active) { DraWing.drawcircle("drawW", 1, curposition, W.Range, Color.Black); }
            if (E.IsReady() && dE.Active) { DraWing.drawcircle("drawE", 1, curposition, E.Range, Color.Bisque); }
            if (R.IsReady() && dR.Active) { DraWing.drawcircle("drawR", 1, curposition, R.Range, Color.Aquamarine); }
            if (kalm.GetValue<MenuBool>("drawsoulmatelink").Value) {
                draw_soulmate_link();
            }
            if (dj.Active) {
                draw_jump_spots();
            }

            if (soulmate != null) {
                if (soulmate.ChampionName == "Blitzcrank" || soulmate.ChampionName == "Skarner" && R.IsReady()) {
                    if (kalm.GetValue<MenuBool>("balistaActive").Value) {
                        var enemy = HeroManager.Enemies.Find(a => a.Buffs.Any(b => b.Name.ToLower().Contains("rocketgrab2") || b.Name.ToLower().Contains("skarnerimpale")));
                        //balista stuff...
                        if (enemy != null) {
                            //do both checks since we might have both in a game...
                            var doult = 0;
                            if (kalimenu.GetValue<MenuBool>("target" + enemy.ChampionName).Value && enemy.Health > 200 && isbalista(enemy)) {
                                if (enemy.HasBuff("rocketgrab2") && soulmate.ChampionName == "Blitzcrank") { doult = 1; }
                                if (enemy.HasBuff("skarnerimpale") && soulmate.ChampionName == "Skarner") { doult = 1; }
                                if (doult == 1) { R.Cast(); }
                            }
                        }
                        if (kalm.GetValue<MenuBool>("drawminrange").Value) {
                            DraWing.drawcircle("drawminrange", 1, curposition, kalm.GetValue<MenuSlider>("balistaminrange").Value, Color.Chartreuse);
                        }
                        if (kalm.GetValue<MenuBool>("drawmaxrange").Value) {
                            DraWing.drawcircle("drawmaxrange", 1, curposition, kalm.GetValue<MenuSlider>("balistamaxrange").Value, Color.Green);
                        }
                        if (kalm.GetValue<MenuBool>("lineformat").Value) {
                            var lineformat = HeroManager.Enemies.FindAll(a => a.ServerPosition.Distance(Player.ServerPosition) <= kalm.GetValue<MenuSlider>("balistenemyamaxrange").Value && !a.IsDead && a.IsVisible);
                            var foundvalidtarget = 0;
                            if (lineformat != null && isbalista(soulmate)) {
                                foreach (var x in lineformat) {
                                    if (isbalista(x)) {
                                        DraWing.drawline("drawtargetline" + x.CharData.BaseSkinName, ondrawmenutimer, x.HPBarPosition.X, x.HPBarPosition.Y, soulmate.HPBarPosition.X, soulmate.HPBarPosition.Y, 2.0f, Color.Red);
                                        foundvalidtarget++;
                                    }
                                }
                                if (foundvalidtarget > 0) {
                                    DraWing.drawline("drawsoulmate", ondrawmenutimer, soulmate.HPBarPosition.X, soulmate.HPBarPosition.Y, Player.HPBarPosition.X, Player.HPBarPosition.Y, 2.0f, Color.Red);
                                }
                            }
                        }
                    }
                }
            }
            var dEDmG = kalm.GetValue<MenuColor>("drawEdmg");
            if (dEDmG.Active && E.Level > 0) {
                var enemieswithspears = HeroManager.Enemies.Where(x => x.HasBuff("kalistaexpungemarker") && x.IsHPBarRendered);
                if (enemieswithspears != null) {
                    var barsize = 104f;
                    foreach (var enemy in enemieswithspears) {
                        var health = enemy.Health;
                        var maxhealth = enemy.MaxHealth;
                        var pos = enemy.HPBarPosition;
                        var percent = GetEDamage(enemy) / maxhealth * barsize;
                        var start = pos + (new Vector2(10f, 19f));
                        var end = pos + (new Vector2(10f + percent, 19f));

                        //DraWing.drawline("drawEdmg" + enemy.ChampionName, ondrawmenutimer, start[0], start[1], end[0], end[1], 3.0f, dEDmG.Color);
                        DraWing.drawline("drawEdmg" + enemy.ChampionName, ondrawmenutimer, start[0], start[1], end[0], end[1], 3.0f, Color.CornflowerBlue);
                    }
                }
            }

            var dEsps = kalm.GetValue<MenuColor>("drawEspearsneeded");
            if (dEsps.Active && E.Level > 0) {
                var enemieswithspears = HeroManager.Enemies.Where(x => x.HasBuff("kalistaexpungemarker"));
                if (enemieswithspears != null) {
                    foreach (var enemy in enemieswithspears) {
                        var spearcount = enemy.GetBuffCount("kalistaexpungemarker");
                        for (int spears = 1; spears < 250; spears++) {
                            var Edmg = Math.Round(GetEDamage(enemy, spears));//shorten output size..
                            if (Edmg == 1) { break; }
                            if (Edmg > enemy.Health) {
                                var kill = 0;
                                for (int spearstokill = 1; spearstokill <= (spears - spearcount); spearstokill++) {
                                    var futdmg = Math.Round(GetEDamage(enemy, spearstokill)) + (spearstokill * Player.GetAutoAttackDamage(enemy));
                                    var getfutdmg = (GetEDamage(enemy) + futdmg);
                                    if (getfutdmg > enemy.Health) {
                                        kill = spearstokill;
                                        break;
                                    }
                                }
                                //DraWing.drawtext("drawEspears", ondrawmenutimer, enemy.HPBarPosition.X + 150, enemy.HPBarPosition.Y + 19, dEsps.Color, "[E: " + spearcount + " / L: " + (kill + 1) + "]");
                                DraWing.drawtext("drawEspears", ondrawmenutimer, enemy.HPBarPosition.X + 150, enemy.HPBarPosition.Y + 19, Color.CadetBlue, "[E: " + spearcount + " / L: " + (kill + 1) + "]");
                                break;
                            }
                        }
                    }
                }
            }

            if (kalm.GetValue<MenuBool>("drawcoords").Value) {
                DraWing.drawtext("drawcoords", 1, Drawing.Width * 0.45f, Drawing.Height * 0.70f, Color.GreenYellow, "Coords:" + Player.Position + " Dmg: " + Player.TotalAttackDamage);
            }
        }

        static bool isbalista(Obj_AI_Hero hero) {
            if (soulmate == null) { return false; }
            if (soulmate.IsDead) { return false; }
            if (soulmate.CharData.BaseSkinName == "Blitzcrank" || soulmate.CharData.BaseSkinName == "Skarner") {
                var enemymaxrange = kalm.GetValue<MenuSlider>("balistenemyamaxrange").Value;
                var balistaminrange = kalm.GetValue<MenuSlider>("balistaminrange").Value;
                var balistamaxrange = kalm.GetValue<MenuSlider>("balistamaxrange").Value;

                var closestenemiestome = HeroManager.Enemies.FindAll(x =>
                    x.Distance(Player.ServerPosition) < enemymaxrange && !x.IsDead &&
                    x.Distance(Player.ServerPosition) > balistaminrange &&
                    x.Distance(soulmate.ServerPosition) < balistamaxrange
                    );
                if (closestenemiestome == null) { return false; }

                var mysoul = HeroManager.Allies.Find(x =>
                    x.CharData.BaseSkinName == soulmate.CharData.BaseSkinName &&
                    Player.Distance(x.ServerPosition) > balistaminrange &&
                    Player.Distance(x.ServerPosition) < balistamaxrange && closestenemiestome != null);
                if (mysoul == null) { return false; }

                if (mysoul.CharData.BaseSkinName == hero.CharData.BaseSkinName) {
                    return true;
                } else {
                    var enemy = closestenemiestome.Find(x => x.CharData.BaseSkinName == hero.CharData.BaseSkinName);
                    if (enemy != null) { return true; }
                }
            }
            return false;
        }

        static void draw_soulmate_link() {
            if (Player.IsDead || W.Level == 0) { return; }
            if (soulmate == null) {
                soulmate = HeroManager.Allies.Find(a => !a.IsMe && a.Buffs.Any(b => b.Name.Contains("kalistacoopstrikeally")));
                if (soulmate == null) { return; }
            } else if (Game.ClockTime < (60 * 5)) {//check for changes until 5 minutes in game which then kali isnt allowed to change coop
                soulmate = HeroManager.Allies.Find(a => !a.IsMe && a.Buffs.Any(b => b.Name.Contains("kalistacoopstrikeally")));
                if (soulmate == null) { return; }
            }

            if (soulmate.IsDead) {
                DraWing.drawtext("drawlink", 1, Drawing.Width * 0.45f, Drawing.Height * 0.82f, Color.Red, "Connection Signal with " + soulmate.ChampionName + ": None");
            } else {
                var soulrange = Player.Distance(soulmate.Position);
                if (soulrange > soulmateRange) {
                    DraWing.drawtext("drawlink", 1, Drawing.Width * 0.45f, Drawing.Height * 0.82f, Color.Red, "Connection Signal with " + soulmate.ChampionName + ": None");
                } else if (soulrange > 800) {
                    DraWing.drawtext("drawlink", 1, Drawing.Width * 0.45f, Drawing.Height * 0.82f, Color.Yellow, "Connection Signal with " + soulmate.ChampionName + ": Low");
                } else {
                    DraWing.drawtext("drawlink", 1, Drawing.Width * 0.45f, Drawing.Height * 0.82f, Color.Green, "Connection Signal with " + soulmate.ChampionName + ": Good");
                }
            }
        }
        #endregion

        #region MISC FUNCTIONS
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
            double realtotalad = Player.TotalAttackDamage * .99;//remove 1% until its fixed in l# to reflect the new 0.9 AD changes
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
        //idea from hellsing
        static bool hasundyingbuff(Obj_AI_Hero target) {
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
            if (!E.IsReady() || !E.CanCast(target)) { return false; }
            var cancast = false;
            if (ecasttimer != null) {
                if ((Game.ClockTime - ecasttimer) > 0.300) {//check with e's timer
                    ecasttimer = null;
                    cancast = true;
                } else { return false; }
            } else { cancast = true; }
            if (cancast) { return true; }
            return false;
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
            _mysentinels.Add(new mysentinels("Blue Camp Blue Buff", new Vector3(3910, 7920, -71)));
            _mysentinels.Add(new mysentinels("Blue Camp Red Buff", new Vector3(7890, 4120, -71)));
            _mysentinels.Add(new mysentinels("Red Camp Blue Buff", new Vector3(10830, 7000, -71)));
            _mysentinels.Add(new mysentinels("Red Camp Red Buff", new Vector3(6980, 10760, -71)));
            _mysentinels.Add(new mysentinels("Dragon", new Vector3(9860, 4410, -71)));
            _mysentinels.Add(new mysentinels("Baron", new Vector3(4950, 10400, -71)));
            _mysentinels.Add(new mysentinels("Mid Bot River", new Vector3(8370f, 6176f, -71.2406f)));
            //add river mid bush here...
            //_mysentinels.Add(new mysentinels("RiverTop", (Vector3)SummonersRift.Bushes.);
        }
        static float? autoWtimers;
        static void AutoW() {
            var useW = kalm.GetValue<MenuBool>("autoW").Value;
            if (useW && W.IsReady()) {
                if (autoWtimers != null) {
                    if ((Game.ClockTime - autoWtimers) > 2) {
                        autoWtimers = null;
                    } else { return; }
                }
                var closestenemy = HeroManager.Enemies.Find(x => Player.ServerPosition.Distance(x.ServerPosition) < kalm.GetValue<MenuSlider>("autowenemyclose").Value);
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
                        DraWing.drawtext("sendingbugto", 3, Drawing.Width * 0.45f, Drawing.Height * 0.90f, Color.PapayaWhip, "Sending bug to:" + destinations.Name);
                        return;
                    }
                }
            }
        }

        #endregion

        #region WALLJUMP
        static void draw_jump_spots() {
            const float circleRange = 75f;
            foreach (var pos in jumpPos) {
                if (Player.Distance(pos.Key) <= 500f || Player.Distance(pos.Value) <= 500f) {
                    DraWing.drawcircle("jump" + pos, 0.0333, pos.Key, circleRange, Color.Blue);
                    DraWing.drawcircle("jump" + pos, 0.0333, pos.Value, circleRange, Color.Blue);
                }
            }
        }

        static void ShowjumpsandFlee() {
            if (!Q.IsReady()) { return; }
            DraWing.drawtext("jumpactive", 0.0333, Drawing.Width * 0.45f, Drawing.Height * 0.10f, Color.GreenYellow, "Wall Jump Active");
            var XXX = (Vector3)canjump();
            if (XXX != null) {
                DraWing.drawtext("couldjump", 0.0333, Drawing.Width * 0.45f, Drawing.Height * 0.50f, Color.GreenYellow, "could jump here");
                Q.Cast(XXX);
                Orbwalker.Orbwalk(null, XXX);
            } else {
                DraWing.drawtext("couldjump", 0.0333, Drawing.Width * 0.45f, Drawing.Height * 0.50f, Color.GreenYellow, "can't jump here");
            }

            foreach (var pos in jumpPos) {
                if (Player.Distance(pos.Key) <= 50f || Player.Distance(pos.Value) <= 50f) {
                    var x = (Vector3)canjump();
                    if (x != null) {
                        Q.Cast(x);
                        Orbwalker.Orbwalk(null, x);
                    }
                } else { return; }
            }
        }

        static Vector3? canjump() {
            var wallCheck = VectorHelper.GetFirstWallPoint(Player.Position, Player.Position);
            //loop angles around the player to check for a point to jump to
            //credits to hellsing wherever it has his code here somewhere... xD
            float maxAngle = 80;
            float step = maxAngle / 20;
            float currentAngle = 0;
            float currentStep = 0;
            Vector3 currentPosition = Player.Position;
            Vector2 direction = ((Player.Position.ToVector2() + 50) - currentPosition.ToVector2()).Normalized();
            while (true) {
                if (currentStep > maxAngle && currentAngle < 0) { break; }

                if ((currentAngle == 0 || currentAngle < 0) && currentStep != 0) {
                    currentAngle = (currentStep) * (float)Math.PI / 180;
                    currentStep += step;
                } else if (currentAngle > 0) {
                    currentAngle = -currentAngle;
                }

                Vector3 checkPoint;

                // One time only check for direct line of sight without rotating
                if (currentStep == 0) {
                    currentStep = step;
                    checkPoint = currentPosition + 300 * direction.ToVector3();
                } else {
                    checkPoint = currentPosition + 300 * direction.Rotated(currentAngle).ToVector3();
                }
                if (checkPoint.IsWall()) { continue; }
                // Check if there is a wall between the checkPoint and currentPosition
                wallCheck = VectorHelper.GetFirstWallPoint(checkPoint, currentPosition);
                if (wallCheck == null) { continue; } //jump to the next loop
                //get the jump point
                Vector3 wallPositionOpposite = (Vector3)VectorHelper.GetFirstWallPoint((Vector3)wallCheck, currentPosition, 5);
                //check if the walking path is big enough to be worth a jump..if not then just skip to the next loop
                if (Player.GetPath(wallPositionOpposite).ToList().ToVector2().PathLength() - Player.Distance(wallPositionOpposite) < 230) {
                    DraWing.drawtext("couldjump", 0.0333, Drawing.Width * 0.45f, Drawing.Height * 0.50f, Color.GreenYellow, "not worth a jump...");
                    continue;
                }

                //check the jump distance and if its short enough then jump...
                if (Player.Distance(wallPositionOpposite) < Math.Pow(300 - Player.BoundingRadius / 2, 2)) {
                    return wallPositionOpposite;
                }
            }
            return null;
        }

        static void FillPositions() {
            jumpPos = new Dictionary<Vector3, Vector3>();
            var pos1001 = new Vector3(9340f, 4474f, -71.2406f);
            var pos1002 = new Vector3(9084f, 4640f, 51.95212f);
            jumpPos.Add(pos1001, pos1002);

            var pos1003 = new Vector3(7824f, 5998f, 51.4058f);
            var pos1004 = new Vector3(8010f, 6228f, -71.2406f);
            jumpPos.Add(pos1003, pos1004);

            var pos1005 = new Vector3(9830f, 3040f, 60.5358f);
            var pos1006 = new Vector3(9774f, 2760f, 49.22291f);
            jumpPos.Add(pos1005, pos1006);

            var pos1007 = new Vector3(6616f, 11674f, 53.83324f);
            var pos1008 = new Vector3(6462f, 12004f, 56.4768f);
            jumpPos.Add(pos1007, pos1008);
        }
        #endregion
    }
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

    #region VECTOR HELPER FROM STACKOVERFLOW
    internal class VectorHelper {
        private static readonly Obj_AI_Hero player = ObjectManager.Player;

        // Credits to furikuretsu from Stackoverflow (http://stackoverflow.com/a/10772759)
        // Modified for my needs
        #region ConeCalculations

        public static bool IsLyingInCone(Vector2 position, Vector2 apexPoint, Vector2 circleCenter, double aperture) {
            // This is for our convenience
            double halfAperture = aperture / 2;

            // Vector pointing to X point from apex
            Vector2 apexToXVect = apexPoint - position;

            // Vector pointing from apex to circle-center point.
            Vector2 axisVect = apexPoint - circleCenter;

            // X is lying in cone only if it's lying in 
            // infinite version of its cone -- that is, 
            // not limited by "round basement".
            // We'll use dotProd() to 
            // determine angle between apexToXVect and axis.
            bool isInInfiniteCone = DotProd(apexToXVect, axisVect) / Magn(apexToXVect) / Magn(axisVect) >
                // We can safely compare cos() of angles 
                // between vectors instead of bare angles.
            Math.Cos(halfAperture);

            if (!isInInfiniteCone)
                return false;

            // X is contained in cone only if projection of apexToXVect to axis
            // is shorter than axis. 
            // We'll use dotProd() to figure projection length.
            bool isUnderRoundCap = DotProd(apexToXVect, axisVect) / Magn(axisVect) < Magn(axisVect);

            return isUnderRoundCap;
        }

        private static float DotProd(Vector2 a, Vector2 b) {
            return a.X * b.X + a.Y * b.Y;
        }

        private static float Magn(Vector2 a) {
            return (float)(Math.Sqrt(a.X * a.X + a.Y * a.Y));
        }

        #endregion

        public static Vector2? GetFirstWallPoint(Vector3 from, Vector3 to, float step = 25) {
            return GetFirstWallPoint(from.ToVector2(), to.ToVector2(), step);
        }

        public static Vector2? GetFirstWallPoint(Vector2 from, Vector2 to, float step = 25) {
            var direction = (to - from).Normalized();

            for (float d = 0; d < from.Distance(to); d = d + step) {
                var testPoint = from + d * direction;
                var flags = NavMesh.GetCollisionFlags(testPoint.X, testPoint.Y);
                if (flags.HasFlag(CollisionFlags.Wall) || flags.HasFlag(CollisionFlags.Building)) {
                    return from + (d - step) * direction;
                }
            }

            return null;
        }
    }
    #endregion
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