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

namespace Kalimá
{
    class Program
    {
        //decs
        private static readonly Obj_AI_Hero Player = ObjectManager.Player;
        private static Menu kalimenu;
        private static Orbwalking.Orbwalker Orbwalker;
        private static Spell Q, W, E, R;
        private static Obj_AI_Hero soulmate;//store the soulbound friend..
        private static float soulmateRange = 1250f;
        
        //#Drawing color vars (updated on the OnUpdate event...)
        private static Circle drawAA = new Circle(true, Color.FromArgb(0, 230, 255));
        private static Color drawAAC = drawAA.Color;
        private static Circle drawQ = new Circle(true, Color.FromArgb(0, 230, 255));
        private static Color drawQC = drawQ.Color;
        private static Circle drawW = new Circle(true, Color.FromArgb(0, 230, 255));
        private static Color drawWC = drawW.Color;
        private static Circle drawE = new Circle(true, Color.FromArgb(0, 230, 255));
        private static Color drawEC = drawE.Color;
        private static Circle drawR = new Circle(true, Color.FromArgb(0, 230, 255));
        private static Color drawRC = drawR.Color;
        //#Drawing color vars

        static void Game_OnGameLoad(EventArgs args) {
            if (Player.ChampionName != "Kalista") { return; }
            menuload();
            //#Events
            Drawing.OnDraw += Drawing_OnDraw;
            Game.OnUpdate += Game_OnUpdate;
            //#events
        }
        static void Main(string[] args) {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }
        static void menuload() {
            kalimenu = new Menu("Kalimá", "mymenu", true);
            Orbwalker = new Orbwalking.Orbwalker(kalimenu.AddSubMenu(new Menu(Player.ChampionName + ": Orbwalker", "Orbwalker")));
            TargetSelector.AddToMenu(kalimenu.AddSubMenu(new Menu(Player.ChampionName + ": Target Selector", "Target Selector")));
            kalimenu.SubMenu("Drawings").AddItem(new MenuItem("drawAA", "Auto Attack Range", true).SetValue(new Circle(true, Color.FromArgb(0, 230, 255))));
            kalimenu.SubMenu("Drawings").AddItem(new MenuItem("drawQ", "Q Range", true).SetValue(new Circle(false, Color.FromArgb(0, 230, 255))));
            kalimenu.SubMenu("Drawings").AddItem(new MenuItem("drawW", "W Range", true).SetValue(new Circle(false, Color.FromArgb(0, 230, 255))));
            kalimenu.SubMenu("Drawings").AddItem(new MenuItem("drawE", "E Range", true).SetValue(new Circle(false, Color.FromArgb(0, 230, 255))));
            kalimenu.SubMenu("Drawings").AddItem(new MenuItem("drawR", "R Range", true).SetValue(new Circle(false, Color.FromArgb(0, 230, 255))));
            kalimenu.SubMenu("Drawings").AddItem(new MenuItem("drawsoulmatelink", "Draw Link Signal", true).SetValue(true));
            kalimenu.AddToMainMenu();
        }
        static void Game_OnUpdate(EventArgs args) {

            //#store the drawing color values for usage in the drawing event instead of having it update frame by frame on the ondraw
            //creates too much of a slowdown on high fps if used ondraw
            drawAA = kalimenu.Item("drawingAA", true).GetValue<Circle>();
            drawQ = kalimenu.Item("drawingQ", true).GetValue<Circle>();
            drawW = kalimenu.Item("drawingW", true).GetValue<Circle>();
            drawE = kalimenu.Item("drawingE", true).GetValue<Circle>();
            drawR = kalimenu.Item("drawingR", true).GetValue<Circle>();
            draw_soulmate_link();

        }

        static void Drawing_OnDraw(EventArgs args) {
            if (Player.IsDead) { return; }
            /* don't use vars to store colors = because ondraw is heavier than onupdate
               so store the values on the OnUpdate event and just check if they're active here
            */
            var currentposition = Player.Position;

            if (drawAA.Active) { Render.Circle.DrawCircle(currentposition, Orbwalking.GetRealAutoAttackRange(Player), drawAAC); }
            if (drawQ.Active) { Render.Circle.DrawCircle(currentposition, Q.Range, drawAAC); }
            if (drawW.Active) { Render.Circle.DrawCircle(currentposition, W.Range, drawWC); }
            if (drawE.Active) { Render.Circle.DrawCircle(currentposition, E.Range, drawEC); }
            if (drawR.Active) { Render.Circle.DrawCircle(currentposition, R.Range, drawRC); }
        
        }
        static void draw_soulmate_link() {
            if (Player.IsDead) { return; }
            var drawlink = kalimenu.Item("drawsoulmatelink", true).GetValue<Boolean>();
            if (!drawlink || W.Level == 0) { return; }
            if (soulmate == null)
            {
                Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.70f, Color.Red, "Connection Signal: None");
            } else {
                Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.70f, Color.Red, "Connection Signal: " + soulmate);
            }
            if (soulmate == null) {
                soulmate = HeroManager.Allies.Find(a => !a.IsMe && a.Buffs.Any(b => b.Name.Contains("kalistacoopstrikeally")));
            } else if (soulmate.IsDead && drawlink) {
                Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.82f, Color.Red, "Connection Signal with " + soulmate.ChampionName + ": None");
            } else {
                if (drawlink) {
                    var soulrange = Player.Distance(soulmate.Position);
                    if (soulrange > soulmateRange) {
                        Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.82f, Color.Red, "Connection Signal with " + soulmate.ChampionName + ": None");
                    } else if (soulrange > 800) {
                        Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.82f, Color.Gold, "Connection Signal with " + soulmate.ChampionName + ": Low");
                    }
                    else { Drawing.DrawText(Drawing.Width * 0.45f, Drawing.Height * 0.82f, Color.GreenYellow, "Connection Signal with " + soulmate.ChampionName + ": Good"); }
                }
            }
        }

        private int GetRStacks(Obj_AI_Base target)
        {
            foreach (var buff in target.Buffs)
            {
                if (buff.Name == "kalistaexpungemarker")
                    return buff.Count;
            }
            return 0;
        }
    }
}
