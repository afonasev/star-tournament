import { validateGameDesignProfile as validateLegacyProfile } from './legacyGameDesignProfileV6'
import { BOT_RULES, BOT_DESCRIPTORS, type BotRules } from './botRules'
import { canonicalSerialize, contentHash, isContentHash, type ContentHash } from '../core/canonical'

import { deepFreeze, type DeepReadonly } from './deepFreeze'

export const GAME_DESIGN_PROFILE_SCHEMA_VERSION = 7 as const

export type GameDesignProfileSource = 'shipped' | 'local'

export type ArenaGeneratorSize = 'small' | 'medium' | 'large'

export interface ArenaGeneratorPresetRules {
  widthMeters: number
  depthMeters: number
  wallHeightMeters: number
  wallThicknessMeters: number
  corridorWidthMeters: number
  doorwayWidthMeters: number
  perimeterInsetMeters: number
  routeInsetMeters: number
  recipeCount: number
  elevationDeltaMeters: number
  upperDeckHeightMultiplier: number
  upperDeckFootprintRatio: number
  basementDepthMultiplier: number
  rampWidthMeters: number
  rampLengthMeters: number
  barrierLowMeters: number
  barrierHighMeters: number
  coverCount: number
  coverSizeMeters: number
  spawnSlotCount: number
  minimumSpawnSeparationMeters: number
  maximumInitialEnemyLosRatio: number
  maximumTeamDistanceRatio: number
}

export interface GameDesignRules {
  bots: BotRules
  player: {
    startingHealth: number
    capsule: {
      radius: number
      halfHeight: number
      skinWidth: number
    }
    eyeHeight: number
    movement: {
      maximumGroundSpeed: number
      groundAcceleration: number
      groundDeceleration: number
      airAcceleration: number
      gravity: number
      jumpSpeed: number
      autostepMaxHeight: number
      autostepMinWidth: number
      snapToGroundDistance: number
      maxSlopeClimbDegrees: number
      minSlopeSlideDegrees: number
    }
  }
  camera: {
    horizontalSensitivity: number
    verticalSensitivity: number
    maximumPitchDegrees: number
    fieldOfViewDegrees: number
    nearPlane: number
    farPlane: number
  }
  weapons: {
    doubleBarrelShotgun: {
      startingAmmo: number
      emptyRefillAmmo: number
      pelletCount: number
      spreadDegrees: number
      rangeMeters: number
      cooldownSeconds: number
      muzzleFeedbackSeconds: number
      impactFeedbackSeconds: number
      hitmarkerSeconds: number
      damage: {
        head: number
        torso: number
        limb: number
      }
    }
  }
  match: {
    duration: {
      defaultMinutes: number
      minimumMinutes: number
      maximumMinutes: number
    }
    scoreTarget: {
      defaultPoints: number
      minimumPoints: number
      maximumPoints: number
      stepPoints: number
    }
  }
  scoring: {
    assist: {
      windowSeconds: number
      points: number
    }
    killChain: {
      maximumGapSeconds: number
      cumulativeTotals: [number, number, number, number, number]
      postFiveIncrementPoints: number
    }
  }
  death: {
    killcamSeconds: number
    corpseSeconds: number
  }
  arenaGenerator: {
    presets: Record<ArenaGeneratorSize, ArenaGeneratorPresetRules>
  }
}

export interface GameDesignProfilePayload {
  schemaVersion: typeof GAME_DESIGN_PROFILE_SCHEMA_VERSION
  id: string
  name: string
  revision: number
  source: GameDesignProfileSource
  rules: GameDesignRules
}

export interface GameDesignProfile extends GameDesignProfilePayload {
  contentHash: string
}

export type ShippedGameDesignProfile = DeepReadonly<GameDesignProfile>
export type ValidatedGameDesignProfile = DeepReadonly<GameDesignProfile>

export type ProfileDescriptorGroup =
  | 'bots'
  | 'player'
  | 'player-capsule'
  | 'player-movement'
  | 'camera'
  | 'shotgun'
  | 'match'
  | 'scoring-assist'
  | 'scoring-kill-chain'
  | 'death'
  | 'arena-generator-small'
  | 'arena-generator-medium'
  | 'arena-generator-large'

export type ProfileDescriptorUnit =
  | 'hp'
  | 'ammo'
  | 'damage'
  | 'pellets'
  | 'meters'
  | 'meters-per-second'
  | 'meters-per-second-squared'
  | 'degrees'
  | 'minutes'
  | 'seconds'
  | 'points'
  | 'count'
  | 'ratio'

export interface NumericProfileDescriptor {
  path: string
  group: ProfileDescriptorGroup
  label: string
  description: string
  unit: ProfileDescriptorUnit
  minimum: number
  maximum: number
  step: number
}

const ARENA_GENERATOR_SIZES = ['small', 'medium', 'large'] as const

