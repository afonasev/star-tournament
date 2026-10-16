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
            // Import in version order so generated compatibility counterparts have the
            // same ordering. Preserve each entry's original package provenance afterwards.
            var ordered=releases.Clone();var origins=new Dictionary<int,LabReleaseEntry>();int sequence=0;
            ordered.Entries=ordered.Entries.OrderBy(e=>e.ProfileId,StringComparer.Ordinal).ThenBy(e=>e.Revision).ThenBy(e=>e.Sequence).ToList();
            foreach(var entry in ordered.Entries){origins.Add(++sequence,entry.Clone());entry.Sequence=sequence;}
            var imported=ResolvePublishedDefault(shipped,ordered).data.Clone();
            foreach(var profile in imported.Profiles)
                foreach(var revision in profile.Revisions.Where(r=>r.ReleaseSequence>0))revision.ReleaseSequence=origins[revision.ReleaseSequence].Sequence;
            return imported;
        }

        static LabRevision AuthorDefault(LabHistoryFile file)=>file.Profiles.Single(p=>p.Id==ReleaseId).Revisions
            .Where(r=>r.ReleaseCandidate).OrderByDescending(r=>r.Number).FirstOrDefault();

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

        public void MarkForRelease(string profileId,int revision,bool marked)
        {
            if(!authoring)throw new InvalidOperationException("Отметки авторского каталога доступны только в проекте");
            var saved=data.Profiles.Single(p=>p.Id==profileId).Revisions.Single(r=>r.Number==revision);
            if(marked&&!Current(saved.Snapshot))throw new InvalidOperationException("Несовместимую версию нельзя включить в релиз");
            Commit(f=>f.Profiles.Single(p=>p.Id==profileId).Revisions.Single(r=>r.Number==revision).ReleaseCandidate=marked);
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
                    result.Entries.Add(new LabReleaseEntry{Sequence=++sequence,ProfileId=profile.Id,ProfileName=profile.Name,
                        Revision=revision.Number,Date=string.IsNullOrEmpty(revision.Date)?revision.ReleaseDate:revision.Date,
                        Hash=revision.Hash,Snapshot=revision.Snapshot.Clone()});
                }
            return result;
        }
    }

    public static class LabProjectStorage
    {
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
