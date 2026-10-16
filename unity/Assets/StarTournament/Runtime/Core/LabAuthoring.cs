using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace StarTournament.ProvingGround
{
    public sealed partial class DesignLabHistory
    {
        LabHistoryFile SeedAuthoring()
        {
            // Import in stable source order; technical counterparts can still use higher keys.
            // Saved ordering resolves public identities, not those generated storage keys.
            var ordered=releases.Clone();var origins=new Dictionary<int,LabReleaseEntry>();int sequence=0;
            ordered.Entries=ordered.Entries.OrderBy(e=>e.ProfileId,StringComparer.Ordinal).ThenBy(e=>e.Revision).ThenBy(e=>e.Sequence).ToList();
            foreach(var entry in ordered.Entries){origins.Add(++sequence,entry.Clone());entry.Sequence=sequence;}
            var imported=ResolvePublishedDefault(shipped,ordered).data.Clone();
            foreach(var profile in imported.Profiles)
                foreach(var revision in profile.Revisions.Where(r=>r.ReleaseSequence>0))revision.ReleaseSequence=origins[revision.ReleaseSequence].Sequence;
            return imported;
        }

        static LabRevision AuthorDefault(LabHistoryFile file)=>file.Profiles.Single(p=>p.Id==ReleaseId).Revisions
            .Where(r=>r.ReleaseCandidate).OrderByDescending(SavedVersionNumber).ThenByDescending(r=>r.Number).FirstOrDefault();

        public string ReleaseError
        {
            get
            {
                if(!authoring)return null;
                var revision=AuthorDefault(data);
                return revision==null?"Отметьте хотя бы одну сохранённую версию Default для релиза":
                    !Current(revision.Snapshot)?"Последняя отмеченная версия Default несовместима с текущим runtime":null;
            }
        }

        public void ResetToRelease()=>Commit(SelectLatestDefault);
        public void PersistAuthoring()
        {
            if(!authoring)throw new InvalidOperationException("Сохранение проекта доступно только авторскому клиенту");
            Commit(_=>{});
        }

        public void MarkForRelease(string profileId,int revision,bool marked)
        {
            if(!authoring)throw new InvalidOperationException("Отметки авторского каталога доступны только в проекте");
            var saved=data.Profiles.Single(p=>p.Id==profileId).Revisions.Single(r=>r.Number==revision);
            if(marked&&!Current(saved.Snapshot))throw new InvalidOperationException("Несовместимую версию нельзя включить в релиз");
            Commit(f=>{
                var target=f.Profiles.Single(p=>p.Id==profileId).Revisions.Single(r=>r.Number==revision);
                target.ReleaseCandidate=marked;
                if(marked)FreezeCompatibilityView(target);
            });
        }

        // A projection for explicit build tooling. This does not write, commit or publish.
        public LabReleaseCatalog BuildReleaseCatalog()
        {
            if(!authoring)throw new InvalidOperationException("Релизный каталог создаётся только из авторских данных проекта");
            if(!Writable)throw new IOException(StorageError);
            if(ReleaseError!=null)throw new InvalidOperationException(ReleaseError);
            var result=new LabReleaseCatalog();int sequence=0;
            foreach(var profile in data.Profiles.OrderBy(p=>p.Id,StringComparer.Ordinal))
                foreach(var revision in profile.Revisions.Where(r=>r.ReleaseCandidate).OrderBy(r=>r.Number))
                {
                    if(!Current(revision.Snapshot)||revision.Snapshot.Validate().Count!=0)
                        throw new InvalidOperationException("Несовместимая релизная версия: "+profile.Name+" v"+revision.Number);
                    // Current-schema counterparts are execution views of immutable saved
                    // package versions. Export their original identity/content, not a reused
                    // public number with different interpreted bytes.
                    var original=revision.ReleaseSequence>0?profile.Revisions.FirstOrDefault(r=>r.Hash==revision.ReleaseHash):null;
                    if(revision.ReleaseSequence>0&&original==null)throw new InvalidOperationException("Исходный снимок релизной версии не найден");
                    int number=original==null?revision.Number:revision.ReleaseNumber;
                    if(result.Entries.Any(e=>e.ProfileId==profile.Id&&e.Revision==number))
                        throw new InvalidOperationException("Повтор идентичности релизной версии: "+profile.Name+" v"+number);
                    result.Entries.Add(new LabReleaseEntry{Sequence=++sequence,ProfileId=profile.Id,ProfileName=profile.Name,
                        Revision=number,Date=original==null?revision.Date:revision.ReleaseDate,
                        Hash=original?.Hash??revision.Hash,Snapshot=(original?.Snapshot??revision.Snapshot).Clone()});
                }
            return result;
        }
    }

    public static class LabProjectStorage
    {
        public static string SelectProject(bool editor,bool development,string unityProjectRoot,string[] args,string qaHistory,string qaProject)
        {
            if(!editor&&!development)return null;
            string selected=qaProject;
            int index=Array.IndexOf(args,"-labProject");
            if(string.IsNullOrEmpty(selected)&&index>=0)
            {
                if(index+1>=args.Length)throw new ArgumentException("-labProject требует абсолютный путь проекта Unity");
                selected=args[index+1];
            }
            if(string.IsNullOrEmpty(selected))
            {
                if(!editor||!string.IsNullOrEmpty(qaHistory)||args.Contains("-labReview")||args.Contains("-colorIdentityReview"))return null;
                selected=unityProjectRoot;
            }
            ResolvePath(selected);return Path.GetFullPath(selected);
        }
        public static string ResolvePath(string unityProjectRoot)
        {
            if(string.IsNullOrWhiteSpace(unityProjectRoot)||!Path.IsPathFullyQualified(unityProjectRoot))
                throw new ArgumentException("Нужен абсолютный путь проекта Unity");
            string root=Path.GetFullPath(unityProjectRoot);
            if(!Directory.Exists(Path.Combine(root,"Assets","StarTournament")))throw new IOException("Проект Star Tournament не найден");
            var parent=new DirectoryInfo(root);
            while(parent!=null&&!Directory.Exists(Path.Combine(parent.FullName,".git"))&&!File.Exists(Path.Combine(parent.FullName,".git")))parent=parent.Parent;
            if(parent==null)throw new IOException("Авторские данные требуют проекта в Git");
            return Path.Combine(root,"DesignLab","authoring-history-v1.json");
        }

        public static DesignLabHistory Open(string unityProjectRoot,LabBundle baseline,LabReleaseCatalog seed)
        {
            if(seed==null)throw new ArgumentNullException(nameof(seed));
            return new DesignLabHistory(ResolvePath(unityProjectRoot),baseline,releases:seed,resetToLatestDefault:true,
                persistMigration:false,authoring:true);
        }
    }
}
