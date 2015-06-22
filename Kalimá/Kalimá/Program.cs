using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Color = System.Drawing.Color;
using Collision = LeagueSharp.Common.Collision;

namespace Kalimá {
    internal class Kalista {
        static Dictionary<Vector3, Vector3> jumpPos;
        static readonly Obj_AI_Hero Player = ObjectManager.Player;
        static Orbwalking.Orbwalker Orbwalker;
        static Menu kalimenu;
        static Menu kalm { get { return Kalista.kalimenu; } }
        static float Manapercent { get { return Player.Mana / Player.MaxMana * 100; } }

        static Spell Q, W, E, R;
        static Obj_AI_Hero soulmate;//store the soulbound friend..
        static float soulmateRange = 1250f;
        //        const string List<SpellSlot> AUTO_LEVEL_SEQUENCE = new List<SpellSlot>() { SpellSlot.E, SpellSlot.Q, SpellSlot.W, SpellSlot.E, SpellSlot.Q, SpellSlot.R, SpellSlot.E, SpellSlot.E, SpellSlot.E, SpellSlot.Q, SpellSlot.R, SpellSlot.Q, SpellSlot.Q, SpellSlot.W, SpellSlot.W, SpellSlot.R, SpellSlot.W, SpellSlot.W }; //Assuming E>Q>R>W

        static void Game_OnGameLoad(EventArgs args) {
            if (Player.ChampionName != "Kalista") { return; }
            Q = new Spell(SpellSlot.Q, 1150f);
            Q.SetSkillshot(0.25f, 40f, 1700f, true, SkillshotType.SkillshotLine);
            W = new Spell(SpellSlot.W, 5200f);
            E = new Spell(SpellSlot.E, 1200f);
            R = new Spell(SpellSlot.R, 1200f);

            menuload();
            Game.OnUpdate += Game_OnUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Obj_AI_Hero.OnProcessSpellCast += Event_OnProcessSpellCast;
            Orbwalking.OnNonKillableMinion += Event_OnNonKillableMinion;
            FillPositions();
        }
        static void Main(string[] args) { CustomEvents.Game.OnGameLoad += Game_OnGameLoad; }