function arenaGeneratorDescriptors(size: ArenaGeneratorSize): NumericProfileDescriptor[] {
  const group = `arena-generator-${size}` as ProfileDescriptorGroup
  const prefix = `rules.arenaGenerator.presets.${size}`
  const sizeLabel = size === 'small' ? 'маленькой' : size === 'medium' ? 'средней' : 'большой'
  return [
    { path: `${prefix}.widthMeters`, group, label: `Ширина ${sizeLabel} арены`, description: 'Полная ширина playable footprint.', unit: 'meters', minimum: 20, maximum: 80, step: 1 },
    { path: `${prefix}.depthMeters`, group, label: `Глубина ${sizeLabel} арены`, description: 'Полная глубина playable footprint.', unit: 'meters', minimum: 16, maximum: 64, step: 1 },
    { path: `${prefix}.wallHeightMeters`, group, label: `Высота стен ${sizeLabel} арены`, description: 'Высота внешних и route-разделяющих стен.', unit: 'meters', minimum: 2, maximum: 10, step: 0.5 },
    { path: `${prefix}.wallThicknessMeters`, group, label: `Толщина стен ${sizeLabel} арены`, description: 'Толщина связанных perimeter, room и corridor wall runs.', unit: 'meters', minimum: 0.4, maximum: 2, step: 0.1 },
    { path: `${prefix}.corridorWidthMeters`, group, label: `Ширина коридоров ${sizeLabel} арены`, description: 'Чистая боевая ширина между стенами: минимум три effective capsule диаметра для стрейфа.', unit: 'meters', minimum: 2.5, maximum: 10, step: 0.5 },
    { path: `${prefix}.doorwayWidthMeters`, group, label: `Ширина дверей ${sizeLabel} арены`, description: 'Чистый читаемый разрыв: минимум три effective capsule диаметра.', unit: 'meters', minimum: 2.5, maximum: 8, step: 0.5 },
    { path: `${prefix}.perimeterInsetMeters`, group, label: `Глубина уступов ${sizeLabel} арены`, description: 'Глубина размерно-зависимых ниш замкнутого внешнего контура.', unit: 'meters', minimum: 1, maximum: 10, step: 0.5 },
    { path: `${prefix}.routeInsetMeters`, group, label: `Отступ маршрута ${sizeLabel} арены`, description: 'Отступ внешнего кольцевого маршрута от границы арены.', unit: 'meters', minimum: 3, maximum: 16, step: 0.5 },
    { path: `${prefix}.recipeCount`, group, label: `Количество рецептов ${sizeLabel}`, description: 'Число доступных именованных композиций.', unit: 'count', minimum: 5, maximum: 5, step: 1 },
    { path: `${prefix}.elevationDeltaMeters`, group, label: `Перепад уровня ${sizeLabel}`, description: 'Высота поднятой зоны над базовым полом.', unit: 'meters', minimum: 0.5, maximum: 1.5, step: 0.25 },
    { path: `${prefix}.upperDeckHeightMultiplier`, group, label: `Высота второго этажа ${sizeLabel}`, description: 'Множитель перепада для самостоятельной верхней боевой палубы.', unit: 'ratio', minimum: 1, maximum: 3, step: 0.25 },
    { path: `${prefix}.upperDeckFootprintRatio`, group, label: `Площадь второго этажа ${sizeLabel}`, description: 'Доля центрального footprint, занимаемая галереей, балконом или второй боевой палубой.', unit: 'ratio', minimum: 0.2, maximum: 0.75, step: 0.05 },
    { path: `${prefix}.basementDepthMultiplier`, group, label: `Глубина подвала ${sizeLabel}`, description: 'Множитель перепада для нижнего обходного маршрута.', unit: 'ratio', minimum: 1, maximum: 3, step: 0.25 },
    { path: `${prefix}.rampWidthMeters`, group, label: `Ширина рампы ${sizeLabel}`, description: 'Чистая ширина двунаправленного перехода: минимум три effective capsule диаметра.', unit: 'meters', minimum: 2.5, maximum: 8, step: 0.5 },
    { path: `${prefix}.rampLengthMeters`, group, label: `Длина рампы ${sizeLabel}`, description: 'Горизонтальный пробег перехода между уровнями.', unit: 'meters', minimum: 3, maximum: 8, step: 0.5 },
    { path: `${prefix}.barrierLowMeters`, group, label: `Низкий барьер ${sizeLabel}`, description: 'Высота movement-only барьера.', unit: 'meters', minimum: 0.5, maximum: 1.5, step: 0.25 },
    { path: `${prefix}.barrierHighMeters`, group, label: `Высокий барьер ${sizeLabel}`, description: 'Высота высокого movement-only барьера.', unit: 'meters', minimum: 1, maximum: 2.5, step: 0.25 },
    { path: `${prefix}.coverCount`, group, label: `Количество укрытий ${sizeLabel} арены`, description: 'Число примыкающих к архитектуре buttress/pilaster cover primitives.', unit: 'count', minimum: 4, maximum: 32, step: 1 },
    { path: `${prefix}.coverSizeMeters`, group, label: `Размер укрытий ${sizeLabel} арены`, description: 'Базовый footprint примыкающего к стене укрытия.', unit: 'meters', minimum: 1, maximum: 6, step: 0.5 },
    { path: `${prefix}.spawnSlotCount`, group, label: `Spawn slots ${sizeLabel} арены`, description: 'Общее число distinct spawn slots во всех regions.', unit: 'count', minimum: 12, maximum: 32, step: 4 },
    { path: `${prefix}.minimumSpawnSeparationMeters`, group, label: `Разделение spawn ${sizeLabel} арены`, description: 'Минимальная дистанция между одновременно выбранными spawn slots.', unit: 'meters', minimum: 1, maximum: 8, step: 0.5 },
    { path: `${prefix}.maximumInitialEnemyLosRatio`, group, label: `Enemy LOS ${sizeLabel} арены`, description: 'Максимальная доля стартовых пар противников с прямой видимостью.', unit: 'ratio', minimum: 0, maximum: 1, step: 0.05 },
    { path: `${prefix}.maximumTeamDistanceRatio`, group, label: `Team fairness ${sizeLabel} арены`, description: 'Максимальное отношение средних навигационных дистанций команд до combat regions.', unit: 'ratio', minimum: 1, maximum: 2, step: 0.05 },
  ]
}

