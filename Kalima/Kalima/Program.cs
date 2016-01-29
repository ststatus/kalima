#region REFS
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
//using Orbwalking = Kalima.refs.Orbwalking;
#endregion

namespace Kalima {
    internal class Kalista {

        #region GAME LOAD
        static Dictionary<Vector3, Vector3> jumpPos;
        static readonly Obj_AI_Hero Player = ObjectManager.Player;
        static int playerlevel { get { return Player.Level; } }
        static Vector3 MyPosition { get { return Player.Position; } }
        static Orbwalking.Orbwalker Orbwalker;
        static Menu kalimenu;
        static Menu kalm { get { return Kalista.kalimenu; } }
        static float Manapercent;
        static Circle drawAA;
        static Circle drawQ;
        static Circle drawW;
        static Circle drawE;
        static Circle drawR;
        static Circle drawJumpSpots;

        static bool laneclearQ;
        static bool laneclearQmana;
        static bool laneclearE;
        static bool laneclearEmana;
        static float laneclearEminhealth;
        static float laneclearEcastmin;
        static float laneclearEcastincr;
        //minminions + (playerlvl * (incr / 10))
        static double laneclearEminminions { get { return Math.Round(laneclearEcastmin + (playerlevel * laneclearEcastincr / 10), 0, MidpointRounding.ToEven); } }
        static bool laneclearbigminions { get { return kalm.Item("laneclearbigminionsE", true).GetValue<Boolean>(); } }
        //store the soulbound friend..
        static Obj_AI_Hero soulmate = null;
        static float soulmateHealthPercent = 0;
        static float soulmateRange = 1400f;
        static float soulmaterangefromme = 0;
        static int soulmatelinkrange = 0;
        static Vector3 soulmateposition { get { return soulmate.Position; } }
        static bool soulmatesave { get { return kalm.Item("savesoulbound", true).GetValue<Boolean>(); } }
        static float soulmatesaveat { get { return kalm.Item("savesoulboundat", true).GetValue<Slider>().Value; } }
        static bool soulbalistador = false;

        static bool balista { get { return kalm.Item("balistaActive", true).GetValue<Boolean>(); } }
        static float balistaminEnemyFromSoul { get { return kalm.Item("balistaminrangefromsoul", true).GetValue<Slider>().Value; } }
        static float balistaminSoulFromMe { get { return kalm.Item("balistaminrange", true).GetValue<Slider>().Value; } }
        static float balistamaxSoulFromMe { get { return kalm.Item("balistamaxrange", true).GetValue<Slider>().Value; } }
        static float balistamaxEnemyFromMe { get { return kalm.Item("balistenemyamaxrange", true).GetValue<Slider>().Value; } }


        static bool spellsreadyEQ { get { if (E.IsReady() && Q.IsReady()) { return true; };return false; } }
        static bool spellsEQmana { get { if (Player.Mana > (E.ManaCost + Q.ManaCost)) { return true; } return false; } }
        static bool playerisready { get { if (Player.IsRecalling() || Player.IsCastingInterruptableSpell() || Player.IsDead) { return false; };return true; } }
        static bool canuseheal = false;
        static long? onupdatetimers20000 = DateTime.Now.Ticks;
        static bool onupdate20000 { get { if ((DateTime.Now.Ticks - onupdatetimers20000) > 200000000) { onupdatetimers20000 = DateTime.Now.Ticks; return true; }; return false; } }
        static long? onupdatetimers2000 = DateTime.Now.Ticks;
        static bool onupdate2000 { get { if ((DateTime.Now.Ticks - onupdatetimers2000) > 20000000) { onupdatetimers2000 = DateTime.Now.Ticks; return true; }; return false; } }
        static long? onupdatetimers1000 = DateTime.Now.Ticks;
        static bool onupdate1000 { get { if ((DateTime.Now.Ticks - onupdatetimers1000) > 10000000) { onupdatetimers1000 = DateTime.Now.Ticks; return true; }; return false; } }
        static long? onupdatetimers500 = DateTime.Now.Ticks;
        static bool onupdate500 { get { if ((DateTime.Now.Ticks - onupdatetimers500) > 5000000) { onupdatetimers500 = DateTime.Now.Ticks; return true; }; return false; } }
        static long? onupdatetimers200 = DateTime.Now.Ticks;
        static bool onupdate200 { get { if ((DateTime.Now.Ticks - onupdatetimers200) > 2000000) { onupdatetimers200 = DateTime.Now.Ticks; return true; }; return false; } }
        static long? onupdatetimers100 = DateTime.Now.Ticks;
        static bool onupdate100 { get { if ((DateTime.Now.Ticks - onupdatetimers100) > 1000000) { onupdatetimers100 = DateTime.Now.Ticks; return true; }; return false; } }
        static long? onupdatetimers50 = DateTime.Now.Ticks;
        static bool onupdate50 { get { if ((DateTime.Now.Ticks - onupdatetimers50) > 500000) { onupdatetimers50 = DateTime.Now.Ticks; return true; }; return false; } }


