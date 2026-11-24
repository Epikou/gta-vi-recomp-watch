// Fenetre de suivi des projets GTA VI -> PC.
// Interroge GitHub toutes les 4 minutes (flux Atom des commits, README, recherche publique)
// et met en avant ce qui a change. Aucun compte, aucune cle : lecture seule de pages publiques.
//
// Compilation (csc de .NET Framework, present sur tout Windows) : voir build.cmd
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml;

class Commit
{
    public string Updated, Author, Title;
    public string Id = ""; // identifiant du commit dans le flux : sert a reconnaitre ce qui est deja vu
}

class Project
{
    public string Name, Repo, Branch, StatusPath; // StatusPath nul : on ne suit que les commits
    public string Note;                           // description courte, pour les nouveaux acteurs
    public Func<string, string> Extract;
    public string Status, Error;
    public string LastCommit = "";
    public string Baseline = "";  // dernier commit deja vu : ce qui est plus recent est "nouveau"
    public bool StatusFresh;      // etat modifie et pas encore marque comme lu
    public bool Loaded;
    public List<Commit> Commits = new List<Commit>();
    public HashSet<string> Seen = new HashSet<string>(); // commits deja vus, par identifiant
    public string PendingStatus;                          // nouvel etat lu une fois, en attente de confirmation
    public int PendingCount;
}

class Other
{
    public string FullName, Desc, Created;
    public int Stars;
    public bool IsNew;
}

class News
{
    public string Id, Date, Title, Dek;
}

// Projet tel que le decrit PortWatch VI.
class Effort
{
    public string Name, Repo, Stage, Summary;
    public double World, Playable, Story, Render; // suite de l'echelle et pistes de qualite (histoire, rendu)
    public double Loads, Boots, Menu, InGame; // etapes PortWatch, de 0 a 1 (0,5 = credit partiel)
    public double Fps;                        // images par seconde en jeu selon PortWatch (0 = inconnu)
    public double Groundwork;                 // part du travail preparatoire, de 0 a 1
    public string Stability = "unknown";
    public bool Nuked;
    public string Tier = "", Branch = "main";
    public string Id = "", Link = ""; // Link : article PortWatch, pour un projet sans depot public
    public int Stars;
    public double Manual = -1; // score fixe a la main par PortWatch ("manualScore"), -1 s'il n'y en a pas
    public bool Missing;
}

class Evt
{
    public DateTime When;
    public string Text;
    public bool Major;
    public bool Fresh = true; // pas encore marque comme lu
    // Forme structuree, pour que l'affichage puisse traduire le titre de l'evenement.
    public string Kind = "", Subject = "", Detail = "";
    public int Count;
}

// Palette inspiree du visuel de GTA VI : violet nuit, rose, orange coucher de soleil.
static class Pal
{
    public static readonly Color Night = Color.FromArgb(22, 9, 38);
    public static readonly Color Plum = Color.FromArgb(44, 18, 72);
    public static readonly Color PlumHi = Color.FromArgb(66, 28, 104);
    public static readonly Color Purple = Color.FromArgb(112, 36, 178);
    public static readonly Color Pink = Color.FromArgb(255, 70, 160);
    public static readonly Color Orange = Color.FromArgb(255, 150, 66);
    public static readonly Color Sun = Color.FromArgb(255, 212, 110);
    public static readonly Color Text = Color.FromArgb(248, 236, 246);
    public static readonly Color Dim = Color.FromArgb(184, 156, 204);
    public static readonly Color Grey = Color.FromArgb(128, 116, 142);
    public static readonly Color Bad = Color.FromArgb(255, 120, 120);
}

class GradientBar : FlowLayoutPanel
{
    public GradientBar()
    {
        ResizeRedraw = true;
        DoubleBuffered = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Rectangle r = ClientRectangle;
        if (r.Width <= 0 || r.Height <= 0) return;
        using (LinearGradientBrush b = new LinearGradientBrush(r, Pal.Purple, Pal.Orange, 0F))
        {
            ColorBlend blend = new ColorBlend();
            blend.Colors = new Color[] { Pal.Purple, Pal.Pink, Pal.Orange };
            blend.Positions = new float[] { 0F, 0.55F, 1F };
            b.InterpolationColors = blend;
            e.Graphics.FillRectangle(b, r);
        }
    }
}

static class Program
{
    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();

    public static bool Demo;

    [STAThread]
    static void Main(string[] args)
    {
        Demo = Array.IndexOf(args, "--demo") >= 0;
        SetProcessDPIAware();
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
        Application.EnableVisualStyles();
        // L'oeil est la fenetre principale ; la fenetre de details s'ouvre en cliquant dessus.
        // MainForm n'est plus affichee : elle sert de moteur de releve. EyeWindow dessine tout.
        MainForm main = new MainForm();
        EyeWindow eye = new EyeWindow(main, args);
        main.Start();
        Application.Run(eye);
    }

    // Vrai quand l'oeil se ferme : la fenetre de details doit alors se fermer pour de bon.
    public static bool Exiting;
    public static bool Headless = true;
}

class MainForm : Form
{
    const int PeriodSeconds = 30;
    const int SearchEvery = 8; // recherche de nouveaux depots : un releve sur 8 (toutes les 4 min)
    const int TrackerEvery = 4; // PortWatch VI : un releve sur 4 (toutes les 2 min)
    const string TrackerSite = "https://portwatch-vi.xyz/";
    const int SectionCount = 7; // 0..2 projets, 3 nouveaux acteurs, 4 autres projets, 5 PortWatch VI, 6 aide
    const int MinStars = 25;
    const string Collapsed = "▶";
    const string Expanded = "▼";

