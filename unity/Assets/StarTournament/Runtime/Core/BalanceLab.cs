using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    public static partial class BalanceLab
    {
        public static LabBundle RuntimeDefaults()=>new LabBundle{Profiles=new List<ProvingProfile>{ProvingProfile.CreateDefault(),ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateTrooperDefault(),ProvingProfile.CreateMatchDefault(),ProvingProfile.CreateTeamDefault(),ProvingProfile.CreateRosterDefault(),ProvingProfile.CreateBotPerceptionDefault(),ProvingProfile.CreateNavigationDefault(),ProvingProfile.CreateBotBehaviorDefault(),ProvingProfile.CreateCombatBowlRingPresentationDefault(),CombatBowlCatalog.AuthoringProfile(),ProvingProfile.CreateCutterDefault(),ProvingProfile.CreatePlasmaDefault(),ProvingProfile.CreateParticipantPaletteDefault(),ProvingProfile.CreateBotEvaluationDefault(),ProvingProfile.CreateDeathDefault(),ProvingProfile.CreateBloodDefault(),ProvingProfile.CreateRocketEffectsDefault(),ProvingProfile.CreateIndustrialTunnelsPresentation(),IndustrialTunnelsCatalog.AuthoringProfile(),ProvingProfile.CreateLunarPresentation(),LunarLaboratoryCatalog.AuthoringProfile(),ProvingProfile.CreateWellDefault(),WellArenaCatalog.AuthoringProfile(),FiringRangeCatalog.AuthoringProfile(),ProvingProfile.CreateHitFeedbackDefault(),ProvingProfile.CreateLobbyDefault()}};
        public const string FullArchiveSha256="8a87fcee3a78ea099bc9de0cc29d8bed5abbc9c46cbeb164444b333254ea885e";
        // First author import only. This is the exact previously shipped immutable blob,
        // outside runtime Assets. Its checksum, not a mutable file's metadata, grants trust.
        internal static LabBundle[] AuthoringRegistries(string source)
        {
            string path=Path.Combine(Path.GetDirectoryName(source),"Archive","LabReleaseCatalog-full-v1.json");
            var catalog=ReadAuthoringArchive(path);
            return catalog.Entries.SelectMany(e=>
            {
                if(e.Hash!=e.Snapshot.Hash())throw new InvalidDataException("Повреждён исходный снимок авторского архива");
                var registry=e.Snapshot.Clone();foreach(var d in registry.Descriptors)registry.Set(d.Path,d.DefaultValue);return new[]{e.Snapshot,registry};
            }).ToArray();
        }
        public static LabReleaseCatalog ReadAuthoringArchive(string path)
        {
            byte[] bytes=File.ReadAllBytes(path);string hash;
            using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            if(hash!=FullArchiveSha256)throw new InvalidDataException("Авторский архив изменён; восстановите точный исходник из Git");
            var catalog=JsonUtility.FromJson<LabReleaseCatalog>(System.Text.Encoding.UTF8.GetString(bytes));
            return catalog;
        }
        public const string Suffix="-balance-v1";
        public static bool IsProjection(LabBundle bundle)=>bundle.Profiles.Count>0&&bundle.Profiles.All(p=>p.Id.EndsWith(Suffix,StringComparison.Ordinal));
        // Owners outside this projection remain full, independent runtime resources.
        public static LabBundle Project(LabBundle runtime)
        {
            if(IsProjection(runtime))return runtime.Clone();
            var projected=new LabBundle{Profiles=runtime.Profiles.Select(p=>p.BalanceProjection()).Where(p=>p.Descriptors.Count>0).ToList()};
            return projected;
        }
        public static LabBundle Overlay(LabBundle source,LabBundle defaults)
        {
            var result=defaults.Clone();var paths=new HashSet<string>(source.Descriptors.Select(d=>d.Path),StringComparer.Ordinal);
            foreach(var d in result.Descriptors)if(paths.Contains(d.Path))result.Set(d.Path,source.Get(d.Path));
            return result;
        }
        public static string Group(string path)=>Fields[path].Group;
        public static readonly IReadOnlyDictionary<string,string> Groups=new Dictionary<string,string>
        {
            {"movement","Участник → Движение"},{"life","Участник → Здоровье и возрождение"},{"damage","Участник → Правила урона"},
            {"rifle","Оружие → Автомат"},{"shotgun","Оружие → Дробовик"},{"rocket","Оружие → Ракетница"},{"cutter","Оружие → Лазер"},{"plasma","Оружие → Плазмомёт"},{"weapon-switch","Оружие → Смена оружия"},
            {"pickups","Арена и бонусы → Подбор и усиления"},{"arena","Арена и бонусы → Лифты"},{"spawn","Арена и бонусы → Появление участников"},
            {"bot-easy","Боты → Рекрут"},{"bot-normal","Боты → Боец"},{"bot-hard","Боты → Ветеран"},{"bot-tactics","Боты → Тактика и взаимодействие"},{"bot-navigation","Боты → Движение по маршруту"},
            {"match","Матч → Условия матча"},{"scoring","Матч → Очки и награды"}
        };
    }
    public sealed partial class ProvingProfile
    {
        internal ProvingProfile BalanceProjection()
        {
            var kept=descriptors.Where(d=>BalanceLab.Fields.ContainsKey(d.Path)).ToList();
            var paths=new HashSet<string>(kept.Select(d=>d.Path),StringComparer.Ordinal);
            var copy=new ProvingProfile{id=id+BalanceLab.Suffix,version=1,
                descriptors=kept.Select(d=>d.DetachedCopy()).ToList(),
                values=values.Where(v=>paths.Contains(v.Path)).Select(v=>new ProvingProfileValue{Path=v.Path,Value=v.Value}).ToList()};
            return copy;
        }
    }
}
