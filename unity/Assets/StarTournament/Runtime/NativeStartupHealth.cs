using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace StarTournament.ProvingGround
{
    // Launcher readiness IPC only. Call after successful critical startup and two player-loop frames.
    public static class NativeStartupHealth
    {
        public const string ReadyEnvironment="STAR_TOURNAMENT_STARTUP_READY";
        public const string VersionEnvironment="STAR_TOURNAMENT_STARTUP_VERSION";
        public const string TokenEnvironment="STAR_TOURNAMENT_STARTUP_TOKEN";
        public static bool ReportReady()
        {
            return Publish(Environment.GetEnvironmentVariable(ReadyEnvironment),
                Environment.GetEnvironmentVariable(VersionEnvironment),Environment.GetEnvironmentVariable(TokenEnvironment));
        }
        public static bool Publish(string path,string version,string token)
        {
            if(string.IsNullOrEmpty(path)&&string.IsNullOrEmpty(version)&&string.IsNullOrEmpty(token))return false;
            if(string.IsNullOrEmpty(path)||!Path.IsPathFullyQualified(path)||string.IsNullOrEmpty(version)||version.Length>128||
                !Regex.IsMatch(version,@"\A[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?\z")||
                string.IsNullOrEmpty(token)||!Regex.IsMatch(token,@"\A[0-9a-f]{32}\z"))throw new ArgumentException("Invalid startup readiness handoff");
            // Never overwrite a previous launch's marker or an unrelated file.
            if(File.Exists(path))return false;
            var next=path+".unity-"+Guid.NewGuid().ToString("N");
            try
            {
                var bytes=Encoding.UTF8.GetBytes("{\"schema\":1,\"version\":\""+version+"\",\"token\":\""+token+"\",\"phase\":\"menu-ready\"}");
                using(var output=new FileStream(next,FileMode.CreateNew,FileAccess.Write,FileShare.None))output.Write(bytes,0,bytes.Length);
                File.Move(next,path);return true;
            }
            finally{if(File.Exists(next))File.Delete(next);}
        }
    }
}