const descriptors: NumericProfileDescriptor[] = [
  {
    path: 'rules.player.startingHealth',
    group: 'player',
    label: 'Стартовое здоровье',
    description: 'Запас здоровья игрока при старте жизни и после возрождения.',
    unit: 'hp',
    minimum: 1,
    maximum: 500,
    step: 1,
  },
  {
    path: 'rules.player.capsule.radius',
    group: 'player-capsule',
    label: 'Радиус капсулы игрока',
    description: 'Радиус collision-капсулы игрока.',
    unit: 'meters',
    minimum: 0.2,
    maximum: 1,
    step: 0.01,
  },
  {
    path: 'rules.player.capsule.halfHeight',
    group: 'player-capsule',
    label: 'Полувысота капсулы игрока',
    description: 'Половина высоты цилиндрической части collision-капсулы.',
    unit: 'meters',
    minimum: 0.2,
    maximum: 1.5,
    step: 0.01,
  },
  {
    path: 'rules.player.capsule.skinWidth',
    group: 'player-capsule',
    label: 'Collision skin игрока',
    description: 'Минимальный зазор между капсулой игрока и статической геометрией.',
    unit: 'meters',
    minimum: 0.001,
    maximum: 0.1,
    step: 0.001,
  },
  {
    path: 'rules.player.eyeHeight',
    group: 'player-capsule',
    label: 'Высота глаз',
    description: 'Вертикальное смещение first-person камеры от центра капсулы.',
    unit: 'meters',
    minimum: 0.1,
    maximum: 2.5,
    step: 0.01,
  },
  {
    path: 'rules.player.movement.maximumGroundSpeed',
    group: 'player-movement',
    label: 'Максимальная скорость',
    description: 'Предельная горизонтальная скорость игрока на земле и в воздухе.',
    unit: 'meters-per-second',
    minimum: 1,
    maximum: 30,
    step: 0.1,
  },
  {
    path: 'rules.player.movement.groundAcceleration',
    group: 'player-movement',
    label: 'Ускорение на земле',
    description: 'Скорость набора горизонтальной скорости при управлении на земле.',
    unit: 'meters-per-second-squared',
    minimum: 0.1,
    maximum: 200,
    step: 0.1,
  },
  {
    path: 'rules.player.movement.groundDeceleration',
    group: 'player-movement',
    label: 'Торможение на земле',
    description: 'Скорость остановки игрока без directional input.',
    unit: 'meters-per-second-squared',
    minimum: 0.1,
    maximum: 200,
    step: 0.1,
  },
  {
    path: 'rules.player.movement.airAcceleration',
    group: 'player-movement',
    label: 'Ускорение в воздухе',
    description: 'Сила изменения горизонтальной скорости directional input в воздухе.',
    unit: 'meters-per-second-squared',
    minimum: 0.1,
    maximum: 100,
    step: 0.1,
  },
  {
    path: 'rules.player.movement.gravity',
    group: 'player-movement',
    label: 'Гравитация',
    description: 'Положительная величина вертикального ускорения вниз.',
    unit: 'meters-per-second-squared',
    minimum: 0.1,
    maximum: 100,
    step: 0.1,
  },
  {
    path: 'rules.player.movement.jumpSpeed',
    group: 'player-movement',
    label: 'Скорость прыжка',
    description: 'Вертикальная скорость, задаваемая при новом допустимом прыжке.',
    unit: 'meters-per-second',
    minimum: 0.1,
    maximum: 30,
    step: 0.1,
  },
  {
    path: 'rules.player.movement.autostepMaxHeight',
    group: 'player-movement',
    label: 'Высота autostep',
    description: 'Максимальная высота препятствия для автоматического шага.',
    unit: 'meters',
    minimum: 0,
    maximum: 1,
    step: 0.01,
  },
  {
    path: 'rules.player.movement.autostepMinWidth',
    group: 'player-movement',
    label: 'Минимальная ширина autostep',
    description: 'Минимальная доступная опорная ширина для автоматического шага.',
    unit: 'meters',
    minimum: 0,
    maximum: 1,
    step: 0.01,
  },
  {
    path: 'rules.player.movement.snapToGroundDistance',
    group: 'player-movement',
    label: 'Привязка к земле',
    description: 'Максимальная дистанция привязки капсулы к поверхности под ней.',
    unit: 'meters',
    minimum: 0,
    maximum: 1,
    step: 0.01,
  },
  {
    path: 'rules.player.movement.maxSlopeClimbDegrees',
    group: 'player-movement',
    label: 'Предельный подъём склона',
    description: 'Наибольший угол склона, по которому игрок может подниматься.',
    unit: 'degrees',
    minimum: 0,
    maximum: 89,
    step: 1,
  },
  {
    path: 'rules.player.movement.minSlopeSlideDegrees',
    group: 'player-movement',
    label: 'Начало скольжения по склону',
    description: 'Минимальный угол склона, при котором начинается скольжение.',
    unit: 'degrees',
    minimum: 1,
    maximum: 90,
    step: 1,
  },
  {
    path: 'rules.camera.horizontalSensitivity',
    group: 'camera',
    label: 'Горизонтальная чувствительность',
    description: 'Изменение yaw за единицу нормализованного look input.',
    unit: 'degrees',
    minimum: 0.1,
    maximum: 30,
    step: 0.1,
  },
  {
    path: 'rules.camera.verticalSensitivity',
    group: 'camera',
    label: 'Вертикальная чувствительность',
    description: 'Изменение pitch за единицу нормализованного look input.',
    unit: 'degrees',
    minimum: 0.1,
    maximum: 30,
    step: 0.1,
  },
  {
    path: 'rules.camera.maximumPitchDegrees',
    group: 'camera',
    label: 'Предельный pitch',
    description: 'Симметричная граница вертикального угла first-person камеры.',
    unit: 'degrees',
    minimum: 30,
    maximum: 89,
    step: 1,
  },
  {
    path: 'rules.camera.fieldOfViewDegrees',
    group: 'camera',
    label: 'Поле зрения',
    description: 'Вертикальный угол обзора first-person камеры.',
    unit: 'degrees',
    minimum: 50,
    maximum: 130,
    step: 1,
  },
  {
    path: 'rules.camera.nearPlane',
    group: 'camera',
    label: 'Ближняя плоскость камеры',
    description: 'Минимальная дистанция отображения first-person камеры.',
    unit: 'meters',
    minimum: 0.01,
    maximum: 10,
    step: 0.01,
  },
  {
    path: 'rules.camera.farPlane',
    group: 'camera',
    label: 'Дальняя плоскость камеры',
    description: 'Максимальная дистанция отображения first-person камеры.',
    unit: 'meters',
    minimum: 1,
    maximum: 1_000,
    step: 1,
  },
  {
    path: 'rules.weapons.doubleBarrelShotgun.startingAmmo',
    group: 'shotgun',
    label: 'Боезапас двухстволки',
    description: 'Количество выстрелов в начале каждой жизни.',
    unit: 'ammo',
    minimum: 0,
    maximum: 200,
    step: 1,
  },
  {
    path: 'rules.weapons.doubleBarrelShotgun.pelletCount',
    group: 'shotgun',
    label: 'Количество дробин',
    description: 'Количество детерминированных ray pellets в одном выстреле.',
    unit: 'pellets',
    minimum: 1,
    maximum: 64,
    step: 1,
  },
  {
    path: 'rules.weapons.doubleBarrelShotgun.spreadDegrees',
    group: 'shotgun',
    label: 'Разброс дроби',
    description: 'Максимальный угловой радиус детерминированного pellet pattern.',
    unit: 'degrees',
    minimum: 0,
    maximum: 45,
    step: 0.1,
  },
  {
    path: 'rules.weapons.doubleBarrelShotgun.rangeMeters',
    group: 'shotgun',
    label: 'Дальность дроби',
    description: 'Предельная дистанция hitscan-проверки каждой дробины.',
    unit: 'meters',
    minimum: 1,
    maximum: 200,
    step: 0.5,
  },
  {
    path: 'rules.weapons.doubleBarrelShotgun.cooldownSeconds',
    group: 'shotgun',
    label: 'Интервал между выстрелами',
    description: 'Минимальное время между двумя допустимыми выстрелами.',
    unit: 'seconds',
    minimum: 0.05,
    maximum: 5,
    step: 0.05,
  },
  {
    path: 'rules.weapons.doubleBarrelShotgun.muzzleFeedbackSeconds',
    group: 'shotgun',
    label: 'Длительность вспышки',
    description: 'Время показа visual-only muzzle feedback после выстрела.',
    unit: 'seconds',
    minimum: 0,
    maximum: 2,
    step: 0.01,
  },
  {
    path: 'rules.weapons.doubleBarrelShotgun.impactFeedbackSeconds',
    group: 'shotgun',
    label: 'Длительность impact feedback',
    description: 'Время показа visual-only попадания дробины.',
    unit: 'seconds',
    minimum: 0,
    maximum: 2,
    step: 0.01,
  },
  {
    path: 'rules.weapons.doubleBarrelShotgun.hitmarkerSeconds',
    group: 'shotgun',
    label: 'Длительность hitmarker',
    description: 'Время показа подтверждения нанесённого урона.',
    unit: 'seconds',
    minimum: 0,
    maximum: 2,
    step: 0.01,
  },
  {
    path: 'rules.weapons.doubleBarrelShotgun.damage.head',
    group: 'shotgun',
    label: 'Урон в голову',
    description: 'Суммарный урон полного попадания дроби в голову.',
    unit: 'damage',
    minimum: 0,
    maximum: 500,
    step: 1,
  },
  {
    path: 'rules.weapons.doubleBarrelShotgun.damage.torso',
    group: 'shotgun',
    label: 'Урон в корпус',
    description: 'Суммарный урон полного попадания дроби в корпус.',
    unit: 'damage',
    minimum: 0,
    maximum: 500,
    step: 1,
  },
  {
    path: 'rules.weapons.doubleBarrelShotgun.damage.limb',
    group: 'shotgun',
    label: 'Урон в конечности',
    description: 'Суммарный урон полного попадания дроби в руку или ногу.',
    unit: 'damage',
    minimum: 0,
    maximum: 500,
    step: 1,
  },
  {
    path: 'rules.match.duration.defaultMinutes',
    group: 'match',
    label: 'Длительность матча по умолчанию',
    description: 'Предустановленная длительность нового матча.',
    unit: 'minutes',
    minimum: 1,
    maximum: 30,
    step: 1,
  },
  {
    path: 'rules.match.duration.minimumMinutes',
    group: 'match',
    label: 'Минимальная длительность матча',
    description: 'Нижняя граница длительности, доступная в настройках матча.',
    unit: 'minutes',
    minimum: 1,
    maximum: 30,
    step: 1,
  },
  {
    path: 'rules.match.duration.maximumMinutes',
    group: 'match',
    label: 'Максимальная длительность матча',
    description: 'Верхняя граница длительности, доступная в настройках матча.',
    unit: 'minutes',
    minimum: 1,
    maximum: 30,
    step: 1,
  },
  {
    path: 'rules.match.scoreTarget.defaultPoints',
    group: 'match',
    label: 'Цель по очкам по умолчанию',
    description: 'Стартовое значение при включении опциональной цели матча.',
    unit: 'points',
    minimum: 1_000,
    maximum: 20_000,
    step: 100,
  },
  {
    path: 'rules.match.scoreTarget.minimumPoints',
    group: 'match',
    label: 'Минимальная цель по очкам',
    description: 'Нижняя граница цели по очкам в настройках матча.',
    unit: 'points',
    minimum: 1_000,
    maximum: 20_000,
    step: 100,
  },
  {
    path: 'rules.match.scoreTarget.maximumPoints',
    group: 'match',
    label: 'Максимальная цель по очкам',
    description: 'Верхняя граница цели по очкам в настройках матча.',
    unit: 'points',
    minimum: 1_000,
    maximum: 20_000,
    step: 100,
  },
  {
    path: 'rules.match.scoreTarget.stepPoints',
    group: 'match',
    label: 'Шаг цели по очкам',
    description: 'Дискретность выбора цели по очкам в настройках матча.',
    unit: 'points',
    minimum: 100,
    maximum: 1_000,
    step: 100,
  },
  {
    path: 'rules.scoring.assist.windowSeconds',
    group: 'scoring-assist',
    label: 'Окно участия в убийстве',
    description: 'Максимальное время между нанесённым уроном и смертью цели для зачёта участия.',
    unit: 'seconds',
    minimum: 0,
    maximum: 30,
    step: 0.1,
  },
  {
    path: 'rules.scoring.assist.points',
    group: 'scoring-assist',
    label: 'Очки за участие',
    description: 'Количество очков, начисляемое за одно участие в убийстве.',
    unit: 'points',
    minimum: 0,
    maximum: 1_000,
    step: 1,
  },
  {
    path: 'rules.scoring.killChain.maximumGapSeconds',
    group: 'scoring-kill-chain',
    label: 'Интервал серии убийств',
    description: 'Максимальный интервал между соседними убийствами одной серии.',
    unit: 'seconds',
    minimum: 0,
    maximum: 60,
    step: 0.1,
  },
  ...[1, 2, 3, 4, 5].map(
    (killCount): NumericProfileDescriptor => ({
      path: `rules.scoring.killChain.cumulativeTotals.${killCount - 1}`,
      group: 'scoring-kill-chain',
      label: `Сумма очков за серию из ${killCount}`,
      description: `Итоговая сумма очков за непрерывную серию из ${killCount} убийств.` ,
      unit: 'points',
      minimum: 0,
      maximum: 100_000,
      step: 1,
    }),
  ),
  {
    path: 'rules.scoring.killChain.postFiveIncrementPoints',
    group: 'scoring-kill-chain',
    label: 'Прирост после пятого убийства',
    description: 'Очки за каждое следующее убийство после пятого в той же серии.',
    unit: 'points',
    minimum: 0,
    maximum: 10_000,
    step: 1,
  },
  {
    path: 'rules.death.killcamSeconds',
    group: 'death',
    label: 'Длительность killcam',
    description: 'Время наблюдения после смерти до возрождения.',
    unit: 'seconds',
    minimum: 0,
    maximum: 15,
    step: 0.1,
  },
  {
    path: 'rules.death.corpseSeconds',
    group: 'death',
    label: 'Время существования тела',
    description: 'Время показа visual-only тела погибшей жизни.',
    unit: 'seconds',
    minimum: 0,
    maximum: 60,
    step: 0.1,
  },
  ...ARENA_GENERATOR_SIZES.flatMap(arenaGeneratorDescriptors),
  ...BOT_DESCRIPTORS,
  { path: 'rules.weapons.doubleBarrelShotgun.emptyRefillAmmo', group: 'shotgun', label: 'Пополнение при нуле', description: 'Автоматическое пополнение без сброса cooldown.', unit: 'ammo', minimum: 1, maximum: 100, step: 1 },
]

