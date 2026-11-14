using System.Collections.Generic;
using UnityEngine;

namespace MountainRemover
{
    /// <summary>All on-screen UI, drawn with Unity's immediate-mode GUI so no prefabs or canvases are needed.</summary>
    public partial class GameManager
    {
        class ToastMsg { public string text; public int kind; public float t; }
        readonly List<ToastMsg> toasts = new List<ToastMsg>();
        string bannerSub = "", bannerTitle = "";
        float bannerT = -10;
        Vector2 shopScroll;

        static readonly Color Ink = Data.Hex(0xf3efe6), Muted = Data.Hex(0xb7b0a1), Rust = Data.Hex(0xd0602f), Ok = Data.Hex(0x86c25c);
        static readonly Color PanelCol = new Color(0.118f, 0.125f, 0.137f, 0.88f), Solid = new Color(0.118f, 0.125f, 0.137f, 0.98f);

        GUIStyle sLabel, sSmall, sMoney, sTitle, sBig, sBtn, sBtnGhost, sCaps, sMono, sCenter;
        Texture2D texPanel, texSolid, texHazard, texWhite, texDim;
        float uiScale = 1;

        public void Toast(string msg, int kind = 0)
        {
            toasts.Add(new ToastMsg { text = msg, kind = kind, t = Time.unscaledTime });
            while (toasts.Count > 4) toasts.RemoveAt(0);
        }

        public void Banner(string sub, string title) { bannerSub = sub; bannerTitle = title; bannerT = Time.unscaledTime; }

        public static string Money(double v)
        {
            double a = System.Math.Abs(v);
            if (a >= 1e9) return "$" + (v / 1e9).ToString("0.##") + "B";
            if (a >= 1e6) return "$" + (v / 1e6).ToString("0.##") + "M";
            if (a >= 1e5) return "$" + (v / 1e3).ToString("0.#") + "K";
            return "$" + v.ToString("N0");
        }