        static Spell Q, W, E, R;
        static int MyLevel = 0;
        static Items.Item botrk = new Items.Item(3153, 550);
        static Items.Item mercurial = new Items.Item(3139,0f);//debuff
        static Items.Item dervish = new Items.Item(3137, 0f);//debuff
        static Items.Item qss = new Items.Item(3140,0f);//debuff
        static SpellSlot summHeal;

        static void Game_OnGameLoad(EventArgs args) {//"1 3 1 2 1 4 1 3 1 3 4 3 3 2 2 4 2 2";
            if (Player.ChampionName != "Kalista") { return; }
            Q = new Spell(SpellSlot.Q, 1150f);
            Q.SetSkillshot(0.25f, 40f, 1700f, true, SkillshotType.SkillshotLine);
            W = new Spell(SpellSlot.W, 5200f);
            E = new Spell(SpellSlot.E, 1200f);
            R = new Spell(SpellSlot.R, 1400f);

            menuload();
            Game.OnUpdate += Game_OnUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            //time based ondraws...
            Drawing.OnDraw += DraWing.Drawing_OnDraw;
            Obj_AI_Hero.OnProcessSpellCast += Event_OnPreProcessSpellCast;
            Orbwalking.OnNonKillableMinion += Event_OnNonKillableMinion;
            Orbwalking.AfterAttack += Event_OnAfterAttack;
            Orbwalking.BeforeAttack += Event_OnBeforeAttack;
            AntiGapcloser.OnEnemyGapcloser += Event_OnEnemyGapcloser;
            Obj_AI_Hero.OnBuffAdd += Event_OnBuffAdd;
            MenuGlobals.MenuState =
            FillPositions();
            summHeal = Player.GetSpellSlot("summonerheal");
        }

        static void Main(string[] args) { CustomEvents.Game.OnGameLoad += Game_OnGameLoad; }
        #endregion

        #region MENU