export const GAME_DESIGN_PROFILE_DESCRIPTORS = deepFreeze(descriptors)

export type ProfileValidationErrorCode =
  | 'MISSING_FIELD'
  | 'INVALID_TYPE'
  | 'UNSUPPORTED_SCHEMA_VERSION'
  | 'INVALID_SOURCE'
  | 'UNRECOGNIZED_FIELD'
  | 'OUT_OF_RANGE'
  | 'INVALID_STEP'
  | 'MATCH_DURATION_ORDER'
  | 'MATCH_SCORE_TARGET_ORDER'
  | 'KILL_CHAIN_TOTALS_NOT_INCREASING'
  | 'CAPSULE_GEOMETRY_INVALID'
  | 'EYE_HEIGHT_OUTSIDE_CAPSULE'
  | 'CAMERA_PLANES_ORDER'
  | 'SLOPE_ANGLE_ORDER'
  | 'SHOTGUN_CONTRACT_INVALID'
  | 'BOT_CONTRACT_INVALID'
  | 'ARENA_GENERATOR_CONTRACT_INVALID'
  | 'CONTENT_HASH_MISMATCH'

export interface ProfileValidationError {
  code: ProfileValidationErrorCode
  paths: readonly string[]
  message: string
}

export type ProfileValidationResult =
  | { ok: true; profile: ValidatedGameDesignProfile }
  | { ok: false; errors: readonly ProfileValidationError[] }

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readPath(value: unknown, path: string): unknown {
  let cursor = value
  for (const segment of path.split('.')) {
    if (Array.isArray(cursor)) {
      const index = Number(segment)
      if (!Number.isInteger(index) || index < 0 || index >= cursor.length) return undefined
      cursor = cursor[index]
      continue
    }
    if (!isRecord(cursor) || !Object.hasOwn(cursor, segment)) return undefined
    cursor = cursor[segment]
  }
  return cursor
}