    [StructLayout(LayoutKind.Sequential)]
    struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags, uCount, dwTimeout;
    }

    [DllImport("user32.dll")]
    static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    internal readonly List<Project> projects = new List<Project>();
    internal readonly List<Evt> events = new List<Evt>();
    readonly HashSet<string> seenOthers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> freshOthers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    List<Other> others = new List<Other>();
    string othersError;
    bool othersLoaded;

    // PortWatch VI : site communautaire qui suit tous les projets (data/tracker.json)
    readonly HashSet<string> seenNews = new HashSet<string>();
    readonly HashSet<string> seenChanges = new HashSet<string>();
    readonly HashSet<string> freshNews = new HashSet<string>();
    // Nouveaux acteurs : commits releves toutes les 2 minutes, sans evenement (ils n'ouvrent pas l'oeil).
    internal List<Project> actors = new List<Project>(); // remplacee d'un bloc a chaque synchronisation
    // Depots mis en sourdine par l'utilisateur : releves, mais sans aucun evenement.
    internal readonly HashSet<string> Muted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    bool actorsLoaded, actorsDirty;
    internal List<Effort> pwEfforts = new List<Effort>();
    internal List<News> pwNews = new List<News>();
    List<string> pwChanges = new List<string>();
    string pwUpdated = "", pwError;
    bool pwLoaded;

    // Appele apres chaque releve et apres "Marquer comme lu" (l'oeil s'y abonne).
    public Action StateChanged;

    // Nouveautes importantes non lues (etat modifie, nouveau projet, article) : ce sont elles qui ouvrent l'oeil.
    public int FreshCount
    {
        get { int n = 0; foreach (Evt e in events) if (e.Fresh && e.Major) n++; return n; }
    }

    // Simples commits non lus : visibles dans les details, sans ouvrir l'oeil.
    public int MinorFreshCount
    {
        get { int n = 0; foreach (Evt e in events) if (e.Fresh && !e.Major) n++; return n; }
    }

    readonly RichTextBox news = new RichTextBox();
    readonly RichTextBox details = new RichTextBox();
    readonly List<Button> sectionButtons = new List<Button>();
    readonly Label lblStatus = new Label();
    readonly Button btnRefresh = new Button();
    readonly Button btnRead = new Button();
    readonly CheckBox chkTop = new CheckBox();
    readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer();
    readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
    readonly DateTime started = DateTime.Now;
    internal DateTime lastCheck = DateTime.MinValue;
    internal int remaining;
    int unread;
    internal int checks;
    internal string lastResult = "";
    string lastErrors = "";
    int openSection = -1;
    internal bool busy;

    // Depots deja examines : affiches sans la mention NOUVEAU.
    static readonly string[] Known = {
        "projectleonida/leonida", "r0y00/xbox2windows", "freminet/scarlettx", "sammwyy/xvalkyrie",
        "jadeowaowa/scarlett-layer", "trinitronclient/zenith-x", "t-crypt/bathyal", "dom-in/leonida",
        "etoile-bleu/leonida-reviews", "ahmet-dedeler/awesome-gta6-reverse-engineering", "hylanderry/dedsec",
        "fablinoo/palmwave", "omonkeyz/xbox-emulator", "rpatty-dev/percyscarlett", "onlymoisties/gta6-boottest",
        "californiaalmonds/openscarlett", "leonidareforge/leonida", "ubiquitarianism/leonida", "luizwidmer/leonida-work"
    };

    public MainForm()
    {
        projects.Add(new Project { Name = "VIctory", Repo = "Marvisak/VIctory", Branch = "main",
            StatusPath = "README.md", Extract = ExtractVictory });
        projects.Add(new Project { Name = "XCL", Repo = "xcl-dev/xcl", Branch = "main",
            StatusPath = "README.md", Extract = ExtractXcl });
        projects.Add(new Project { Name = "TuxBox", Repo = "brcly/TuxBox", Branch = "main",
            StatusPath = "docs/PROGRESS.md", Extract = ExtractTuxBox });

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "GTA VI sur PC - suivi";
        BackColor = Pal.Night;
        ForeColor = Pal.Text;
        Font = new Font("Segoe UI", 10F);
        ClientSize = new Size(760, 900);
        MinimumSize = new Size(520, 420);
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;

        // --- bandeau ---
        GradientBar bar = new GradientBar();
        bar.Dock = DockStyle.Top;
        bar.AutoSize = true;
        bar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        bar.Padding = new Padding(12, 8, 12, 10);

        Label title = new Label();
        title.Text = "GTA VI SUR PC";
        title.Font = new Font("Segoe UI Black", 20F, FontStyle.Bold);
        title.ForeColor = Color.White;
        title.BackColor = Color.Transparent;
        title.AutoSize = true;
        title.Margin = new Padding(0, 0, 0, 4);
        bar.Controls.Add(title);
        bar.SetFlowBreak(title, true);

        StyleBarButton(btnRefresh, "Actualiser");
        btnRefresh.Click += delegate { StartRefresh(); };
        StyleBarButton(btnRead, "Marquer comme lu");
        btnRead.Click += delegate { MarkRead(); };

        chkTop.Text = "Toujours au premier plan";
        chkTop.Checked = true;
        chkTop.AutoSize = true;
        chkTop.BackColor = Color.Transparent;
        chkTop.ForeColor = Color.White;
        chkTop.Margin = new Padding(10, 7, 10, 0);
        chkTop.CheckedChanged += delegate { TopMost = chkTop.Checked; };

        lblStatus.AutoSize = true;
        lblStatus.BackColor = Color.Transparent;
        lblStatus.ForeColor = Color.White;
        lblStatus.Margin = new Padding(4, 8, 0, 0);

        bar.Controls.Add(btnRefresh);
        bar.Controls.Add(btnRead);
        bar.Controls.Add(chkTop);
        bar.Controls.Add(lblStatus);

        // --- nouveautes (zone principale) ---
        StyleBox(news);
        Panel body = new Panel();
        body.Dock = DockStyle.Fill;
        body.Padding = new Padding(16, 12, 6, 6);
        body.Controls.Add(news);

        // --- volets repliables ---
        Panel bottom = new Panel();
        bottom.Dock = DockStyle.Bottom;
        bottom.AutoSize = true;
        bottom.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        bottom.BackColor = Pal.Plum;

        StyleBox(details);
        details.BackColor = Pal.Plum;
        Panel detailsHost = new Panel();
        detailsHost.Dock = DockStyle.Top;
        detailsHost.Height = 300;
        detailsHost.Padding = new Padding(16, 8, 6, 8);
        detailsHost.BackColor = Pal.Plum;
        detailsHost.Visible = false;
        detailsHost.Controls.Add(details);
        details.Tag = detailsHost;

        // Dock Top : le dernier controle ajoute se place le plus haut.
        bottom.Controls.Add(detailsHost);
        for (int i = SectionCount - 1; i >= 0; i--)
        {
            Button b = new Button();
            int index = i;
            b.Dock = DockStyle.Top;
            b.Height = 34;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Pal.PlumHi;
            b.BackColor = Pal.Plum;
            b.ForeColor = Pal.Text;
            b.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(10, 0, 0, 0);
            b.Cursor = Cursors.Hand;
            b.Click += delegate { ToggleSection(index); };
            sectionButtons.Insert(0, b);
            bottom.Controls.Add(b);
        }
        Panel rule = new Panel();
        rule.Dock = DockStyle.Top;
        rule.Height = 2;
        rule.BackColor = Pal.Pink;
        bottom.Controls.Add(rule);

        Controls.Add(body);
        Controls.Add(bottom);
        Controls.Add(bar);

        tick.Interval = 1000;
        tick.Tick += delegate { OnTick(); };
        Activated += delegate { if (unread > 0) { unread = 0; UpdateTitle(); } };
        // Fermer la fenetre de details la masque seulement : c'est l'oeil qui quitte le programme.
        FormClosing += delegate(object s, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !Program.Exiting) { e.Cancel = true; Hide(); }
        };
    }

    internal bool IsMuted(string repo)
    {
        lock (Muted) return Muted.Contains(repo ?? "");
    }

    // Les projets suivis en direct sont ceux que PortWatch classe "tracked" (une douzaine), hors depots disparus.
    // Un projet deja suivi garde son objet, donc la memoire de ses commits deja vus.
    void SyncActors(List<Effort> efforts)
    {
        Dictionary<string, Project> current = new Dictionary<string, Project>(StringComparer.OrdinalIgnoreCase);
        foreach (Project a in actors) current[a.Repo] = a;
        List<Project> next = new List<Project>();
        foreach (Effort x in efforts)
        {
            if (x.Tier != "tracked" || x.Missing || x.Nuked) continue;
            if (!Regex.IsMatch(x.Repo ?? "", @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) continue;
            bool primary = false;
            foreach (Project p in projects) if (string.Equals(p.Repo, x.Repo, StringComparison.OrdinalIgnoreCase)) primary = true;
            if (primary) continue;
            Project a;
            if (!current.TryGetValue(x.Repo, out a)) a = new Project { Repo = x.Repo };
            a.Name = x.Name;
            a.Branch = string.IsNullOrEmpty(x.Branch) ? "main" : x.Branch;
            a.Note = x.Summary;
            next.Add(a);
        }
        actors = next;
    }

    // Demarre les releves sans afficher la fenetre de details.
    public void Start()
    {
        IntPtr handle = Handle; // force la creation de la fenetre, necessaire a BeginInvoke
        Render();
        StartRefresh();
        tick.Start();
    }

    public void ShowDetails()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    void Notify()
    {
        if (StateChanged != null) StateChanged();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int on = 1;
            DwmSetWindowAttribute(Handle, 20, ref on, 4);          // barre de titre sombre
            int caption = 0x00260916;                              // COLORREF 0x00BBGGRR = violet nuit
            DwmSetWindowAttribute(Handle, 35, ref caption, 4);     // couleur de la barre (Windows 11)
            int text = 0x00FFFFFF;
            DwmSetWindowAttribute(Handle, 36, ref text, 4);
        }
        catch (Exception) { } // dwmapi absent ou attribut inconnu : sans importance
    }

    void StyleBarButton(Button b, string text)
    {
        b.Text = text;
        b.AutoSize = true;
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Color.White;
        b.FlatAppearance.MouseOverBackColor = Pal.PlumHi;
        b.BackColor = Pal.Night;
        b.ForeColor = Color.White;
        b.Padding = new Padding(8, 2, 8, 2);
        b.Margin = new Padding(0, 0, 8, 0);
    }

    void StyleBox(RichTextBox r)
    {
        r.Dock = DockStyle.Fill;
        r.ReadOnly = true;
        r.BorderStyle = BorderStyle.None;
        r.BackColor = Pal.Night;
        r.ForeColor = Pal.Text;
        r.DetectUrls = true;
        r.ScrollBars = RichTextBoxScrollBars.Vertical;
        r.LinkClicked += delegate(object s, LinkClickedEventArgs e) { OpenLink(e.LinkText); };
    }

    static void OpenLink(string url)
    {
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
        try { Process.Start(url); } catch (Exception) { }
    }

    // Tout ce qui etait nouveau (blanc) devient deja vu (gris) ; rien n'est efface.
    public void MarkRead()
    {
        freshNews.Clear();
        foreach (Evt e in events) e.Fresh = false;
        foreach (Project p in projects) { p.Baseline = p.LastCommit; p.StatusFresh = false; }
        foreach (Project a in actors) a.Baseline = a.LastCommit;
        freshOthers.Clear();
        unread = 0;
        UpdateTitle();
        Render();
        Notify();
    }

    void ToggleSection(int index)
    {
        openSection = openSection == index ? -1 : index;
        ((Panel)details.Tag).Visible = openSection >= 0;
        RenderSections();
        ScrollTop(news); // la zone des nouveautes vient de changer de hauteur
    }

    // ---------- releve ----------

    void OnTick()
    {
        if (busy) return;
        remaining--;
        if (remaining <= 0) { StartRefresh(); return; }
        lblStatus.Text = string.Format("Relevé n° {0} à {1:HH:mm:ss} : {2}  -  prochain dans {3} s",
            checks, lastCheck, lastResult, remaining);
    }

    public void StartRefresh()
    {
        if (busy) return;
        busy = true;
        btnRefresh.Enabled = false;
        btnRefresh.Text = "Relevé...";
        lblStatus.Text = "Relevé en cours...";
        bool withSearch = checks % SearchEvery == 0;
        bool withTracker = checks % TrackerEvery == 0;
        ThreadPool.QueueUserWorkItem(delegate
        {
            Stopwatch watch = Stopwatch.StartNew();
            List<Evt> found = new List<Evt>();
            foreach (Project p in projects) LoadProject(p, found);
            if (withSearch) LoadOthers(found);
            if (withTracker)
            {
                LoadTracker(found);
                foreach (Project a in actors) LoadProject(a, found);
                actorsDirty = true;
                actorsLoaded = true;
            }
            // Un releve trop rapide ne se voit pas : on laisse le bouton en attente au moins 0,7 s.
            if (watch.ElapsedMilliseconds < 700) Thread.Sleep(700 - (int)watch.ElapsedMilliseconds);
            try
            {
                BeginInvoke((MethodInvoker)delegate { OnRefreshed(found); });
            }
            catch (InvalidOperationException) { } // fenetre fermee pendant le releve
        });
    }

    void OnRefreshed(List<Evt> found)
    {
        busy = false;
        btnRefresh.Enabled = true;
        btnRefresh.Text = "Actualiser";
        lastCheck = DateTime.Now;
        checks++;
        lastResult = found.Count == 0 ? "aucun changement"
            : found.Count == 1 ? "1 nouveauté" : found.Count + " nouveautés";
        remaining = PeriodSeconds;
        // Le meme evenement deja signale il y a moins de dix minutes n'est pas repete.
        found.RemoveAll(delegate(Evt n)
        {
            if (n.Kind.Length == 0) return false;
            foreach (Evt o in events)
                if (o.Kind == n.Kind && o.Subject == n.Subject && o.Detail == n.Detail && (n.When - o.When).TotalMinutes < 10) return true;
            return false;
        });
        if (found.Count > 0)
        {
            // Les changements importants d'abord.
            found.Sort(delegate(Evt a, Evt b) { return b.Major.CompareTo(a.Major); });
            if (found.Count > 14) found.RemoveRange(14, found.Count - 14); // jamais d'avalanche en un seul releve
            events.InsertRange(0, found);
            if (events.Count > 60) events.RemoveRange(60, events.Count - 60);
            unread += found.Count;
            bool major = false;
            foreach (Evt e in found) if (e.Major) major = true;
            if (major) SystemSounds.Exclamation.Play();
            if (!ContainsFocus) Flash();
        }
        if (Program.Demo && events.Count == 0)
        {
            // Apercu de l'affichage (lancement avec --demo) : faux evenements, clairement marques.
            events.Add(new Evt { When = DateTime.Now, Major = true,
                Text = "[DÉMO] VIctory : l'état GTA VI a changé\n      The world : first frames visible, 4 fps" });
            events.Add(new Evt { When = DateTime.Now, Major = false,
                Text = "[DÉMO] XCL : 2 nouveaux commits\n      - Minimap mesh pipelines accepted\n      - Merge PR #310" });
            events.Add(new Evt { When = DateTime.Now.AddMinutes(-9), Major = false, Fresh = false,
                Text = "[DÉMO] TuxBox : 1 nouveau commit (déjà lu)\n      - Prewarm shaders" });
            projects[0].StatusFresh = true;
            if (projects[0].Commits.Count > 2) projects[0].Baseline = projects[0].Commits[2].Updated;
            ToggleSection(0);
        }
        // On ne redessine que si quelque chose a change : sinon le defilement sauterait toutes les 30 s.
        StringBuilder err = new StringBuilder(othersError + "|" + pwError);
        foreach (Project p in projects) err.Append("|" + p.Error);
        bool redraw = found.Count > 0 || checks == 1 || actorsDirty || err.ToString() != lastErrors || Program.Demo;
        actorsDirty = false;
        lastErrors = err.ToString();
        UpdateTitle();
        if (redraw) Render();
        OnTick();
        Notify();
    }

    void UpdateTitle()
    {
        Text = unread > 0
            ? string.Format("({0}) GTA VI sur PC - suivi", unread)
            : "GTA VI sur PC - suivi";
    }

    void Flash()
    {
        FLASHWINFO fi = new FLASHWINFO();
        fi.cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO));
        fi.hwnd = Handle;
        fi.dwFlags = 3 | 12; // FLASHW_ALL | FLASHW_TIMERNOFG
        fi.uCount = uint.MaxValue;
        FlashWindowEx(ref fi);
    }

    static string Fetch(string url)
    {
        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
        req.UserAgent = "gta-pc-watch";
        req.Timeout = 30000;
        using (WebResponse resp = req.GetResponse())
        using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            return sr.ReadToEnd();
    }

    static string Normalize(string text)
    {
        StringBuilder sb = new StringBuilder();
        foreach (string line in (text ?? "").Replace("\r", "").Split('\n'))
        {
            string t = Regex.Replace(line, @"[ \t]+", " ").Trim();
            if (t.Length > 0) sb.Append(sb.Length > 0 ? "\n" : "").Append(t);
        }
        return sb.ToString();
    }

    void LoadProject(Project p, List<Evt> found)
    {
        bool muted = IsMuted(p.Repo);
        try
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(Fetch("https://github.com/" + p.Repo + "/commits/" + p.Branch + ".atom"));
            XmlNamespaceManager ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("a", "http://www.w3.org/2005/Atom");
            List<Commit> all = new List<Commit>();
            foreach (XmlNode n in doc.SelectNodes("//a:entry", ns))
            {
                Commit c = new Commit();
                c.Updated = NodeText(n, "a:updated", ns);
                c.Author = NodeText(n, "a:author/a:name", ns);
                c.Title = Regex.Replace(NodeText(n, "a:title", ns), @"\s+", " ").Trim();
                c.Id = NodeText(n, "a:id", ns);
                if (c.Id.Length == 0) c.Id = c.Updated + "|" + c.Title;
                all.Add(c);
            }
            // Un flux vide n'est pas "plus aucun commit" : c'est une lecture ratee. On garde l'etat precedent.
            if (all.Count == 0) throw new InvalidDataException("flux de commits vide");

            // Nouveau = identifiant jamais vu. Les dates ne suffisent pas : une PR fusionnee apporte
            // souvent des commits plus anciens que le dernier releve.
            string newest = p.LastCommit;
            List<Commit> fresh = new List<Commit>();
            foreach (Commit c in all)
            {
                if (!p.Seen.Contains(c.Id)) fresh.Add(c);
                if (string.CompareOrdinal(c.Updated, newest) > 0) newest = c.Updated;
            }
            if (p.Loaded && fresh.Count > 0 && !muted)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(p.Name + " : " + (fresh.Count > 1 ? fresh.Count + " nouveaux commits" : "1 nouveau commit"));
                List<string> titles = new List<string>();
                for (int i = 0; i < fresh.Count && i < 4; i++) { titles.Add(Cut(fresh[i].Title, 110)); sb.Append("\n      - " + titles[i]); }
                found.Add(new Evt { When = DateTime.Now, Text = sb.ToString(), Major = false, Kind = "commits",
                    Subject = p.Name, Count = fresh.Count, Detail = string.Join("\n", titles.ToArray()) });
            }
            if (p.Seen.Count > 600) p.Seen.Clear(); // le flux ne montre que les 20 derniers : inutile de tout garder
            foreach (Commit c in all) p.Seen.Add(c.Id);
            if (!p.Loaded) p.Baseline = newest;
            p.LastCommit = newest;
            p.Commits = all.Count > 8 ? all.GetRange(0, 8) : all;

            if (p.StatusPath == null) { p.Error = null; p.Loaded = true; return; }
            string status = Normalize(p.Extract(Fetch("https://raw.githubusercontent.com/" + p.Repo + "/" + p.Branch + "/" + p.StatusPath)));
            p.Loaded = true;
            // Extraction ratee (section deplacee, page tronquee) : on le dit, sans annoncer un faux changement.
            if (status.Length < 12 || status.StartsWith("("))
            {
                p.Error = "état illisible dans " + p.StatusPath;
                return;
            }
            p.Error = null;
            if (p.Status == null) p.Status = status;
            else if (status == p.Status) { p.PendingStatus = null; p.PendingCount = 0; }
            else if (status == p.PendingStatus)
            {
                // Le meme nouvel etat lu deux fois de suite : c'est un vrai changement, pas un cache qui hesite.
                if (++p.PendingCount >= 2)
                {
                    if (!muted)
                    {
                        found.Add(new Evt { When = DateTime.Now, Major = true, Kind = "status", Subject = p.Name, Detail = status,
                            Text = p.Name + " : l'état GTA VI a changé\n" + Indent(status) });
                        p.StatusFresh = true;
                    }
                    p.Status = status;
                    p.PendingStatus = null;
                    p.PendingCount = 0;
                }
            }
            else { p.PendingStatus = status; p.PendingCount = 1; }
        }
        catch (Exception ex)
        {
            p.Error = ex.Message;
        }
    }

    void LoadOthers(List<Evt> found)
    {
        try
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            string since = DateTime.UtcNow.AddDays(-4).ToString("yyyy-MM-dd");
            string[] queries = { "xbox series compatibility layer windows", "gta vi xbox pc" };
            Dictionary<string, Other> map = new Dictionary<string, Other>(StringComparer.OrdinalIgnoreCase);
            foreach (string q in queries)
            {
                string url = "https://api.github.com/search/repositories?q="
                    + Uri.EscapeDataString(q + " pushed:>" + since + " stars:>=" + MinStars)
                    + "&sort=updated&per_page=30";
                Dictionary<string, object> d = (Dictionary<string, object>)ser.DeserializeObject(Fetch(url));
                foreach (object item in (object[])d["items"])
                {
                    Dictionary<string, object> r = (Dictionary<string, object>)item;
                    Other o = new Other();
                    o.FullName = (string)r["full_name"];
                    o.Desc = r["description"] as string ?? "";
                    o.Created = ((string)r["created_at"]).Substring(0, 10);
                    o.Stars = Convert.ToInt32(r["stargazers_count"]);
                    map[o.FullName] = o;
                }
            }
            foreach (Project p in projects) map.Remove(p.Repo);
            List<Other> list = new List<Other>(map.Values);
            list.Sort(delegate(Other a, Other b) { return b.Stars.CompareTo(a.Stars); });
            foreach (Other o in list)
            {
                // Deja examine par moi, ou deja recense par PortWatch : pas une decouverte.
                bool known = Array.IndexOf(Known, o.FullName.ToLowerInvariant()) >= 0;
                foreach (Effort x in pwEfforts)
                    if (string.Equals(x.Repo, o.FullName, StringComparison.OrdinalIgnoreCase)) known = true;
                if (othersLoaded && !known && !seenOthers.Contains(o.FullName))
                    freshOthers.Add(o.FullName);
                if (freshOthers.Contains(o.FullName) && !seenOthers.Contains(o.FullName))
                    found.Add(new Evt { When = DateTime.Now, Major = true, Kind = "project", Subject = o.FullName,
                        Count = o.Stars, Detail = Cut(o.Desc, 150),
                        Text = "Nouveau projet : " + o.FullName + " (" + o.Stars + " étoiles)\n      "
                            + Cut(o.Desc, 150) + "\n      https://github.com/" + o.FullName });
                o.IsNew = !known;
                seenOthers.Add(o.FullName);
            }
            others = list;
            othersError = null;
            othersLoaded = true;
        }
        catch (Exception ex)
        {
            othersError = ex.Message;
        }
    }

    static double Unit(double v) { return Math.Max(0, Math.Min(1, v)); }

    static double Num(Dictionary<string, object> d, string key)
    {
        object v;
        if (!d.TryGetValue(key, out v) || v == null) return 0;
        try { return Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture); }
        catch (Exception) { return 0; }
    }

    static string Str(Dictionary<string, object> d, string key)
    {
        object v;
        return d.TryGetValue(key, out v) && v != null ? v.ToString() : "";
    }

    // PortWatch VI publie ses donnees dans data/tracker.json : articles ("news") et journal ("changes").
    void LoadTracker(List<Evt> found)
    {
        try
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> d = (Dictionary<string, object>)ser.DeserializeObject(Fetch(TrackerSite + "data/tracker.json"));

            // Garde-fous. Une copie plus ancienne que la derniere acceptee vient d'un cache : on l'ignore.
            // Une liste de projets vide ou amputee de moitie est une lecture ratee, pas une information.
            string stamp = Str(d, "updated");
            object rawEfforts;
            int effortCount = d.TryGetValue("efforts", out rawEfforts) && rawEfforts is object[] ? ((object[])rawEfforts).Length : 0;
            if (pwLoaded && string.CompareOrdinal(stamp, pwUpdated) < 0) return;
            if (effortCount == 0 || (pwLoaded && effortCount * 2 < pwEfforts.Count))
                throw new InvalidDataException("données PortWatch incomplètes (" + effortCount + " projets)");

            List<News> news = new List<News>();
            object raw;
            if (d.TryGetValue("news", out raw) && raw is object[])
                foreach (object item in (object[])raw)
                {
                    Dictionary<string, object> n = item as Dictionary<string, object>;
                    if (n == null) continue;
                    News x = new News { Id = Str(n, "id"), Date = Str(n, "date"), Title = Str(n, "title"), Dek = Str(n, "dek") };
                    if (x.Id.Length == 0) x.Id = x.Title;
                    news.Add(x);
                    if (pwLoaded && !seenNews.Contains(x.Id))
                    {
                        freshNews.Add(x.Id);
                        found.Add(new Evt { When = DateTime.Now, Major = true, Kind = "news", Subject = x.Title,
                            Detail = Cut(x.Dek, 320),
                            Text = "PortWatch : " + x.Title + "\n      " + Cut(x.Dek, 320) });
                    }
                    seenNews.Add(x.Id);
                }

            List<string> changes = new List<string>();
            if (d.TryGetValue("changes", out raw) && raw is object[])
                foreach (object item in (object[])raw)
                {
                    Dictionary<string, object> c = item as Dictionary<string, object>;
                    if (c == null) continue;
                    string text = Str(c, "text");
                    changes.Add(Str(c, "date") + "  " + text);
                    // "up" = un projet franchit une etape, "removed" = un projet disparait : important.
                    string kind = Str(c, "kind");
                    // Les reperages automatiques ("Scout spotted ...") arrivent par dizaines : ils restent
                    // dans le journal du volet PortWatch, sans encombrer les nouveautes.
                    bool scout = text.StartsWith("Scout spotted", StringComparison.Ordinal);
                    if (pwLoaded && !scout && !seenChanges.Contains(text))
                        found.Add(new Evt { When = DateTime.Now, Major = kind == "up" || kind == "removed",
                            Kind = "change", Subject = Cut(text, 320),
                            Text = "PortWatch : " + Cut(text, 320) });
                    seenChanges.Add(text);
                }
            changes.Reverse(); // le journal est du plus ancien au plus recent

            List<Effort> efforts = new List<Effort>();
            if (d.TryGetValue("efforts", out raw) && raw is object[])
                foreach (object item in (object[])raw)
                {
                    Dictionary<string, object> x = item as Dictionary<string, object>;
                    if (x == null) continue;
                    Dictionary<string, object> gta = x.ContainsKey("gta") ? x["gta"] as Dictionary<string, object> : null;
                    Dictionary<string, object> stats = x.ContainsKey("stats") ? x["stats"] as Dictionary<string, object> : null;
                    string stage = "pas encore de GTA VI";
                    if (gta != null)
                    {
                        if (Str(gta, "playable") == "1") stage = "jouable";
                        else if (Str(gta, "world") == "1") stage = "dans le monde";
                        else if (Str(gta, "menu") == "1") stage = "menus";
                        else if (Str(gta, "boots") == "1") stage = "démarre";
                        else if (Str(gta, "loads") == "1") stage = "l'exécutable tourne, écran noir";
                    }
                    if (stats != null && Str(stats, "missing") == "True") stage = "dépôt disparu";
                    Effort eff = new Effort { Name = Str(x, "name"), Repo = Str(x, "repo"), Stage = stage, Summary = Str(x, "summary"),
                        Tier = Str(x, "tier"), Branch = Str(x, "branch"), Id = Str(x, "id"), Link = Str(x, "link") };
                    if (stats != null) eff.Stars = (int)Num(stats, "stars");
                    if (x.ContainsKey("manualScore") && x["manualScore"] != null) eff.Manual = Num(x, "manualScore");
                    if (gta != null)
                    {
                        eff.Loads = Num(gta, "loads"); eff.Boots = Num(gta, "boots");
                        eff.Menu = Num(gta, "menu"); eff.InGame = Num(gta, "ingame");
                        eff.World = gta.ContainsKey("world") ? Num(gta, "world") : eff.InGame;
                        eff.Playable = Num(gta, "playable"); eff.Story = Num(gta, "story"); eff.Render = Num(gta, "render");
                        eff.Fps = Num(gta, "fps");
                        eff.Stability = Str(gta, "stability");
                    }
                    eff.Missing = stats != null && Str(stats, "missing") == "True";
                    eff.Nuked = Str(x, "nuked") == "True";
                    // Travail preparatoire : memes parts que PortWatch (chargeur 20 %, shaders 28 %, graphismes 32 %, autre jeu 20 %).
                    Dictionary<string, object> gw = x.ContainsKey("groundwork") ? x["groundwork"] as Dictionary<string, object> : null;
                    if (gw != null)
                        eff.Groundwork = 0.2 * Unit(Num(gw, "loader")) + 0.28 * Unit(Num(gw, "shaders"))
                            + 0.32 * Unit(Num(gw, "graphics")) + 0.2 * Unit(Num(gw, "proven"));
                    efforts.Add(eff);
                }
            // Un depot peut changer de compte (XCL : ivall/xcl devenu xcl-dev/xcl). On suit le projet par son nom,
            // sinon il serait releve deux fois et perdrait son etat declare dans le classement.
            foreach (Project p in projects)
                foreach (Effort x in efforts)
                    if (string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase) && !x.Missing && !x.Nuked
                        && Regex.IsMatch(x.Repo ?? "", @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")
                        && !string.Equals(x.Repo, p.Repo, StringComparison.OrdinalIgnoreCase))
                        p.Repo = x.Repo;
            pwEfforts = efforts;
            SyncActors(efforts);
            pwNews = news;
            pwChanges = changes;
            pwUpdated = Str(d, "updated");
            pwError = null;
            pwLoaded = true;
        }
        catch (Exception ex)
        {
            pwError = ex.Message;
        }
    }

    // ---------- extraction de l'etat ----------

    static string NodeText(XmlNode n, string path, XmlNamespaceManager ns)
    {
        XmlNode x = n.SelectSingleNode(path, ns);
        return x == null ? "" : x.InnerText;
    }

    static string Cut(string s, int max)
    {
        return s.Length <= max ? s : s.Substring(0, max - 3) + "...";
    }

    static string Indent(string s)
    {
        return "      " + s.Replace("\n", "\n      ");
    }

    static string Plain(string s)
    {
        s = Regex.Replace(s, @"\[([^\]]+)\]\([^)]*\)", "$1"); // liens markdown
        return s.Replace("**", "").Replace("`", "").Trim();
    }

    static string[] Cells(string row)
    {
        string[] parts = row.Trim().Trim('|').Split('|');
        for (int i = 0; i < parts.Length; i++) parts[i] = Plain(parts[i]);
        return parts;
    }

    static string FindLine(string text, string needle)
    {
        foreach (string line in text.Split('\n'))
            if (line.IndexOf(needle, StringComparison.Ordinal) >= 0) return line;
        return null;
    }

    static string ExtractVictory(string text)
    {
        Match m = Regex.Match(text, @"^## Status.*?(?=^Tested on)", RegexOptions.Singleline | RegexOptions.Multiline);
        if (!m.Success) return "(section Status introuvable dans le README)";
        StringBuilder head = new StringBuilder();
        StringBuilder rows = new StringBuilder();
        foreach (string raw in m.Value.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            if (line.StartsWith("|"))
            {
                string[] c = Cells(line);
                if (c.Length < 2 || c[0] == "Stage" || c[0].StartsWith("---")) continue;
                rows.Append("\n• " + c[0] + " : " + c[1]);
            }
            else head.Append(Plain(line) + " ");
        }
        return head.ToString().Trim() + rows;
    }

    static string ExtractXcl(string text)
    {
        string line = FindLine(text, "newer retail game");
        if (line == null) return "(ligne « A newer retail game » introuvable dans le README)";
        string[] c = Cells(line);
        if (c.Length < 5) return Plain(line);
        return "Fonctionne : " + c[3] + "\nLimites : " + c[4];
    }

    static string ExtractTuxBox(string text)
    {
        string line = FindLine(text, "ProjectScarlett (GTA VI");
        if (line == null) return "(ligne GTA VI introuvable dans docs/PROGRESS.md)";
        string[] c = Cells(line);
        if (c.Length < 3) return Plain(line);
        return "Résultat : " + c[1] + "\nReste : " + c[2];
    }

    // ---------- affichage ----------

    Font F(float size, bool bold)
    {
        string key = size + (bold ? "b" : "r");
        Font f;
        if (!fonts.TryGetValue(key, out f))
        {
            f = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
            fonts[key] = f;
        }
        return f;
    }

    void Put(RichTextBox r, string text, Color color, float size, bool bold)
    {
        r.SelectionStart = r.TextLength;
        r.SelectionLength = 0;
        r.SelectionColor = color;
        r.SelectionFont = F(size, bold);
        r.AppendText(text);
    }

    static void ScrollTop(RichTextBox r)
    {
        r.SelectionStart = 0;
        r.ScrollToCaret();
    }

    static string LocalTime(string iso)
    {
        DateTime d;
        if (!DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out d)) return "?";
        d = d.ToLocalTime();
        return d.Date == DateTime.Today ? d.ToString("HH:mm") : d.ToString("dd/MM HH:mm");
    }

    void Render()
    {
        if (Program.Headless) return; // la fenetre unique dessine tout elle-meme
        RenderNews();
        RenderSections();
    }

    void RenderNews()
    {
        news.Clear();
        Put(news, "NOUVEAUTÉS\n", Pal.Pink, 22F, true);
        foreach (Project p in projects)
            if (p.Error != null)
                Put(news, p.Name + " : relevé impossible (" + Cut(p.Error, 120) + ")\n", Pal.Bad, 11.5F, true);
        if (othersError != null)
            Put(news, "Recherche d'autres projets impossible (" + Cut(othersError, 120) + ")\n", Pal.Bad, 11.5F, true);
        if (events.Count == 0)
        {
            Put(news, "\nRien de nouveau", Pal.Grey, 18F, true);
            Put(news, string.Format("\ndepuis {0:HH:mm}. Les changements apparaîtront ici en blanc.\n", started), Pal.Grey, 12F, false);
        }
        // Blanc = nouveau, pas encore marque comme lu. Gris = deja vu.
        foreach (Evt e in events)
        {
            Color c = e.Fresh ? Color.White : Pal.Grey;
            Put(news, "\n" + e.When.ToString("HH:mm:ss") + "\n", c, 11F, true);
            int cut = e.Text.IndexOf('\n');
            string head = cut < 0 ? e.Text : e.Text.Substring(0, cut);
            string rest = cut < 0 ? "" : e.Text.Substring(cut);
            Put(news, head, c, e.Major ? 18F : 14F, true);
            Put(news, rest + "\n", c, e.Major ? 13F : 11.5F, false);
        }
        ScrollTop(news);
    }

    string SectionTitle(int i)
    {
        if (i < projects.Count)
        {
            Project p = projects[i];
            string info = p.Error != null ? "erreur de relevé"
                : p.Commits.Count > 0 ? "dernier commit " + LocalTime(p.Commits[0].Updated) : "chargement...";
            return p.Name + "    -    " + info;
        }
        if (i == 3)
            return "Nouveaux acteurs    -    " + actors.Count + " suivis"
                + (pwEfforts.Count > 0 ? ", " + pwEfforts.Count + " recensés par PortWatch" : "");
        if (i == 4)
        {
            int fresh = 0;
            foreach (Other o in others) if (o.IsNew) fresh++;
            return "Autres projets    -    " + (othersLoaded ? others.Count + " repérés" : "chargement...")
                + (fresh > 0 ? ", " + fresh + " non examiné" + (fresh > 1 ? "s" : "") : "");
        }
        if (i == 5)
            return "PortWatch VI    -    " + (pwError != null ? "erreur de relevé"
                : pwNews.Count > 0 ? Cut(pwNews[0].Title, 62) : "chargement...");
        return "Que fait ce programme ?";
    }

    void RenderSections()
    {
        for (int i = 0; i < sectionButtons.Count; i++)
        {
            sectionButtons[i].Text = (i == openSection ? Expanded : Collapsed) + "   " + SectionTitle(i);
            sectionButtons[i].ForeColor = SectionFresh(i) ? Color.White : Pal.Grey;
        }
        if (openSection < 0) return;

        details.Clear();
        if (openSection < projects.Count) RenderProject(projects[openSection]);
        else if (openSection == 3) RenderActors();
        else if (openSection == 4) RenderOthers();
        else if (openSection == 5) RenderTracker();
        else RenderHelp();
        ScrollTop(details);
    }

    static bool IsFresh(Project p, Commit c)
    {
        return string.CompareOrdinal(c.Updated, p.Baseline) > 0;
    }

    bool SectionFresh(int i)
    {
        if (i < projects.Count)
        {
            Project p = projects[i];
            if (p.StatusFresh) return true;
            foreach (Commit c in p.Commits) if (IsFresh(p, c)) return true;
            return false;
        }
        if (i == 3)
        {
            foreach (Project a in actors)
                foreach (Commit c in a.Commits) if (IsFresh(a, c)) return true;
            return false;
        }
        if (i == 5) return freshNews.Count > 0;
        return i == 4 && freshOthers.Count > 0;
    }

    void RenderActors()
    {
        Put(details, "Projets récents suivis de près : leurs commits sont relevés toutes les 2 minutes. "
            + "Ils n'ouvrent pas l'œil.\n\n", Pal.Grey, 9.5F, false);
        foreach (Project a in actors)
        {
            bool fresh = false;
            foreach (Commit c in a.Commits) if (IsFresh(a, c)) fresh = true;
            Put(details, a.Name, fresh ? Color.White : Pal.Grey, 10.5F, true);
            Put(details, "    https://github.com/" + a.Repo + "\n", Pal.Grey, 9F, false);
            Put(details, "      " + a.Note + "\n", Pal.Grey, 9.5F, false);
            if (a.Error != null) Put(details, "      Relevé impossible : " + Cut(a.Error, 100) + "\n", Pal.Bad, 9.5F, false);
            for (int i = 0; i < a.Commits.Count && i < 2; i++)
            {
                Commit c = a.Commits[i];
                Put(details, "      " + LocalTime(c.Updated) + "  " + Cut(c.Title, 105) + "\n",
                    IsFresh(a, c) ? Color.White : Pal.Grey, 9.5F, IsFresh(a, c));
            }
        }

        Put(details, "\nTous les projets recensés par PortWatch VI\n", Pal.Grey, 10.5F, true);
        if (pwEfforts.Count == 0) Put(details, "Chargement...\n", Pal.Grey, 9.5F, false);
        foreach (Effort x in pwEfforts)
        {
            Put(details, x.Name + "  -  " + x.Stage, Pal.Grey, 10F, true);
            Put(details, "    " + x.Repo + "\n", Pal.Grey, 9F, false);
            if (x.Summary.Length > 0) Put(details, "      " + Cut(x.Summary, 230) + "\n", Pal.Grey, 9.5F, false);
        }
    }

    void RenderTracker()
    {
        Put(details, TrackerSite + "    données du " + LocalTime(pwUpdated) + "\n", Pal.Grey, 9.5F, false);
        if (pwError != null) Put(details, "Erreur de relevé : " + pwError + "\n", Pal.Bad, 10F, false);
        Put(details, "Articles\n", Pal.Grey, 10.5F, true);
        for (int i = 0; i < pwNews.Count && i < 8; i++)
        {
            News n = pwNews[i];
            Color c = freshNews.Contains(n.Id) ? Color.White : Pal.Grey;
            Put(details, n.Date + "  " + n.Title + "\n", c, 10.5F, true);
            Put(details, "      " + Cut(n.Dek, 260) + "\n", c, 9.5F, false);
        }
        Put(details, "\nJournal des changements\n", Pal.Grey, 10.5F, true);
        for (int i = 0; i < pwChanges.Count && i < 12; i++)
            Put(details, Cut(pwChanges[i], 260) + "\n", Pal.Grey, 9.5F, false);
    }

    void RenderProject(Project p)
    {
        Put(details, "https://github.com/" + p.Repo + "\n", Pal.Grey, 9.5F, false);
        if (p.Error != null) Put(details, "Erreur de relevé : " + p.Error + "\n", Pal.Bad, 10F, false);
        if (p.Status == null) return;
        Color sc = p.StatusFresh ? Color.White : Pal.Grey;
        Put(details, "État GTA VI déclaré par l'auteur\n", sc, 10.5F, true);
        Put(details, p.Status + "\n\n", sc, 10.5F, false);
        Put(details, "Derniers commits\n", Pal.Grey, 10.5F, true);
        foreach (Commit c in p.Commits)
        {
            Color cc = IsFresh(p, c) ? Color.White : Pal.Grey;
            Put(details, LocalTime(c.Updated) + "  " + Cut(c.Title, 120) + "\n", cc, 9.5F, IsFresh(p, c));
        }
    }

    void RenderOthers()
    {
        Put(details, "Dépôts GitHub du même genre, au moins " + MinStars
            + " étoiles, actifs ces 4 derniers jours.\n\n", Pal.Grey, 9.5F, false);
        if (othersError != null) Put(details, "Erreur de recherche : " + othersError + "\n", Pal.Bad, 10F, false);
        foreach (Other o in others)
        {
            Color c = freshOthers.Contains(o.FullName) ? Color.White : Pal.Grey;
            if (o.IsNew) Put(details, "NON EXAMINÉ  ", c, 10F, true);
            Put(details, o.FullName, c, 10.5F, true);
            Put(details, "   " + o.Stars + " étoiles, créé le " + o.Created + "\n", c, 9.5F, false);
            if (o.Desc.Length > 0) Put(details, "      " + Cut(o.Desc, 150) + "\n", c, 9.5F, false);
        }
    }

    void RenderHelp()
    {
        Put(details, "Ce programme surveille les projets qui essaient de faire tourner GTA VI "
            + "(version Xbox Series) sur PC.\n\n", Pal.Text, 10.5F, false);
        Put(details, "Toutes les 30 secondes, il lit sur GitHub\n", Pal.Orange, 10.5F, true);
        Put(details, "• les derniers commits de VIctory, XCL et TuxBox ;\n"
            + "• le tableau d'état que chaque auteur tient dans son README ;\n"
            + "• une recherche de nouveaux dépôts du même genre (toutes les 4 minutes seulement).\n"
            + "Toutes les 2 minutes, il lit aussi les articles et le journal de PortWatch VI, un site qui suit tous ces projets.\n"
            + "L'œil ne s'ouvre que pour l'important : un état GTA VI modifié, un nouveau projet ou un article PortWatch. "
            + "Les simples commits s'ajoutent à la liste sans l'ouvrir.\n"
            + "GitHub garde les README en cache quelques minutes : un changement d'état peut arriver avec ce retard.\n\n",
            Pal.Text, 10.5F, false);
        Put(details, "Ce qu'il affiche\n", Pal.Orange, 10.5F, true);
        Put(details, "Il compare avec le relevé précédent et ne montre en haut que ce qui a changé.\n"
            + "• En blanc : ce qui est nouveau. En gros avec un son : un état GTA VI modifié ou un nouveau projet.\n"
            + "• En gris : ce qui est déjà vu. « Marquer comme lu » fait passer tout le blanc en gris.\n"
            + "Les volets ci-dessus donnent le détail de chaque projet, avec la même règle de couleurs.\n\n",
            Pal.Text, 10.5F, false);
        Put(details, "Ce qu'il ne fait pas\n", Pal.Orange, 10.5F, true);
        Put(details, "Il ne télécharge aucun jeu ni aucun fichier, n'envoie rien et n'utilise aucun compte. "
            + "Il ne vérifie pas ce que disent les auteurs : « works » est leur déclaration. "
            + "Les nouveautés sont perdues à la fermeture de la fenêtre.\n\n", Pal.Text, 10.5F, false);
        Put(details, "Attention : aucun de ces projets ne distribue le jeu. Un « GTA 6 PC » téléchargeable "
            + "tout prêt est une arnaque ou un malware.\n", Pal.Sun, 10.5F, true);
    }
}
