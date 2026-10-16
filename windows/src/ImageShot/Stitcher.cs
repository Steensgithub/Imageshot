using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ImageShot
{
    // ------------------------------------------------------------------
    // Sammensyning: finder, hvor meget indholdet har flyttet sig mellem to
    // billeder af samme område, og bygger et højt billede af det hele.
    // Indholdskoordinat = skærmrække i området + S (hvor langt der er scrollet).
    // ------------------------------------------------------------------
    class Stitcher : IDisposable
    {
        public const int MaxHeight = 30000;
        public readonly int Width, Height;
        public int S;                 // samlet forskydning i pixels (positiv = scrollet ned)
        public int MovTop, MovBot;    // den del af området, der flyttede sig sidst
        public bool HasMoved;
        public bool Lost;             // kunne ikke følge med (fx video i området)
        Bitmap canvas;
        int originY;                  // indholdskoordinat for lærredets række 0
        int minC, maxC;               // skrevne indholdsrækker [minC, maxC)
        readonly int strips;          // smalle lodrette striber (ca. 32 px)
        readonly int[] sx0, sx1;
        readonly int groups;          // grove grupper af striber, der stemmer om forskydningen
        readonly bool[][] groupUse;
        ulong[][] prev;               // [stribe][række]
        int misses;
        int lastD;                    // seneste forskydning (scroll sker i jævne skridt)

        public Stitcher(Bitmap first)
        {
            Width = first.Width;
            Height = first.Height;
            // Området deles i smalle lodrette striber. Faste dele (sidepanel, scrollbjælke) giver
            // striber, der ikke flytter sig: de stemmer ikke med og skrives kun én gang.
            strips = Math.Max(1, Math.Min(64, Width / 32));
            sx0 = new int[strips]; sx1 = new int[strips];
            for (int i = 0; i < strips; i++) { sx0[i] = Width * i / strips; sx1[i] = Width * (i + 1) / strips; }
            groups = Math.Min(6, strips);
            groupUse = new bool[groups][];
            for (int g = 0; g < groups; g++)
            {
                groupUse[g] = new bool[strips];
                for (int i = g * strips / groups; i < (g + 1) * strips / groups; i++) groupUse[g][i] = true;
            }
            canvas = new Bitmap(Width, Height, PixelFormat.Format32bppRgb);
            originY = 0;
            Write(first, 0, Height, 0, null);
            prev = Hashes(first);
            MovTop = 0; MovBot = Height;
        }

        ulong[][] Hashes(Bitmap b)
        {
            ulong[][] h = new ulong[strips][];
            for (int s = 0; s < strips; s++) h[s] = new ulong[b.Height];
            BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
            try
            {
                int[] row = new int[b.Width];
                for (int y = 0; y < b.Height; y++)
                {
                    Marshal.Copy(d.Scan0 + y * d.Stride, row, 0, b.Width);
                    for (int s = 0; s < strips; s++)
                    {
                        ulong v = 1469598103934665603UL;
                        for (int x = sx0[s]; x < sx1[s]; x++)
                        {
                            v ^= (uint)(row[x] & 0xFFFFFF);
                            v *= 1099511628211UL;
                        }
                        h[s][y] = v;
                    }
                }
            }
            finally { b.UnlockBits(d); }
            return h;
        }

        // Find d, så cur[i] == prev[i + d] i rækkerne [t, b). Ved tvivl foretrækkes retningen dir.
        static bool FindOffset(ulong[] prev, ulong[] cur, int t, int b, int dir, int expect, out int best)
        {
            best = 0;
            int n = cur.Length;
            int len = b - t;
            if (len < 20) return false;
            int maxD = Math.Min(n - 30, 900);
            double[] score = new double[2 * maxD + 1];
            bool[] valid = new bool[2 * maxD + 1];
            double top = -1;
            for (int d = -maxD; d <= maxD; d++)
            {
                int i0 = Math.Max(t, -d), i1 = Math.Min(b, n - d);
                if (i1 - i0 < 20) continue;
                int inf = 0, m = 0;
                for (int i = i0; i < i1; i++)
                {
                    if (i > 0 && cur[i] == cur[i - 1]) continue; // ensfarvede rækker siger intet
                    inf++;
                    if (cur[i] == prev[i + d]) m++;
                }
                if (inf < 6 || m < 4) continue;
                double sc = (double)m / inf;
                score[d + maxD] = sc;
                valid[d + maxD] = true;
                if (sc > top) top = sc;
            }
            if (top < 0.7) return false;
            // Blandt næsten lige gode bud (fx ens tabelrækker): vælg i scrollretningen, mindste afstand.
            bool found = false;
            for (int d = -maxD; d <= maxD; d++)
            {
                if (!valid[d + maxD] || score[d + maxD] < top - 0.03) continue;
                if (!found) { best = d; found = true; continue; }
                if (Prefer(d, best, dir, expect)) best = d;
            }
            return found;
        }

        // Er a et bedre bud end b? Først scrollretningen, så tættest på forventet skridt (eller mindst).
        static bool Prefer(int a, int b, int dir, int expect)
        {
            bool aDir = dir != 0 && Math.Sign(a) == dir, bDir = dir != 0 && Math.Sign(b) == dir;
            if (aDir != bDir) return aDir;
            int ea = expect != 0 && Math.Sign(expect) == Math.Sign(a) ? Math.Abs(a - expect) : Math.Abs(a);
            int eb = expect != 0 && Math.Sign(expect) == Math.Sign(b) ? Math.Abs(b - expect) : Math.Abs(b);
            return ea < eb;
        }

        const byte NEW = 0, MOVE = 1, AMBIG = 2, STATIC = 3, CHANGED = 4;

        // Den sammenhængende del, der flytter sig med d (uden faste top/bundlinjer).
        // Hver række afgøres ved flertal blandt de striber, der følger forskydningen, så et lille
        // område med anden bevægelse (scrollbjælkens greb, en blinkende markør) ikke knækker rækken.
        static bool Region(ulong[][] prev, ulong[][] cur, bool[] use, int d, out int t, out int b)
        {
            int n = cur[0].Length;
            byte[] st = new byte[n];
            for (int i = 0; i < n; i++)
            {
                int j = i + d;
                if (j < 0 || j >= n) { st[i] = NEW; continue; }
                int good = 0, moves = 0, bad = 0, statics = 0;
                for (int f = 0; f < cur.Length; f++)
                {
                    if (!use[f]) continue;
                    bool shifted = cur[f][i] == prev[f][j], same = cur[f][i] == prev[f][i];
                    if (shifted) { good++; if (!same) moves++; }
                    else { bad++; if (same) statics++; }
                }
                if (good + bad == 0) { st[i] = AMBIG; continue; }
                if (good >= 0.6 * (good + bad)) st[i] = moves > 0 ? MOVE : AMBIG;
                else st[i] = statics * 2 >= bad ? STATIC : CHANGED;
            }
            // Største blok af MOVE/AMBIG (små huller med ændringer tilladt).
            int bestStart = -1, bestEnd = -1, bestMoves = 0;
            int start = -1, mv = 0, gap = 0, lastGood = -1;
            for (int i = 0; i <= n; i++)
            {
                byte sState = i < n ? st[i] : STATIC;
                bool ok = sState == MOVE || sState == AMBIG;
                if (ok)
                {
                    if (start < 0) { start = i; mv = 0; }
                    if (sState == MOVE) mv++;
                    gap = 0;
                    lastGood = i;
                }
                else if (start >= 0)
                {
                    if (sState == CHANGED && gap < 3 && i < n) { gap++; continue; }
                    if (mv > bestMoves) { bestMoves = mv; bestStart = start; bestEnd = lastGood + 1; }
                    start = -1; gap = 0;
                }
            }
            t = 0; b = 0;
            if (bestMoves < 5) return false;
            t = bestStart; b = bestEnd;
            if (d > 0)
            {
                // Rækker øverst, der er på vej ud, er skrevet før – start ved første sikre række.
                while (t < b && st[t] != MOVE) t++;
                if (b >= n - d) b = n;          // nyt indhold kommer ind nedefra
            }
            else
            {
                while (b > t && st[b - 1] != MOVE) b--;
                if (t <= -d) t = 0;             // nyt indhold kommer ind oppefra
            }
            return b - t >= 5;
        }

        static ulong[] Combine(ulong[][] h, bool[] use)
        {
            int n = h[0].Length;
            ulong[] r = new ulong[n];
            for (int i = 0; i < n; i++)
            {
                ulong v = 14695981039346656037UL;
                for (int s = 0; s < h.Length; s++)
                    if (use[s]) { v ^= h[s][i]; v *= 1099511628211UL; }
                r[i] = v;
            }
            return r;
        }

        // Tilføj et nyt billede af området. dir = forventet retning (1 ned, -1 op, 0 ukendt).
        // Returnerer true, hvis indholdet har flyttet sig.
        public bool Add(Bitmap cur, int dir)
        {
            ulong[][] h = Hashes(cur);
            int n = Height;
            bool any = false;
            bool[] still = new bool[strips];       // stribe helt uændret på stedet
            for (int f = 0; f < strips; f++)
            {
                bool same = true;
                for (int i = 0; i < n; i++) if (h[f][i] != prev[f][i]) { same = false; break; }
                still[f] = same;
                if (!same) any = true;
            }
            if (!any) return false;

            // Hver grov gruppe stemmer om forskydningen.
            Dictionary<int, int> votes = new Dictionary<int, int>();
            bool failed = false;
            for (int g = 0; g < groups; g++)
            {
                ulong[] gp = Combine(prev, groupUse[g]), gc = Combine(h, groupUse[g]);
                int t = 0;
                while (t < n && gc[t] == gp[t]) t++;
                if (t == n) continue; // gruppen er uændret
                int b = n;
                while (b > t && gc[b - 1] == gp[b - 1]) b--;
                int d;
                if (FindOffset(gp, gc, t, b, dir, lastD, out d)) { if (d != 0) { int v; votes.TryGetValue(d, out v); votes[d] = v + 1; } }
                else failed = true;
            }
            if (votes.Count == 0)
            {
                if (failed)
                {
                    // Kunne ikke genkendes. Behold udgangspunktet og prøv med næste billede.
                    if (++misses > 25) Lost = true;
                    return false;
                }
                prev = h; // ændring på stedet (fx blinkende markør), ikke scroll
                return false;
            }
            int dBest = 0, cnt = -1;
            foreach (KeyValuePair<int, int> kv in votes)
            {
                bool better = kv.Value > cnt || (kv.Value == cnt && Prefer(kv.Key, dBest, dir, lastD));
                if (better) { dBest = kv.Key; cnt = kv.Value; }
            }

            // Striber, der tydeligt følger forskydningen, bestemmer hvilke rækker der flytter sig.
            bool[] agree = new bool[strips];
            bool anyAgree = false;
            for (int f = 0; f < strips; f++)
            {
                if (still[f]) continue;
                int i0 = Math.Max(0, -dBest), i1 = Math.Min(n, n - dBest), inf = 0, m = 0;
                for (int i = i0; i < i1; i++)
                {
                    if (i > 0 && h[f][i] == h[f][i - 1]) continue;
                    inf++;
                    if (h[f][i] == prev[f][i + dBest]) m++;
                }
                if (inf >= 6 && m >= 0.7 * inf) { agree[f] = true; anyAgree = true; }
            }
            int rt, rb;
            if (!anyAgree || !Region(prev, h, agree, dBest, out rt, out rb))
            {
                if (++misses > 25) Lost = true;
                return false;
            }
            misses = 0;
            Lost = false;
            prev = h;
            bool[] write = new bool[strips];
            for (int f = 0; f < strips; f++) write[f] = !still[f];
            if (!HasMoved)
            {
                // Første billede indeholdt også faste bund-/toplinjer. Fjern dem i striber,
                // der ikke skrives igen, så de ikke står midt i det færdige billede.
                if (dBest > 0 && rb < n) Blank(rb, n, write);
                if (dBest < 0 && rt > 0) Blank(0, rt, write);
            }
            S += dBest;
            lastD = dBest;
            MovTop = rt; MovBot = rb;
            HasMoved = true;
            // Alle striber, der ikke står helt stille, skrives (så indhold ved et sidepanel ikke mistes).
            Write(cur, rt, rb, rt + S, write);
            return true;
        }

        // Hvid i indholdsrækkerne [c0, c1) for striber, der ikke skrives (skip[f] = true springes over).
        void Blank(int c0, int c1, bool[] skip)
        {
            if (c0 < originY) c0 = originY;
            if (c1 > originY + canvas.Height) c1 = originY + canvas.Height;
            if (c1 <= c0) return;
            using (Graphics g = Graphics.FromImage(canvas))
            using (SolidBrush w = new SolidBrush(Color.White))
                for (int f = 0; f < strips; f++)
                    if (!skip[f]) g.FillRectangle(w, sx0[f], c0 - originY, sx1[f] - sx0[f], c1 - c0);
        }

        void Write(Bitmap src, int y0, int y1, int contentY, bool[] cols)
        {
            int h = y1 - y0;
            if (h <= 0) return;
            int c0 = contentY, c1 = contentY + h;
            if (!Ensure(ref c0, ref c1)) return;
            int skip = c0 - contentY;
            h = c1 - c0;
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                for (int s = 0; s < strips; s++)
                {
                    if (cols != null && !cols[s]) continue; // faste striber (sidepanel o.l.) skrives kun én gang
                    int x0 = cols == null ? 0 : sx0[s], x1 = cols == null ? Width : sx1[s];
                    g.DrawImage(src, new Rectangle(x0, c0 - originY, x1 - x0, h), new Rectangle(x0, y0 + skip, x1 - x0, h), GraphicsUnit.Pixel);
                    if (cols == null) break;
                }
            }
            if (maxC == minC) { minC = c0; maxC = c1; }
            else { minC = Math.Min(minC, c0); maxC = Math.Max(maxC, c1); }
        }

        // Gør lærredet stort nok til [c0, c1). Klipper ved MaxHeight.
        bool Ensure(ref int c0, ref int c1)
        {
            int top = originY, bottom = originY + canvas.Height;
            if (c0 >= top && c1 <= bottom) return true;
            int grow = Math.Max(1500, canvas.Height / 2); // voks i store skridt (færre kopieringer)
            int newTop = c0 < top ? c0 - grow : top;
            int newBottom = c1 > bottom ? c1 + grow : bottom;
            if (newBottom - newTop > MaxHeight)
            {
                if (c1 > bottom) newBottom = Math.Max(bottom, Math.Min(newBottom, newTop + MaxHeight));
                if (c0 < top) newTop = Math.Min(top, Math.Max(newTop, newBottom - MaxHeight));
                c0 = Math.Max(c0, newTop);
                c1 = Math.Min(c1, newBottom);
                if (c1 <= c0) return false;
            }
            if (newTop != top || newBottom != bottom)
            {
                Bitmap nb = NewWhite(Width, newBottom - newTop);
                using (Graphics g = Graphics.FromImage(nb))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(canvas, new Rectangle(0, top - newTop, Width, canvas.Height), new Rectangle(0, 0, Width, canvas.Height), GraphicsUnit.Pixel);
                }
                canvas.Dispose();
                canvas = nb;
                originY = newTop;
            }
            return true;
        }

        // Ny bitmap fyldt med hvid (direkte i hukommelsen – ens resultat overalt).
        static Bitmap NewWhite(int w, int h)
        {
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            BitmapData d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try
            {
                int[] row = new int[w];
                for (int x = 0; x < w; x++) row[x] = -1; // 0xFFFFFFFF
                for (int y = 0; y < h; y++) Marshal.Copy(row, 0, d.Scan0 + y * d.Stride, w);
            }
            finally { b.UnlockBits(d); }
            return b;
        }

        // Udsnit af indholdsrækkerne [c0, c1).
        public Bitmap Crop(int c0, int c1)
        {
            c0 = Math.Max(c0, minC);
            c1 = Math.Min(c1, maxC);
            if (c1 - c0 > MaxHeight) c1 = c0 + MaxHeight;
            if (c1 <= c0) return null;
            Bitmap b = NewWhite(Width, c1 - c0);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImage(canvas, new Rectangle(0, 0, Width, c1 - c0), new Rectangle(0, c0 - originY, Width, c1 - c0), GraphicsUnit.Pixel);
            }
            return b;
        }

        public void Dispose()
        {
            if (canvas != null) { canvas.Dispose(); canvas = null; }
        }
    }
}