function matchesStep(value: number, minimum: number, step: number): boolean {
  const scaled = (value - minimum) / step
  return Math.abs(scaled - Math.round(scaled)) <= 1e-9
}

function collectLeafPaths(value: unknown, prefix: string, ancestors: Set<object>): string[] {
  if (typeof value !== 'object' || value === null) return [prefix]
  if (ancestors.has(value)) return [prefix]

  ancestors.add(value)
  try {
    if (Array.isArray(value)) {
      return value.length === 0
        ? [prefix]
        : value.flatMap((child, index) => collectLeafPaths(child, `${prefix}.${index}`, ancestors))
    }
    return Object.entries(value).flatMap(([key, child]) =>
      collectLeafPaths(child, `${prefix}.${key}`, ancestors),
    )
  } finally {
    ancestors.delete(value)
  }
}

export function profileContentPayload(profile: GameDesignProfile): GameDesignProfilePayload {
  const { contentHash: _contentHash, ...payload } = profile
  return payload
}

export function computeProfileContentHash(profile: GameDesignProfile): ContentHash {
  return contentHash(profileContentPayload(profile))
}

export function canonicalProfileJson(profile: GameDesignProfile): string {
  return canonicalSerialize(profile)
}

// Only cache detached outputs owned and frozen by this validator. Untrusted
// inputs are always validated again, even when the caller reuses their identity.
const validatedProfiles = new WeakMap<object, ProfileValidationResult>()
export function validateGameDesignProfile(value: unknown): ProfileValidationResult {
  // Frozen v6 reader retains old rule bytes; new generation requires the v7 recipe fields.
  if (typeof value === 'object' && value !== null && 'schemaVersion' in value && value.schemaVersion === 6) return validateLegacyProfile(value) as unknown as ProfileValidationResult
  if (value !== null && typeof value === 'object') {
    const cached = validatedProfiles.get(value)
    if (cached) return cached
  }
  const errors: ProfileValidationError[] = []
  if (!isRecord(value)) {
    return {
      ok: false,
      errors: [{ code: 'INVALID_TYPE', paths: ['$'], message: 'Профиль должен быть объектом.' }],
    }
  }

  const requiredStrings = ['id', 'name', 'contentHash'] as const
  for (const path of requiredStrings) {
    const field = readPath(value, path)
    if (field === undefined) {
      errors.push({ code: 'MISSING_FIELD', paths: [path], message: `Отсутствует обязательное поле ${path}.` })
    } else if (typeof field !== 'string' || field.length === 0) {
      errors.push({ code: 'INVALID_TYPE', paths: [path], message: `Поле ${path} должно быть непустой строкой.` })
    }
  }
  if (typeof value.contentHash === 'string' && value.contentHash.length > 0 && !isContentHash(value.contentHash)) {
    errors.push({
      code: 'INVALID_TYPE',
      paths: ['contentHash'],
      message: 'contentHash должен соответствовать формату fnv1a64-v1.',
    })
  }

  const allowedRootFields = new Set(['schemaVersion', 'id', 'name', 'revision', 'source', 'rules', 'contentHash'])
  for (const key of Object.keys(value).sort()) {
    if (!allowedRootFields.has(key)) {
      errors.push({
        code: 'UNRECOGNIZED_FIELD',
        paths: [key],
        message: `Поле ${key} не входит в schema GameDesignProfile.`,
      })
    }
  }

  const descriptorPathSet = new Set(GAME_DESIGN_PROFILE_DESCRIPTORS.map(({ path }) => path))
  for (const path of collectLeafPaths(value.rules, 'rules', new Set<object>()).sort()) {
    if (!descriptorPathSet.has(path)) {
      errors.push({
        code: 'UNRECOGNIZED_FIELD',
        paths: [path],
        message: `Поле ${path} не зарегистрировано в descriptor registry.`,
      })
    }
  }

  const schemaVersion = readPath(value, 'schemaVersion')
  if (schemaVersion === undefined) {
    errors.push({ code: 'MISSING_FIELD', paths: ['schemaVersion'], message: 'Отсутствует schemaVersion.' })
  } else if (schemaVersion !== GAME_DESIGN_PROFILE_SCHEMA_VERSION) {
    errors.push({
      code: 'UNSUPPORTED_SCHEMA_VERSION',
      paths: ['schemaVersion'],
      message: `Поддерживается schemaVersion ${GAME_DESIGN_PROFILE_SCHEMA_VERSION}.`,
    })
  }

  const revision = readPath(value, 'revision')
  if (revision === undefined) {
    errors.push({ code: 'MISSING_FIELD', paths: ['revision'], message: 'Отсутствует revision.' })
  } else if (typeof revision !== 'number' || !Number.isInteger(revision) || revision < 1) {
    errors.push({ code: 'INVALID_TYPE', paths: ['revision'], message: 'revision должен быть целым числом не меньше 1.' })
  }

  const source = readPath(value, 'source')
  if (source === undefined) {
    errors.push({ code: 'MISSING_FIELD', paths: ['source'], message: 'Отсутствует source.' })
  } else if (source !== 'shipped' && source !== 'local') {
    errors.push({ code: 'INVALID_SOURCE', paths: ['source'], message: 'source должен быть shipped или local.' })
  }

  for (const descriptor of GAME_DESIGN_PROFILE_DESCRIPTORS) {
    const field = readPath(value, descriptor.path)
    if (field === undefined) {
      errors.push({
        code: 'MISSING_FIELD',
        paths: [descriptor.path],
        message: `Отсутствует обязательное поле ${descriptor.path}.`,
      })
      continue
    }
    if (typeof field !== 'number' || !Number.isFinite(field)) {
      errors.push({
        code: 'INVALID_TYPE',
        paths: [descriptor.path],
        message: `Поле ${descriptor.path} должно быть конечным числом.`,
      })
      continue
    }
    if (field < descriptor.minimum || field > descriptor.maximum) {
      errors.push({
        code: 'OUT_OF_RANGE',
        paths: [descriptor.path],
        message: `${descriptor.path}: допустимый диапазон ${descriptor.minimum}–${descriptor.maximum} ${descriptor.unit}.`,
      })
    } else if (!matchesStep(field, descriptor.minimum, descriptor.step)) {
      errors.push({
        code: 'INVALID_STEP',
        paths: [descriptor.path],
        message: `${descriptor.path}: значение должно соответствовать шагу ${descriptor.step}.`,
      })
    }
  }

  const minimumMinutes = readPath(value, 'rules.match.duration.minimumMinutes')
  const defaultMinutes = readPath(value, 'rules.match.duration.defaultMinutes')
  const maximumMinutes = readPath(value, 'rules.match.duration.maximumMinutes')
  if (
    typeof minimumMinutes === 'number' &&
    Number.isFinite(minimumMinutes) &&
    typeof defaultMinutes === 'number' &&
    Number.isFinite(defaultMinutes) &&
    typeof maximumMinutes === 'number' &&
    Number.isFinite(maximumMinutes) &&
    !(minimumMinutes <= defaultMinutes && defaultMinutes <= maximumMinutes)
  ) {
    errors.push({
      code: 'MATCH_DURATION_ORDER',
      paths: [
        'rules.match.duration.minimumMinutes',
        'rules.match.duration.defaultMinutes',
        'rules.match.duration.maximumMinutes',
      ],
      message: 'Длительность матча должна удовлетворять minimum <= default <= maximum.',
    })
  }

  const minimumTarget = readPath(value, 'rules.match.scoreTarget.minimumPoints')
  const defaultTarget = readPath(value, 'rules.match.scoreTarget.defaultPoints')
  const maximumTarget = readPath(value, 'rules.match.scoreTarget.maximumPoints')
  const targetStep = readPath(value, 'rules.match.scoreTarget.stepPoints')
  if (
    typeof minimumTarget === 'number' && Number.isFinite(minimumTarget) &&
    typeof defaultTarget === 'number' && Number.isFinite(defaultTarget) &&
    typeof maximumTarget === 'number' && Number.isFinite(maximumTarget) &&
    typeof targetStep === 'number' && Number.isFinite(targetStep) &&
    (!(minimumTarget <= defaultTarget && defaultTarget <= maximumTarget) ||
      !matchesStep(defaultTarget, minimumTarget, targetStep) ||
      !matchesStep(maximumTarget, minimumTarget, targetStep))
  ) {
    errors.push({
      code: 'MATCH_SCORE_TARGET_ORDER',
      paths: [
        'rules.match.scoreTarget.minimumPoints',
        'rules.match.scoreTarget.defaultPoints',
        'rules.match.scoreTarget.maximumPoints',
        'rules.match.scoreTarget.stepPoints',
      ],
      message: 'Цель по очкам должна удовлетворять minimum <= default <= maximum и принятому шагу.',
    })
  }

  const capsuleRadius = readPath(value, 'rules.player.capsule.radius')
  const capsuleHalfHeight = readPath(value, 'rules.player.capsule.halfHeight')
  const capsuleSkinWidth = readPath(value, 'rules.player.capsule.skinWidth')
  const eyeHeight = readPath(value, 'rules.player.eyeHeight')
  if (
    typeof capsuleRadius === 'number' &&
    Number.isFinite(capsuleRadius) &&
    typeof capsuleSkinWidth === 'number' &&
    Number.isFinite(capsuleSkinWidth) &&
    capsuleSkinWidth >= capsuleRadius
  ) {
    errors.push({
      code: 'CAPSULE_GEOMETRY_INVALID',
      paths: ['rules.player.capsule.radius', 'rules.player.capsule.skinWidth'],
      message: 'Collision skin должен быть меньше радиуса капсулы игрока.',
    })
  }
  if (
    typeof capsuleRadius === 'number' &&
    Number.isFinite(capsuleRadius) &&
    typeof capsuleHalfHeight === 'number' &&
    Number.isFinite(capsuleHalfHeight) &&
    typeof capsuleSkinWidth === 'number' &&
    Number.isFinite(capsuleSkinWidth) &&
    typeof eyeHeight === 'number' &&
    Number.isFinite(eyeHeight) &&
    eyeHeight > capsuleHalfHeight + capsuleRadius - capsuleSkinWidth
  ) {
    errors.push({
      code: 'EYE_HEIGHT_OUTSIDE_CAPSULE',
      paths: [
        'rules.player.eyeHeight',
        'rules.player.capsule.halfHeight',
        'rules.player.capsule.radius',
        'rules.player.capsule.skinWidth',
      ],
      message: 'Высота глаз должна оставаться внутри верхней границы collision-капсулы.',
    })
  }

  const cameraNearPlane = readPath(value, 'rules.camera.nearPlane')
  const cameraFarPlane = readPath(value, 'rules.camera.farPlane')
  if (
    typeof cameraNearPlane === 'number' &&
    Number.isFinite(cameraNearPlane) &&
    typeof cameraFarPlane === 'number' &&
    Number.isFinite(cameraFarPlane) &&
    cameraNearPlane >= cameraFarPlane
  ) {
    errors.push({
      code: 'CAMERA_PLANES_ORDER',
      paths: ['rules.camera.nearPlane', 'rules.camera.farPlane'],
      message: 'Ближняя плоскость камеры должна быть ближе дальней.',
    })
  }

  const maxSlopeClimbDegrees = readPath(value, 'rules.player.movement.maxSlopeClimbDegrees')
  const minSlopeSlideDegrees = readPath(value, 'rules.player.movement.minSlopeSlideDegrees')
  if (
    typeof maxSlopeClimbDegrees === 'number' &&
    Number.isFinite(maxSlopeClimbDegrees) &&
    typeof minSlopeSlideDegrees === 'number' &&
    Number.isFinite(minSlopeSlideDegrees) &&
    maxSlopeClimbDegrees > minSlopeSlideDegrees
  ) {
    errors.push({
      code: 'SLOPE_ANGLE_ORDER',
      paths: [
        'rules.player.movement.maxSlopeClimbDegrees',
        'rules.player.movement.minSlopeSlideDegrees',
      ],
      message: 'Угол подъёма по склону не должен превышать угол начала скольжения.',
    })
  }

  const pelletCount = readPath(value, 'rules.weapons.doubleBarrelShotgun.pelletCount')
  const rangeMeters = readPath(value, 'rules.weapons.doubleBarrelShotgun.rangeMeters')
  const cooldownSeconds = readPath(value, 'rules.weapons.doubleBarrelShotgun.cooldownSeconds')
  if (
    (typeof pelletCount === 'number' && Number.isFinite(pelletCount) && pelletCount <= 0) ||
    (typeof rangeMeters === 'number' && Number.isFinite(rangeMeters) && rangeMeters <= 0) ||
    (typeof cooldownSeconds === 'number' && Number.isFinite(cooldownSeconds) && cooldownSeconds <= 0)
  ) {
    errors.push({
      code: 'SHOTGUN_CONTRACT_INVALID',
      paths: [
        'rules.weapons.doubleBarrelShotgun.pelletCount',
        'rules.weapons.doubleBarrelShotgun.rangeMeters',
        'rules.weapons.doubleBarrelShotgun.cooldownSeconds',
      ],
      message: 'Количество дробин, дальность и интервал между выстрелами должны быть положительными.',
    })
  }

  const totals = readPath(value, 'rules.scoring.killChain.cumulativeTotals')
  for (const difficulty of ['easy', 'normal', 'hard']) {
    const prefix = `rules.bots.${difficulty}`
    const memory = readPath(value, `${prefix}.memorySeconds`)
    const decision = readPath(value, `${prefix}.decisionSeconds`)
    const preferred = readPath(value, `${prefix}.preferredDistanceMeters`)
    if ((typeof memory === 'number' && typeof decision === 'number' && memory < decision) ||
        (typeof preferred === 'number' && typeof rangeMeters === 'number' && preferred >= rangeMeters)) {
      errors.push({ code: 'BOT_CONTRACT_INVALID', paths: [`${prefix}.memorySeconds`, `${prefix}.decisionSeconds`, `${prefix}.preferredDistanceMeters`, 'rules.weapons.doubleBarrelShotgun.rangeMeters'], message: 'Память бота должна охватывать цикл решений, а дистанция боя — оставаться внутри дальности оружия.' })
    }
  }
  if (
    Array.isArray(totals) &&
    totals.length === 5 &&
    totals.every((total) => typeof total === 'number' && Number.isFinite(total)) &&
    totals.some((total, index) => index > 0 && total <= totals[index - 1]!)
  ) {
    errors.push({
      code: 'KILL_CHAIN_TOTALS_NOT_INCREASING',
      paths: [
        'rules.scoring.killChain.cumulativeTotals.0',
        'rules.scoring.killChain.cumulativeTotals.1',
        'rules.scoring.killChain.cumulativeTotals.2',
        'rules.scoring.killChain.cumulativeTotals.3',
        'rules.scoring.killChain.cumulativeTotals.4',
      ],
      message: 'Суммарные очки серии убийств должны строго возрастать.',
    })
  }

  const arenaPaths: string[] = []
  let arenaContractInvalid = false
  for (const size of ARENA_GENERATOR_SIZES) {
    const prefix = `rules.arenaGenerator.presets.${size}`
    const width = readPath(value, `${prefix}.widthMeters`)
    const depth = readPath(value, `${prefix}.depthMeters`)
    const wallThickness = readPath(value, `${prefix}.wallThicknessMeters`)
    const corridorWidth = readPath(value, `${prefix}.corridorWidthMeters`)
    const doorwayWidth = readPath(value, `${prefix}.doorwayWidthMeters`)
    const perimeterInset = readPath(value, `${prefix}.perimeterInsetMeters`)
    const routeInset = readPath(value, `${prefix}.routeInsetMeters`)
    const coverSize = readPath(value, `${prefix}.coverSizeMeters`)
    const slotCount = readPath(value, `${prefix}.spawnSlotCount`)
    const separation = readPath(value, `${prefix}.minimumSpawnSeparationMeters`)
    const elevation = readPath(value, `${prefix}.elevationDeltaMeters`) as number
    const upperDeckHeightMultiplier = readPath(value, `${prefix}.upperDeckHeightMultiplier`) as number
    const upperDeckFootprintRatio = readPath(value, `${prefix}.upperDeckFootprintRatio`) as number
    const basementDepthMultiplier = readPath(value, `${prefix}.basementDepthMultiplier`) as number
    const rampWidth = readPath(value, `${prefix}.rampWidthMeters`) as number
    const rampLength = readPath(value, `${prefix}.rampLengthMeters`) as number
    const low = readPath(value, `${prefix}.barrierLowMeters`) as number
    const high = readPath(value, `${prefix}.barrierHighMeters`) as number
    const ceiling = readPath(value, `${prefix}.wallHeightMeters`) as number
    const capsuleHeight = 2 * ((readPath(value, 'rules.player.capsule.halfHeight') as number) + (capsuleRadius as number))
    const tripleCapsuleWidth = 3 * 2 * ((capsuleRadius as number) + (capsuleSkinWidth as number))
    const maxSlope = readPath(value, 'rules.player.movement.maxSlopeClimbDegrees') as number
    const upperHeight = elevation * upperDeckHeightMultiplier
    if (rampWidth < tripleCapsuleWidth || rampWidth < (doorwayWidth as number) || rampWidth > (corridorWidth as number) || Math.atan2(upperHeight, rampLength) * 180 / Math.PI > maxSlope || upperHeight + capsuleHeight >= ceiling || upperDeckFootprintRatio * (width as number) < rampWidth || basementDepthMultiplier < 1 || low > high || high >= ceiling || rampLength * 2 >= (width as number) - 2 * ((perimeterInset as number) + (routeInset as number))) {
      arenaContractInvalid = true
      arenaPaths.push(...['elevationDeltaMeters', 'upperDeckHeightMultiplier', 'upperDeckFootprintRatio', 'basementDepthMultiplier', 'rampWidthMeters', 'rampLengthMeters', 'barrierLowMeters', 'barrierHighMeters'].map(field => `${prefix}.${field}`))
    }
    const numeric = [width, depth, wallThickness, corridorWidth, doorwayWidth, perimeterInset, routeInset, coverSize, slotCount, separation]
    if (!numeric.every((entry) => typeof entry === 'number' && Number.isFinite(entry))) continue
    if (
      (routeInset as number) * 2 + (coverSize as number) >= Math.min(width as number, depth as number) ||
      (perimeterInset as number) * 2 + (corridorWidth as number) >= Math.min(width as number, depth as number) ||
      (doorwayWidth as number) > (corridorWidth as number) ||
      (doorwayWidth as number) < tripleCapsuleWidth ||
      (corridorWidth as number) < tripleCapsuleWidth ||
      (corridorWidth as number) <= (doorwayWidth as number) + (wallThickness as number) ||
      (slotCount as number) < 12 ||
      (separation as number) <= 2 * ((capsuleRadius as number) + (capsuleSkinWidth as number))
    ) {
      arenaContractInvalid = true
      arenaPaths.push(
        `${prefix}.widthMeters`,
        `${prefix}.depthMeters`,
        `${prefix}.wallThicknessMeters`,
        `${prefix}.corridorWidthMeters`,
        `${prefix}.doorwayWidthMeters`,
        `${prefix}.perimeterInsetMeters`,
        `${prefix}.routeInsetMeters`,
        `${prefix}.coverSizeMeters`,
        `${prefix}.spawnSlotCount`,
        `${prefix}.minimumSpawnSeparationMeters`,
      )
    }
  }
  if (arenaContractInvalid) {
    errors.push({
      code: 'ARENA_GENERATOR_CONTRACT_INVALID',
      paths: [...new Set(arenaPaths)].sort(),
      message: 'Generator preset должен оставлять проходимые двери/коридоры, неровный perimeter, минимум 12 slots и capsule-safe opponent separation.',
    })
  }

  if (errors.length === 0) {
    const profile = value as unknown as GameDesignProfile
    if (computeProfileContentHash(profile) !== profile.contentHash) {
      errors.push({
        code: 'CONTENT_HASH_MISMATCH',
        paths: ['contentHash'],
        message: 'contentHash не соответствует эффективному содержимому профиля.',
      })
    }
  }

  if (errors.length !== 0) {
    return { ok: false, errors: deepFreeze(errors) }
  }

  // Validation is the trust boundary: callers receive a detached immutable
  // snapshot, so later mutation of the untrusted input cannot stale its hash.
  const detached = JSON.parse(canonicalSerialize(value)) as GameDesignProfile
  const result: ProfileValidationResult = { ok: true, profile: deepFreeze(detached) }
  validatedProfiles.set(result.profile, result)
  return result
}

