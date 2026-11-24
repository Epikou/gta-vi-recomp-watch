// Fenetre unique : un oeil realiste pose dans un coin, d'ou se deplie le panneau.
// Tout est dessine a la main dans une image a transparence par pixel (fenetre "layered"),
// ce qui donne des bords lisses et permet aux cils de depasser de la forme.
//
// Clic sur l'oeil : deplier / replier. Glisser : deplacer. Poignee sous l'oeil : redimensionner.
// Clic droit : menu (langue, premier plan, quitter).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Textes de l'interface : anglais par defaut, francais et espagnol en option.
static class Tr
{
    public static int Lang; // 0 = English, 1 = Français, 2 = Español
    public static readonly string[] Names = { "English", "Français", "Español" };
    public static readonly string[] Codes = { "EN", "FR", "ES" };

    static readonly Dictionary<string, string[]> T = new Dictionary<string, string[]>
    {
        { "title", new[] { "GTA VI recomp PC status", "GTA VI recomp PC status", "GTA VI recomp PC status" } },
        { "checking", new[] { "Checking...", "Relevé en cours...", "Comprobando..." } },
        { "status", new[] { "Check {0} at {1:HH:mm:ss} · next in {2} s", "Relevé n° {0} à {1:HH:mm:ss} · prochain dans {2} s", "Rev. {0} a las {1:HH:mm:ss} · siguiente en {2} s" } },
        { "refresh", new[] { "Refresh", "Actualiser", "Actualizar" } },
        { "markread", new[] { "Mark read", "Tout lu", "Marcar leído" } },
        { "podium", new[] { "Podium", "Podium", "Podio" } },
        { "podiumNote", new[] { "scored on 7 stages reached", "calculé sur 7 étapes franchies", "calculado sobre 7 etapas superadas" } },
        { "ranking", new[] { "Rest of the ranking", "Suite du classement", "Resto de la clasificación" } },
        { "news", new[] { "What's new", "Nouveautés", "Novedades" } },
        { "nothing", new[] { "Nothing new since {0:HH:mm}. Changes will show up here.", "Rien de nouveau depuis {0:HH:mm}. Les changements apparaîtront ici.", "Nada nuevo desde las {0:HH:mm}. Los cambios aparecerán aquí." } },
        { "loading", new[] { "Loading...", "Chargement...", "Cargando..." } },
        { "back", new[] { "Back", "Retour", "Volver" } },
        { "stages", new[] { "Stages", "Étapes", "Etapas" } },
        { "declared", new[] { "Status declared by the author", "État déclaré par l'auteur", "Estado declarado por el autor" } },
        { "commits", new[] { "Latest commits", "Derniers commits", "Últimos commits" } },
        { "summary", new[] { "Summary", "Résumé", "Resumen" } },
        { "gone", new[] { "repository gone", "dépôt disparu", "repositorio desaparecido" } },
        { "error", new[] { "Check failed: {0}", "Relevé impossible : {0}", "Comprobación fallida: {0}" } },
        { "st0", new[] { "no GTA VI yet", "pas encore de GTA VI", "aún sin GTA VI" } },
        { "st1", new[] { "groundwork", "préparation", "preparación" } },
        { "st2", new[] { "exe runs", "exe lancé", "exe arranca" } },
        { "st3", new[] { "first frame", "1re image", "1.ª imagen" } },
        { "st4", new[] { "main menu", "menu principal", "menú principal" } },
        { "st5", new[] { "in-game", "en jeu", "en juego" } },
        { "st6", new[] { "30 FPS", "30 FPS", "30 FPS" } },
        { "st7", new[] { "stable", "stable", "estable" } },
        { "ev.commits1", new[] { "{0}: 1 new commit", "{0} : 1 nouveau commit", "{0}: 1 commit nuevo" } },
        { "ev.commitsN", new[] { "{0}: {1} new commits", "{0} : {1} nouveaux commits", "{0}: {1} commits nuevos" } },
        { "ev.status", new[] { "{0}: GTA VI status changed", "{0} : l'état GTA VI a changé", "{0}: el estado de GTA VI ha cambiado" } },
        { "ev.project", new[] { "New project: {0} ({1} stars)", "Nouveau projet : {0} ({1} étoiles)", "Proyecto nuevo: {0} ({1} estrellas)" } },
        { "ev.score", new[] { "{0} moves to {1} (was {2})", "{0} passe à {1} (était à {2})", "{0} pasa a {1} (estaba en {2})" } },
        { "ev.leader", new[] { "New leader: {0}", "Nouveau premier : {0}", "Nuevo líder: {0}" } },
        { "m.toggle", new[] { "Open / close the panel", "Déplier / replier le panneau", "Abrir / cerrar el panel" } },
        { "m.lang", new[] { "Language", "Langue", "Idioma" } },
        { "m.top", new[] { "Always on top", "Toujours au premier plan", "Siempre visible" } },
        { "m.quit", new[] { "Quit", "Quitter", "Salir" } },
        { "mute", new[] { "Mute this project", "Couper ce projet", "Silenciar este proyecto" } },
        { "unmute", new[] { "Muted - turn back on", "En sourdine - réactiver", "Silenciado - reactivar" } },
        { "m.tray", new[] { "Minimize next to the clock", "Réduire près de l'horloge", "Minimizar junto al reloj" } },
        { "m.show", new[] { "Show the eye", "Afficher l'œil", "Mostrar el ojo" } },
        { "tip.none", new[] { "GTA VI recomp PC status - nothing new", "GTA VI recomp PC status - rien de nouveau", "GTA VI recomp PC status - nada nuevo" } },
        { "tip.some", new[] { "GTA VI recomp PC status - {0} important change(s)", "GTA VI recomp PC status - {0} changement(s) important(s)", "GTA VI recomp PC status - {0} cambio(s) importante(s)" } },
    };

    public static string S(string key)
    {
        string[] v;
        return T.TryGetValue(key, out v) ? v[Lang] : key;
    }

    public static string F(string key, params object[] args)
    {
        return string.Format(S(key), args);
    }
}

// Un projet dans notre classement.
class Entry
{
    public string Name, Repo, Summary = "";
    // Les sept paliers de PortWatch VI et le score ou ils se trouvent sur l'echelle de 0 a 100.
    public const int Stages = 7;
    public static readonly int[] Tick = { 0, 25, 40, 55, 70, 85, 100 };
    public double[] St = new double[Stages]; // 0 a 1 par palier
    public double Score;
    public int Reached = 1;                  // dernier palier atteint, de 1 (preparation) a 7 (stable)
    public bool Missing;
    public string Key = "";  // identite du projet : son depot, ou son identifiant PortWatch s'il n'en a pas
    public string Url = "";  // page a ouvrir : le depot GitHub, sinon l'article PortWatch
    public int Stars;
    public Project Proj;

    public int TopStage() { return Reached; }
    public string ScoreText() { return Score.ToString("0.#", CultureInfo.InvariantCulture) + " %"; }

    static double Unit(double v) { return double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v)); }

    static double StabilityValue(string s)
    {
        switch (s)
        {
            case "often": return 0.25;
            case "sometimes": return 0.55;
            case "rarely": return 0.8;
            case "stable": return 1;
        }
        return 0; // unknown, constant
    }

    // Meme calcul que scoreEffort() dans le scoring.js de PortWatch VI.
    // Le travail preparatoire remplit les 25 premiers points ; ensuite chaque palier franchi fixe le score
    // (25, 40, 55, 70), un palier partiel avance d'une fraction vers le suivant, et une fois en jeu
    // les 30 FPS puis la stabilite apportent 15 points chacun.
    public void Apply(Effort e)
    {
        double[] ladder = { Unit(e.Loads), Unit(e.Boots), Unit(e.Menu), Unit(e.InGame) };
        double score = Tick[1] * Unit(e.Groundwork), prev = score;
        int reached = 0;
        St[0] = Unit(e.Groundwork);
        for (int k = 0; k < 4; k++)
        {
            double v = ladder[k];
            if (v >= 1) { score = Tick[k + 1]; prev = score; reached = k + 1; St[k + 1] = 1; continue; }
            if (v > 0) { score = Math.Max(score, prev + v * (Tick[k + 1] - prev)); St[k + 1] = v; }
            break;
        }
        if (reached >= 1) St[0] = 1; // le jeu tourne : la preparation est derriere
        if (reached == 4)
        {
            double fps = Unit(e.Fps / 30), stab = StabilityValue(e.Stability);
            score = Tick[4] + fps * (Tick[5] - Tick[4]) + stab * (Tick[6] - Tick[5]);
            St[5] = fps; St[6] = stab;
            if (fps >= 1) reached = 5;
            if (fps >= 1 && stab >= 1) reached = 6;
        }
        // PortWatch fixe parfois un score a la main, quand son echelle ne sait pas exprimer le cas.
        if (e.Manual >= 0) score = Math.Min(100, Math.Max(0, e.Manual));
        Score = Math.Round(score * 10) / 10;
        Reached = reached + 1;
    }
}

