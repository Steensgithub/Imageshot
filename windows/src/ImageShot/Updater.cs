using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageShot
{
    // Søger efter en nyere udgave på GitHub Releases og tilbyder at installere den.
    // Ingen persondata sendes – kun en almindelig forespørgsel efter seneste udgivelse.
    static class Updater
    {
        // Hvor udgivelserne ligger. Arkivet skal være offentligt (eller et separat offentligt
        // udgivelsesarkiv), ellers svarer GitHub 404, og tjekket springes stille over.
        public const string Repo = "Steensgithub/Imageshot";
        static string Api { get { return "https://api.github.com/repos/" + Repo + "/releases/latest"; } }

        public sealed class Release
        {
            public Version Version;
            public string Page;       // udgivelsens side
            public string Installer;  // direkte link til ImageShot-Setup-x.y.z.exe
        }

        public static Version Current
        {
            get { Version v = typeof(Updater).Assembly.GetName().Version; return new Version(v.Major, v.Minor, Math.Max(0, v.Build)); }
        }

        // Fortolker GitHubs svar (holdes enkel; kun de felter, vi bruger).
        public static Release Parse(string json)
        {
            Match tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([0-9]+(?:\\.[0-9]+){1,3})\"");
            if (!tag.Success) return null;
            Release r = new Release { Version = new Version(tag.Groups[1].Value) };
            Match page = Regex.Match(json, "\"html_url\"\\s*:\\s*\"(https://github\\.com/[^\"]+/releases/tag/[^\"]+)\"");
            if (page.Success) r.Page = page.Groups[1].Value;
            Match exe = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"(https://[^\"]+ImageShot-Setup-[^\"]+\\.exe)\"");
            if (exe.Success) r.Installer = exe.Groups[1].Value;
            return r;
        }

        public static Task<Release> LatestAsync()
        {
            return Task.Run(delegate
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "ImageShot/" + Current;
                    wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                    wc.Encoding = System.Text.Encoding.UTF8;
                    return Parse(wc.DownloadString(Api));
                }
            });
        }

        // Automatisk tjek: stille ved fejl. Manuelt tjek: svarer altid.
        public static async void Check(bool manual, Form owner)
        {
            Release r;
            try { r = await LatestAsync(); }
            catch (Exception ex)
            {
                ErrorLog.Info("Opdateringstjek: " + ex.Message);
                if (manual) MessageBox.Show(owner, L.T("update.failed", ex.Message), "ImageShot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (r == null || r.Version <= Current)
            {
                if (manual) MessageBox.Show(owner, L.T("about.upToDate") + "  (" + Current + ")", "ImageShot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string v = r.Version.ToString(3);
            ErrorLog.Info("Ny udgave fundet: " + v);
            if (manual)
            {
                if (MessageBox.Show(owner, L.T("update.ask", v), "ImageShot", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    Install(r);
            }
            else Toast.ShowText(L.T("toast.updateAvailable", v), false, delegate { Install(r); });
        }

        static async void Install(Release r)
        {
            if (string.IsNullOrEmpty(r.Installer))
            {
                if (!string.IsNullOrEmpty(r.Page)) Process.Start(r.Page);
                return;
            }
            try
            {
                Toast.ShowText(L.T("update.downloading"), false);
                string file = Path.Combine(Path.GetTempPath(), "ImageShot-Setup-" + r.Version.ToString(3) + ".exe");
                await Task.Run(delegate
                {
                    using (WebClient wc = new WebClient())
                    {
                        wc.Headers[HttpRequestHeader.UserAgent] = "ImageShot/" + Current;
                        wc.DownloadFile(r.Installer, file);
                    }
                });
                // Installationsprogrammet lukker ImageShot selv og starter den nye udgave bagefter.
                Process.Start(new ProcessStartInfo(file, "/SILENT /SP- /NOCANCEL") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Opdatering", ex);
                if (!string.IsNullOrEmpty(r.Page)) { try { Process.Start(r.Page); } catch { } }
            }
        }
    }
}