        static void menuload() {
            kalimenu = new Menu("Kalimá", Player.ChampionName, true);
            Menu OrbwalkerMenu = kalimenu.AddSubMenu(new Menu("Orbwalker", "Orbwalker"));
            Orbwalker = new Orbwalking.Orbwalker(OrbwalkerMenu);
            //            Menu combM = kalimenu.AddSubMenu(new Menu("Combo", "Combo"));
            Menu haraM = kalimenu.AddSubMenu(new Menu("Harass", "Harass"));
            Menu LaneM = kalimenu.AddSubMenu(new Menu("Lane Clear", "LaneClear"));
            Menu JungM = kalimenu.AddSubMenu(new Menu("Jungle Clear", "Jungleclear"));
            Menu MiscM = kalimenu.AddSubMenu(new Menu("Misc", "Misc"));
            Menu ItemM = kalimenu.AddSubMenu(new Menu("Items", "Items"));
            Menu DrawM = kalimenu.AddSubMenu(new Menu("Drawing", "Drawing"));

            haraM.AddItem(new MenuItem("harassQ", "Use Q", true).SetValue(true));
            haraM.AddItem(new MenuItem("harassQchance", "Q cast if Chance of hit is:", true).SetValue(new Slider(2, 1, 4)));
            haraM.AddItem(new MenuItem("harassmanaminQ", "Q requires % mana", true).SetValue(new Slider(75, 0, 100)));
            haraM.AddItem(new MenuItem("harassuseE", "Use E", true).SetValue(true));
            haraM.AddItem(new MenuItem("harassEoutOfRange", "Use E when out of range", true).SetValue(true));
            haraM.AddItem(new MenuItem("harassE", "when being able to kill X minions and E champion", true).SetValue(new Slider(1, 1, 10)));
            haraM.AddItem(new MenuItem("harassEminhealth", "E req minion % health to prevent E cooldown", true).SetValue(new Slider(7, 1, 50)));
            haraM.AddItem(new MenuItem("harassmanaminE", "E requires % mana", true).SetValue(new Slider(60, 0, 100)));
            haraM.AddItem(new MenuItem("harassActive", "Active", true).SetValue(true));

            JungM.AddItem(new MenuItem("jungleclearQ", "Use Q", true).SetValue(false));
            JungM.AddItem(new MenuItem("jungleclearQdistance", "Q Max distance from jungle minion", true).SetValue(new Slider(250, 0, 1150)));
            JungM.AddItem(new MenuItem("jungleclearE", "Use E", true).SetValue(true));
            JungM.AddItem(new MenuItem("jungleclearmana", "E requires % mana", true).SetValue(new Slider(40, 0, 100)));
            JungM.AddItem(new MenuItem("bardragsteal", "Steal dragon/baron", true).SetValue(true));
            JungM.AddItem(new MenuItem("jungleclearQbd", "Use Q on dragon/baron?", true).SetValue(true));
            JungM.AddItem(new MenuItem("jungleActive", "Active", true).SetValue(true));

            LaneM.AddItem(new MenuItem("laneclearQ", "Use Q", true).SetValue(true));
            LaneM.AddItem(new MenuItem("laneclearQcast", "Q cast if minions >= X", true).SetValue(new Slider(2, 1, 10)));
            LaneM.AddItem(new MenuItem("laneclearmanaminQ", "Q requires % mana", true).SetValue(new Slider(65, 0, 100)));
            LaneM.AddItem(new MenuItem("laneclearE", "Use E", true).SetValue(true));
            LaneM.AddItem(new MenuItem("laneclearEcast", "E cast if minions >= X (min value)", true).SetValue(new Slider(2, 0, 10)));
            LaneM.AddItem(new MenuItem("laneclearEcastincr", "Increase number by Level (decimal):", true).SetValue(new Slider(2, 0, 4)));
            LaneM.AddItem(new MenuItem("laneclearEminhealth", "E req minion % health to prevent E cooldown", true).SetValue(new Slider(7, 1, 50)));
            LaneM.AddItem(new MenuItem("laneclearmanaminE", "E requires % mana", true).SetValue(new Slider(55, 0, 100)));
            LaneM.AddItem(new MenuItem("laneclearbigminionsE", "E when it can kill siege/super minions", true).SetValue(true));
            LaneM.AddItem(new MenuItem("laneclearlasthit", "E when non-killable by AA", true).SetValue(false));

            Menu BotrkM = ItemM.AddSubMenu(new Menu("Botrk", "Botrk"));
            BotrkM.AddItem(new MenuItem("botrkKS", "Use when target has < x% health + Q+E(dmg)", true).SetValue(new Slider(70, 10, 100)));
            BotrkM.AddItem(new MenuItem("botrkmyheal", "Use when my health is at: < x%", true).SetValue(new Slider(40, 0, 100)));
            BotrkM.AddItem(new MenuItem("botrkactive", "Active", true).SetValue(true));

            Menu Debuffs = ItemM.AddSubMenu(new Menu("Debuffs", "Debuffs"));
            Menu Debuffspells = Debuffs.AddSubMenu(new Menu("Debuff Spells", "Debuff Spells"));
            Debuffs.AddItem(new MenuItem("debuffitems", "Supports QSS/Mercurial/Dervish"));
            Debuffs.AddItem(new MenuItem("debuffitemsactive", "Active", true).SetValue(true));

            Debuffspells.AddItem(new MenuItem("debuff_blind", "Blind", true).SetValue(false));
            Debuffspells.AddItem(new MenuItem("debuff_rocketgrab2", "Blitz Grab", true).SetValue(true));
            Debuffspells.AddItem(new MenuItem("debuff_charm", "Charm", true).SetValue(true));
            Debuffspells.AddItem(new MenuItem("debuff_dehancer", "Dehancer", true).SetValue(false));
            Debuffspells.AddItem(new MenuItem("debuff_dispellExhaust", "Exhaust", true).SetValue(true));
            Debuffspells.AddItem(new MenuItem("debuff_fear", "Fear", true).SetValue(true));
            Debuffspells.AddItem(new MenuItem("debuff_flee", "Flee", true).SetValue(true));
            Debuffspells.AddItem(new MenuItem("debuff_polymorph", "Polymorph", true).SetValue(false));
            Debuffspells.AddItem(new MenuItem("debuff_snare", "Snare", true).SetValue(true));
            Debuffspells.AddItem(new MenuItem("debuff_suppression", "Suppression", true).SetValue(true));
            Debuffspells.AddItem(new MenuItem("debuff_stun", "Stun", true).SetValue(true));
            Debuffspells.AddItem(new MenuItem("debuff_silence", "Silence", true).SetValue(false));
            Debuffspells.AddItem(new MenuItem("debuff_taunt", "Taunt", true).SetValue(true));
            Debuffspells.AddItem(new MenuItem("debuff_zedultexecute", "Zed Ult", true).SetValue(true));

            MiscM.AddItem(new MenuItem("AutoLevel", "Auto Level Skills", true).SetValue(true));
            MiscM.AddItem(new MenuItem("autoresetAA", "Auto Reset AA", true).SetValue(true));
            Menu AutoW = MiscM.AddSubMenu(new Menu("Auto W", "Auto W"));
            AutoW.AddItem(new MenuItem("autoW", "Auto W (Toggle)", true).SetValue(true));
            AutoW.AddItem(new MenuItem("autoWmana", "Min Mana for AutoW", true).SetValue(new Slider(60, 1, 100)));
            AutoW.AddItem(new MenuItem("autoWKey", "Auto W HotKey").SetValue(new KeyBind("Y".ToCharArray()[0], KeyBindType.Press)));
            AutoW.AddItem(new MenuItem("autowenemyclose", "Dont Send W with an enemy in X Range:", true).SetValue(new Slider(2000, 0, 5000)));
            AutoW.AddItem(new MenuItem("autowsentinelclose", "Dont Send W with a sentinel in X Range of it:", true).SetValue(new Slider(1500, 500, 5000)));
            AutoW.AddItem(new MenuItem("autowspottooclosetome", "Dont Send W with me in X Range of spot:", true).SetValue(new Slider(1500, 500, 5000)));

            Menu AutoWSpots = AutoW.AddSubMenu(new Menu("Auto W Spots", "Auto W Spots"));
            AutoWSpots.AddItem(new MenuItem("Blue_Camp_Blue_Buff", "Blue Camp Blue Buff", true).SetValue(true));
            AutoWSpots.AddItem(new MenuItem("Blue_Camp_Red_Buff", "Blue Camp Red Buff", true).SetValue(false));
            AutoWSpots.AddItem(new MenuItem("Red_Camp_Blue_Buff", "Red Camp Blue Buff", true).SetValue(true));
            AutoWSpots.AddItem(new MenuItem("Red_Camp_Red_Buff", "Red Camp Red Buff", true).SetValue(true));
            AutoWSpots.AddItem(new MenuItem("Dragon", "Dragon", true).SetValue(true));
            AutoWSpots.AddItem(new MenuItem("Baron", "Baron", true).SetValue(true));
            AutoWSpots.AddItem(new MenuItem("Mid_Bot_River", "Mid Bot River", true).SetValue(true));

            MiscM.AddItem(new MenuItem("useheal", "Use heal to save myself", true).SetValue(true));
            MiscM.AddItem(new MenuItem("usehealat", "Use heal when health < %", true).SetValue(new Slider(20, 0, 100)));

            MiscM.AddItem(new MenuItem("killsteal", "Kill Steal", true).SetValue(true));
            MiscM.AddItem(new MenuItem("randomizeEpop", "Look more human by failing E pop on kill?", true).SetValue(true));
            MiscM.AddItem(new MenuItem("randomizeEpopminkills", "Min kills to randomize E pop", true).SetValue(new Slider(3, 1, 50)));
            MiscM.AddItem(new MenuItem("gapcloser", "use Q on GapCloser", true).SetValue(true));
            MiscM.AddItem(new MenuItem("savesoulbound", "Save Soulbound (With R)", true).SetValue(true));
            MiscM.AddItem(new MenuItem("savesoulboundat", "Save when health < %", true).SetValue(new Slider(25, 0, 100)));
            MiscM.AddItem(new MenuItem("preventdouble", "Prevent double E with timer", true).SetValue(true));
            MiscM.AddItem(new MenuItem("popEbeforedying", "Pop E before dying", true).SetValue(true));
            MiscM.AddItem(new MenuItem("fleeKey", "Flee Toggle").SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press)));
            Menu TimersM = MiscM.AddSubMenu(new Menu("Timer Limits", "Timer Limits"));
            TimersM.AddItem(new MenuItem("onupdateT", "OnUpdate Timer (max times per second)", true).SetValue(new Slider(30, 1, 100)));
            TimersM.AddItem(new MenuItem("ondrawT", "OnDraw Timer (max times per second)", true).SetValue(new Slider(30, 1, 500)));

