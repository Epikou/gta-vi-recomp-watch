// Mise a jour du programme depuis les versions publiees sur GitHub.
//
// Chaque version publiee porte deux fichiers : GtaPcWatch.exe et version.txt.
// version.txt tient en une ligne : le numero de version, puis l'empreinte SHA-256 du programme.
// Le programme en cours compare ce numero au sien ; s'il est plus recent, il telecharge le nouveau
// fichier, controle son empreinte, se remplace et redemarre.
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

static class Updater
{
    public const string Version = "1.2.1";
    const string Base = "https://github.com/Epikou/gta-vi-recomp-watch/releases/latest/download/";
    const int MaxBytes = 20 * 1024 * 1024;

    public static string ExePath { get { return System.Reflection.Assembly.GetExecutingAssembly().Location; } }

    // Supprime l'ancien programme laisse par la mise a jour precedente.
    public static void Cleanup()
    {
        try { File.Delete(ExePath + ".old"); } catch (Exception) { }
        try { File.Delete(ExePath + ".new"); } catch (Exception) { }
    }

    // Vrai si un programme plus recent a ete mis en place : il reste a redemarrer.
    // Faux si le programme est a jour. Leve une exception si la verification echoue.
    public static bool Apply(out string newVersion)
    {
        newVersion = Version;
        string line = Encoding.UTF8.GetString(Download(Base + "version.txt", 4096)).Trim();
        Match m = Regex.Match(line, @"^(\d+\.\d+\.\d+)\s+([0-9a-fA-F]{64})$");
        if (!m.Success) throw new InvalidDataException("version.txt illisible");
        if (new Version(m.Groups[1].Value) <= new Version(Version)) return false;

        byte[] exe = Download(Base + "GtaPcWatch.exe", MaxBytes);
        if (exe.Length < 20000 || exe[0] != 'M' || exe[1] != 'Z') throw new InvalidDataException("programme tronque");
        string hash;
        using (SHA256 sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(exe)).Replace("-", "");
        if (!string.Equals(hash, m.Groups[2].Value, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("empreinte differente de celle annoncee");

        // Windows interdit d'ecraser un programme en cours, mais permet de le renommer.
        string path = ExePath;
        File.WriteAllBytes(path + ".new", exe);
        try { File.Delete(path + ".old"); } catch (Exception) { }
        File.Move(path, path + ".old");
        try { File.Move(path + ".new", path); }
        catch (Exception) { File.Move(path + ".old", path); throw; }
        newVersion = m.Groups[1].Value;
        return true;
    }

    static byte[] Download(string url, int max)
    {
        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
        req.UserAgent = "gta-pc-watch";
        req.Timeout = 30000;
        req.ReadWriteTimeout = 30000;
        using (WebResponse resp = req.GetResponse())
        using (Stream s = resp.GetResponseStream())
        using (MemoryStream ms = new MemoryStream())
        {
            byte[] buf = new byte[65536];
            int n;
            while ((n = s.Read(buf, 0, buf.Length)) > 0)
            {
                ms.Write(buf, 0, n);
                if (ms.Length > max) throw new InvalidDataException("fichier trop gros");
            }
            return ms.ToArray();
        }
    }
}
