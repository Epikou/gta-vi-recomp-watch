using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

// Ou suivre chaque projet : Discord, stream, reseaux. La liste vit dans links.txt a la racine du depot ;
// le programme la relit regulierement, pour qu'un lien mort se corrige sans publier de version.
// La copie ci-dessous sert tant que la lecture n'a pas reussi.
static class Links
{
    const string Remote = "https://raw.githubusercontent.com/Epikou/gta-vi-recomp-watch/main/links.txt";

    const string Builtin =
        "victory-gta6|Rumble|https://rumble.com/user/hyphennn\n" +
        "victory-gta6|YouTube|https://www.youtube.com/@hyphennnnnnn\n" +
        "omonkey|Rumble (Swaggely)|https://rumble.com/user/Swaggely\n" +
        "cgbj|X (video)|https://x.com/raul_bs3/status/2125172278975729688\n" +
        "xcl|Discord|https://discord.gg/xcl\n" +
        "xbox2windows|Discord|https://discord.gg/DMcmNtpd6R\n" +
        "tuxbox|X|https://x.com/brclyxo\n" +
        "tuxbox|YouTube|https://www.youtube.com/@Brclyxo\n";

    // Seuls ces sites sont acceptes : la liste est lue sur Internet, elle ne doit pas pouvoir ouvrir n'importe quoi.
    static readonly Regex Allowed = new Regex(
        @"^https://(discord\.gg|rumble\.com|www\.youtube\.com|x\.com|www\.twitch\.tv|kick\.com)/[A-Za-z0-9_@./?=&%-]{1,200}$");

    static Dictionary<string, List<string[]>> map = Parse(Builtin);
    static readonly List<string[]> None = new List<string[]>();

    // Liens d'un projet, par identifiant PortWatch : des paires { libelle, adresse }.
    public static List<string[]> For(string id)
    {
        List<string[]> l;
        return map.TryGetValue(id ?? "", out l) ? l : None;
    }

    public static void Refresh()
    {
        try
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(Remote);
            req.UserAgent = "gta-pc-watch";
            req.Timeout = 20000;
            string text;
            using (WebResponse resp = req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                text = sr.ReadToEnd();
            if (text.Length > 20000) return;
            Dictionary<string, List<string[]>> m = Parse(text);
            if (m.Count > 0) map = m; // une reponse vide ou illisible ne remplace rien
        }
        catch (Exception) { }
    }

    static Dictionary<string, List<string[]>> Parse(string text)
    {
        Dictionary<string, List<string[]>> m = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in text.Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            string[] p = line.Split('|');
            if (p.Length != 3) continue;
            string id = p[0].Trim(), label = p[1].Trim(), url = p[2].Trim();
            if (id.Length == 0 || label.Length == 0 || label.Length > 24 || !Allowed.IsMatch(url)) continue;
            List<string[]> l;
            if (!m.TryGetValue(id, out l)) m[id] = l = new List<string[]>();
            if (l.Count < 5) l.Add(new[] { label, url });
        }
        return m;
    }
}
