using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace StarTournament.ProvingGround.Editor
{
    public static class LabReleaseStaging
    {
        public static bool Stage(string project,LabBundle baseline,Action<string,string> publish=null)
        {
            string source=LabProjectStorage.ResolvePath(project);
            string target=Path.Combine(Path.GetFullPath(project),"Assets","StarTournament","Resources","LabReleaseCatalog.json");
            if(!File.Exists(source))throw new IOException("Авторский файл отсутствует; выполните явный bootstrap проекта");
            if(!File.Exists(target))throw new IOException("Каталог поставки отсутствует; восстановите исходный каталог из Git");
            var sourceBytes=File.ReadAllBytes(source);var targetBytes=File.ReadAllBytes(target);
            var previous=JsonUtility.FromJson<LabReleaseCatalog>(File.ReadAllText(target));
            Validate(baseline,previous);
            var history=LabProjectStorage.Open(project,baseline,previous);
            var proposed=history.BuildReleaseCatalog();Validate(baseline,proposed);
            foreach(var entry in proposed.Entries)
            {
                var old=previous.Entries.SingleOrDefault(e=>e.ProfileId==entry.ProfileId&&e.Revision==entry.Revision);
                if(old!=null&&(old.Hash!=entry.Hash||!string.IsNullOrEmpty(old.Date)&&old.Date!=entry.Date))
                    throw new InvalidDataException("Изменена опубликованная версия: "+entry.ProfileId+" v"+entry.Revision);
            }
            if(DesignLabHistory.ResolvePublishedDefault(baseline,proposed).Selected.Hash!=history.Selected.Hash)
                throw new InvalidDataException("Default каталога не соответствует авторскому проекту");
            void CheckUnchanged()
            {
                if(!File.ReadAllBytes(source).SequenceEqual(sourceBytes)||!File.ReadAllBytes(target).SequenceEqual(targetBytes))
                    throw new IOException("Исходные данные изменились во время подготовки каталога; повторите после проверки Git");
            }
            CheckUnchanged();
            if(Same(previous,proposed))return false;
            string temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {var bytes=new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(proposed,true));output.Write(bytes,0,bytes.Length);output.Flush(true);}
                CheckUnchanged();
                if(publish!=null)publish(temp,target);else File.Replace(temp,target,null);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
            return true;
        }
        static void Validate(LabBundle baseline,LabReleaseCatalog catalog)
        {
            var history=DesignLabHistory.ResolvePublishedDefault(baseline,catalog);
            if(catalog.Entries.GroupBy(e=>new {e.ProfileId,e.Revision}).Any(g=>g.Count()!=1))throw new InvalidDataException("Повтор идентичности версии каталога");
            foreach(var entry in catalog.Entries)
                if(!history.Profiles.Single(p=>p.Id==entry.ProfileId).Revisions.Any(r=>r.ReleaseSequence==entry.Sequence&&history.Compatible(r)))
                    throw new InvalidDataException("Неисполняемая версия каталога: "+entry.ProfileId+" v"+entry.Revision);
        }
        static bool Same(LabReleaseCatalog a,LabReleaseCatalog b)
        {
            string[] Keys(LabReleaseCatalog c)=>c.Entries.OrderBy(e=>e.ProfileId,StringComparer.Ordinal).ThenBy(e=>e.Revision)
                .Select(e=>JsonUtility.ToJson(new Identity{Id=e.ProfileId,Name=e.ProfileName,Revision=e.Revision,Date=e.Date??"",Hash=e.Hash})).ToArray();
            return Keys(a).SequenceEqual(Keys(b));
        }
        [Serializable] sealed class Identity{public string Id,Name,Date,Hash;public int Revision;}
    }
}