            DrawM.AddItem(new MenuItem("drawAA", "Auto Attack Range").SetValue(new Circle(false, Color.FromArgb(207, 207, 23))));
            DrawM.AddItem(new MenuItem("drawjumpspots", "Jump Spots").SetValue(new Circle(false, Color.FromArgb(0, 0, 255))));
            DrawM.AddItem(new MenuItem("drawQ", "Q Range").SetValue(new Circle(false, Color.FromArgb(43, 255, 0))));
            DrawM.AddItem(new MenuItem("drawW", "W Range").SetValue(new Circle(false, Color.FromArgb(0, 0, 255))));
            DrawM.AddItem(new MenuItem("drawE", "E Range").SetValue(new Circle(false, Color.FromArgb(57, 138, 204))));
            DrawM.AddItem(new MenuItem("drawR", "R Range").SetValue(new Circle(false, Color.FromArgb(19, 154, 161))));
            //marksman...
            DrawM.AddItem(new MenuItem("drawEdmg", "Draw E dmg HPbar").SetValue(new Circle(true, Color.FromArgb(0, 138, 184))));
            DrawM.AddItem(new MenuItem("drawEspearsneeded", "Draw E# Spears Needed").SetValue(new Circle(false, Color.FromArgb(255, 140, 0))));
            DrawM.AddItem(new MenuItem("drawsoulmatelink", "Draw Link Signal", true).SetValue(true));
            DrawM.AddItem(new MenuItem("drawcoords", "Draw Map Coords", true).SetValue(false));
            var blitzskarneringame = HeroManager.Allies.Find(x => x.ChampionName == "Blitzcrank" || x.ChampionName == "Skarner" || x.ChampionName == "TahmKench");
            if (blitzskarneringame != null) {
                //check if its blitz or skarner the ally...
                string menuname;
                if (blitzskarneringame.CharData.BaseSkinName == "Blitzcrank") {
                    menuname = "Balista";
                } else if (blitzskarneringame.CharData.BaseSkinName == "Skarner") {
                    menuname = "Salista";
                } else { menuname = "Talista"; }

                Menu balista = kalimenu.AddSubMenu(new Menu(menuname, menuname));

                Menu targetselect = balista.AddSubMenu(new Menu("Target Selector", "Target Selector"));
                Menu champselect = balista.AddSubMenu(new Menu("Drawings", "Drawings"));
                champselect.AddItem(new MenuItem("drawminrange", "Min Range", true).SetValue(false));
                champselect.AddItem(new MenuItem("drawmaxrange", "Max Range", true).SetValue(false));
                champselect.AddItem(new MenuItem("lineformat", "Line Range", true).SetValue(true));

                foreach (var enemy in HeroManager.Enemies.FindAll(x => x.IsEnemy)) {
                    targetselect.AddItem(new MenuItem("target" + enemy.ChampionName, enemy.ChampionName).SetValue(true));
                    targetselect.AddItem(new MenuItem("maxhealth" + enemy.ChampionName, "Max Health to pull",true).SetValue(new Slider(200, 1, 100)));
                }
                if (blitzskarneringame.CharData.BaseSkinName == "Blitzcrank") {
                    balista.AddItem(new MenuItem("balistaminrangefromsoul", "Min Range enemy from Soulmate", true).SetValue(new Slider(300, 300, 925)));
                }
                balista.AddItem(new MenuItem("balistaminrange", "Min Range me from Soulmate", true).SetValue(new Slider(450, 450, 1400)));
                balista.AddItem(new MenuItem("balistamaxrange", "Max Range me from soulmate", true).SetValue(new Slider(1400, 500, 1400)));
                balista.AddItem(new MenuItem("balistenemyamaxrange", "Enemy Max Range from me", true).SetValue(new Slider(2300, 500, 2300)));
                balista.AddItem(new MenuItem("balistaActive", "Active", true).SetValue(true));
            }

