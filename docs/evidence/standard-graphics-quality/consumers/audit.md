# Consumers и предложение уровней
Baseline codex/game-standard-alignment@eca21449. Финальная одна native EditMode fixture прошла; старые assertions сохранены, дополнительно проверено восстановление skinWeights/aniso/mipLimit/lodBias/realtimeProbes. Production UI/rendering/Lab storage не меняются.

## Проверенные данные
{
  "skins": 17,
  "multi": 9585,
  "three_plus": 3713,
  "max_legacy_weights": 4,
  "textures": 30,
  "ignore_mips": 30,
  "prefab_lod_groups": 0
}

3713 вершин имеют3+ ненулевых влияния в legacy BoneWeight API. Max4 в этом API не доказывает отсутствие более широких импортированных данных, поэтому высокий уровень сохраняет исходный Unlimited255, не заменяет его искусственным4. Реальные Auto skins делают лимиты1/2/Unlimited различимыми по исполняемому contract; visual/FPS результат ещё отдельно проверяется при реализации.

В sampled textures (TrooperBody/Arms materials и2 настоящие Well floor base/normal) ignoreMipmapLimit=true. Поэтому scalar globalTextureMipmapLimit не служит доказательством уменьшения их разрешения/VRAM. Эту настройку textures не переопределяем. Well textures реально Trilinear/aniso4, character textures Bilinear/aniso1 с mip chains. Source runtime identity materials копируют исходные texture properties, не удаляют карты. Mipmap/aniso других всех assets не объявляются автоматически эквивалентными sampled set.

Все slots — один URP; legacy pixelLightCount/shadow resolution/cascades/distance/MSAA не являются6 отдельными URP tiers. Shadow/AA/VSync/FPS/effect controls уже имеют владельцев. NativeVisualPresentation создаёт Realtime/ViaScripting ReflectionProbe и явно RenderProbe; generic quality slot меняет global realtimeReflectionProbes. Поэтому future quality integration обязана сохранить отдельные отражения/global gate, как и остальные owned preferences. Headless fixture саму GPU reflection capture не запускает.

В2 sampled prefabs LODGroup0; runtime project code не создаёт LODGroup через собственный API (bounded source search). Это не является обещанием одинакового LOD для каждого будущего imported asset. Skin fidelity и texture filtering — конкретное обоснование трёх предложенных уровней. Не обещаем FPS/VRAM и не выдаём все6 generic Unity labels за6 доказанных режимов.

## Предложение Q10/макет31 — ещё не реализовано
| UI | Existing engine slot | Skin cap | Anisotropic mode |
|---|---:|---:|---|
| Низкое |0 Very Low |1|0 Disable|
| Среднее |2 Medium |2|1 Enable per texture|
| Высокое |5 Ultra |255 Unlimited|2 ForceEnable|

Default сохраняет нынешний slot5; сохранённые AA/shadows/VSync/FPS/reflections/effects и Lab balance не переписываются. Global setting ownership следует переприменить после SetQualityLevel, не полагаться на legacy native slot defaults. Никаких новых художественных коэффициентов/симуляции и никакого отключения ignoreMipmapLimit. UI — одна cycling value button в согласованной странице A; названия Low/Medium/High говорят о моделях/фильтрации, отдельные эффекты ниже. Q10 явно pending; до ответа runtime selector не реализовывать. До закрытия реализации необходим affected settings/persistence/reset/global ownership/rendering QA, а native/device/human gates не заменять этим мета-пробником.

## Источники
- Actual imported meshes/Texture2D objects и QualitySettings в final probe.json/XML.
- Unity6000.3 [Texture2D.ignoreMipmapLimit](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Texture2D-ignoreMipmapLimit.html), [QualitySettings.anisotropicFiltering](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/QualitySettings-anisotropicFiltering.html), ранее проверенные SkinQuality.Auto/URP docs в parent audit.
- Runtime TrooperIdentityPresentation копирует texture properties; WellLabDetail создаёт mipmapped/aniso4; NativeVisualPresentation владеет отдельными effects/probe/SMAA; ProvingGroundGraphicsSettings владеет shadow/VSync preferences.
