using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable]
    public sealed class NumericDescriptor
    {
        public string Path;
        public string Group;
        public string Label;
        public string Description;
        public string Unit;
        public float Minimum;
        public float Maximum;
        public float Step;
        public float DefaultValue;

        internal NumericDescriptor DetachedCopy()
        {
            var copy=(NumericDescriptor)MemberwiseClone();
            copy.Path=Path??"";copy.Group=Group??"";copy.Label=Label??"";copy.Description=Description??"";copy.Unit=Unit??"";
            return copy;
        }

        public bool Validate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(Path)) { reason = "Path is required."; return false; }
            if (string.IsNullOrWhiteSpace(Group)) { reason = "Group is required."; return false; }
            if (string.IsNullOrWhiteSpace(Label)) { reason = "Label is required."; return false; }
            if (string.IsNullOrWhiteSpace(Description)) { reason = "Description is required."; return false; }
            if (string.IsNullOrWhiteSpace(Unit)) { reason = "Unit is required."; return false; }
            if (!IsFinite(Minimum) || !IsFinite(Maximum) || Minimum > Maximum) { reason = "Minimum must be finite and no greater than Maximum."; return false; }
            if (!IsFinite(Step) || Step <= 0f) { reason = "Step must be finite and positive."; return false; }
            if (!IsFinite(DefaultValue) || DefaultValue < Minimum || DefaultValue > Maximum) { reason = "DefaultValue is outside the descriptor range."; return false; }
            reason = string.Empty;
            return true;
        }

        public bool Contains(float value) => IsFinite(value) && value >= Minimum && value <= Maximum;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class ProvingProfileValue
    {
        public string Path;
        public float Value;
    }

    [Serializable]
    public sealed class ProfileValidationIssue
    {
        public string Path;
        public string Message;
    }

    /// <summary>
    /// Single numeric registry for the Unity proving ground. It is deliberately data-only:
    /// input, cameras, fixtures, and motors can share its ranges without sharing runtime objects.
    /// </summary>
    [Serializable]
    public sealed partial class ProvingProfile
    {
        public const string DefaultId = "unity-proving-ground-v1";
        public const int DefaultVersion = 14;

        [SerializeField] private string id = DefaultId;
        [SerializeField] private int version = DefaultVersion;
        [SerializeField] private List<NumericDescriptor> descriptors = new List<NumericDescriptor>();
        [SerializeField] private List<ProvingProfileValue> values = new List<ProvingProfileValue>();

        public string Id => id;
        public int Version => version;
        public IReadOnlyList<NumericDescriptor> Descriptors => descriptors;
        public NumericDescriptor Descriptor(string path) => FindDescriptor(path);
        // Copy serialized data without a JSON round trip. Cached lookup tables belong to the
        // original mutable instance and must never be shared with a detached snapshot.
        internal ProvingProfile DetachedCopy()
        {
            if(descriptors==null||values==null||descriptors.Exists(d=>d==null)||values.Exists(v=>v==null))
                return JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            var copy=(ProvingProfile)MemberwiseClone();copy.id=id??"";
            copy.descriptors=descriptors.ConvertAll(d=>d?.DetachedCopy());
            copy.values=values.ConvertAll(v=>v==null?null:new ProvingProfileValue{Path=v.Path??"",Value=v.Value});
            copy.valueIndex=null;
            return copy;
        }
        /// <summary>Append-only migration for serialized proving-ground fixtures when a descriptor is introduced.</summary>
        public void EnsureDefaultDescriptors()
        {
            int previousVersion=version;
            valueIndex=null;
            descriptors.RemoveAll(d=>d.Path.StartsWith("fixture.",StringComparison.Ordinal));values.RemoveAll(v=>v.Path.StartsWith("fixture.",StringComparison.Ordinal));
            descriptors.RemoveAll(d=>d.Path=="presentation.rifleMuzzleSize");values.RemoveAll(v=>v.Path=="presentation.rifleMuzzleSize");
            if(id==DefaultId)version=DefaultVersion;
            var current=CreateDefault();
            foreach(var descriptor in current.descriptors)
            {
                if(FindDescriptor(descriptor.Path)!=null)continue;
                descriptors.Add(JsonUtility.FromJson<NumericDescriptor>(JsonUtility.ToJson(descriptor)));
                values.Add(new ProvingProfileValue{Path=descriptor.Path,Value=(descriptor.Path==GamepadLookSettings.HorizontalPath||descriptor.Path==GamepadLookSettings.VerticalPath)?Get("input.gamepadDegreesPerSecond"):current.Get(descriptor.Path)});
            }
            // The shipped v9/v10 cadence was 1.7 m. Retune only that old value;
            // update descriptor metadata even when a custom interval is retained.
            if(id==DefaultId&&previousVersion>=9&&previousVersion<=10)
            {
                FindDescriptor("audio.footstepDistanceMeters").DefaultValue=current.Descriptor("audio.footstepDistanceMeters").DefaultValue;
                if(Mathf.Approximately(Get("audio.footstepDistanceMeters"),1.7f))
                    Set("audio.footstepDistanceMeters",current.Get("audio.footstepDistanceMeters"));
            }
            valueIndex=null;
        }
        /// <summary>Exact trusted predecessor shared by pre-blood histories from before bonus alerts.</summary>
        public ProvingProfile BeforeDamageBonusAlerts()
        {
            if(id!=DefaultId||FindDescriptor("audio.damageBonusSpawnGain")==null)return this;
            var copy=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            bool Added(string path)=>path.StartsWith("audio.damageBonus",StringComparison.Ordinal)||path.StartsWith("ui.damageBonus",StringComparison.Ordinal);
            copy.descriptors.RemoveAll(d=>Added(d.Path));copy.values.RemoveAll(v=>Added(v.Path));
            copy.version=Math.Min(copy.version,12);return copy;
        }
        public ProvingProfile BeforeMovementAudio()
        {
            if(id!=DefaultId||FindDescriptor("audio.jumpGain")==null)return this;
            var copy=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            bool Added(string path)=>path=="audio.jumpGain"||path=="audio.landGain"||path=="audio.movementPitchVariation"||path=="audio.movementGainVariation";
            copy.descriptors.RemoveAll(d=>Added(d.Path));copy.values.RemoveAll(v=>Added(v.Path));
            copy.version=Math.Min(copy.version,11);return copy;
        }
        public ProvingProfile BeforeCompactMatchMenu()
        {
            if(id!=DefaultId||FindDescriptor("ui.matchMenuWidth")==null)return this;
            var copy=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            bool Added(string path)=>path=="ui.matchMenuWidth"||path=="ui.matchMenuButtonHeight"||path=="ui.matchMenuSpacing";
            copy.descriptors.RemoveAll(d=>Added(d.Path));copy.values.RemoveAll(v=>Added(v.Path));
            // The earlier UI change appended these descriptors without changing its registry version.
            return copy;
        }
        public ProvingProfile BeforeFootstepMix()
        {
            if(id!=DefaultId||FindDescriptor("audio.footstepGain")==null)return this;
            var copy=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(BeforeMovementAudio()));
            copy.descriptors.RemoveAll(d=>d.Path=="audio.footstepGain");
            copy.values.RemoveAll(v=>v.Path=="audio.footstepGain");
            copy.version=Math.Max(6,copy.version-1);
            copy.FindDescriptor("audio.footstepDistanceMeters").DefaultValue=1.7f;
            if(Mathf.Approximately(copy.Get("audio.footstepDistanceMeters"),2.1f))copy.Set("audio.footstepDistanceMeters",1.7f);
            return copy;
        }
        internal ProvingProfile BeforeGamepadLook()
        {
            if(id!=DefaultId||Descriptor(GamepadLookSettings.HorizontalPath)==null)return this;
            var old=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            bool NewLookPath(string path)=>path==GamepadLookSettings.HorizontalPath||path==GamepadLookSettings.VerticalPath||path.StartsWith("input.gamepadReturn",StringComparison.Ordinal);
            old.descriptors.RemoveAll(d=>NewLookPath(d.Path));old.values.RemoveAll(v=>NewLookPath(v.Path));
            old.version=Math.Max(6,old.version-1);return old;
        }
        internal ProvingProfile BeforeGamepadTriggerAim()
        {
            const string path="input.gamepadTapAimThresholdSeconds";
            if(id!=DefaultId||Descriptor(path)==null)return this;
            var old=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            old.descriptors.RemoveAll(d=>d.Path==path);old.values.RemoveAll(v=>v.Path==path);
            old.version=Math.Min(13,old.version);return old;
        }
        public ProvingProfile BeforeDamageVignette()
        {
            // Only this registry changed; share untouched profiles rather than serialize every
            // historical schema combination again while opening the Lab.
            if(id!=DefaultId||FindDescriptor("ui.damageVignette.width")==null)return this;
            var copy=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            copy.descriptors.RemoveAll(d=>d.Path.StartsWith("ui.damageVignette.",StringComparison.Ordinal));
            copy.values.RemoveAll(v=>v.Path.StartsWith("ui.damageVignette.",StringComparison.Ordinal));
            copy.version=Math.Max(6,copy.version-1);
            return copy;
        }
        public ProvingProfile BeforeAudio()
        {
            if(id!=DefaultId||FindDescriptor("audio.effectsDefaultPercent")==null)return this;
            var copy=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            copy.descriptors.RemoveAll(d=>d.Path.StartsWith("audio.",StringComparison.Ordinal));
            copy.values.RemoveAll(v=>v.Path.StartsWith("audio.",StringComparison.Ordinal));
            copy.version=Math.Max(6,copy.version-1);
            return copy;
        }
        /// <summary>Exact trusted registry for immutable pre-projectile Lab revisions.</summary>
        public ProvingProfile BeforeTravelingRifle()
        {
            if(id!="unity-native-combat-v1"||FindDescriptor("rifle.speed")==null)return this;
            var copy=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            int index=copy.descriptors.FindIndex(d=>d.Path=="rifle.speed");
            copy.descriptors.RemoveAt(index);copy.values.RemoveAll(v=>v.Path=="rifle.speed");
            copy.version=Math.Min(copy.version,copy.FindDescriptor("rifle.damage")!=null?5:4);
            copy.Add("rifle.range","rifle","Дальность винтовки","Дальность одиночной пули.","meters",1,200,.5f,80);
            var descriptor=copy.descriptors[copy.descriptors.Count-1];copy.descriptors.RemoveAt(copy.descriptors.Count-1);copy.descriptors.Insert(index,descriptor);
            return copy;
        }
        public void EnsureNativeCombatDescriptors()
        {
            valueIndex=null;
            descriptors.RemoveAll(d=>d.Path=="spawn.spacing"||d.Path=="spawn.inset");values.RemoveAll(v=>v.Path=="spawn.spacing"||v.Path=="spawn.inset");
            UpgradeBodyDamage();
            // Obsolete hitscan range must not become a speed or remain an editable no-op.
            descriptors.RemoveAll(d=>d.Path=="rifle.range");values.RemoveAll(v=>v.Path=="rifle.range");
            var current=CreateNativeCombatDefault();
            foreach(var descriptor in current.descriptors)
            {
                if(FindDescriptor(descriptor.Path)!=null)continue;
                descriptors.Add(JsonUtility.FromJson<NumericDescriptor>(JsonUtility.ToJson(descriptor)));
                values.Add(new ProvingProfileValue{Path=descriptor.Path,Value=current.Get(descriptor.Path)});
            }
            version=7;
        }
        internal ProvingProfile BeforeModeTargets()
        {
            if(id!="unity-native-match-v1")return this;
            var copy=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            copy.descriptors.RemoveAll(d=>d.Path=="match.ffaTargetDefault"||d.Path=="match.teamTargetDefault");
            copy.values.RemoveAll(v=>v.Path=="match.ffaTargetDefault"||v.Path=="match.teamTargetDefault");return copy;
        }
        internal ProvingProfile BeforeMatchAchievements()
        {
            if(id!="unity-native-match-v1"||(FindDescriptor("achievement.minimumShots")==null&&FindDescriptor("achievement.minimumBeamSeconds")==null))return this;
            var copy=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            copy.descriptors.RemoveAll(d=>d.Path=="achievement.minimumShots"||d.Path=="achievement.minimumBeamSeconds");
            copy.values.RemoveAll(v=>v.Path=="achievement.minimumShots"||v.Path=="achievement.minimumBeamSeconds");return copy;
        }
        public void EnsureMatchDescriptors()
        {
            valueIndex=null;
            // The old time gap is no longer a gameplay control: a chain ends only on death.
            descriptors.RemoveAll(d=>d.Path=="score.chainGap");values.RemoveAll(v=>v.Path=="score.chainGap");
            var current=CreateMatchDefault();
            version=current.version;
            foreach(var descriptor in current.descriptors)
            {
                if(FindDescriptor(descriptor.Path)!=null)continue;
                descriptors.Add(JsonUtility.FromJson<NumericDescriptor>(JsonUtility.ToJson(descriptor)));
                values.Add(new ProvingProfileValue{Path=descriptor.Path,Value=current.Get(descriptor.Path)});
            }
        }
        /// <summary>Append-only migration for serialized combat lifecycle fixtures.</summary>
        public void EnsureCombatDescriptors()
        {
            valueIndex=null;
            descriptors.RemoveAll(d=>d.Path=="combat.emptyRefillAmmo");
            values.RemoveAll(v=>v.Path=="combat.emptyRefillAmmo");
            version=7;
            var current=CreateCombatDefault();
            var cadence=FindDescriptor("rifle.cooldownSeconds");
            if(cadence!=null)
            {
                cadence.Label="Скорострельность (интервал)";
                cadence.Description="Время между выстрелами при удержании огня: меньше секунд — выше скорострельность.";
            }
            foreach(var descriptor in current.descriptors)
            {
                if(FindDescriptor(descriptor.Path)!=null)continue;
                descriptors.Add(JsonUtility.FromJson<NumericDescriptor>(JsonUtility.ToJson(descriptor)));
                values.Add(new ProvingProfileValue{Path=descriptor.Path,Value=current.Get(descriptor.Path)});
            }
        }
        public void EnsureTrooperDescriptors()
        {
            descriptors.RemoveAll(d=>d.Path=="view.switchClearance");
            values.RemoveAll(v=>v.Path=="view.switchClearance");
            valueIndex=null;
            var current=CreateTrooperDefault();
            version=current.version;
            foreach(var descriptor in current.descriptors)
            {
                var old=FindDescriptor(descriptor.Path);
                if(old!=null)
                {
                    float value=SafeSwitchUpgrade(old,descriptor,Get(descriptor.Path));
                    descriptors[descriptors.IndexOf(old)]=JsonUtility.FromJson<NumericDescriptor>(JsonUtility.ToJson(descriptor));
                    Set(descriptor.Path,value);continue;
                }
                descriptors.Add(JsonUtility.FromJson<NumericDescriptor>(JsonUtility.ToJson(descriptor)));
                values.Add(new ProvingProfileValue{Path=descriptor.Path,Value=current.Get(descriptor.Path)});
            }
        }

        [NonSerialized] Dictionary<string,ProvingProfileValue> valueIndex;
        void IndexValues()
        {
            if(valueIndex!=null)return;
            valueIndex=new Dictionary<string,ProvingProfileValue>();
            foreach(var value in values)if(value!=null&&!valueIndex.ContainsKey(value.Path))valueIndex.Add(value.Path,value);
        }
        public float Get(string path)
        {
            IndexValues();if(valueIndex.TryGetValue(path,out var value))return value.Value;
            throw new KeyNotFoundException($"Unknown proving profile path '{path}'.");
        }
        /// <summary>Sets an authored value; call Validate before using a changed profile.</summary>
        public void Set(string path,float value)
        {
            IndexValues();if(valueIndex.TryGetValue(path,out var entry)){entry.Value=value;return;}
            throw new KeyNotFoundException($"Unknown proving profile path '{path}'.");
        }

        public IReadOnlyList<ProfileValidationIssue> Validate()
        {
            var issues = new List<ProfileValidationIssue>();
            var knownPaths = new HashSet<string>();
            // Local to this validation: metadata can change between calls. Keep the first
            // duplicate, matching FindDescriptor, and preserve null-path diagnostics.
            var descriptorIndex = new Dictionary<string,NumericDescriptor>(StringComparer.Ordinal);
            NumericDescriptor nullPathDescriptor=null;
            for (var index = 0; index < descriptors.Count; index++)
            {
                var descriptor = descriptors[index];
                if (descriptor == null)
                {
                    issues.Add(new ProfileValidationIssue { Path = $"descriptors[{index}]", Message = "Descriptor is required." });
                    continue;
                }
                if (!descriptor.Validate(out var reason))
                    issues.Add(new ProfileValidationIssue { Path = descriptor.Path, Message = reason });
                if(descriptor.Path==null){if(nullPathDescriptor==null)nullPathDescriptor=descriptor;}
                else if(!descriptorIndex.ContainsKey(descriptor.Path))descriptorIndex.Add(descriptor.Path,descriptor);
                if (!knownPaths.Add(descriptor.Path))
                    issues.Add(new ProfileValidationIssue { Path = descriptor.Path, Message = "Descriptor path must be unique." });
            }

            var valuePaths = new HashSet<string>();
            for (var index = 0; index < values.Count; index++)
            {
                var value = values[index];
                if (value == null)
                {
                    issues.Add(new ProfileValidationIssue { Path = $"values[{index}]", Message = "Profile value is required." });
                    continue;
                }
                if (!valuePaths.Add(value.Path))
                    issues.Add(new ProfileValidationIssue { Path = value.Path, Message = "Profile value path must be unique." });
                NumericDescriptor descriptor;
                if(value.Path==null)descriptor=nullPathDescriptor;
                else descriptorIndex.TryGetValue(value.Path,out descriptor);
                if (descriptor == null)
                    issues.Add(new ProfileValidationIssue { Path = value.Path, Message = "Value has no descriptor." });
                else if ((descriptor.Unit == "ammo" || descriptor.Unit == "count") && value.Value != Mathf.Round(value.Value))
                    issues.Add(new ProfileValidationIssue { Path = value.Path, Message = "Count must be integral." });
                else if ((value.Path=="rifle.damage"||value.Path=="shot.damage"||value.Path.StartsWith("damage.",StringComparison.Ordinal)) &&
                    Math.Abs((value.Value-descriptor.DefaultValue)/descriptor.Step-Math.Round((value.Value-descriptor.DefaultValue)/descriptor.Step))>.002)
                    issues.Add(new ProfileValidationIssue { Path=value.Path, Message="Value must match descriptor step." });
                else if (!descriptor.Contains(value.Value))
                    issues.Add(new ProfileValidationIssue { Path = value.Path, Message = $"Value {value.Value} is outside [{descriptor.Minimum}, {descriptor.Maximum}]." });
            }
            for (var index = 0; index < descriptors.Count; index++)
            {
                var descriptor = descriptors[index];
                if (descriptor != null && !valuePaths.Contains(descriptor.Path))
                    issues.Add(new ProfileValidationIssue { Path = descriptor.Path, Message = "Descriptor has no profile value." });
            }
            return issues;
        }

        /// <summary>Scene-independent lifecycle tuning, consumed by the native session adapter.</summary>
        public static ProvingProfile CreateCombatDefault()
        {
            var profile = new ProvingProfile { id = "unity-combat-state-v1", version = 7 };
            profile.Add("combat.maximumHealth", "combat", "Здоровье", "Полное здоровье каждой новой жизни.", "health", 1, 1000, 1, 100);
            profile.Add("armor.maximum", "armor-pickup", "Максимум брони", "Верхняя граница брони участника.", "armor", 1, 1000, 1, 100);
            profile.Add("armor.pickupAmount", "armor-pickup", "Броня от бонуса", "Количество брони от одного центрального бонуса.", "armor", 1, 1000, 1, 50);
            profile.Add("armor.respawnSeconds", "armor-pickup", "Возврат бонуса", "Simulation-время до возвращения щита после подбора.", "seconds", 1, 120, 1, 30);
            profile.Add("armor.pickupRadius", "armor-pickup", "Радиус подбора", "Горизонтальный радиус authoritative подбора щита.", "meters", .1f, 5, .05f, 1.2f);
            profile.Add("weaponPickup.respawnSeconds", "weapon-pickup", "Возврат оружия", "Независимое игровое время после успешного подбора.", "seconds", .1f, 120, .1f, 15);
            profile.Add("weaponPickup.radius", "weapon-pickup", "Радиус оружия", "Горизонтальный контакт; support и преграды проверяются отдельно.", "meters", .1f, 3, .05f, 1.2f);
            profile.Add("weaponPickup.hoverMeters", "weapon-pickup", "Высота модели", "Высота центра парящего оружия над anchor пола.", "meters", .1f, 3, .05f, 1.1f);
            profile.Add("weaponPickup.rotationDegreesPerSecond", "weapon-pickup", "Вращение модели", "Вращение доступного оружия вокруг вертикали игрового времени.", "degrees/second", 0, 360, 1, 45);
            profile.Add("weaponPickup.modelLengthMeters", "weapon-pickup", "Размер модели", "Максимальная сторона world-модели для читаемости и свободного прохода.", "meters", .2f, 3, .05f, 1.4f);
            profile.Add("pickup.maximumHeightDifference", "pickups", "Допуск высоты подбора", "Максимальная разница высот стоп и anchor; исключает подбор с другого этажа.", "meters", .05f, 1, .05f, .5f);
            profile.Add("damageBoost.multiplier", "damage-boost", "Множитель урона", "Множитель урона оружия до поглощения бронёй.", "ratio", 1, 4, .1f, 1.5f);
            profile.Add("damageBoost.durationSeconds", "damage-boost", "Длительность усиления", "Время усиления; смерть сразу снимает эффект.", "seconds", .1f, 60, .1f, 10);
            profile.Add("damageBoost.initialDelaySeconds", "damage-boost", "Первое появление", "Задержка первого появления от начала матча.", "seconds", 0, 120, .1f, 15);
            profile.Add("damageBoost.respawnSeconds", "damage-boost", "Возврат бонуса", "Задержка после успешного подбора.", "seconds", .1f, 120, .1f, 45);
            profile.Add("damageBoost.pickupRadius", "damage-boost", "Радиус подбора", "Горизонтальный радиус; дополнительно проверяется этаж.", "meters", .1f, 5, .05f, 1.2f);
            profile.Add("heal.targetHealth", "full-heal", "Здоровье после лечения", "Целевое здоровье; ограничено максимумом и не снижает здоровье.", "health", 1, 1000, 1, 100);
            profile.Add("heal.initialDelaySeconds", "full-heal", "Первое появление", "Задержка от начала нового матча.", "seconds", 0, 120, .1f, 20);
            profile.Add("heal.respawnSeconds", "full-heal", "Возврат бонуса", "Simulation-время после подбора.", "seconds", .1f, 120, .1f, 30);
            profile.Add("heal.pickupRadius", "full-heal", "Радиус подбора", "Горизонтальный радиус с общей проверкой этажа.", "meters", .1f, 5, .05f, 1.2f);
            profile.Add("speed.multiplier", "speed-pickup", "Множитель скорости", "Множитель horizontal maximum speed во время speed bonus.", "ratio", 1.05f, 3, .05f, 1.5f);
            profile.Add("speed.durationSeconds", "speed-pickup", "Длительность ускорения", "Simulation-время действия speed bonus.", "seconds", .1f, 60, .1f, 10);
            profile.Add("speed.initialDelaySeconds", "speed-pickup", "Первое появление", "Simulation-время от старта матча до первого speed bonus.", "seconds", 0, 60, .1f, 10);
            profile.Add("speed.respawnSeconds", "speed-pickup", "Возврат ускорения", "Simulation-время от успешного подбора до следующего speed bonus.", "seconds", .1f, 120, .1f, 20);
            profile.Add("speed.pickupRadius", "speed-pickup", "Радиус подбора ускорения", "Горизонтальный authoritative радиус speed bonus.", "meters", .1f, 5, .05f, 1.2f);
            profile.Add("combat.startingAmmo", "shotgun", "Стартовый боезапас", "Количество выстрелов в начале жизни.", "ammo", 1, 200, 1, 20);
            profile.Add("rifle.startingAmmo", "rifle", "Боезапас винтовки", "Число выстрелов до следующего возрождения.", "ammo", 1, 300, 1, 200);
            profile.Add("rifle.cooldownSeconds", "rifle", "Скорострельность (интервал)", "Время между выстрелами при удержании огня: меньше секунд — выше скорострельность.", "seconds", .02f, 1f, .01f, .10f);
            profile.Add("weapon.switchSeconds", "weapon-switch", "Время смены оружия", "Время запрета стрельбы до выбора другого слота.", "seconds", .1f, 3f, .1f, .5f);
            profile.Add("combat.cooldownSeconds", "shotgun", "Интервал выстрелов", "Минимальное simulation время до следующего выстрела.", "seconds", .05f, 5, .05f, .7f);
            profile.Add("combat.killcamSeconds", "death", "Ожидание возрождения", "Simulation время смерти до готовности к выбору spawn.", "seconds", 0, 15, .1f, 3);
            AddRocketLife(profile);
            return profile;
        }

        public static ProvingProfile CreateDefault()
        {
            var profile = new ProvingProfile();
            profile.AddRoundMusicDescriptors();
            profile.Add("audio.musicDefaultPercent", "audio", "Музыка по умолчанию", "Начальная громкость музыкального канала до пользовательского изменения.", "percent", 0f, 100f, 5f, 70f);
            profile.Add("audio.effectsDefaultPercent", "audio", "Эффекты по умолчанию", "Начальная общая громкость звуков меню и боя до пользовательского изменения.", "percent", 0f, 100f, 5f, 80f);
            profile.Add("audio.damageBonusSpawnGain", "audio", "Сигнал появления бонуса урона", "Громкость общего сигнала независимо от расстояния до бонуса.", "ratio", 0f, 1f, .05f, .8f);
            profile.Add("audio.damageBonusPickupGain", "audio", "Тревога подбора бонуса урона", "Громкость общего тревожного сигнала без раскрытия собравшего.", "ratio", 0f, 1f, .05f, 1f);
            profile.Add("audio.maxVoices", "audio", "Короткие голоса", "Предел одновременных коротких эффектов в общем выходе split-screen; длительные лучи и меню имеют отдельные ограниченные источники.", "count", 4f, 32f, 1f, 16f);
            profile.Add("audio.footstepDistanceMeters", "audio", "Интервал шагов", "Горизонтальная дистанция между шагами живого участника на опоре.", "meters", .5f, 5f, .1f, 2.1f);
            profile.Add("audio.footstepGain", "audio", "Уровень шагов", "Относительная громкость одного шага до общей громкости эффектов и позиции участника.", "ratio", 0f, 1f, .05f, .5f);
            profile.Add("audio.jumpGain", "audio", "Уровень толчка", "Громкость отрыва при прыжке до общей громкости и позиции участника.", "ratio", 0f, 1f, .01f, .38f);
            profile.Add("audio.landGain", "audio", "Уровень приземления", "Громкость контакта после прыжка до общей громкости и позиции участника.", "ratio", 0f, 1f, .01f, .6f);
            profile.Add("audio.movementPitchVariation", "audio", "Разброс тона движения", "Максимальное отклонение скорости звучания шагов, толчков и приземлений от исходной записи.", "ratio", 0f, .1f, .001f, .018f);
            profile.Add("audio.movementGainVariation", "audio", "Разброс уровня движения", "Максимальное относительное отклонение громкости отдельного контакта для естественной вариативности.", "ratio", 0f, .2f, .01f, .05f);
            profile.Add("audio.maxDistanceMeters", "audio", "Дальность слышимости", "Расстояние, на котором удалённые боевые и двигательные эффекты затухают.", "meters", 5f, 100f, 1f, 28f);
            profile.Add("audio.localGain", "audio", "Свои события", "Относительный уровень важного звука своего local participant до общей пользовательской громкости.", "ratio", 0f, 1f, .05f, .65f);
            profile.Add("audio.remoteGain", "audio", "Другие участники", "Относительный уровень событий остальных участников до затухания по расстоянию.", "ratio", 0f, 1f, .05f, .40f);
            profile.Add("audio.hitIntervalSeconds", "audio", "Разделение попаданий", "Минимальный интервал повторного звука урона одной цели при частых контактных событиях.", "seconds", .02f, 1f, .01f, .12f);
            profile.Add("simulation.fixedTickHz", "simulation", "Fixed tick frequency", "Frequency of native movement and gameplay ticks, independent of rendered FPS.", "Hz", 30f, 120f, 1f, 50f);
            profile.Add("player.capsule.radius", "player-capsule", "Capsule radius", "Collision radius of a participant capsule.", "meters", .2f, 1f, .01f, .55f);
            profile.Add("player.capsule.height", "player-capsule", "Capsule height", "Full CharacterController capsule height.", "meters", 1f, 4f, .01f, 1.8f);
            profile.Add("player.capsule.skinWidth", "player-capsule", "Controller skin width", "Minimum collision separation used by CharacterController.", "meters", .001f, .1f, .001f, .01f);
            profile.Add("player.movement.maximumGroundSpeed", "player-movement", "Maximum ground speed", "Horizontal speed cap on ground and in air.", "meters-per-second", 1f, 30f, .1f, 9f);
            profile.Add("player.movement.groundAcceleration", "player-movement", "Ground acceleration", "Horizontal acceleration while grounded.", "meters-per-second-squared", .1f, 200f, .1f, 45f);
            profile.Add("player.movement.groundDeceleration", "player-movement", "Ground deceleration", "Horizontal deceleration without directional input.", "meters-per-second-squared", .1f, 200f, .1f, 32f);
            profile.Add("player.movement.airAcceleration", "player-movement", "Air acceleration", "Horizontal steering acceleration while airborne.", "meters-per-second-squared", .1f, 100f, .1f, 12f);
            profile.Add("player.movement.gravity", "player-movement", "Gravity", "Positive downward acceleration.", "meters-per-second-squared", .1f, 100f, .1f, 24f);
            profile.Add("player.movement.jumpSpeed", "player-movement", "Jump speed", "Vertical speed applied to a grounded jump.", "meters-per-second", .1f, 30f, .1f, 8.5f);
            profile.Add("player.movement.stepOffset", "player-movement", "Step offset", "Highest stair step traversable by CharacterController without jumping.", "meters", 0f, 1f, .01f, .45f);
            profile.Add("player.movement.slopeLimitDegrees", "player-movement", "Slope limit", "Maximum climbable CharacterController slope angle.", "degrees", 0f, 89f, 1f, 45f);
            profile.Add("input.mouseDegreesPerPixel", "input", "Mouse degrees per pixel", "Look rotation created by one mouse pixel.", "degrees-per-pixel", .01f, 2f, .01f, .12f);
            profile.Add("input.gamepadDegreesPerSecond", "input", "Gamepad degrees per second", "Maximum analog-stick look rotation rate.", "degrees-per-second", 1f, 720f, 1f, 180f);
            profile.Add("input.gamepadHorizontalDegreesPerSecond", "input", "Геймпад: горизонталь", "Скорость поворота при полном отклонении правого стика по горизонтали.", "°/с", 1f, 720f, 1f, 180f);
            profile.Add("input.gamepadVerticalDegreesPerSecond", "input", "Геймпад: вертикаль", "Скорость наклона при полном отклонении правого стика по вертикали.", "°/с", 1f, 720f, 1f, 180f);
            profile.Add("input.gamepadTapAimThresholdSeconds", "input", "Геймпад: граница короткого LT", "Максимальная длительность нажатия LT, которое при отпускании сбрасывает наклон взгляда к мировому горизонту; более долгое нажатие фиксирует текущий наклон.", "с", .05f, .5f, .01f, .22f);
            profile.Add("input.gamepadReturnDelay", "input", "Задержка выравнивания", "Пауза после отпускания правого стика перед возвратом взгляда.", "с", 0f, 3f, .05f, .35f);
            profile.Add("input.gamepadReturnDegreesPerSecond", "input", "Скорость выравнивания", "Максимальная скорость плавного возврата наклона к уклону пола.", "°/с", 1f, 180f, 1f, 45f);
            profile.Add("input.gamepadReturnSmoothingSeconds", "input", "Плавность выравнивания", "Время плавного приближения наклона к целевому уклону без скачка.", "с", .01f, 2f, .01f, .3f);
            profile.Add("input.deadzone", "input", "Input deadzone", "Radial deadzone applied before creating LocalAction.", "ratio", 0f, .95f, .01f, .15f);
            profile.Add("camera.eyeHeight", "camera", "Eye height", "Camera height above the participant feet anchor.", "meters", .1f, 2.5f, .01f, 1.55f);
            profile.Add("camera.maximumPitchDegrees", "camera", "Maximum pitch", "Symmetric vertical aim clamp.", "degrees", 30f, 89f, 1f, 85f);
            profile.Add("camera.fieldOfViewDegrees", "camera", "Field of view", "Vertical camera field of view.", "degrees", 50f, 130f, 1f, 90f);
            profile.Add("camera.nearClipPlane", "camera", "Near clip plane", "Nearest camera rendering distance.", "meters", .01f, 10f, .01f, .05f);
            profile.Add("camera.farClipPlane", "camera", "Far clip plane", "Farthest camera rendering distance.", "meters", 10f, 1000f, 1f, 250f);
            profile.Add("camera.stairSmoothingSeconds", "camera", "Stair smoothing", "Camera-only vertical smoothing duration for stair traversal.", "seconds", 0f, 1f, .01f, .12f);
            profile.Add("camera.stairMaximumOffsetMeters", "camera", "Maximum stair smoothing offset", "Maximum camera-only vertical smoothing offset.", "meters", 0f, 1f, .01f, .2f);
            // Palette and layer identities are technical fixture constants, not balance controls.
            profile.Add("ui.standingsFontSize", "standings", "Таблица · цифры", "Размер цифр; автоматически уменьшается при нехватке ширины всей группы.", "points", 12, 48, 1, 20);
            profile.Add("ui.standingsIconSize", "standings", "Таблица · значки", "Высота заголовков и markers в общей сетке.", "pixels", 12, 40, 1, 22);
            profile.Add("ui.standingsPadding", "standings", "Таблица · отступ", "Внутренний отступ сетки от края таблицы.", "pixels", 0, 40, 1, 16);
            profile.Add("ui.standingsDeadOpacity", "standings", "Таблица · погибшие", "Яркость погибших строк; красные события сохраняют полную яркость.", "ratio", .2f, 1, .01f, .55f);
            profile.Add("ui.standingsLeaderAccent", "standings", "Таблица · лидер", "Доля цвета лидера в фоне строки.", "ratio", 0, 1, .01f, .14f);
            profile.Add("ui.fontSize", "ui", "Body font size", "Base font size for four-seat HUD labels.", "points", 12f, 48f, 1f, 20f);
            profile.Add("ui.headingFontSize", "ui", "Heading font size", "Heading font size for setup and pause UI.", "points", 16f, 64f, 1f, 28f);
            profile.Add("ui.matchMenuButtonHeight", "ui", "Высота кнопок меню матча", "Высота кнопок общей паузы и итогового экрана без растяжения на свободную область.", "points", 32f, 80f, 1f, 44f);
            profile.Add("ui.matchMenuWidth", "ui", "Ширина меню матча", "Ширина компактной колонки действий общей паузы и результатов.", "points", 320f, 800f, 10f, 480f);
            profile.Add("ui.matchMenuSpacing", "ui", "Промежутки меню матча", "Расстояние между соседними действиями общей паузы и результатов.", "points", 0f, 24f, 1f, 8f);
            profile.Add("ui.indicatorVisualHeight", "ui", "HUD indicator height", "Visible height shared by health, armor and ammo icons and their numbers.", "points", 20f, 64f, 1f, 32f);
            profile.Add("ui.nameFontSize", "ui", "Player name size", "Font size of the bottom-centered per-seat name.", "points", 10f, 32f, 1f, 16f);
            profile.Add("ui.damageBonusFontSize", "ui", "Размер предупреждения бонуса урона", "Размер красной надписи в верхней части каждого игрового viewport.", "points", 12f, 48f, 1f, 26f);
            profile.Add("ui.damageBonusSeconds", "ui", "Время предупреждения бонуса урона", "Время видимости в simulation time; пауза не расходует его.", "seconds", .5f, 8f, .1f, 3f);
            profile.Add("ui.damageBonusTopInset", "ui", "Отступ предупреждения сверху", "Отступ от верхнего края viewport, чтобы не перекрывать таймер матча.", "ratio", .05f, .25f, .01f, .1f);
            profile.Add("ui.damageBonusHeight", "ui", "Высота предупреждения", "Высота области надписи как доля viewport.", "ratio", .06f, .2f, .01f, .12f);
            profile.Add("ui.killNoticeFontSize", "ui", "Kill notice size", "Font size of the per-seat kill and death notice.", "points", 12f, 48f, 1f, 26f);
            profile.Add("ui.killNoticeSeconds", "ui", "Kill notice lifetime", "Simulation-time visibility after a direct kill.", "seconds", .5f, 8f, .1f, 2.5f);
            profile.Add("ui.killNoticeBottom", "ui", "Kill notice bottom", "Bottom of the notice as a viewport-height fraction.", "ratio", .1f, .45f, .01f, .27f);
            profile.Add("ui.killNoticeTop", "ui", "Kill notice top", "Top of the notice as a viewport-height fraction.", "ratio", .35f, .6f, .01f, .44f);
            profile.Add("ui.centerTimerSeamGap", "ui", "Center timer seam gap", "Distance below the horizontal viewport seam for the shared timer in the three-seat layout.", "points", 0f, 64f, 1f, 16f);
            profile.Add("ui.damageVignette.width", "damage-vignette", "Ширина рамки урона", "Доля ширины и высоты viewport: градиент исчезает до чистого центра.", "доля viewport", .03f, .25f, .01f, .16f);
            profile.Add("ui.damageVignette.opacity", "damage-vignette", "Непрозрачность рамки урона", "Максимальная непрозрачность у края при одиночных и повторных попаданиях.", "доля", .05f, .6f, .01f, .34f);
            profile.Add("ui.damageVignette.attackSeconds", "damage-vignette", "Появление рамки урона", "Время быстрого плавного появления после применённого урона.", "секунды", .01f, .15f, .01f, .04f);
            profile.Add("ui.damageVignette.fadeSeconds", "damage-vignette", "Затухание рамки урона", "Время плавного исчезновения после пика; отсчитывается по времени матча.", "секунды", .1f, 1.5f, .05f, .55f);
            profile.Add("ui.viewportDividerWidth", "ui", "Viewport divider width", "Thickness of opaque black lines between local viewports.", "points", 2f, 24f, 1f, 6f);
            profile.Add("presentation.lightIntensity", "presentation", "Fixture light intensity", "Presentation-only fixture light intensity.", "intensity", .1f, 4f, .1f, 1.1f);
            profile.Add("presentation.armorPickupHoverHeight", "armor-pickup", "Высота парения щита", "Presentation-only высота бонуса над опорной поверхностью.", "meters", 0, 3, .01f, .7f);
            profile.Add("presentation.armorPickupRotationDegreesPerSecond", "armor-pickup", "Вращение щита", "Presentation-only скорость вращения бонуса.", "degrees-per-second", 0, 720, 1, 80);
            profile.Add("presentation.healPickupHoverHeight", "full-heal", "Высота парения", "Высота сердца над support.", "meters", 0, 3, .01f, .7f);
            profile.Add("presentation.healPickupRotationDegreesPerSecond", "full-heal", "Вращение сердца", "Presentation-only скорость вращения вокруг вертикальной оси.", "degrees-per-second", 0, 720, 1, 80);
            profile.Add("presentation.healPickupScale", "full-heal", "Размер сердца", "Масштаб GLB; не меняет радиус подбора.", "ratio", .25f, 3, .05f, 1);
            profile.Add("presentation.speedPickupHoverHeight", "speed-pickup", "Высота парения ускорения", "Presentation-only высота speed bonus над опорной поверхностью.", "meters", 0, 3, .01f, .7f);
            profile.Add("presentation.speedPickupRotationDegreesPerSecond", "speed-pickup", "Вращение ускорения", "Presentation-only скорость вращения speed bonus.", "degrees-per-second", 0, 720, 1, 110);
            profile.Add("presentation.viewWeaponX", "presentation", "View weapon X", "First-person weapon mount horizontal translation from camera origin.", "meters", -2f, 2f, .01f, .36f);
            profile.Add("presentation.viewWeaponY", "presentation", "View weapon Y", "First-person weapon mount vertical translation from camera origin.", "meters", -2f, 2f, .01f, -.34f);
            profile.Add("presentation.viewWeaponZ", "presentation", "View weapon Z", "First-person weapon mount forward translation from camera origin.", "meters", -2f, 2f, .01f, .68f);
            profile.Add("presentation.worldWeaponMountY", "presentation", "World weapon mount Y", "World weapon mount translation above the robot semantic hand mount.", "meters", -1f, 1f, .01f, .22f);
            profile.Add("presentation.vector.muzzleSeconds","vector-shot-feedback","Вспышка Vector","Время двойной дульной вспышки.","seconds",0.01f,0.2f,0.01f,0.07f);
            profile.Add("presentation.vector.muzzleSize","vector-shot-feedback","Размер вспышки Vector","Размер короткой вспышки без перекрытия прицела.","meters",0.01f,0.5f,0.01f,0.09f);
            profile.Add("presentation.vector.smokeSeconds","vector-shot-feedback","Дым Vector","Время быстрого рассеивания дульного дыма.","seconds",0.1f,1.5f,0.05f,0.4f);
            profile.Add("presentation.vector.smokeSize","vector-shot-feedback","Размер дыма Vector","Диаметр отдельного облачка дыма.","meters",0.01f,0.4f,0.01f,0.09f);
            profile.Add("presentation.vector.smokeSpeed","vector-shot-feedback","Скорость дыма Vector","Скорость удаления дыма от ствола.","meters-per-second",0.01f,2f,0.01f,0.24f);
            profile.Add("presentation.vector.sparkCount","vector-shot-feedback","Искры Vector","Число коротких искр из каждого ствола.","count",0f,16f,1f,5f);
            profile.Add("presentation.vector.sparkSpeed","vector-shot-feedback","Скорость искр Vector","Скорость затухающих искр.","meters-per-second",0.1f,10f,0.1f,2f);
            profile.Add("presentation.vector.sparkSize","vector-shot-feedback","Размер искр Vector","Диаметр мелких искр.","meters",0.001f,0.04f,0.001f,0.008f);
            profile.Add("presentation.vector.sparkSeconds","vector-shot-feedback","Время искр Vector","Время жизни мелких искр.","seconds",0.01f,0.4f,0.01f,0.12f);
            profile.Add("presentation.vector.sparkConeDegrees","vector-shot-feedback","Конус искр Vector","Угол визуального разлёта, не gameplay spread.","degrees",0f,60f,1f,18f);
            profile.Add("presentation.shotFlightSeconds", "shot-feedback", "Длительность дроби", "Время видимого полёта дробины до authoritative конечной точки.", "seconds", .02f, 1f, .01f, .12f);
            profile.Add("presentation.shotTracerWidth", "shot-feedback", "Толщина дроби", "Толщина читаемого presentation-only пути дробины.", "meters", .005f, .2f, .005f, .01f);
            profile.Add("presentation.rifleTracerWidth", "rifle", "Толщина трассера", "Ширина короткого трассера винтовки.", "meters", .002f, .1f, .001f, .006f);
            profile.Add("presentation.rifleTracerLength", "rifle", "Длина трассера", "Длина видимого следа за летящей пулей и участка совмещения с дульной точкой.", "meters", .1f, 10f, .1f, 3f);
            profile.Add("presentation.rifleTracerRed", "rifle", "Трассер · красный", "Красный канал цвета трассера винтовки.", "ratio", 0f, 1f, .01f, .65f);
            profile.Add("presentation.rifleTracerGreen", "rifle", "Трассер · зелёный", "Зелёный канал цвета трассера винтовки.", "ratio", 0f, 1f, .01f, 1f);
            profile.Add("presentation.rifleTracerBlue", "rifle", "Трассер · синий", "Синий канал цвета трассера винтовки.", "ratio", 0f, 1f, .01f, .08f);
            profile.Add("presentation.rifleModelScale", "rifle", "Масштаб модели винтовки", "Размер на grip mount для читаемого хвата и вида от первого лица.", "ratio", .5f, 3f, .1f, 2f);
            profile.Add("presentation.shotMuzzleClearance", "shot-feedback", "Зазор от muzzle", "Начальный presentation-only зазор, отделяющий дробь от muzzle burst first-person оружия.", "meters", 0f, 3f, .05f, .1f);
            profile.Add("presentation.shotImpactSeconds", "shot-feedback", "Длительность impact", "Время видимого contact impact без persistent decal.", "seconds", .02f, 1f, .01f, .12f);
            profile.Add("presentation.shotImpactSize", "shot-feedback", "Размер impact", "Размер краткого material-aware contact impact.", "meters", .01f, 2f, .01f, .16f);
            profile.Add("presentation.shotMaxEffects", "shot-feedback", "Лимит дроби", "Максимум одновременно живых visual дробин одного presentation owner.", "count", 8f, 256f, 1f, 96f);
            profile.Add("navigation.sampleDistance", "navigation", "Navigation sample distance", "Maximum endpoint projection distance for fixture navigation queries; not bake spacing.", "meters", .1f, 10f, .1f, 2f);
            profile.Add("weapon.probeRange", "weapon", "Weapon probe range", "Fixture hitscan diagnostic range.", "meters", 1f, 100f, 1f, 35f);
            profile.Add("weapon.probeCooldown", "weapon", "Weapon probe cooldown", "Minimum interval between fixture firing probes.", "seconds", .1f, 3f, .05f, .7f);
            profile.Add("world.minimumSupportHeight","world-query","Lowest support probe","Lower world bound for corpse support queries; does not author geometry.","meters",-100,0,1,-10);
            profile.Add("world.maximumTransitionSegment","world-query","Declared route segment bound","Maximum distance between declared traversal samples.","meters",1,100,.5f,20);
            AddRocketPresentation(profile);
            return profile;
        }

        private void Add(string path, string group, string label, string description, string unit, float minimum, float maximum, float step, float defaultValue)
        {
            valueIndex=null;
            descriptors.Add(new NumericDescriptor { Path = path, Group = group, Label = label, Description = description, Unit = unit, Minimum = minimum, Maximum = maximum, Step = step, DefaultValue = defaultValue });
            values.Add(new ProvingProfileValue { Path = path, Value = defaultValue });
        }

        private NumericDescriptor FindDescriptor(string path)
        {
            for (var index = 0; index < descriptors.Count; index++)
                if (descriptors[index] != null && descriptors[index].Path == path) return descriptors[index];
            return null;
        }
    }
}