        static void menuload() {
            kalimenu = new Menu("Kalimá", Player.ChampionName, true);
            Menu OrbwalkerMenu = kalimenu.AddSubMenu(new Menu("Orbwalker", "Orbwalker"));
            Orbwalker = new Orbwalking.Orbwalker(OrbwalkerMenu);
            TargetSelector.AddToMenu(kalimenu.AddSubMenu(new Menu(Player.ChampionName + ": Target Selector", "Target Selector")));
            //            Menu combM = kalimenu.AddSubMenu(new Menu("Combo", "Combo"));
            Menu haraM = kalimenu.AddSubMenu(new Menu("Harass", "Harass"));
            Menu LaneM = kalimenu.AddSubMenu(new Menu("Lane Clear", "LaneClear"));
            Menu JungM = kalimenu.AddSubMenu(new Menu("Jungle Clear", "Jungleclear"));
            Menu MiscM = kalimenu.AddSubMenu(new Menu("Misc", "Misc"));
            Menu DrawM = kalimenu.AddSubMenu(new Menu("Drawing", "Drawing"));

            haraM.AddItem(new MenuItem("harassQ", "Use Q", true).SetValue(true));
            haraM.AddItem(new MenuItem("harassQchance", "Q cast if Chance of hit is:", true).SetValue(new Slider(4, 1, 4)));
            haraM.AddItem(new MenuItem("harassmanaminQ", "Q requires % mana", true).SetValue(new Slider(60, 0, 100)));
            haraM.AddItem(new MenuItem("harassuseE", "Use E", true).SetValue(true));
            haraM.AddItem(new MenuItem("harassEoutOfRange", "Use E when out of range", true).SetValue(true));
            haraM.AddItem(new MenuItem("harassE", "when being able to kill X minions and E champion", true).SetValue(new Slider(2, 1, 10)));
            haraM.AddItem(new MenuItem("harassmanaminE", "E requires % mana", true).SetValue(new Slider(30, 0, 100)));
            haraM.AddItem(new MenuItem("harassActive", "Active", true).SetValue(true));

            JungM.AddItem(new MenuItem("jungleclearQ", "Use Q", true).SetValue(false));
            JungM.AddItem(new MenuItem("jungleclearE", "Use E", true).SetValue(true));
            JungM.AddItem(new MenuItem("jungleclearmana", "E requires % mana", true).SetValue(new Slider(20, 0, 100)));
            JungM.AddItem(new MenuItem("bardragsteal", "Steal dragon/baron", true).SetValue(true));
            JungM.AddItem(new MenuItem("jungleActive", "Active", true).SetValue(true));

            LaneM.AddItem(new MenuItem("laneclearQ", "Use Q", true).SetValue(true));
            LaneM.AddItem(new MenuItem("laneclearQcast", "Q cast if minions >= X", true).SetValue(new Slider(2, 0, 10)));
            LaneM.AddItem(new MenuItem("laneclearQcastchance", "Q cast if Chance of hit is:", true).SetValue(new Slider(4, 0, 4)));
            LaneM.AddItem(new MenuItem("laneclearmanaminQ", "Q requires % mana", true).SetValue(new Slider(65, 0, 100)));
            LaneM.AddItem(new MenuItem("laneclearE", "Use E", true).SetValue(true));
            LaneM.AddItem(new MenuItem("laneclearEcast", "E cast if minions >= X", true).SetValue(new Slider(3, 0, 10)));
            LaneM.AddItem(new MenuItem("laneclearmanaminE", "E requires % mana", true).SetValue(new Slider(30, 0, 100)));
            LaneM.AddItem(new MenuItem("laneclearlasthit", "E when non-killable by AA", true).SetValue(true));

            MiscM.AddItem(new MenuItem("AutoLevel", "Auto Level Skills", true).SetValue(true));
            MiscM.AddItem(new MenuItem("autoW", "Auto W", true).SetValue(true));
            MiscM.AddItem(new MenuItem("killsteal", "Kill Steal", true).SetValue(true));
            MiscM.AddItem(new MenuItem("savesoulbound", "Save Soulbound (With R)", true).SetValue(true));
            MiscM.AddItem(new MenuItem("savesoulboundat", "Save when health < %", true).SetValue(new Slider(25, 0, 100)));
            MiscM.AddItem(new MenuItem("fleeKey", "Flee Toggle").SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press)));