            kalimenu.AddToMainMenu();
        }
        #endregion    
        #region EVENT GAME ON UPDATE
        static void Game_OnUpdate(EventArgs args) {
            if (Player.IsRecalling() || Player.IsDead) { return; }
            if (onupdate20000) {
            }
            if (onupdate2000) {
                //store heavily used menu values every 2 seconds so theres less lookups
                store_menu();
            }
            if (onupdate1000) {
                 Manapercent = Player.ManaPercent;
            }
            if (onupdate500) {
            }
            if (onupdate200) {
            }
            if (onupdate100) {
            }
            if (onupdate50) {
            }
        }

        #endregion

        #region MISC FUNCTIONS
        static void store_menu() {
            drawAA = kalm.Item("drawAA").GetValue<Circle>();
            drawQ = kalm.Item("drawQ").GetValue<Circle>();
            drawW = kalm.Item("drawW").GetValue<Circle>();
            drawE = kalm.Item("drawE").GetValue<Circle>();
            drawR = kalm.Item("drawR").GetValue<Circle>();
            drawJumpSpots = kalm.Item("drawjumpspots").GetValue<Circle>();
            laneclearQ = kalm.Item("laneclearQ", true).GetValue<Boolean>();
            if (kalm.Item("laneclearmanaminQ", true).GetValue<Slider>().Value > Manapercent) {
                laneclearQmana = true;
            } { laneclearQmana = false; }
            laneclearE = kalm.Item("laneclearE", true).GetValue<Boolean>();
            if (kalm.Item("laneclearmanaminE", true).GetValue<Slider>().Value > Manapercent) {
                laneclearEmana = true;
            } { laneclearEmana = false; }
            laneclearEminhealth = kalm.Item("laneclearEminhealth", true).GetValue<Slider>().Value;
            laneclearEcastmin = kalm.Item("laneclearEcast", true).GetValue<Slider>().Value;
            laneclearEcastincr = kalm.Item("laneclearEcastincr", true).GetValue<Slider>().Value;        
        }
        #endregion
    }
    
    
}