        void OpenShop() { shopOpen = true; }
        void CloseShop() { shopOpen = false; }

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c); t.Apply();
            return t;
        }

        void InitStyles()
        {
            if (sLabel != null) return;
            texPanel = Tex(PanelCol); texSolid = Tex(Solid); texHazard = Tex(Hazard); texWhite = Tex(Color.white);
            texDim = Tex(new Color(0.07f, 0.08f, 0.09f, 0.62f));
            sLabel = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, richText = true };
            sLabel.normal.textColor = Ink;
            sSmall = new GUIStyle(sLabel) { fontSize = 12 };
            sSmall.normal.textColor = Muted;
            sCaps = new GUIStyle(sSmall) { fontSize = 11, fontStyle = FontStyle.Bold, wordWrap = false };
            sMono = new GUIStyle(sLabel) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, wordWrap = false };
            sMoney = new GUIStyle(sLabel) { fontSize = 28, fontStyle = FontStyle.Bold, wordWrap = false };
            sMoney.normal.textColor = Hazard;
            sTitle = new GUIStyle(sLabel) { fontSize = 46, fontStyle = FontStyle.Bold, wordWrap = true };
            sTitle.normal.textColor = Hazard;
            sBig = new GUIStyle(sLabel) { fontSize = 54, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            sCenter = new GUIStyle(sLabel) { alignment = TextAnchor.MiddleCenter };
            sBtn = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold, padding = new RectOffset(14, 14, 8, 8) };
            sBtn.normal.background = texHazard; sBtn.hover.background = Tex(Data.Hex(0xffc928)); sBtn.active.background = Tex(Data.Hex(0xd99f00));
            sBtn.normal.textColor = sBtn.hover.textColor = sBtn.active.textColor = Data.Hex(0x1e2023);
            sBtnGhost = new GUIStyle(sBtn);
            sBtnGhost.normal.background = Tex(new Color(1, 1, 1, 0.08f)); sBtnGhost.hover.background = Tex(new Color(1, 1, 1, 0.16f)); sBtnGhost.active.background = Tex(new Color(1, 1, 1, 0.22f));
            sBtnGhost.normal.textColor = sBtnGhost.hover.textColor = sBtnGhost.active.textColor = Ink;
        }

        void Panel(Rect r, bool solid = false) => GUI.DrawTexture(r, solid ? texSolid : texPanel);
        void Fill(Rect r, Color c) { var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, texWhite); GUI.color = old; }

        void Stripes(Rect r)
        {
            Fill(r, Hazard);
            float w = 12;
            for (float x = r.x + w; x < r.xMax; x += w * 2) Fill(new Rect(x, r.y, Mathf.Min(w, r.xMax - x), r.height), Data.Hex(0x1e2023));
        }

        void OnGUI()
        {
            if (world == null || world.vox == null) return;
            InitStyles();
            uiScale = Mathf.Clamp(Screen.height / 820f, 0.75f, 2.2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
            float sw = Screen.width / uiScale, sh = Screen.height / uiScale;

            if (started) DrawHUD(sw, sh);
            DrawBanner(sw, sh);
            DrawToasts(sw, sh);

            if (menuOpen) DrawMenu(sw, sh);
            else if (shopOpen) DrawShop(sw, sh);
            else if (clearedOpen) DrawCleared(sw, sh);
            else if (winOpen) DrawWin(sw, sh);
        }

        // ------------------------------------------------------------------ HUD
        void DrawHUD(float sw, float sh)
        {
            // Top-left: money and mountain progress
            var tl = new Rect(16, 14, 270, 116);
            Panel(tl);
            GUI.Label(new Rect(30, 20, 250, 34), Money(S.money), sMoney);
            float pct = world.total > 0 ? (1 - (float)world.remaining / world.total) * 100 : 100;
            GUI.Label(new Rect(30, 56, 150, 22), "<b>" + world.M.name + "</b>", sLabel);
            GUI.Label(new Rect(140, 57, 132, 22), pct.ToString("0.0") + "% removed", new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight });
            Fill(new Rect(30, 82, 242, 8), new Color(1, 1, 1, 0.12f));
            Stripes(new Rect(30, 82, 242 * Mathf.Clamp01(pct / 100), 8));
            GUI.Label(new Rect(30, 96, 250, 20), "SUMMIT  <color=#f3efe6><b>" + (summitH * world.metresPerBlock).ToString("N0") + " m</b></color>", sCaps);

            // Top-right: operation stats
            float rw = 250, rx = sw - rw - 16;
            Panel(new Rect(rx, 14, rw, 134));
            string[] keys = { "BACKPACK", "SILO", "DRILL RIGS", "AUTO-SELL", "SALE BONUS" };
            string[] vals =
            {
                (S.bag.n >= BagCap ? "<color=#d0602f>" : "") + S.bag.n.ToString("0") + "/" + BagCap.ToString("N0") + (S.bag.n >= BagCap ? "</color>" : ""),
                (S.silo.n >= SiloCap ? "<color=#d0602f>" : "") + S.silo.n.ToString("N0") + "/" + SiloCap.ToString("N0") + (S.silo.n >= SiloCap ? "</color>" : ""),
                S.rigs > 0 ? S.rigs + " · " + (Data.RigRate(S.rigLv) * S.rigs).ToString("0.0") + "/s" : "none",
                S.belt > 0 ? Data.BeltRate(S.belt).ToString("0") + "/s" : "off",
                "×" + CrMult.ToString("0.0") + " rock · ×" + (CrMult * SmMult).ToString("0.0") + " ore",
            };
            for (int i = 0; i < keys.Length; i++)
            {
                GUI.Label(new Rect(rx + 14, 22 + i * 24, 100, 22), keys[i], sCaps);
                GUI.Label(new Rect(rx + 90, 19 + i * 24, rw - 104, 22), vals[i], sMono);
            }

            if (Paused) return;

            // Crosshair
            float cx = sw / 2, cy = sh / 2;
            Fill(new Rect(cx - 1, cy - 8, 2, 16), new Color(1, 1, 1, 0.9f));
            Fill(new Rect(cx - 8, cy - 1, 16, 2), new Color(1, 1, 1, 0.9f));

            // Target info
            if (hasTarget && tool == 0)
            {
                float hp = HardOf(targetMat);
                float d = world.Idx(target.x, target.y, target.z) == dmgKey ? dmg : 0;
                GUI.Label(new Rect(cx - 100, cy + 16, 200, 22), "<b>" + Data.Mats[targetMat].name + "</b>", sCenter);
                GUI.Label(new Rect(cx - 100, cy + 34, 200, 18), Money(RawValue(targetMat)) + " raw", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter });
                Fill(new Rect(cx - 55, cy + 56, 110, 4), new Color(0, 0, 0, 0.45f));
                Fill(new Rect(cx - 55, cy + 56, 110 * (1 - d / hp), 4), Hazard);
            }

            // Prompt
            string prompt = null;
            if (NearPost) prompt = S.bag.n >= 1 ? "<color=#f2b705><b>[E]</b></color>  Sell backpack · " + Money(SaleValue(S.bag)) : "Trading post · your backpack is empty";
            else if (NearSilo) prompt = S.silo.n >= 1 ? "<color=#f2b705><b>[E]</b></color>  Ship silo contents · " + Money(SaleValue(S.silo)) : "Silo · drills and dynamite fill this";
            if (prompt != null)
            {
                var r = new Rect(cx - 200, sh - 118, 400, 34);
                Panel(r);
                GUI.Label(r, prompt, sCenter);
            }

            // Hotbar
            float hw = 170, gap = 8, hx = cx - (hw * 2 + 90 + gap * 2) / 2, hy = sh - 70;
            HotSlot(new Rect(hx, hy, hw, 52), "1  TOOL", Data.Picks[S.pick].name, tool == 0);
            HotSlot(new Rect(hx + hw + gap, hy, hw, 52), "2  EXPLOSIVES", "Dynamite ×" + S.dyn, tool == 1);
            HotSlot(new Rect(hx + hw * 2 + gap * 2, hy, 90, 52), "B", "Shop", false);
        }

        void HotSlot(Rect r, string key, string name, bool on)
        {
            Panel(r);
            if (on)
            {
                Fill(new Rect(r.x, r.y, r.width, 2), Hazard); Fill(new Rect(r.x, r.yMax - 2, r.width, 2), Hazard);
                Fill(new Rect(r.x, r.y, 2, r.height), Hazard); Fill(new Rect(r.xMax - 2, r.y, 2, r.height), Hazard);
            }
            GUI.Label(new Rect(r.x + 12, r.y + 6, r.width - 16, 18), key, sCaps);
            GUI.Label(new Rect(r.x + 12, r.y + 24, r.width - 16, 22), "<b>" + name + "</b>", sLabel);
        }

        void DrawToasts(float sw, float sh)
        {
            float y = sh - 24;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                float age = Time.unscaledTime - t.t;
                if (age > 3.2f) { toasts.RemoveAt(i); continue; }
                var size = sLabel.CalcSize(new GUIContent(t.text));
                float w = Mathf.Min(size.x + 26, 460);
                y -= 34;
                var r = new Rect(sw - w - 16, y, w, 30);
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((3.2f - age) * 3));
                Panel(r);
                Fill(new Rect(r.x, r.y, 3, r.height), t.kind > 0 ? Ok : t.kind < 0 ? Rust : Muted);
                GUI.Label(new Rect(r.x + 12, r.y + 5, r.width - 16, 22), t.text, new GUIStyle(sLabel) { fontSize = 13, fontStyle = FontStyle.Bold, wordWrap = false });
                GUI.color = old;
            }
        }

        void DrawBanner(float sw, float sh)
        {
            float age = Time.unscaledTime - bannerT;
            if (age > 4) return;
            float a = Mathf.Clamp01(age * 2) * Mathf.Clamp01((4 - age) * 1.5f);
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, a);
            GUI.Label(new Rect(0, sh * 0.22f, sw, 22), "<b>" + bannerSub + "</b>", new GUIStyle(sCenter) { fontSize = 14 });
            var shadow = new GUIStyle(sBig); shadow.normal.textColor = new Color(0.12f, 0.13f, 0.14f, 0.8f);
            GUI.Label(new Rect(3, sh * 0.22f + 27, sw, 70), bannerTitle.ToUpper(), shadow);
            GUI.Label(new Rect(0, sh * 0.22f + 24, sw, 70), bannerTitle.ToUpper(), sBig);
            GUI.color = old;
        }

        // ------------------------------------------------------------------ overlays
        Rect Card(float sw, float sh, float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, sw, sh), texDim);
            w = Mathf.Min(w, sw - 32); h = Mathf.Min(h, sh - 32);
            var r = new Rect((sw - w) / 2, (sh - h) / 2, w, h);
            Panel(r, true);
            return r;
        }

        void DrawMenu(float sw, float sh)
        {
            var r = Card(sw, sh, 560, 470);
            float x = r.x + 28, w = r.width - 56, y = r.y + 26;
            Stripes(new Rect(x, y, w, 10)); y += 24;
            GUI.Label(new Rect(x, y, w, 110), "MOUNTAIN\nREMOVER", sTitle); y += 112;
            GUI.Label(new Rect(x, y, w, 64), "Start with a rusty pickaxe and a small hill. Sell what you dig at the trading post, buy drill rigs and machinery, and work your way up to removing Mount Everest.", new GUIStyle(sLabel) { normal = { textColor = Muted } }); y += 70;
            GUI.Label(new Rect(x, y, w, 18), "CONTROLS", sCaps); y += 22;
            string[] c =
            {
                "<b>WASD</b>  Move", "<b>Mouse</b>  Look", "<b>Click</b>  Mine / throw",
                "<b>Space</b>  Jump", "<b>Shift</b>  Sprint", "<b>E</b>  Sell at post or silo",
                "<b>1 / 2</b>  Pickaxe / dynamite", "<b>B</b>  Shop", "<b>Esc</b>  Pause · <b>M</b> Mute",
            };
            for (int i = 0; i < c.Length; i++)
                GUI.Label(new Rect(x + (i % 3) * (w / 3), y + (i / 3) * 22, w / 3, 22), c[i], new GUIStyle(sLabel) { fontSize = 13 });
            y += 76;

            if (confirmNew)
            {
                GUI.Label(new Rect(x, y, w, 22), "This erases your saved company.", sLabel);
                if (GUI.Button(new Rect(x, y + 26, 190, 40), "Erase and restart", sBtn)) { NewGame(); StartPlaying(); }
                if (GUI.Button(new Rect(x + 200, y + 26, 110, 40), "Cancel", sBtnGhost)) confirmNew = false;
                return;
            }
            string label = started ? "Resume" : (hasSave ? "Continue" : "Start mining");
            if (GUI.Button(new Rect(x, y + 10, 170, 44), label, sBtn)) StartPlaying();
            if (hasSave || started)
                if (GUI.Button(new Rect(x + 182, y + 10, 140, 44), "New game", sBtnGhost)) confirmNew = true;
        }

        void StartPlaying()
        {
            menuOpen = false;
            if (!started)
            {
                started = true;
                Banner($"STAGE {S.mtn + 1} OF {Data.Mountains.Length}", world.M.name);
            }
            nextSwing = tGame + 0.25f; // don't swing from the click that closed the menu
        }

        struct ShopItem { public string cat, name, now, next; public int cost; public System.Action buy; }

        List<ShopItem> ShopItems()
        {
            var pk = Data.Picks[S.pick];
            var bg = Data.Packs[S.pack];
            bool morePick = S.pick + 1 < Data.Picks.Length, morePack = S.pack + 1 < Data.Packs.Length;
            return new List<ShopItem>
            {
                new ShopItem { cat = "TOOLS", name = "Pickaxe", now = $"{pk.name} · {pk.damage:0} damage per hit",
                    next = morePick ? $"{Data.Picks[S.pick + 1].name} · {Data.Picks[S.pick + 1].damage:0} damage, faster swing" : null,
                    cost = morePick ? Data.Picks[S.pick + 1].cost : 0, buy = () => S.pick++ },
                new ShopItem { cat = "TOOLS", name = "Hauling", now = $"{bg.name} · carries {bg.cap:N0} blocks",
                    next = morePack ? $"{Data.Packs[S.pack + 1].name} · carries {Data.Packs[S.pack + 1].cap:N0}" : null,
                    cost = morePack ? Data.Packs[S.pack + 1].cost : 0, buy = () => S.pack++ },
                new ShopItem { cat = "EXPLOSIVES", name = "Dynamite ×5", now = $"{S.dyn} sticks on hand",
                    next = "Throw at rock. Debris goes to the silo.", cost = Data.DynamitePackCost, buy = () => S.dyn += 5 },
                new ShopItem { cat = "EXPLOSIVES", name = "Bigger charges", now = $"Blast radius {Data.BlastRadius(S.blastLv):0.0} m",
                    next = S.blastLv < Data.MaxBlast ? $"Radius {Data.BlastRadius(S.blastLv + 1):0.0} m" : null,
                    cost = Data.BlastCost(S.blastLv), buy = () => S.blastLv++ },
                new ShopItem { cat = "MACHINES", name = "Drill rig", now = $"{S.rigs} of {Data.MaxRigs} rigs working",
                    next = S.rigs < Data.MaxRigs ? "Drives to the mountain and mines top-down into the silo" : null,
                    cost = Data.RigCost(S.rigs), buy = () => { S.rigs++; SyncRigs(false); } },
                new ShopItem { cat = "MACHINES", name = "Drill motors", now = $"{Data.RigRate(S.rigLv):0.00} blocks/s per rig",
                    next = S.rigLv < Data.MaxRigLv ? $"{Data.RigRate(S.rigLv + 1):0.00} blocks/s per rig" : null,
                    cost = Data.RigUpCost(S.rigLv), buy = () => S.rigLv++ },
                new ShopItem { cat = "MACHINES", name = "Silo", now = $"Holds {Data.SiloCap(S.siloLv):N0} blocks",
                    next = S.siloLv < Data.MaxSiloLv ? $"Holds {Data.SiloCap(S.siloLv + 1):N0}" : null,
                    cost = Data.SiloCost(S.siloLv), buy = () => S.siloLv++ },
                new ShopItem { cat = "MACHINES", name = "Conveyor to post",
                    now = S.belt > 0 ? $"Auto-sells {Data.BeltRate(S.belt):0} blocks/s from the silo" : "Not built. You ship the silo by hand.",
                    next = S.belt < Data.MaxBelt ? $"Auto-sell {Data.BeltRate(S.belt + 1):0} blocks/s" : null,
                    cost = Data.BeltCost(S.belt), buy = () => { S.belt++; BuildFacilities(); } },
                new ShopItem { cat = "PROCESSING", name = "Crusher", now = $"All sales ×{CrMult:0.0}",
                    next = S.crusher < Data.MaxCrusher ? $"All sales ×{Data.CrusherMult(S.crusher + 1):0.0}" : null,
                    cost = Data.CrusherCost(S.crusher), buy = () => { S.crusher++; BuildFacilities(); } },
                new ShopItem { cat = "PROCESSING", name = "Smelter", now = $"Ore sales ×{SmMult:0.0} on top of the crusher",
                    next = S.smelter < Data.MaxSmelter ? $"Ore ×{Data.SmelterMult(S.smelter + 1):0.0}" : null,
                    cost = Data.SmelterCost(S.smelter), buy = () => { S.smelter++; BuildFacilities(); } },
            };
        }

        void DrawShop(float sw, float sh)
        {
            var r = Card(sw, sh, 920, 720);
            float x = r.x + 24, w = r.width - 48, y = r.y + 18;
            GUI.Label(new Rect(x, y, 300, 40), "<b>SUPPLY DEPOT</b>", new GUIStyle(sLabel) { fontSize = 26 });
            GUI.Label(new Rect(x + w / 2 - 100, y + 2, 200, 36), Money(S.money), new GUIStyle(sMoney) { alignment = TextAnchor.MiddleCenter, fontSize = 24 });
            if (GUI.Button(new Rect(x + w - 120, y, 120, 38), "Close (B)", sBtnGhost)) CloseShop();
            y += 52;

            var items = ShopItems();
            int cols = Mathf.Max(1, Mathf.FloorToInt((w - 16) / 280));
            float cw = (w - 16 - (cols - 1) * 10) / cols, chh = 116;
            // measure content height
            float contentH = 0; string lastCat = null; int col = 0;
            foreach (var it in items)
            {
                if (it.cat != lastCat) { if (lastCat != null && col != 0) contentH += chh + 10; contentH += 26; lastCat = it.cat; col = 0; }
                col++; if (col == cols) { contentH += chh + 10; col = 0; }
            }
            if (col != 0) contentH += chh + 10;
            contentH += 26 + 74 + 60;

            shopScroll = GUI.BeginScrollView(new Rect(x, y, w, r.yMax - y - 16), shopScroll, new Rect(0, 0, w - 16, contentH));
            float yy = 0; lastCat = null; col = 0;
            foreach (var it in items)
            {
                if (it.cat != lastCat)
                {
                    if (lastCat != null && col != 0) yy += chh + 10;
                    GUI.Label(new Rect(0, yy, 300, 20), it.cat, sCaps); yy += 26;
                    lastCat = it.cat; col = 0;
                }
                var cr = new Rect(col * (cw + 10), yy, cw, chh);
                Fill(cr, new Color(1, 1, 1, 0.04f));
                GUI.Label(new Rect(cr.x + 12, cr.y + 8, cr.width - 24, 22), "<b>" + it.name + "</b>", sLabel);
                GUI.Label(new Rect(cr.x + 12, cr.y + 30, cr.width - 24, 18), it.now, sSmall);
                if (it.next == null)
                    GUI.Label(new Rect(cr.x + 12, cr.y + 52, cr.width - 24, 20), "Fully upgraded", new GUIStyle(sLabel) { fontSize = 13 });
                else
                {
                    GUI.Label(new Rect(cr.x + 12, cr.y + 48, cr.width - 24, 34), it.next, new GUIStyle(sLabel) { fontSize = 13 });
                    bool afford = S.money >= it.cost;
                    GUI.enabled = afford;
                    if (GUI.Button(new Rect(cr.x + 12, cr.yMax - 36, 140, 28), "Buy · " + Money(it.cost), new GUIStyle(sBtn) { fontSize = 13, padding = new RectOffset(8, 8, 4, 4) }))
                    {
                        S.money -= it.cost;
                        it.buy();
                        fx.Play("buy");
                        Toast("Bought " + it.name.ToLower(), 1);
                        Save();
                    }
                    GUI.enabled = true;
                }
                col++;
                if (col == cols) { yy += chh + 10; col = 0; }
            }
            if (col != 0) yy += chh + 10;

            GUI.Label(new Rect(0, yy, 300, 20), "MOUNTAINS", sCaps); yy += 26;
            int n = Data.Mountains.Length;
            float mw = (w - 16 - (n - 1) * 8) / n;
            for (int i = 0; i < n; i++)
            {
                var m = Data.Mountains[i];
                var mr = new Rect(i * (mw + 8), yy, mw, 66);
                Fill(mr, i == S.mtn ? new Color(0.95f, 0.72f, 0.02f, 0.18f) : new Color(1, 1, 1, 0.04f));
                string status = i < S.mtn ? "Removed" : i == S.mtn ? ((1 - (float)world.remaining / Mathf.Max(1, world.total)) * 100).ToString("0.0") + "% removed" : "Locked";
                GUI.Label(new Rect(mr.x + 8, mr.y + 4, mr.width - 12, 20), "<b>" + m.name + "</b>", new GUIStyle(sLabel) { fontSize = 13, wordWrap = false });
                GUI.Label(new Rect(mr.x + 8, mr.y + 24, mr.width - 12, 18), m.metres.ToString("N0") + " m · ore ×" + m.mult, sSmall);
                GUI.Label(new Rect(mr.x + 8, mr.y + 42, mr.width - 12, 18), status, sSmall);
            }
            yy += 74;
            if (S.cleared && S.mtn < n - 1)
            {
                if (GUI.Button(new Rect(0, yy, 300, 40), "Travel to " + Data.Mountains[S.mtn + 1].name, sBtn)) { CloseShop(); Travel(); }
            }
            GUI.EndScrollView();
        }

        void DrawCleared(float sw, float sh)
        {
            var r = Card(sw, sh, 560, 320);
            float x = r.x + 28, w = r.width - 56, y = r.y + 26;
            var next = Data.Mountains[Mathf.Min(S.mtn + 1, Data.Mountains.Length - 1)];
            Stripes(new Rect(x, y, w, 10)); y += 24;
            GUI.Label(new Rect(x, y, w, 60), world.M.name.ToUpper() + " REMOVED", new GUIStyle(sTitle) { fontSize = 32 }); y += 64;
            GUI.Label(new Rect(x, y, w, 90), $"Your crew swept up the last crumbs for {Money(clearedBonus)}. Next up: {next.name}, {next.metres:N0} m. {next.desc} Your machines and money come with you.", new GUIStyle(sLabel) { normal = { textColor = Muted } });
            y += 100;
            if (GUI.Button(new Rect(x, y, 260, 44), "Travel to " + next.name, sBtn)) { clearedOpen = false; Travel(); nextSwing = tGame + 0.25f; }
            if (GUI.Button(new Rect(x + 272, y, 180, 44), "Keep mining here", sBtnGhost)) { clearedOpen = false; nextSwing = tGame + 0.25f; }
        }

        void DrawWin(float sw, float sh)
        {
            var r = Card(sw, sh, 600, 400);
            float x = r.x + 28, w = r.width - 56, y = r.y + 26;
            Stripes(new Rect(x, y, w, 10)); y += 24;
            GUI.Label(new Rect(x, y, w, 56), "EVEREST IS GONE", new GUIStyle(sTitle) { fontSize = 40 }); y += 60;
            GUI.Label(new Rect(x, y, w, 60), "8,849 metres of rock, mined, crushed and sold. The highest point on Earth is now a flat patch of dirt with your trucks parked on it.", new GUIStyle(sLabel) { normal = { textColor = Muted } });
            y += 70;
            int hh = (int)(S.st.time / 3600), mm = (int)(S.st.time / 60) % 60;
            string[,] stats =
            {
                { "BLOCKS BY HAND", S.st.hand.ToString("N0") }, { "BLOCKS BY DRILL", S.st.rig.ToString("N0") }, { "BLOCKS BY DYNAMITE", S.st.blast.ToString("N0") },
                { "TOTAL EARNED", Money(S.st.earned) }, { "TIME", hh + "h " + mm + "m" }, { "", "" },
            };
            for (int i = 0; i < 5; i++)
            {
                float bx = x + (i % 3) * (w / 3), by = y + (i / 3) * 56;
                GUI.Label(new Rect(bx, by, w / 3 - 8, 18), stats[i, 0], sCaps);
                GUI.Label(new Rect(bx, by + 18, w / 3 - 8, 28), "<b>" + stats[i, 1] + "</b>", new GUIStyle(sLabel) { fontSize = 18 });
            }
            y += 124;
            if (GUI.Button(new Rect(x, y, 170, 44), "Look around", sBtn)) { winOpen = false; nextSwing = tGame + 0.25f; }
        }
    }
}