            DrawM.AddItem(new MenuItem("drawAA", "Real Attack Range").SetValue(new Circle(true, Color.FromArgb(0, 230, 255))));
            DrawM.AddItem(new MenuItem("drawjumpspots", "Jump Spots").SetValue(new Circle(true, Color.FromArgb(0, 230, 255))));
            DrawM.AddItem(new MenuItem("drawQ", "Q Range").SetValue(new Circle(true, Color.FromArgb(0, 230, 255))));
            DrawM.AddItem(new MenuItem("drawW", "W Range").SetValue(new Circle(true, Color.FromArgb(0, 230, 255))));
            DrawM.AddItem(new MenuItem("drawE", "E Range").SetValue(new Circle(true, Color.FromArgb(0, 230, 255))));
            DrawM.AddItem(new MenuItem("drawR", "R Range").SetValue(new Circle(true, Color.FromArgb(0, 230, 255))));
            //marksman...
            DrawM.AddItem(new MenuItem("drawsoulmatelink", "Draw Link Signal", true).SetValue(true));
            DrawM.AddItem(new MenuItem("drawcoords", "Draw Map Coords", true).SetValue(true));
            kalimenu.AddToMainMenu();
        }

        static void Game_OnUpdate(EventArgs args) {
            if (Player.IsDead || Player.IsRecalling()) { return; }


            if (kalm.Item("killsteal", true).GetValue<Boolean>()) {
                Killsteal();
            }
            if (Player.IsRecalling()) { return; }

            if (kalm.Item("harassActive", true).GetValue<Boolean>()) {
                harass();
            }
            if (kalm.Item("jungleActive", true).GetValue<Boolean>()) {
                Jungleclear();
            }
            if (kalm.Item("autoW", true).GetValue<Boolean>()) {
                AutoW();
            }
            if (kalm.Item("fleeKey").GetValue<KeyBind>().Active) {
                ShowjumpsandFlee();
            }
           
            switch (Orbwalker.ActiveMode) {
                case Orbwalking.OrbwalkingMode.LaneClear:
                    laneclear();
                    break;
                case Orbwalking.OrbwalkingMode.LastHit:
                    break;
                case Orbwalking.OrbwalkingMode.Mixed:
                    break;
            }
        }
        static float? harasstimers;
        static void harass() {
            //use for E only since its the only one that does "double" shots
            if (harasstimers != null) {
                if ((Game.ClockTime - harasstimers) > 0.200) {
                    harasstimers = null;
                } else { return; }
            }
            var lqmana = kalm.Item("harassmanaminQ", true).GetValue<Slider>().Value;
            var lemana = kalm.Item("harassmanaminE", true).GetValue<Slider>().Value;
            var minmana = lqmana;
            var mymana = Manapercent;
            if (lemana < minmana) { minmana = lemana; }
            if (mymana < minmana) { return; }//quick check to return if less than minmana...

            if (kalm.Item("harassQ", true).GetValue<Boolean>() && mymana > lqmana && Q.IsReady(1) && !Player.IsDashing()) {
                foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(h => h.IsValidTarget(Q.Range) && Q.CanCast(h))) {
                    if (Q.GetPrediction(enemy).Hitchance >= gethitchanceQ) {
                        Q.Cast(enemy);
                    }
                    if (Q.GetPrediction(enemy).Hitchance == HitChance.Collision) {
                        var collide = Q.GetPrediction(enemy).CollisionObjects;
                        var minionkillcount = 0;
                        foreach (var minion in collide) {
                            if (minion.Health > Q.GetDamage(minion)) { minionkillcount++; }
                        }
                        if (minionkillcount == 0 && Q.GetPrediction(enemy).Hitchance >= gethitchanceQ) {
                            Q.Cast(enemy);
                        }
                    }
                }
            }

            if (!kalm.Item("harassuseE", true).GetValue<Boolean>()) { return; }
            if (mymana < lemana || !E.IsReady()) { return; }

            if (kalm.Item("harassE", true).GetValue<Slider>().Value >= 1 && kalm.Item("harassEoutOfRange", true).GetValue<Boolean>()) {
                var minionkillcount = 0;
                //use R.range instead of E.range so it can harass ".outofrange" as long as E is castable
                var Minions = MinionManager.GetMinions(Player.ServerPosition, R.Range, MinionTypes.All, MinionTeam.NotAlly);
                if (Minions != null) {
                    foreach (var Minion in Minions.Where(x => E.CanCast(x) && x.Health <= GetEDamage(x))) { minionkillcount++; }

                    if (minionkillcount > 0) {
                        foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(h => E.CanCast(h))) {
                            if (minionkillcount >= kalm.Item("harassE", true).GetValue<Slider>().Value) {
                                E.Cast();
                                harasstimers = Game.ClockTime;
                                return;
                            }
                        }
                    }
                }
            }
        }

        static HitChance gethitchanceQ { get { return hitchanceQ(); } }
        static HitChance hitchanceQ() {
            switch (kalm.Item("harassQchance", true).GetValue<Slider>().Value) {
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

        static void Jungleclear() {
            var mymana = Manapercent;
            if (mymana < kalm.Item("jungleclearmana", true).GetValue<Slider>().Value) { return; }

            //baron / dragon
            if (kalm.Item("bardragsteal", true).GetValue<Boolean>()) {
                var junglehugeE = MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.MaxHealth).FirstOrDefault(x => x.Health + (x.HPRegenRate / 2) <= GetEDamage(x) && (x.BaseSkinName.ToLower().Contains("dragon") || x.BaseSkinName.ToLower().Contains("baron")));
                if (E.CanCast(junglehugeE)) { E.Cast(); }
                var junglehugeQ = MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.MaxHealth).FirstOrDefault(x => x.Health + (x.HPRegenRate / 2) <= Q.GetDamage(x) && (x.BaseSkinName.ToLower().Contains("dragon") || x.BaseSkinName.ToLower().Contains("baron")));
                if (Q.CanCast(junglehugeQ)) { Q.Cast(junglehugeQ); }
            }
            //other minions in jungle...
            var jungleinside = MinionManager.GetMinions(E.Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth).FirstOrDefault();
            if (kalm.Item("jungleclearQ", true).GetValue<Boolean>()) {
                if (Q.CanCast(jungleinside)) { Q.Cast(jungleinside); }
            }
            if (kalm.Item("jungleclearE", true).GetValue<Boolean>()) {
                if (E.CanCast(jungleinside) && (jungleinside.Health + (jungleinside.HPRegenRate / 2)) <= GetEDamage(jungleinside)) { E.Cast(); }
            }
        }

        static float? lanecleartimer;
        static void laneclear() {
            //only execute every 200 milliseconds...(this should give about 5 times a sec which is more than enough)
            if (lanecleartimer != null) {
                if ((Game.ClockTime - lanecleartimer) > 0.200) {
                    lanecleartimer = null;
                } else { return; }
            }

            var lqmana = kalm.Item("laneclearmanaminQ", true).GetValue<Slider>().Value;
            var lemana = kalm.Item("laneclearmanaminE", true).GetValue<Slider>().Value;
            var minmana = lqmana;
            var mymana = Manapercent;
            if (lemana < minmana) { minmana = lemana; }
            if (mymana < minmana) { return; }

            var Minions = MinionManager.GetMinions(Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.Enemy);
            if (Minions.Count <= 0) { return; }

            if (kalm.Item("laneclearQ", true).GetValue<Boolean>() && Q.IsReady() && mymana >= lqmana && !Player.IsDashing()) {
                foreach (var minion in Minions.Where(x => x.Health <= Q.GetDamage(x))) {
                    var killcount = 0;

                    foreach (var colminion in Q_GetCollisionMinions(Player, Player.ServerPosition.Extend(minion.ServerPosition, Q.Range))) {
                        if (colminion.Health <= Q.GetDamage(colminion))
                            killcount++;
                        else
                            break;
                    }

                    if (killcount >= kalm.Item("laneclearQcast", true).GetValue<Slider>().Value) {
                        Q.Cast(minion);
                        break;
                    }
                }
            }

            if (kalm.Item("laneclearE", true).GetValue<Boolean>() && E.IsReady() && mymana >= lemana && !Player.IsDashing()) {
                var minionkillcount = 0;

                foreach (var Minion in Minions.Where(x => E.CanCast(x) && x.Health <= GetEDamage(x))) { minionkillcount++; }

                if (minionkillcount >= kalm.Item("laneclearEcast", true).GetValue<Slider>().Value) {
                    E.Cast();
                    lanecleartimer = Game.ClockTime;
                }
            }
        }

        static void Event_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args) {
            if (sender.IsMe) {
                if (args.SData.Name == "KalistaExpungeWrapper") {
//                    Orbwalking.ResetAutoAttackTimer();
                }
            }
            if (kalm.Item("savesoulbound", true).GetValue<Boolean>() && R.IsReady() && soulmate != null) {
                if (sender.Type == GameObjectType.obj_AI_Hero && sender.IsEnemy) {
                    if (soulmate.HealthPercent <= kalm.Item("savesoulboundat").GetValue<Slider>().Value) {
                        R.Cast();
                    }
                }
            }
        }

        static void Event_OnNonKillableMinion(AttackableUnit minion) {
            if (!kalm.Item("laneclearE", true).GetValue<Boolean>()) { return; }
            if (Manapercent < kalm.Item("laneclearmanaminE", true).GetValue<Slider>().Value) { return; }

            if (kalm.Item("laneclearlasthit", true).GetValue<Boolean>()) {
                if (E.CanCast((Obj_AI_Base)minion) && minion.Health <= GetEDamage((Obj_AI_Base)minion)) {
                    E.Cast();
                }
            }
        }
        static void Drawing_OnDraw(EventArgs args) {
            if (Player.IsDead) { return; }
            var curposition = Player.Position;
            var dAA = kalm.Item("drawAA").GetValue<Circle>();
            var dQ = kalm.Item("drawQ").GetValue<Circle>();
            var dW = kalm.Item("drawW").GetValue<Circle>();
            var dE = kalm.Item("drawE").GetValue<Circle>();
            var dR = kalm.Item("drawR").GetValue<Circle>();
            var dj = kalm.Item("drawjumpspots").GetValue<Circle>();

            if (dAA.Active) { Render.Circle.DrawCircle(curposition, Orbwalking.GetRealAutoAttackRange(Player), dAA.Color); }
            if (Q.IsReady() && dQ.Active) { Render.Circle.DrawCircle(curposition, Q.Range, dQ.Color); }
            if (W.IsReady() && dW.Active) { Render.Circle.DrawCircle(curposition, W.Range, dW.Color); }
            if (E.IsReady() && dE.Active) { Render.Circle.DrawCircle(curposition, E.Range, dE.Color); }
            if (R.IsReady() && dR.Active) { Render.Circle.DrawCircle(curposition, R.Range, dR.Color); }
            draw_soulmate_link(kalm.Item("drawsoulmatelink", true).GetValue<Boolean>());
            if (dj.Active) {
                draw_jump_spots();
            }
            if (kalm.Item("drawcoords", true).GetValue<Boolean>()) {
                Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.70f, Color.GreenYellow, "Coords:" + Player.Position);
            }
        }

        static List<Obj_AI_Base> Q_GetCollisionMinions(Obj_AI_Hero source, Vector3 targetposition) {
            var input = new PredictionInput {
                Unit = source,
                Radius = Q.Width,
                Delay = Q.Delay,
                Speed = Q.Speed,
            };

            input.CollisionObjects[0] = CollisionableObjects.Minions;

            return Collision.GetCollision(new List<Vector3> { targetposition }, input).OrderBy(obj => obj.Distance(source, false)).ToList();
        }

        static void Killsteal() {
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(h => Q.CanCast(h) || E.CanCast(h))) {
                var edmg = GetEDamage(enemy);
                var enemyhealth = enemy.Health;
                var enemyregen = enemy.HPRegenRate / 2;
                if (((enemyhealth + enemyregen) <= edmg) && E.CanCast(enemy)) { E.Cast(); return; }
                if (Q.GetPrediction(enemy).Hitchance >= HitChance.VeryHigh && Q.CanCast(enemy)) {
                    var qdamage = Player.GetSpellDamage(enemy, SpellSlot.Q);
                    if ((qdamage + edmg) >= (enemyhealth + enemyregen)) {
                        Q.Cast(enemy);
                        return;
                    }
                }
            }
        }

        static float GetEDamage(Obj_AI_Base target) {
            var stacks = target.GetBuffCount("kalistaexpungemarker");
            if (stacks == null || !E.IsReady()) { return 1; }

            var baseDamage = new[] { 20, 30, 40, 50, 60 };
            var bd = baseDamage[E.Level - 1];
            var additionalBaseDamage = new[] { 0.6f, 0.6f, 0.6f, 0.6f, 0.6f };
            var abd = additionalBaseDamage[E.Level - 1];

            var spearDamage = new[] { 10, 14, 19, 25, 32 };
            var sd = spearDamage[E.Level - 1];
            var additionalSpearDamage = new[] { 0.20f, 0.225f, 0.25f, 0.275f, 0.30f };
            var asd = additionalSpearDamage[E.Level - 1];

            double totalDamage = bd + abd * Player.TotalAttackDamage + (stacks - 1) * (sd + asd * Player.TotalAttackDamage);

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
                    var mastery = Player.Masteries.FirstOrDefault(m => m.Page == MasteryPage.Offense && m.Id == 100);
                    if (mastery != null && mastery.Points >= 1 &&
                        target.Health / target.MaxHealth <= 0.05d + 0.15d * mastery.Points) {
                        totalDamage = totalDamage * 1.05;
                    }
                }
            }
            return (float)totalDamage;
        }

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
            _mysentinels.Add(new mysentinels("Blue Camp Blue Buff", (Vector3)SummonersRift.Jungle.Blue_BlueBuff));
            _mysentinels.Add(new mysentinels("Blue Camp Red Buff", (Vector3)SummonersRift.Jungle.Blue_RedBuff));
            _mysentinels.Add(new mysentinels("Red Camp Blue Buff", (Vector3)SummonersRift.Jungle.Red_BlueBuff));
            _mysentinels.Add(new mysentinels("Red Camp Red Buff", (Vector3)SummonersRift.Jungle.Red_RedBuff));
            _mysentinels.Add(new mysentinels("Dragon", (Vector3)SummonersRift.River.Dragon));
            _mysentinels.Add(new mysentinels("Baron", (Vector3)SummonersRift.River.Baron));
            _mysentinels.Add(new mysentinels("Mid Bot River", new Vector3(8370f, 6176f,-71.2406f)));
            //add river mid bush here...
            //_mysentinels.Add(new mysentinels("RiverTop", (Vector3)SummonersRift.Bushes.);
        }
        static float? autoWtimers;
        static void AutoW() {
            var useW = kalm.Item("autoW", true).GetValue<Boolean>();
            if (useW && W.IsReady()) {
                if (autoWtimers != null) {
                    if ((Game.ClockTime - autoWtimers) > 2) {
                        autoWtimers = null;
                    } else { return; }
                }
                if ((Player.ManaPercent < 50) || Player.IsAttackingPlayer || Player.IsDashing() || Player.IsWindingUp) { return; }
                fillsentinels();
//                var sentinels = _mysentinels.Where(s => s.Name.Contains("RobotBuddy"));
                Random rnd = new Random();
                var sentineldestinations = _mysentinels.Where(s => !s.Name.Contains("RobotBuddy")).OrderBy(s => rnd.Next()).ToList();
                foreach (var destinations in sentineldestinations) {
                    var distancefromme = Vector3.Distance(Player.Position, destinations.Position);
                    if (sentinelcloserthan(destinations.Position, 1500) == 0 && distancefromme < W.Range) {
                        autoWtimers = Game.ClockTime;
                        W.Cast(destinations.Position);
                        Notifications.AddNotification(new Notification("sending bug to:" + destinations.Name, 5000).SetTextColor(Color.FromArgb(255, 0, 0)));
                        return;                    
                    }
                }
            }
        }

        static void draw_jump_spots() {
            const float circleRange = 75f;
            foreach (var pos in jumpPos) {
                if (Player.Distance(pos.Key) <= 500f || Player.Distance(pos.Value) <= 500f) {
                    Render.Circle.DrawCircle(pos.Key, circleRange, System.Drawing.Color.Blue);
                    Render.Circle.DrawCircle(pos.Value, circleRange, System.Drawing.Color.Blue);
                }
            }
        }

        static void draw_soulmate_link(bool drawlink) {
            if (Player.IsDead || !drawlink || W.Level == 0) { return; }

            if (soulmate == null) {
                soulmate = HeroManager.Allies.Find(a => !a.IsMe && a.Buffs.Any(b => b.Name.Contains("kalistacoopstrikeally")));
            }
            if (soulmate.IsDead) {
                Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.82f, Color.Red, "Connection Signal with " + soulmate.ChampionName + ": None");
            } else {
                var soulrange = Player.Distance(soulmate.Position);
                if (soulrange > soulmateRange) {
                    Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.82f, Color.Red, "Connection Signal with " + soulmate.ChampionName + ": None");
                } else if (soulrange > 800) {
                    Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.82f, Color.Gold, "Connection Signal with " + soulmate.ChampionName + ": Low");
                } else { Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.82f, Color.GreenYellow, "Connection Signal with " + soulmate.ChampionName + ": Good"); }
            }
        }

        static void ShowjumpsandFlee() {
            if (!Q.IsReady()) { return; }

            Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.10f, Color.GreenYellow, "Wall Jump Active");
            var XXX = (Vector3)canjump();
            if (XXX != null) {
                Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.50f, Color.GreenYellow, "could jump here");
                Q.Cast(XXX);
                Orbwalking.Orbwalk(null, XXX, 90f, 0f, false, false);
            } else {
                Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.50f, Color.GreenYellow, "can't jump here");
            }

            foreach (var pos in jumpPos) {
                if (Player.Distance(pos.Key) <= 50f || Player.Distance(pos.Value) <= 50f) {
                    var x = (Vector3)canjump();
                    if (x != null) {
                        Q.Cast(x);
                        Orbwalking.Orbwalk(null, x, 90f, 0f, false, false);
                    }
                } else { return; }
            }

        }


        static Vector3? canjump() {
            var wallCheck = VectorHelper.GetFirstWallPoint(Player.Position, Player.Position);
            //loop angles around the player to check for a point to jump to
            float maxAngle = 80;
            float step = maxAngle / 20;
            float currentAngle = 0;
            float currentStep = 0;
            Vector3 currentPosition = Player.Position;
            Vector2 direction = ((Player.Position.To2D() + 50) - currentPosition.To2D()).Normalized();
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
                    checkPoint = currentPosition + 300 * direction.To3D();
                } else {
                    checkPoint = currentPosition + 300 * direction.Rotated(currentAngle).To3D();
                }
                if (checkPoint.IsWall()) { continue; }
                // Check if there is a wall between the checkPoint and currentPosition
                wallCheck = VectorHelper.GetFirstWallPoint(checkPoint, currentPosition);
                if (wallCheck == null) { continue; } //jump to the next loop
                //get the jump point
                Vector3 wallPositionOpposite = (Vector3)VectorHelper.GetFirstWallPoint((Vector3)wallCheck, currentPosition, 5);
                //check if the walking path is big enough to be worth a jump..if not then just skip to the next loop
                if (Player.GetPath(wallPositionOpposite).ToList().To2D().PathLength() - Player.Distance(wallPositionOpposite) < 230) {
                    Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.30f, Color.GreenYellow, "not worth a jump...");
                    continue;
                }

                //check the jump distance and if its short enough then jump...
                if (Player.Distance(wallPositionOpposite, true) < Math.Pow(300 - Player.BoundingRadius / 2, 2)) {
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

    }

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
            return GetFirstWallPoint(from.To2D(), to.To2D(), step);
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

        public static List<Obj_AI_Base> GetDashObjects(IEnumerable<Obj_AI_Base> predefinedObjectList = null) {
            List<Obj_AI_Base> objects;
            if (predefinedObjectList != null)
                objects = predefinedObjectList.ToList();
            else
                objects = ObjectManager.Get<Obj_AI_Base>().Where(o => o.IsValidTarget(Orbwalking.GetRealAutoAttackRange(o))).ToList();

            var apexPoint = player.ServerPosition.To2D() + (player.ServerPosition.To2D() - Game.CursorPos.To2D()).Normalized() * Orbwalking.GetRealAutoAttackRange(player);

            return objects.Where(o => VectorHelper.IsLyingInCone(o.ServerPosition.To2D(), apexPoint, player.ServerPosition.To2D(), Math.PI)).OrderBy(o => o.Distance(apexPoint, true)).ToList();
        }
    }
}