class EyeWindow : Form
{
    // Espace de dessin "maquette" : 680 x 590, oeil en haut a gauche. Les retournements le mettent dans les autres coins.
    const float DW = 680F, DH = 590F;
    const float EyeScale = 0.6F;
    static readonly PointF EyeOrigin = new PointF(26F, 22F);
    static readonly PointF EyeCenter = new PointF(26F + 150F * 0.6F, 22F + 88F * 0.6F);
    static readonly RectangleF EyeBox = new RectangleF(14F, 0F, 208F, 140F);     // fenetre repliee
    static readonly RectangleF Header0 = new RectangleF(256F, 82F, 398F, 44F);
    static readonly RectangleF Content0 = new RectangleF(36F, 142F, 608F, 422F);

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int W, H; public SIZE(int w, int h) { W = w; H = h; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLENDFUNCTION { public byte Op, Flags, Alpha, Format; }

    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dst, ref POINT pos, ref SIZE size, IntPtr src, ref POINT srcPos, int key, ref BLENDFUNCTION blend, int flags);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);

    readonly MainForm main;
    readonly Timer anim = new Timer();
    readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
    readonly List<KeyValuePair<RectangleF, Action>> hits = new List<KeyValuePair<RectangleF, Action>>();
    readonly Dictionary<string, double> prevScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    readonly DateTime started = DateTime.Now;
    readonly string settingsPath;
    readonly string snapPath;
    readonly bool snapExit, snapDetail;
    readonly float snapAnim;
    readonly int snapRank;

    List<Entry> ranking = new List<Entry>();
    bool baselined;
    string prevLeader;
    Entry detail;

    float scale = 1F;
    float eyeX, eyeY;        // centre de l'oeil a l'ecran, en pixels
    bool flipX, flipY, expanded;
    float open, look_x, look_y;
    int blink = -1, idle, lastRemaining = -1, snapTicks;
    bool dirty = true;
    float newsScroll, newsMax;

    float animT = 1F;       // 0 = replie, 1 = deplie
    int animDir;            // +1 ouverture en cours, -1 fermeture en cours
    Bitmap panelCache;      // image du panneau, reutilisee pendant l'animation

    NotifyIcon tray;
    Icon iconOpen, iconClosed;
    int trayFresh = -1;

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);

    Bitmap irisBmp;
    float irisScale;

    // souris
    bool down, moved, resizing, downOnHit;
    Point downScreen;
    float downEyeX, downEyeY, downScale, downDist;

    public EyeWindow(MainForm main, string[] args)
    {
        this.main = main;
        Text = "GTA VI on PC";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = true;
        TopMost = true;

        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96F * 0.85F;
        Rectangle area = Screen.PrimaryScreen.WorkingArea;
        eyeX = area.Right - 150F * scale;
        eyeY = area.Top + 110F * scale;

        settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GtaPcWatch", "settings.ini");
        LoadSettings();
        foreach (string a in args)
        {
            if (a == "--open") expanded = true;
            else if (a == "--exit") snapExit = true;
            else if (a == "--detail") snapDetail = true;
            else if (a.StartsWith("--detail=")) { snapDetail = true; int.TryParse(a.Substring(9), out snapRank); }
            else if (a.StartsWith("--makeicon=")) { WriteIco(a.Substring(11)); Environment.Exit(0); }
            else if (a.StartsWith("--anim=")) float.TryParse(a.Substring(7), NumberStyles.Float, CultureInfo.InvariantCulture, out snapAnim);
            else if (a.StartsWith("--snap=")) snapPath = a.Substring(7);
            else if (a.StartsWith("--lang=")) Tr.Lang = Math.Max(0, Array.IndexOf(Tr.Codes, a.Substring(7).ToUpperInvariant()));
            else if (a.StartsWith("--at="))
            {
                string[] xy = a.Substring(5).Split(',');
                float x, y;
                if (xy.Length == 2 && float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                    && float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) { eyeX = x; eyeY = y; }
            }
            else if (a.StartsWith("--scale="))
            {
                float sc;
                if (float.TryParse(a.Substring(8), NumberStyles.Float, CultureInfo.InvariantCulture, out sc)) scale = sc;
            }
        }
        scale = Math.Max(0.5F, Math.Min(2.5F, scale));

        // Icone de la barre des taches et icone pres de l'horloge : l'oeil lui-meme, ouvert ou ferme.
        iconOpen = MakeIcon(1F);
        iconClosed = MakeIcon(0F);
        Icon = iconOpen;
        tray = new NotifyIcon();
        tray.Icon = iconClosed;
        tray.Text = "GTA VI recomp PC status";
        tray.Visible = snapPath == null;
        tray.MouseClick += delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { if (Visible) ToTray(); else FromTray(); }
            else if (e.Button == MouseButtons.Right) { SetForegroundWindow(Handle); ShowMenu(Cursor.Position); }
        };

        main.StateChanged = OnState;
        FormClosing += delegate { Program.Exiting = true; SaveSettings(); tray.Visible = false; tray.Dispose(); };
        MouseWheel += delegate(object s, MouseEventArgs e) { if (expanded) { newsScroll = Math.Max(0, Math.Min(newsMax, newsScroll - e.Delta / 4F)); dirty = true; } };

        anim.Interval = 33;
        anim.Tick += delegate { Step(); };
        Shown += delegate { Place(); Redraw(); anim.Start(); };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80000; // WS_EX_LAYERED
            return cp;
        }
    }

    // ---------- reglages ----------

    void LoadSettings()
    {
        try
        {
            if (!File.Exists(settingsPath)) return;
            foreach (string line in File.ReadAllLines(settingsPath))
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                float f;
                bool ok = float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
                if (k == "scale" && ok) scale = f;
                else if (k == "eyeX" && ok) eyeX = f;
                else if (k == "eyeY" && ok) eyeY = f;
                else if (k == "lang" && ok) Tr.Lang = Math.Max(0, Math.Min(2, (int)f));
                else if (k == "top") TopMost = v != "0";
                else if (k == "muted")
                    lock (main.Muted)
                        foreach (string repo in v.Split(';')) if (repo.Trim().Length > 0) main.Muted.Add(repo.Trim());
            }
            // Si l'ecran memorise n'existe plus, on revient sur l'ecran principal.
            bool visible = false;
            foreach (Screen sc in Screen.AllScreens) if (sc.WorkingArea.Contains((int)eyeX, (int)eyeY)) visible = true;
            if (!visible)
            {
                Rectangle area = Screen.PrimaryScreen.WorkingArea;
                eyeX = area.Right - 150F * scale;
                eyeY = area.Top + 110F * scale;
            }
        }
        catch (Exception) { } // reglages illisibles : valeurs par defaut
    }

    string MutedList()
    {
        lock (main.Muted) return string.Join(";", new List<string>(main.Muted).ToArray());
    }

    void ToggleMute(string repo)
    {
        lock (main.Muted) { if (!main.Muted.Remove(repo)) main.Muted.Add(repo); }
        SaveSettings();
        dirty = true;
    }

    void SaveSettings()
    {
        if (snapPath != null) return; // lancement de verification : ne pas ecraser les reglages de l'utilisateur
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            File.WriteAllLines(settingsPath, new[] {
                "scale=" + scale.ToString(CultureInfo.InvariantCulture),
                "eyeX=" + eyeX.ToString(CultureInfo.InvariantCulture),
                "eyeY=" + eyeY.ToString(CultureInfo.InvariantCulture),
                "lang=" + Tr.Lang,
                "top=" + (TopMost ? "1" : "0"),
                "muted=" + MutedList() });
        }
        catch (Exception) { } // dossier non inscriptible : on garde les reglages pour cette session seulement
    }

    // ---------- geometrie ----------

    static RectangleF Mirror(RectangleF r, bool fx, bool fy)
    {
        return new RectangleF(fx ? DW - r.Right : r.X, fy ? DH - r.Bottom : r.Y, r.Width, r.Height);
    }

    static PointF Mirror(PointF p, bool fx, bool fy)
    {
        return new PointF(fx ? DW - p.X : p.X, fy ? DH - p.Y : p.Y);
    }

    // Partie de l'espace maquette que montre la fenetre.
    RectangleF View() { return expanded ? new RectangleF(0, 0, DW, DH) : Mirror(EyeBox, flipX, flipY); }
    RectangleF HeaderRect() { return Mirror(Header0, flipX, flipY); }
    RectangleF ContentRect() { return Mirror(Content0, flipX, flipY); }
    PointF EyePoint() { return Mirror(EyeCenter, flipX, flipY); }
    // Poignee de redimensionnement : juste sous le coin externe de l'oeil, quel que soit le coin d'ecran.
    PointF GripPoint() { PointF e = EyePoint(); return new PointF(e.X + (flipX ? -86F : 86F), e.Y + 17F); }
    RectangleF EyeHit() { return Mirror(new RectangleF(26F, 18F, 180F, 112F), flipX, flipY); }

    // Place la fenetre pour que l'oeil reste au meme endroit de l'ecran, panneau tourne vers l'interieur.
    void Place()
    {
        Rectangle area = Screen.FromPoint(new Point((int)eyeX, (int)eyeY)).WorkingArea;
        flipX = eyeX > area.Left + area.Width / 2F;
        flipY = eyeY > area.Top + area.Height / 2F;
        RectangleF v = View();
        PointF e = EyePoint();
        Bounds = new Rectangle(
            (int)Math.Round(eyeX - (e.X - v.X) * scale), (int)Math.Round(eyeY - (e.Y - v.Y) * scale),
            Math.Max(1, (int)Math.Ceiling(v.Width * scale)), Math.Max(1, (int)Math.Ceiling(v.Height * scale)));
    }

    PointF ToDesign(Point client)
    {
        RectangleF v = View();
        return new PointF(client.X / scale + v.X, client.Y / scale + v.Y);
    }

    // ---------- classement ----------

    void OnState()
    {
        Recompute();
        UpdateTray();
        dirty = true;
    }

    // Rattache nos releves directs (commits, etat declare) a un projet du classement.
    void Merge(Dictionary<string, Entry> map, Project p)
    {
        Entry x;
        if (!map.TryGetValue(p.Repo, out x)) return; // absent de PortWatch : pas de score, donc hors classement
        x.Proj = p;
        if (x.Summary.Length == 0 && p.Note != null) x.Summary = p.Note;
    }

    void Recompute()
    {
        Dictionary<string, Entry> map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (Effort e in main.pwEfforts)
        {
            // Un projet peut ne pas avoir de depot public (code prive) : PortWatch le classe quand meme,
            // donc nous aussi. Son lien mene alors a l'article qui le decrit.
            bool hasRepo = System.Text.RegularExpressions.Regex.IsMatch(e.Repo ?? "", @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$");
            if (!hasRepo && string.IsNullOrEmpty(e.Id)) continue;
            Entry x = new Entry { Name = e.Name, Repo = hasRepo ? e.Repo : "", Summary = e.Summary ?? "", Missing = e.Missing || e.Nuked, Stars = e.Stars };
            x.Key = hasRepo ? e.Repo : "pw:" + e.Id;
            x.Url = hasRepo ? "https://github.com/" + e.Repo
                : System.Text.RegularExpressions.Regex.IsMatch(e.Link ?? "", @"^news/[a-z0-9-]+\.html$") ? "https://portwatch-vi.xyz/" + e.Link
                : "https://portwatch-vi.xyz/";
            x.Apply(e);
            map[x.Key] = x;
        }
        foreach (Project p in main.projects) Merge(map, p);
        foreach (Project a in main.actors) Merge(map, a);

        List<Entry> list = new List<Entry>();
        foreach (Entry x in map.Values)
            if (!x.Missing && x.Score > 0) list.Add(x);
        list.Sort(delegate(Entry a, Entry b)
        {
            int c = b.Score.CompareTo(a.Score);
            if (c == 0) c = b.Stars.CompareTo(a.Stars); // a egalite, PortWatch place devant le depot le plus etoile
            return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        if (baselined)
        {
            bool sound = false;
            foreach (Entry x in list)
            {
                double old;
                if (prevScores.TryGetValue(x.Key, out old) && Math.Abs(old - x.Score) > 0.05 && !main.IsMuted(x.Key))
                {
                    main.events.Insert(0, new Evt { When = DateTime.Now, Major = true, Kind = "score", Subject = x.Name,
                        Detail = x.ScoreText() + "|" + old.ToString("0.#", CultureInfo.InvariantCulture) + " %", Text = x.Name + " " + x.ScoreText() });
                    sound = true;
                }
            }
            if (list.Count > 0 && prevLeader != null && list[0].Key != prevLeader)
            {
                main.events.Insert(0, new Evt { When = DateTime.Now, Major = true, Kind = "leader", Subject = list[0].Name, Text = list[0].Name });
                sound = true;
            }
            if (sound) SystemSounds.Exclamation.Play();
        }
        if (list.Count > 0)
        {
            baselined = true;
            prevLeader = list[0].Key;
            foreach (Entry x in list) prevScores[x.Key] = x.Score;
        }
        if (detail != null)
        {
            Entry same = null;
            foreach (Entry x in map.Values) if (x.Key == detail.Key) same = x;
            detail = same ?? detail;
        }
        ranking = list;
    }

    // ---------- animation ----------

    void Step()
    {
        if (animDir != 0)
        {
            animT += animDir * 0.09F;
            if (animDir > 0 && animT >= 1F) { animT = 1F; animDir = 0; FreePanel(); }
            else if (animDir < 0 && animT <= 0F) { animT = 1F; animDir = 0; expanded = false; FreePanel(); Place(); }
            dirty = true;
        }
        float target = (expanded && animDir >= 0) || main.FreshCount > 0 ? 1F : 0F;
        if (Math.Abs(open - target) > 0.003F) { open += (target - open) * 0.2F; dirty = true; }
        else if (open != target) { open = target; dirty = true; }

        if (target > 0.5F)
        {
            Point c = Cursor.Position;
            float dx = c.X - eyeX, dy = c.Y - eyeY;
            if (flipX) dx = -dx;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            float wx = len < 1 ? 0 : dx / len * Math.Min(16F, len / 14F), wy = len < 1 ? 0 : dy / len * Math.Min(8F, len / 22F);
            if (Math.Abs(wx - look_x) > 0.25F || Math.Abs(wy - look_y) > 0.25F)
            {
                look_x += (wx - look_x) * 0.25F;
                look_y += (wy - look_y) * 0.25F;
                dirty = true;
            }
            if (blink < 0 && ++idle > 170) { blink = 0; idle = 0; }
            if (blink >= 0) { blink++; if (blink > 8) blink = -1; dirty = true; }
        }
        else if (blink >= 0) { blink = -1; dirty = true; }

        if (expanded && main.remaining != lastRemaining) { lastRemaining = main.remaining; dirty = true; }
        if (dirty) Redraw();

        if (snapPath != null && ++snapTicks == 240) // environ 8 s : le premier releve est arrive
        {
            if (snapDetail && ranking.Count > snapRank) detail = ranking[snapRank];
            if (snapAnim > 0F) { FreePanel(); animT = snapAnim; animDir = 1; } // fige l'animation a mi-course pour la capture
            Snapshot(snapPath);
            if (snapAnim > 0F) { animT = 1F; animDir = 0; FreePanel(); }
            if (snapExit) Close();
        }
    }

    float Openness()
    {
        if (blink < 0) return open;
        return open * Math.Abs(blink - 4) / 4F;
    }

    // ---------- outils de dessin ----------

    static Color C(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
    static Color CA(int alpha, int rgb) { return Color.FromArgb(alpha, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
    static float Rnd(int i) { double x = Math.Sin(i * 127.1 + 3.3) * 43758.5453; return (float)(x - Math.Floor(x)); }

    Font F(float px, bool bold)
    {
        string key = px + (bold ? "b" : "r");
        Font f;
        if (!fonts.TryGetValue(key, out f))
        {
            f = new Font("Segoe UI", px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
            fonts[key] = f;
        }
        return f;
    }

    static GraphicsPath RR(RectangleF r, float rad)
    {
        float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
        GraphicsPath p = new GraphicsPath();
        if (d <= 0.1F) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static void Fill(Graphics g, Color c, RectangleF r, float rad)
    {
        using (GraphicsPath p = RR(r, rad)) using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
    }

    static readonly StringFormat SfLine = MakeFormat(true), SfWrap = MakeFormat(false), SfCenter = MakeCentered();

    static StringFormat MakeCentered()
    {
        StringFormat f = MakeFormat(false);
        f.Alignment = StringAlignment.Center;
        return f;
    }

    static StringFormat MakeFormat(bool oneLine)
    {
        StringFormat f = new StringFormat(StringFormat.GenericTypographic);
        f.Trimming = StringTrimming.EllipsisCharacter;
        if (oneLine) f.FormatFlags |= StringFormatFlags.NoWrap;
        else f.FormatFlags &= ~StringFormatFlags.NoWrap;
        f.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        return f;
    }

    // Texte sur une ligne, coupe par "..." a la largeur donnee. Renvoie la largeur dessinee.
    float Txt(Graphics g, string s, float px, bool bold, Color c, float x, float y, float maxW)
    {
        Font f = F(px, bold);
        using (SolidBrush b = new SolidBrush(c))
            g.DrawString(s, f, b, new RectangleF(x, y, maxW, px * 1.5F), SfLine);
        return Math.Min(maxW, g.MeasureString(s, f, 4000, SfLine).Width);
    }

    float TxtW(Graphics g, string s, float px, bool bold)
    {
        return g.MeasureString(s, F(px, bold), 4000, SfLine).Width;
    }

    void TxtRight(Graphics g, string s, float px, bool bold, Color c, float right, float y)
    {
        Txt(g, s, px, bold, c, right - TxtW(g, s, px, bold), y, 4000);
    }

    void TxtCenter(Graphics g, string s, float px, bool bold, Color c, float cx, float y)
    {
        Txt(g, s, px, bold, c, cx - TxtW(g, s, px, bold) / 2F, y, 4000);
    }

    // Texte sur plusieurs lignes ; renvoie la hauteur occupee.
    float Para(Graphics g, string s, float px, Color c, float x, float y, float w, float maxH)
    {
        Font f = F(px, false);
        SizeF size = g.MeasureString(s, f, new SizeF(w, maxH), SfWrap);
        using (SolidBrush b = new SolidBrush(c)) g.DrawString(s, f, b, new RectangleF(x, y, w, Math.Min(maxH, size.Height + 2)), SfWrap);
        return Math.Min(maxH, size.Height);
    }

    void Hit(RectangleF r, Action a)
    {
        hits.Add(new KeyValuePair<RectangleF, Action>(r, a));
    }

    // Bouton arrondi ; renvoie sa largeur.
    float Pill(Graphics g, string text, float x, float y, Action a)
    {
        float w = TxtW(g, text, 12F, false) + 24F;
        RectangleF r = new RectangleF(x, y, w, 26F);
        using (GraphicsPath p = RR(r, 13F)) using (Pen pen = new Pen(C(0x7B4AA6), 1F)) g.DrawPath(pen, p);
        TxtCenter(g, text, 12F, false, Color.White, x + w / 2F, y + 5F);
        Hit(r, a);
        return w;
    }

    // ---------- oeil ----------

    static void AddQuad(GraphicsPath p, PointF a, PointF c, PointF b)
    {
        p.AddBezier(a, new PointF(a.X + 2F / 3F * (c.X - a.X), a.Y + 2F / 3F * (c.Y - a.Y)),
            new PointF(b.X + 2F / 3F * (c.X - b.X), b.Y + 2F / 3F * (c.Y - b.Y)), b);
    }

    static PointF Bz(PointF[] p, float t)
    {
        float u = 1 - t, a = u * u * u, b = 3 * u * u * t, c = 3 * u * t * t, d = t * t * t;
        return new PointF(a * p[0].X + b * p[1].X + c * p[2].X + d * p[3].X, a * p[0].Y + b * p[1].Y + c * p[2].Y + d * p[3].Y);
    }

    static GraphicsPath Almond()
    {
        GraphicsPath p = new GraphicsPath();
        p.AddBezier(10, 92, 70, 18, 215, 14, 290, 86);
        p.AddBezier(290, 86, 225, 160, 80, 164, 10, 92);
        p.CloseFigure();
        return p;
    }

    // L'iris est dessine une fois dans une image, puis reutilise : c'est la partie la plus couteuse.
    Bitmap Iris(float pixelScale)
    {
        if (irisBmp != null && Math.Abs(irisScale - pixelScale) < 0.01F) return irisBmp;
        if (irisBmp != null) irisBmp.Dispose();
        irisScale = pixelScale;
        int size = Math.Max(16, (int)Math.Ceiling(106 * pixelScale));
        irisBmp = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(irisBmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(size / 106F, size / 106F);
            using (SolidBrush b = new SolidBrush(C(0x16081F))) g.FillEllipse(b, 0, 0, 106, 106);
            using (GraphicsPath disc = new GraphicsPath())
            {
                disc.AddEllipse(2, 2, 102, 102);
                using (PathGradientBrush pg = new PathGradientBrush(disc))
                {
                    ColorBlend blend = new ColorBlend();
                    blend.Colors = new[] { C(0x16081F), C(0x6E2088), C(0xD8368C), C(0xFF7A3D), C(0xFFB45C), C(0xFFB45C) };
                    blend.Positions = new[] { 0F, 0.12F, 0.32F, 0.52F, 0.70F, 1F }; // 0 = bord, 1 = centre
                    pg.InterpolationColors = blend;
                    pg.CenterPoint = new PointF(53, 53);
                    g.FillPath(pg, disc);
                }
            }
            int[] fibre = { 0xFFE6C2, 0x3A0F4D, 0xFF9C6A };
            for (int i = 0; i < 150; i++)
            {
                double a = i * Math.PI / 75 + Rnd(i) * 0.03;
                float r1 = 19 + Rnd(i + 7) * 6, r2 = 34 + Rnd(i + 31) * 15;
                using (Pen pen = new Pen(CA((int)(255 * (0.25F + Rnd(i + 50) * 0.4F)), fibre[i % 3]), 0.5F + Rnd(i + 90) * 0.7F))
                    g.DrawLine(pen, 53 + (float)Math.Cos(a) * r1, 53 + (float)Math.Sin(a) * r1, 53 + (float)Math.Cos(a) * r2, 53 + (float)Math.Sin(a) * r2);
            }
            PointF[] col = new PointF[36];
            for (int i = 0; i < 36; i++)
            {
                double a = i * Math.PI / 18;
                float rc = 25 + (i % 2 == 1 ? 3.5F : 0) + Rnd(i + 200) * 2;
                col[i] = new PointF(53 + (float)Math.Cos(a) * rc, 53 + (float)Math.Sin(a) * rc);
            }
            using (Pen pen = new Pen(CA(180, 0xFFD49A), 1F)) g.DrawPolygon(pen, col);
            using (SolidBrush b = new SolidBrush(CA(120, 0x24102F))) g.FillEllipse(b, 32.5F, 32.5F, 41, 41);
            using (SolidBrush b = new SolidBrush(C(0x050208))) g.FillEllipse(b, 34.5F, 34.5F, 37, 37);
        }
        return irisBmp;
    }

    // Dessine l'oeil dans son repere propre (300 x 180). pixelScale = nombre de pixels par unite.
    void DrawEye(Graphics g, float o, float pixelScale, bool halo)
    {
        float cu = 18 + (1 - o) * 132, cl = 160 - (1 - o) * 8;
        PointF[] up = { new PointF(10, 92), new PointF(70, cu), new PointF(215, cu - 4), new PointF(290, 86) };
        PointF[] lo = { new PointF(10, 92), new PointF(80, cl + 4), new PointF(225, cl), new PointF(290, 86) };

        using (GraphicsPath almond = Almond())
        {
            if (halo)
                using (Pen pen = new Pen(CA(70, 0x08030C), 9F)) { pen.LineJoin = LineJoin.Round; g.DrawPath(pen, almond); }
            using (Pen pen = new Pen(CA(140, 0x6B3FA0), 1.6F))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                g.DrawBezier(pen, 20, 72, 82, -6, 222, -8, 286, 66); // pli de la paupiere
            }

            using (PathGradientBrush white = new PathGradientBrush(almond))
            {
                ColorBlend blend = new ColorBlend();
                blend.Colors = new[] { C(0x9C98A8), C(0xE6E5EC), C(0xFCFCFD), C(0xFCFCFD) };
                blend.Positions = new[] { 0F, 0.35F, 0.75F, 1F };
                white.InterpolationColors = blend;
                white.CenterPoint = new PointF(150, 88);
                g.FillPath(white, almond);
            }

            GraphicsState state = g.Save();
            g.SetClip(almond, CombineMode.Intersect);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            float ix = 150 + look_x, iy = 88 + look_y;
            g.DrawImage(Iris(pixelScale), new RectangleF(ix - 53, iy - 53, 106, 106));
            if (o > 0.4F)
            {
                using (GraphicsPath refl = new GraphicsPath())
                {
                    PointF a = new PointF(ix - 26, iy - 30);
                    refl.AddBezier(a, new PointF(ix - 14, iy - 37), new PointF(ix - 4, iy - 34), new PointF(ix - 4, iy - 34));
                    refl.AddBezier(new PointF(ix - 4, iy - 34), new PointF(ix - 1, iy - 24), new PointF(ix - 6, iy - 15), new PointF(ix - 6, iy - 15));
                    refl.AddBezier(new PointF(ix - 6, iy - 15), new PointF(ix - 18, iy - 11), new PointF(ix - 28, iy - 15), new PointF(ix - 28, iy - 15));
                    refl.AddBezier(new PointF(ix - 28, iy - 15), new PointF(ix - 31, iy - 23), a, a);
                    refl.CloseFigure();
                    using (SolidBrush b = new SolidBrush(CA(235, 0xFFFFFF))) g.FillPath(b, refl);
                }
                using (SolidBrush b = new SolidBrush(CA(190, 0xFFFFFF))) g.FillEllipse(b, ix + 16.4F, iy + 14.4F, 7.2F, 7.2F);
                using (Pen pen = new Pen(CA(34, 0xFFFFFF), 7F)) g.DrawArc(pen, ix - 44, iy - 44, 88, 88, 25, 130);
            }
            using (GraphicsPath shade = new GraphicsPath())
            {
                shade.AddBezier(up[0], up[1], up[2], up[3]);
                shade.AddBezier(new PointF(290, 86), new PointF(215, cu + 26), new PointF(70, cu + 30), new PointF(10, 92));
                shade.CloseFigure();
                using (SolidBrush b = new SolidBrush(CA(96, 0x1A0A22))) g.FillPath(b, shade);
            }
            g.Restore(state);

            // paupieres : bornees par le contour de l'oeil, donc sans decoupe a bords crantes
            using (GraphicsPath lid = new GraphicsPath())
            using (LinearGradientBrush skin = new LinearGradientBrush(new RectangleF(0, 0, 300, 170), C(0x4A2470), C(0x1D0C2E), 90F))
            {
                lid.AddBezier(10, 92, 70, 18, 215, 14, 290, 86);
                lid.AddBezier(up[3], up[2], up[1], up[0]);
                lid.CloseFigure();
                g.FillPath(skin, lid);
            }
            using (GraphicsPath lid = new GraphicsPath())
            using (LinearGradientBrush skin = new LinearGradientBrush(new RectangleF(0, 60, 300, 120), C(0x2C1447), C(0x150821), 90F))
            {
                lid.AddBezier(lo[0], lo[1], lo[2], lo[3]);
                lid.AddBezier(290, 86, 225, 160, 80, 164, 10, 92);
                lid.CloseFigure();
                g.FillPath(skin, lid);
            }
            using (Pen pen = new Pen(CA(128, 0xC9B6DC), 2F)) g.DrawBezier(pen, lo[0], lo[1], lo[2], lo[3]);
            using (Pen pen = new Pen(CA(205, 0x7B4AA6), 2.2F)) g.DrawBezier(pen, 10, 92, 70, cu - 5, 215, cu - 9, 290, 86);
            using (Pen pen = new Pen(C(0x08030C), 4.5F))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                g.DrawBezier(pen, up[0], up[1], up[2], up[3]);
            }

            // cils
            float dir = o < 0.4F ? 1F : -1F;
            using (SolidBrush lash = new SolidBrush(C(0x08030C)))
            {
                for (int i = 0; i < 19; i++)
                {
                    float t = 0.07F + i * 0.048F;
                    PointF p = Bz(up, t);
                    float len = (13 + 15 * (float)Math.Sin(Math.PI * Math.Min(1F, t * 1.15F))) * (o < 0.4F ? 0.8F : 1F);
                    float lean = (t - 0.38F) * 46 + Rnd(i + 400) * 4;
                    PointF tip = new PointF(p.X + lean, p.Y + dir * len), c1 = new PointF(p.X + lean * 0.15F, p.Y + dir * len * 0.75F);
                    using (GraphicsPath one = new GraphicsPath())
                    {
                        AddQuad(one, new PointF(p.X - 1.3F, p.Y), c1, tip);
                        AddQuad(one, tip, new PointF(c1.X + 2.2F, c1.Y), new PointF(p.X + 1.3F, p.Y));
                        one.CloseFigure();
                        g.FillPath(lash, one);
                    }
                }
                if (o > 0.4F)
                    for (int i = 0; i < 11; i++)
                    {
                        float t = 0.16F + i * 0.065F;
                        PointF q = Bz(lo, t);
                        float l2 = 5 + Rnd(i + 500) * 4, le = (t - 0.45F) * 14;
                        PointF tip = new PointF(q.X + le, q.Y + l2), c1 = new PointF(q.X + le * 0.2F, q.Y + l2 * 0.7F);
                        using (GraphicsPath one = new GraphicsPath())
                        {
                            AddQuad(one, new PointF(q.X - 0.8F, q.Y), c1, tip);
                            AddQuad(one, tip, new PointF(c1.X + 1.4F, c1.Y), new PointF(q.X + 0.8F, q.Y));
                            one.CloseFigure();
                            g.FillPath(lash, one);
                        }
                    }
            }
            using (Pen pen = new Pen(CA(235, 0x08030C), 1.8F)) g.DrawPath(pen, almond);
        }
    }

    // ---------- rendu ----------

    Bitmap Compose()
    {
        RectangleF v = View();
        int w = Math.Max(1, (int)Math.Ceiling(v.Width * scale)), h = Math.Max(1, (int)Math.Ceiling(v.Height * scale));
        Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        hits.Clear();
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-v.X, -v.Y);

            // silhouette et oeil : dessines dans le repere d'origine, puis retournes vers le bon coin
            bool animating = expanded && animDir != 0;
            if (animating)
            {
                if (panelCache == null) panelCache = RenderPanel(w, h, v);
                hits.Clear(); // rien n'est cliquable tant que le panneau bouge
                float ease = 1F - (1F - animT) * (1F - animT) * (1F - animT), grow = 0.3F + 0.7F * ease;
                PointF anchor = EyePoint();
                float px = (anchor.X - v.X) * scale, py = (anchor.Y - v.Y) * scale;
                GraphicsState raw = g.Save();
                g.ResetTransform();
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                using (ImageAttributes fade = new ImageAttributes())
                {
                    ColorMatrix cm = new ColorMatrix();
                    cm.Matrix33 = Math.Max(0F, Math.Min(1F, animT * 1.4F));
                    fade.SetColorMatrix(cm);
                    g.DrawImage(panelCache, new Rectangle((int)(px - px * grow), (int)(py - py * grow), (int)(w * grow), (int)(h * grow)),
                        0, 0, w, h, GraphicsUnit.Pixel, fade);
                }
                g.Restore(raw);
            }
            else if (expanded) DrawBodyFlipped(g);

            // l'oeil suit le coin choisi mais reste toujours a l'endroit : miroir horizontal seulement
            PointF ep = EyePoint();
            GraphicsState inner = g.Save();
            g.TranslateTransform(ep.X, ep.Y);
            g.ScaleTransform(flipX ? -EyeScale : EyeScale, EyeScale);
            g.TranslateTransform(-150F, -88F);
            DrawEye(g, Openness() * 0.86F, scale * EyeScale * 1.5F, !expanded);
            g.Restore(inner);

            // pastille des nouveautes importantes
            int fresh = main.FreshCount;
            if (fresh > 0)
            {
                PointF b = new PointF(ep.X + (flipX ? -82F : 82F), ep.Y - 35F);
                using (SolidBrush br = new SolidBrush(C(0xFF46A0))) g.FillEllipse(br, b.X - 11, b.Y - 11, 22, 22);
                using (Pen pen = new Pen(C(0x150A23), 1.5F)) g.DrawEllipse(pen, b.X - 11, b.Y - 11, 22, 22);
                TxtCenter(g, fresh > 99 ? "99" : fresh.ToString(), 13F, true, Color.White, b.X, b.Y - 9F);
            }

            // poignee de redimensionnement
            PointF gp = GripPoint();
            float sx = flipX ? -1 : 1, sy = 1;
            using (Pen pen = new Pen(CA(expanded ? 200 : 150, 0xB7A6C9), 1.4F))
            {
                g.DrawLine(pen, gp.X - 9 * sx, gp.Y + 1 * sy, gp.X + 1 * sx, gp.Y - 9 * sy);
                g.DrawLine(pen, gp.X - 4 * sx, gp.Y + 1 * sy, gp.X + 1 * sx, gp.Y - 4 * sy);
            }

            if (expanded && !animating)
            {
                DrawHeader(g);
                if (detail != null) DrawDetail(g); else DrawBoard(g);
            }
        }
        return bmp;
    }

    void DrawBodyFlipped(Graphics g)
    {
        GraphicsState state = g.Save();
        if (flipX) { g.TranslateTransform(DW, 0); g.ScaleTransform(-1, 1); }
        if (flipY) { g.TranslateTransform(0, DH); g.ScaleTransform(1, -1); }
        DrawBody(g);
        g.Restore(state);
    }

    // Le panneau complet (fond, en-tete, contenu) dans une image a part, pour l'animer d'un bloc.
    Bitmap RenderPanel(int w, int h, RectangleF v)
    {
        Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-v.X, -v.Y);
            DrawBodyFlipped(g);
            DrawHeader(g);
            if (detail != null) DrawDetail(g); else DrawBoard(g);
        }
        return bmp;
    }

    static PointF Lerp(PointF a, PointF b, float t)
    {
        return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }

    void DrawBody(Graphics g)
    {
        using (GraphicsPath p = new GraphicsPath())
        {
            // Le contour epouse le pli de la paupiere : c'est la meme courbe, decalee vers l'exterieur
            // d'une distance constante. Il part du coin interne de l'oeil et retombe au coin externe.
            const float gap = 9F, top = 72F;
            PointF[] crease = { new PointF(20, 72), new PointF(82, -6), new PointF(222, -8), new PointF(286, 66) };
            for (int i = 0; i < 4; i++)
                crease[i] = new PointF(EyeOrigin.X + crease[i].X * EyeScale, EyeOrigin.Y + crease[i].Y * EyeScale);
            const int steps = 20;
            PointF[] brow = new PointF[steps + 1];
            PointF firstDir = PointF.Empty, lastDir = PointF.Empty;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                PointF at = Bz(crease, t), next = Bz(crease, Math.Min(1F, t + 0.01F)), prev = Bz(crease, Math.Max(0F, t - 0.01F));
                float dx = next.X - prev.X, dy = next.Y - prev.Y, len = (float)Math.Sqrt(dx * dx + dy * dy);
                dx /= len; dy /= len;
                brow[i] = new PointF(at.X + dy * gap, at.Y - dx * gap); // normale vers le haut
                if (i == 0) firstDir = new PointF(dx, dy);
                if (i == steps) lastDir = new PointF(dx, dy);
            }
            PointF a = brow[0], z = brow[steps];
            // epaule : du bord gauche du panneau jusqu'au depart du pli, dans sa direction
            p.AddBezier(new PointF(14, 112), new PointF(14, 94), new PointF(a.X - firstDir.X * 18F, a.Y - firstDir.Y * 18F), a);
            p.AddCurve(brow, 0.5F);
            // conge : de la fin du pli jusqu'au bord haut du panneau
            p.AddBezier(z, new PointF(z.X + lastDir.X * 12F, z.Y + lastDir.Y * 12F), new PointF(z.X + 26F, top), new PointF(z.X + 44F, top));
            p.AddArc(632, top, 36, 36, 270, 90);
            p.AddArc(632, 542, 36, 36, 0, 90);
            p.AddArc(14, 542, 36, 36, 90, 90);
            p.CloseFigure();
            using (LinearGradientBrush skin = new LinearGradientBrush(new RectangleF(0, 0, DW, DH), C(0x3A1C5A), C(0x150A23), 90F))
            {
                ColorBlend blend = new ColorBlend();
                blend.Colors = new[] { C(0x3A1C5A), C(0x24113A), C(0x2C1042), C(0x8A2A66), C(0xF47C34) };
                blend.Positions = new[] { 0F, 0.2F, 0.58F, 0.82F, 1F };
                skin.InterpolationColors = blend;
                g.FillPath(skin, p);
            }
            using (Pen pen = new Pen(C(0x6B3FA0), 1.2F)) g.DrawPath(pen, p);
        }
    }

    // Bouton rond a pictogramme (les libelles complets restent dans le menu du clic droit).
    void IconPill(Graphics g, string glyph, float x, float y, Action a)
    {
        RectangleF r = new RectangleF(x, y, 34F, 26F);
        using (GraphicsPath p = RR(r, 13F)) using (Pen pen = new Pen(C(0x7B4AA6), 1F)) g.DrawPath(pen, p);
        using (Font f = new Font("Segoe UI Symbol", 14F, FontStyle.Regular, GraphicsUnit.Pixel))
        using (StringFormat sf = new StringFormat())
        {
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            g.DrawString(glyph, f, Brushes.White, r, sf);
        }
        Hit(r, a);
    }

    // Bouton rond vide, pour un pictogramme dessine a la main. Plein quand "active" est vrai.
    RectangleF IconBox(Graphics g, float x, float y, bool active, Action a)
    {
        RectangleF r = new RectangleF(x, y, 34F, 26F);
        using (GraphicsPath p = RR(r, 13F))
        {
            if (active) using (SolidBrush b = new SolidBrush(C(0xFF46A0))) g.FillPath(b, p);
            using (Pen pen = new Pen(C(active ? 0xFF46A0 : 0x7B4AA6), 1F)) g.DrawPath(pen, p);
        }
        Hit(r, a);
        return r;
    }

    void DrawHeader(Graphics g)
    {
        RectangleF h = HeaderRect();

        // ligne du haut : titre, puis les deux boutons de fenetre (epingler, reduire)
        float x = h.Right - 34F;
        // Epingle au premier plan : "reduire" replie le panneau et laisse l'oeil. Sinon : pres de l'horloge.
        RectangleF min = IconBox(g, x, h.Y - 2F, false, delegate { if (TopMost) Toggle(); else ToTray(); });
        using (Pen pen = new Pen(Color.White, 1.8F))
        {
            pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
            g.DrawLine(pen, min.X + 11, min.Y + 16, min.Right - 11, min.Y + 16);
        }
        x -= 34F + 8F;
        RectangleF pin = IconBox(g, x, h.Y - 2F, TopMost, delegate { TopMost = !TopMost; SaveSettings(); });
        float px = pin.X + 17, py = pin.Y + 13;
        using (Pen pen = new Pen(Color.White, 1.8F))
        {
            pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
            g.DrawLine(pen, px - 1, py + 1, px - 6, py + 6);      // la pointe
            g.DrawLine(pen, px - 4, py - 4, px + 4, py + 4);      // la collerette
        }
        g.FillEllipse(Brushes.White, px - 0.5F, py - 7F, 7.5F, 7.5F); // la tete
        Txt(g, Tr.S("title"), 18F, true, Color.White, h.X, h.Y, x - h.X - 10F);

        // ligne du bas : etat du releve, puis actualiser, tout lu, langue
        float y2 = h.Y + 28F, x2 = h.Right;
        string code = Tr.Codes[Tr.Lang];
        x2 -= TxtW(g, code, 12F, false) + 24F;
        Pill(g, code, x2, y2, delegate { SetLang((Tr.Lang + 1) % 3); });
        x2 -= 34F + 8F;
        IconPill(g, "✓", x2, y2, delegate { main.MarkRead(); });
        x2 -= 34F + 8F;
        IconPill(g, "↻", x2, y2, delegate { main.StartRefresh(); });
        string status = main.busy || main.checks == 0 ? Tr.S("checking")
            : Tr.F("status", main.checks, main.lastCheck, Math.Max(0, main.remaining));
        Txt(g, status, 12F, false, C(0xB7A6C9), h.X, y2 + 5F, x2 - h.X - 10F);
    }

    // Les sept etapes d'un projet : pastilles reliees, pleines, a moitie ou vides.
    void DrawStages(Graphics g, Entry e, RectangleF c, float cy)
    {
        Color grey = C(0x9A86B0);
        float cell = (c.Width - 8) / (float)Entry.Stages;
        for (int i = 0; i < Entry.Stages; i++)
        {
            float cx = c.X + 4 + cell * i + cell / 2F;
            double v = e.St[i];
            if (i < Entry.Stages - 1)
                using (Pen pen = new Pen(C(e.St[i + 1] >= 0.5 ? 0xFF46A0 : 0x3A1C5A), 2F)) g.DrawLine(pen, cx + 10, cy, cx + cell - 10, cy);
            if (v >= 0.99) using (SolidBrush b = new SolidBrush(C(0xFF46A0))) g.FillEllipse(b, cx - 9, cy - 9, 18, 18);
            else
            {
                using (SolidBrush b = new SolidBrush(C(0x2E1650))) g.FillEllipse(b, cx - 9, cy - 9, 18, 18);
                if (v >= 0.25) using (SolidBrush b = new SolidBrush(C(0xFF46A0))) g.FillPie(b, cx - 9, cy - 9, 18, 18, 90, 180);
                using (Pen pen = new Pen(C(v >= 0.25 ? 0xFF46A0 : 0x6E5E84), 1.4F)) g.DrawEllipse(pen, cx - 9, cy - 9, 18, 18);
            }
            using (SolidBrush b = new SolidBrush(v >= 0.25 ? Color.White : grey))
                g.DrawString(StageName(i + 1), F(11F, false), b, new RectangleF(cx - cell / 2F + 2, cy + 13, cell - 4, 30), SfCenter);
        }
    }

    static string StageName(int stage) { return Tr.S("st" + stage); }

    void DrawBlock(Graphics g, RectangleF r, int rank, Entry e, Color metal)
    {
        Fill(g, C(0x2E1650), r, 8F);
        Fill(g, metal, new RectangleF(r.X, r.Y, r.Width, 4F), 2F);
        using (SolidBrush b = new SolidBrush(metal)) g.FillEllipse(b, r.X + 11, r.Y + 15, 26, 26);
        TxtCenter(g, rank.ToString(), 14F, true, C(0x1A0C2B), r.X + 24, r.Y + 19F);
        Txt(g, e.Name, 14F, true, Color.White, r.X + 46, r.Y + 19F, r.Width - 54);
        Txt(g, e.ScoreText(), 26F, true, Color.White, r.X + 14, r.Y + 44F, r.Width - 20);
        Txt(g, StageName(e.TopStage()), 12F, false, C(0xC9B6DC), r.X + 16, r.Y + 76F, r.Width - 24);
        Hit(r, delegate { detail = e; newsScroll = 0; dirty = true; });
    }

    void DrawRow(Graphics g, RectangleF c, float y, int rank, Entry e)
    {
        Txt(g, rank + ".", 12F, false, C(0x9A86B0), c.X + 4, y, 24);
        Txt(g, e.Name, 13F, true, Color.White, c.X + 26, y - 1F, 150);
        RectangleF bar = new RectangleF(c.X + 184, y + 4F, 226, 8);
        Fill(g, C(0x2E1650), bar, 4F);
        if (e.Score > 0)
            using (GraphicsPath p = RR(new RectangleF(bar.X, bar.Y, Math.Max(8F, bar.Width * (float)e.Score / 100F), bar.Height), 4F))
            using (LinearGradientBrush lg = new LinearGradientBrush(bar, C(0xB03A9A), C(0xFF9A42), 0F))
            {
                ColorBlend blend = new ColorBlend();
                blend.Colors = new[] { C(0xB03A9A), C(0xFF46A0), C(0xFF9A42) };
                blend.Positions = new[] { 0F, 0.55F, 1F };
                lg.InterpolationColors = blend;
                g.FillPath(lg, p);
            }
        using (SolidBrush b = new SolidBrush(C(0x150A23)))
        {
            // un cran a chaque palier de l'echelle : 25, 40, 55, 70, 85
            for (int i = 1; i < Entry.Stages - 1; i++)
                g.FillRectangle(b, bar.X + Entry.Tick[i] * bar.Width / 100F - 0.5F, bar.Y, 1F, bar.Height);
        }
        Txt(g, e.ScoreText(), 13F, true, Color.White, bar.Right + 12, y - 1F, 60);
        TxtRight(g, StageName(e.TopStage()), 12F, false, C(0xC9B6DC), c.Right - 4, y);
        Hit(new RectangleF(c.X, y - 5F, c.Width, 24F), delegate { detail = e; newsScroll = 0; dirty = true; });
    }

    static void Describe(Evt e, out string head, out string rest)
    {
        switch (e.Kind)
        {
            case "commits": head = e.Count > 1 ? Tr.F("ev.commitsN", e.Subject, e.Count) : Tr.F("ev.commits1", e.Subject); rest = e.Detail; return;
            case "status": head = Tr.F("ev.status", e.Subject); rest = e.Detail; return;
            case "project": head = Tr.F("ev.project", e.Subject, e.Count); rest = e.Detail; return;
            case "news": head = "PortWatch: " + e.Subject; rest = e.Detail; return;
            case "change": head = "PortWatch: " + e.Subject; rest = ""; return;
            case "score":
                string[] parts = e.Detail.Split('|');
                head = Tr.F("ev.score", e.Subject, parts[0], parts.Length > 1 ? parts[1] : "?"); rest = ""; return;
            case "leader": head = Tr.F("ev.leader", e.Subject); rest = ""; return;
        }
        string text = e.Text ?? "";
        int cut = text.IndexOf('\n');
        head = cut < 0 ? text : text.Substring(0, cut);
        rest = cut < 0 ? "" : text.Substring(cut + 1).Replace("      ", "");
    }

    // Page principale : etapes du premier, podium, suite du classement, nouveautes.
    void DrawBoard(Graphics g)
    {
        RectangleF c = ContentRect();
        Color orange = C(0xFF9A42), grey = C(0x9A86B0), read = C(0xD8C8E6);

        if (ranking.Count == 0) Txt(g, Tr.S("loading"), 13F, false, grey, c.X + 4, c.Y + 60, 300);
        else DrawStages(g, ranking[0], c, c.Y + 10);

        float bx = c.X + 33, bottom = c.Y + 208;
        int[] order = { 1, 0, 2 };                      // 2e, 1er, 3e : le premier au centre
        float[] heights = { 146F, 116F, 96F };
        Color[] metals = { C(0xFFD46E), C(0xD7D2E0), C(0xE08A4A) };
        for (int slot = 0; slot < 3; slot++)
        {
            int k = order[slot];
            if (k >= ranking.Count) continue;
            DrawBlock(g, new RectangleF(bx + slot * 186, bottom - heights[k], 170, heights[k]), k + 1, ranking[k], metals[k]);
        }

        using (SolidBrush b = new SolidBrush(C(0x3A1C5A))) g.FillRectangle(b, c.X + 4, c.Y + 222, c.Width - 8, 2);
        Txt(g, Tr.S("ranking"), 13F, true, orange, c.X + 4, c.Y + 236, 300);
        for (int i = 3; i < 6 && i < ranking.Count; i++) DrawRow(g, c, c.Y + 262 + (i - 3) * 26, i + 1, ranking[i]);

        float ny = c.Y + 346;
        Txt(g, Tr.S("news"), 13F, true, orange, c.X + 4, ny, 300);
        RectangleF area = new RectangleF(c.X, ny + 24, c.Width, c.Bottom - ny - 24);
        GraphicsState state = g.Save();
        g.SetClip(area, CombineMode.Intersect);
        float y = area.Y - newsScroll;
        if (main.events.Count == 0) Txt(g, Tr.F("nothing", started), 13F, false, read, c.X + 4, y, c.Width - 8);
        foreach (Evt e in main.events)
        {
            string head, rest;
            Describe(e, out head, out rest);
            // sur le fond qui vire a l'orange, les elements deja lus sont en lilas clair plutot qu'en gris
            Color col = e.Fresh ? Color.White : read;
            if (y > area.Bottom + 60) { y += 24; continue; }
            Txt(g, e.When.ToString("HH:mm"), 12F, e.Fresh, col, c.X + 4, y + 1, 44);
            Txt(g, head, e.Major ? 14F : 13F, true, col, c.X + 48, y, c.Width - 56);
            y += 21;
            if (rest.Length > 0) y += Para(g, rest, 12F, e.Fresh ? C(0xF0E4F7) : read, c.X + 48, y, c.Width - 56, 48F) + 2;
            y += 6;
        }
        g.Restore(state);
        newsMax = Math.Max(0, y + newsScroll - area.Bottom);
    }

    // Fiche d'un projet : etapes, etat declare, derniers commits.
    void DrawDetail(Graphics g)
    {
        RectangleF c = ContentRect();
        Color orange = C(0xFF9A42), grey = C(0x9A86B0), lilac = C(0xD8C8E6);
        Entry e = detail;
        float w = Pill(g, "‹ " + Tr.S("back"), c.X + 4, c.Y - 2, delegate { detail = null; newsScroll = 0; dirty = true; });
        Txt(g, e.Name, 18F, true, Color.White, c.X + 16 + w, c.Y - 1, 300);
        TxtRight(g, e.ScoreText(), 22F, true, Color.White, c.Right - 4, c.Y - 4);

        string url = e.Url;
        float uw = Txt(g, url, 12F, false, C(0xFF8FC6), c.X + 4, c.Y + 34, c.Width - 8);
        using (Pen pen = new Pen(C(0xFF8FC6), 1F)) g.DrawLine(pen, c.X + 4, c.Y + 50, c.X + 4 + uw, c.Y + 50);
        Hit(new RectangleF(c.X + 4, c.Y + 32, uw, 20), delegate { OpenLink(url); });
        if (e.Missing) TxtRight(g, Tr.S("gone"), 12F, false, C(0xFF7878), c.Right - 4, c.Y + 34);
        else
        {
            // sourdine : plus aucune nouveaute pour ce depot (commits, etat, score)
            bool muted = main.IsMuted(e.Key);
            string label = Tr.S(muted ? "unmute" : "mute");
            float mw = TxtW(g, label, 12F, false) + 24F;
            if (muted) Fill(g, C(0x5A2A6E), new RectangleF(c.Right - 4 - mw, c.Y + 28, mw, 26F), 13F);
            string key = e.Key;
            Pill(g, label, c.Right - 4 - mw, c.Y + 28, delegate { ToggleMute(key); });
        }

        Txt(g, Tr.S("stages"), 13F, true, orange, c.X + 4, c.Y + 66, 200);
        DrawStages(g, e, c, c.Y + 102);

        RectangleF area = new RectangleF(c.X, c.Y + 154, c.Width, c.Bottom - c.Y - 154);
        GraphicsState state = g.Save();
        g.SetClip(area, CombineMode.Intersect);
        float y = area.Y - newsScroll;
        Project p = e.Proj;
        if (p != null && p.Error != null) { Txt(g, Tr.F("error", p.Error), 12F, false, C(0xFF7878), c.X + 4, y, c.Width - 8); y += 22; }
        if (p != null && !string.IsNullOrEmpty(p.Status))
        {
            Txt(g, Tr.S("declared"), 13F, true, orange, c.X + 4, y, 400); y += 22;
            y += Para(g, p.Status, 12.5F, lilac, c.X + 4, y, c.Width - 8, 600F) + 12;
        }
        else if (e.Summary.Length > 0)
        {
            Txt(g, Tr.S("summary"), 13F, true, orange, c.X + 4, y, 400); y += 22;
            y += Para(g, e.Summary, 12.5F, lilac, c.X + 4, y, c.Width - 8, 600F) + 12;
        }
        if (p != null && p.Commits.Count > 0)
        {
            Txt(g, Tr.S("commits"), 13F, true, orange, c.X + 4, y, 400); y += 22;
            foreach (Commit k in p.Commits)
            {
                bool fresh = string.CompareOrdinal(k.Updated, p.Baseline) > 0;
                Color col = fresh ? Color.White : grey;
                DateTime d;
                string when = DateTime.TryParse(k.Updated, null, DateTimeStyles.AdjustToUniversal, out d) ? d.ToLocalTime().ToString("HH:mm") : "";
                Txt(g, when, 12F, false, col, c.X + 4, y, 44);
                Txt(g, k.Title, 12F, fresh, col, c.X + 48, y, c.Width - 56);
                y += 19;
            }
        }
        g.Restore(state);
        newsMax = Math.Max(0, y + newsScroll - area.Bottom);
    }

    void Redraw()
    {
        dirty = false;
        if (!IsHandleCreated || !Visible) return;
        using (Bitmap bmp = Compose())
        {
            IntPtr screen = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(screen), hBmp = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                old = SelectObject(mem, hBmp);
                POINT pos = new POINT(Left, Top), src = new POINT(0, 0);
                SIZE size = new SIZE(bmp.Width, bmp.Height);
                BLENDFUNCTION blend = new BLENDFUNCTION { Op = 0, Flags = 0, Alpha = 255, Format = 1 };
                UpdateLayeredWindow(Handle, screen, ref pos, ref size, mem, ref src, 0, ref blend, 2);
            }
            finally
            {
                if (hBmp != IntPtr.Zero) { SelectObject(mem, old); DeleteObject(hBmp); }
                DeleteDC(mem);
                ReleaseDC(IntPtr.Zero, screen);
            }
        }
    }

    // Enregistre le rendu sur fond sombre (option --snap, pour verifier l'affichage).
    void Snapshot(string path)
    {
        try
        {
            using (Bitmap bmp = Compose())
            using (Bitmap flat = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format24bppRgb))
            {
                using (Graphics g = Graphics.FromImage(flat)) { g.Clear(Color.FromArgb(70, 76, 88)); g.DrawImage(bmp, 0, 0); }
                flat.Save(path, ImageFormat.Png);
            }
        }
        catch (Exception) { } // chemin invalide : la capture de verification est simplement absente
    }

    // ---------- actions ----------

    static void OpenLink(string url)
    {
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
        try { Process.Start(url); } catch (Exception) { }
    }

    void SetLang(int lang)
    {
        Tr.Lang = lang;
        SaveSettings();
        dirty = true;
    }

    // L'oeil seul, dans un carre : sert d'icone (barre des taches, zone de notification, fichier .ico).
    Bitmap EyeBitmap(int size, float openness)
    {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        float keepX = look_x, keepY = look_y;
        look_x = 0; look_y = 0;
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            float k = size / 304F;                       // l'oeil fait 300 de large, avec une petite marge
            g.TranslateTransform(2F * k, (size - 170F * k) / 2F + 8F * k);
            g.ScaleTransform(k, k);
            DrawEye(g, openness * 0.86F, k * 1.5F, false);
        }
        look_x = keepX; look_y = keepY;
        return bmp;
    }

    Icon MakeIcon(float openness)
    {
        using (Bitmap bmp = EyeBitmap(64, openness)) return Icon.FromHandle(bmp.GetHicon());
    }

    // Ecrit un fichier .ico a plusieurs tailles (images PNG), pour l'icone du programme lui-meme.
    void WriteIco(string path)
    {
        int[] sizes = { 16, 24, 32, 48, 64, 256 };
        List<byte[]> images = new List<byte[]>();
        foreach (int size in sizes)
            using (Bitmap bmp = EyeBitmap(size, 1F))
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                images.Add(ms.ToArray());
            }
        using (BinaryWriter w = new BinaryWriter(File.Create(path)))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                w.Write(images[i].Length); w.Write(offset);
                offset += images[i].Length;
            }
            foreach (byte[] image in images) w.Write(image);
        }
    }

    // Masque l'oeil : il ne reste que l'icone pres de l'horloge, qui s'ouvre quand il y a du nouveau.
    void ToTray()
    {
        if (animDir != 0) return;
        Hide();
        UpdateTray();
    }

    void FromTray()
    {
        Show();
        Place();
        Redraw();
        Activate();
    }

    void UpdateTray()
    {
        if (tray == null) return;
        int fresh = main.FreshCount;
        if (fresh == trayFresh) return;
        bool more = fresh > trayFresh && trayFresh >= 0;
        trayFresh = fresh;
        tray.Icon = fresh > 0 ? iconOpen : iconClosed;
        string tip = fresh > 0 ? Tr.F("tip.some", fresh) : Tr.S("tip.none");
        tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        // Fenetre masquee et nouveaute importante : une bulle la signale.
        if (more && !Visible && main.events.Count > 0)
        {
            string head, rest;
            Describe(main.events[0], out head, out rest);
            tray.ShowBalloonTip(5000, "GTA VI recomp PC status", head, ToolTipIcon.None);
        }
    }

    void FreePanel()
    {
        if (panelCache != null) { panelCache.Dispose(); panelCache = null; }
    }

    // Le panneau sort de l'oeil (ou y rentre) en grandissant et en apparaissant progressivement.
    void Toggle()
    {
        if (animDir != 0) return;
        detail = null;
        newsScroll = 0;
        FreePanel();
        if (!expanded) { expanded = true; Place(); animT = 0F; animDir = 1; }
        else { animT = 1F; animDir = -1; }
        Redraw();
    }

    void ShowMenu(Point screen)
    {
        ContextMenuStrip menu = new ContextMenuStrip();
        if (Visible)
        {
            menu.Items.Add(Tr.S("m.toggle"), null, delegate { Toggle(); });
            menu.Items.Add(Tr.S("m.tray"), null, delegate { ToTray(); });
        }
        else menu.Items.Add(Tr.S("m.show"), null, delegate { FromTray(); });
        menu.Items.Add(Tr.S("refresh"), null, delegate { main.StartRefresh(); });
        menu.Items.Add(Tr.S("markread"), null, delegate { main.MarkRead(); });
        menu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem lang = new ToolStripMenuItem(Tr.S("m.lang"));
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            ToolStripMenuItem item = new ToolStripMenuItem(Tr.Names[i], null, delegate { SetLang(index); });
            item.Checked = Tr.Lang == i;
            lang.DropDownItems.Add(item);
        }
        menu.Items.Add(lang);
        ToolStripMenuItem top = new ToolStripMenuItem(Tr.S("m.top"), null, delegate { TopMost = !TopMost; SaveSettings(); });
        top.Checked = TopMost;
        menu.Items.Add(top);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Tr.S("m.quit"), null, delegate { Close(); });
        menu.Show(screen);
    }

    Action HitAt(PointF d)
    {
        for (int i = hits.Count - 1; i >= 0; i--) if (hits[i].Key.Contains(d)) return hits[i].Value;
        return null;
    }

    bool OnGrip(PointF d)
    {
        PointF gp = GripPoint();
        return Math.Abs(d.X - gp.X) < 12 && Math.Abs(d.Y - gp.Y) < 12;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Right) { ShowMenu(Cursor.Position); return; }
        if (e.Button != MouseButtons.Left) return;
        PointF d = ToDesign(e.Location);
        down = true;
        moved = false;
        downScreen = Cursor.Position;
        downEyeX = eyeX; downEyeY = eyeY; downScale = scale;
        resizing = OnGrip(d);
        downOnHit = !resizing && HitAt(d) != null;
        float dx = downScreen.X - eyeX, dy = downScreen.Y - eyeY;
        downDist = Math.Max(20F, (float)Math.Sqrt(dx * dx + dy * dy));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        PointF d = ToDesign(e.Location);
        if (!down)
        {
            Cursor = OnGrip(d) ? (flipX ? Cursors.SizeNESW : Cursors.SizeNWSE) : HitAt(d) != null || EyeHit().Contains(d) ? Cursors.Hand : Cursors.Default;
            return;
        }
        Point now = Cursor.Position;
        int mx = now.X - downScreen.X, my = now.Y - downScreen.Y;
        if (!moved && Math.Abs(mx) < 5 && Math.Abs(my) < 5) return;
        moved = true;
        if (resizing)
        {
            float dx = now.X - eyeX, dy = now.Y - eyeY;
            scale = Math.Max(0.5F, Math.Min(2.5F, downScale * (float)Math.Sqrt(dx * dx + dy * dy) / downDist));
        }
        else if (!downOnHit)
        {
            eyeX = downEyeX + mx;
            eyeY = downEyeY + my;
        }
        else return;
        Place();
        Redraw();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !down) return;
        down = false;
        if (moved) { SaveSettings(); return; }
        PointF d = ToDesign(e.Location);
        Action a = HitAt(d);
        if (a != null) { a(); dirty = true; }
        else if (EyeHit().Contains(d)) Toggle();
    }
}