const prototypePayload: GameDesignProfilePayload = {
  schemaVersion: GAME_DESIGN_PROFILE_SCHEMA_VERSION,
  id: 'prototype-v1',
  name: 'Prototype v1',
  revision: 8,
  source: 'shipped',
  rules: {
    bots: BOT_RULES,
    player: {
      startingHealth: 100,
      capsule: { radius: 0.4, halfHeight: 0.5, skinWidth: 0.01 },
      eyeHeight: 0.55,
      movement: {
        maximumGroundSpeed: 9,
        groundAcceleration: 45,
        groundDeceleration: 32,
        airAcceleration: 12,
        gravity: 24,
        jumpSpeed: 8.5,
        autostepMaxHeight: 0.45,
        autostepMinWidth: 0.25,
        snapToGroundDistance: 0.2,
        maxSlopeClimbDegrees: 45,
        minSlopeSlideDegrees: 60,
      },
    },
    camera: {
      horizontalSensitivity: 4,
      verticalSensitivity: 3.2,
      maximumPitchDegrees: 85,
      fieldOfViewDegrees: 90,
      nearPlane: 0.05,
      farPlane: 250,
    },
    weapons: {
      doubleBarrelShotgun: {
        startingAmmo: 20,
        emptyRefillAmmo: 20,
        pelletCount: 12,
        spreadDegrees: 7,
        rangeMeters: 35,
        cooldownSeconds: 0.7,
        muzzleFeedbackSeconds: 0.08,
        impactFeedbackSeconds: 0.12,
        hitmarkerSeconds: 0.15,
        damage: { head: 100, torso: 70, limb: 30 },
      },
    },
    match: {
      duration: { defaultMinutes: 5, minimumMinutes: 1, maximumMinutes: 30 },
      scoreTarget: { defaultPoints: 3_000, minimumPoints: 1_000, maximumPoints: 20_000, stepPoints: 100 },
    },
    scoring: {
      assist: { windowSeconds: 5, points: 50 },
      killChain: {
        maximumGapSeconds: 10,
        cumulativeTotals: [100, 300, 500, 800, 1_200],
        postFiveIncrementPoints: 400,
      },
    },
    death: { killcamSeconds: 3, corpseSeconds: 10 },
    arenaGenerator: {
      presets: {
        small: { recipeCount: 5, elevationDeltaMeters: 0.75, upperDeckHeightMultiplier: 1.5, upperDeckFootprintRatio: 0.4, basementDepthMultiplier: 1.25, rampWidthMeters: 4, rampLengthMeters: 4, barrierLowMeters: 0.75, barrierHighMeters: 1.5, widthMeters: 34, depthMeters: 26, wallHeightMeters: 4, wallThicknessMeters: 0.9, corridorWidthMeters: 4, doorwayWidthMeters: 3, perimeterInsetMeters: 3, routeInsetMeters: 4.5, coverCount: 4, coverSizeMeters: 2, spawnSlotCount: 16, minimumSpawnSeparationMeters: 4, maximumInitialEnemyLosRatio: 0.35, maximumTeamDistanceRatio: 1.2 },
        medium: { recipeCount: 5, elevationDeltaMeters: 0.75, upperDeckHeightMultiplier: 1.75, upperDeckFootprintRatio: 0.45, basementDepthMultiplier: 1.5, rampWidthMeters: 5, rampLengthMeters: 4, barrierLowMeters: 0.75, barrierHighMeters: 1.5, widthMeters: 46, depthMeters: 34, wallHeightMeters: 4.5, wallThicknessMeters: 1.4, corridorWidthMeters: 5, doorwayWidthMeters: 3.5, perimeterInsetMeters: 4, routeInsetMeters: 5.5, coverCount: 6, coverSizeMeters: 2.5, spawnSlotCount: 16, minimumSpawnSeparationMeters: 5, maximumInitialEnemyLosRatio: 0.3, maximumTeamDistanceRatio: 1.15 },
        large: { recipeCount: 5, elevationDeltaMeters: 0.75, upperDeckHeightMultiplier: 2, upperDeckFootprintRatio: 0.5, basementDepthMultiplier: 1.75, rampWidthMeters: 6, rampLengthMeters: 4, barrierLowMeters: 0.75, barrierHighMeters: 1.5, widthMeters: 60, depthMeters: 44, wallHeightMeters: 5, wallThicknessMeters: 1.8, corridorWidthMeters: 6, doorwayWidthMeters: 4, perimeterInsetMeters: 5, routeInsetMeters: 6.5, coverCount: 8, coverSizeMeters: 3, spawnSlotCount: 20, minimumSpawnSeparationMeters: 6, maximumInitialEnemyLosRatio: 0.25, maximumTeamDistanceRatio: 1.1 },
      },
    },
  },
}

const prototypeWithHash: GameDesignProfile = {
  ...prototypePayload,
  contentHash: contentHash(prototypePayload),
}

const prototypeValidation = validateGameDesignProfile(prototypeWithHash)
if (!prototypeValidation.ok) {
  throw new Error(`Некорректный shipped profile: ${prototypeValidation.errors.map((error) => error.code).join(', ')}`)
}

export const PROTOTYPE_V1_PROFILE: ShippedGameDesignProfile = prototypeValidation.profile
