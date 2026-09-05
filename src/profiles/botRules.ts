import type { NumericProfileDescriptor } from './gameDesignProfile'

export const BOT_DIFFICULTIES = ['easy', 'normal', 'hard'] as const
export type BotDifficulty = (typeof BOT_DIFFICULTIES)[number]
export const BOT_LABELS: Record<BotDifficulty, string> = { easy: 'Салага', normal: 'Боец', hard: 'Ветеран' }

// One source for defaults and editor/validation metadata. Degrees are converted
// only at the action boundary; all timers use simulation time.
const fields = {
  reactionSeconds: [0.6, 0.28, 0.12, 'Реакция', 'Задержка перед атакой новой замеченной цели.', 'seconds', 0.05, 2, 0.01],
  decisionSeconds: [0.35, 0.2, 0.1, 'Планирование', 'Интервал пересмотра цели и пути.', 'seconds', 0.05, 1, 0.01],
  fieldOfViewDegrees: [110, 130, 150, 'Поле зрения', 'Угол обнаружения врагов при наличии прямой видимости.', 'degrees', 60, 180, 1],
  memorySeconds: [2, 4, 6, 'Память', 'Срок хранения последней известной позиции врага.', 'seconds', 0.5, 15, 0.1],
  aimDegreesPerSecond: [90, 160, 230, 'Наведение', 'Максимальная скорость поворота прицела.', 'degrees', 10, 240, 1],
  aimErrorDegrees: [12, 5, 1.5, 'Ошибка прицела', 'Амплитуда плавной ошибки прицеливания.', 'degrees', 0.5, 30, 0.1],
  aimPeriodSeconds: [1.4, 1, 0.7, 'Ритм прицела', 'Период изменения ошибки прицела.', 'seconds', 0.1, 5, 0.1],
  fireToleranceDegrees: [18, 12, 8, 'Окно выстрела', 'Допустимое отклонение прицела для попытки выстрела.', 'degrees', 1, 30, 1],
  preferredDistanceMeters: [6, 5, 4, 'Дистанция', 'Желательная дистанция перестрелки.', 'meters', 1, 20, 0.1],
  strafeSeconds: [1.8, 1.1, 0.7, 'Ритм стрейфа', 'Средняя длительность одного бокового манёвра.', 'seconds', 0.2, 5, 0.1],
  strafeWeight: [0.35, 0.65, 0.85, 'Стрейф', 'Вес бокового движения в перестрелке.', 'ratio', 0, 1, 0.05],
  jumpCooldownSeconds: [6, 4, 3, 'Пауза прыжков', 'Минимальная пауза между прыжками.', 'seconds', 1, 15, 0.1],
  jumpChance: [0.12, 0.18, 0.22, 'Боевой прыжок', 'Вероятность прыжка при смене боевого манёвра и свободном приземлении.', 'ratio', 0, 1, 0.01],
  retreatSeconds: [0.7, 1, 1.2, 'Короткий отход', 'Предельное время отхода после обнаружения угрозы при низком здоровье.', 'seconds', 0.1, 3, 0.1],
  retreatHealthRatio: [0.2, 0.3, 0.35, 'Порог отхода', 'Доля здоровья для временного отхода.', 'ratio', 0.05, 0.6, 0.05],
  supportChance: [0.2, 0.45, 0.65, 'Поддержка', 'Склонность помочь доступному союзнику.', 'ratio', 0, 1, 0.05],
} as const
export type BotTuning = { readonly [K in keyof typeof fields]: number }
export interface BotRules extends Record<BotDifficulty, BotTuning> {
  navigation: { waypointRadius: number; clearance: number; stuckSeconds: number; progressMeters: number; recoverySeconds: number }
  cooperation: { separationMeters: number; supportSeconds: number; communicationSeconds: number; supportDistanceMeters: number; personalitySpread: number }
}
export const BOT_RULES: BotRules = {
  ...Object.fromEntries(BOT_DIFFICULTIES.map((id, index) => [id, Object.fromEntries(Object.entries(fields).map(([key, value]) => [key, value[index]]))])) as Record<BotDifficulty, BotTuning>,
  navigation: { waypointRadius: 0.5, clearance: 0.1, stuckSeconds: 1.5, progressMeters: 0.4, recoverySeconds: 0.7 },
  cooperation: { separationMeters: 2.5, supportSeconds: 3, communicationSeconds: 0.3, supportDistanceMeters: 18, personalitySpread: 0.2 },
}
export const BOT_DESCRIPTORS: NumericProfileDescriptor[] = BOT_DIFFICULTIES.flatMap(id => Object.entries(fields).map(([key, v]) => ({
  path: `rules.bots.${id}.${key}`, group: 'bots' as const, label: `${BOT_LABELS[id]} · ${v[3]}`, description: v[4], unit: v[5], minimum: v[6], maximum: v[7], step: v[8],
})))
const shared: Array<[string, string, string, NumericProfileDescriptor['unit'], number, number, number]> = [
  ['navigation.waypointRadius', 'Достижение точки', 'Радиус завершения сегмента пути.', 'meters', 0.1, 1, 0.1],
  ['navigation.clearance', 'Запас прохода', 'Запас сверх collision radius при поиске пути.', 'meters', 0.01, 0.3, 0.01],
  ['navigation.stuckSeconds', 'Застревание', 'Срок отсутствия прогресса до восстановления.', 'seconds', 0.5, 5, 0.1],
  ['navigation.progressMeters', 'Прогресс', 'Минимальное перемещение за окно застревания.', 'meters', 0.1, 2, 0.1],
  ['navigation.recoverySeconds', 'Уступание', 'Длительность выхода из блокировки.', 'seconds', 0.1, 2, 0.1],
  ['cooperation.separationMeters', 'Личное пространство', 'Дистанция разделения союзников.', 'meters', 1, 5, 0.1],
  ['cooperation.supportSeconds', 'Время поддержки', 'Максимальная длительность одного общего намерения.', 'seconds', 0.5, 8, 0.1],
  ['cooperation.communicationSeconds', 'Сообщения команды', 'Задержка передачи замеченной позиции врага.', 'seconds', 0.1, 2, 0.1],
  ['cooperation.supportDistanceMeters', 'Дальность поддержки', 'Предельная дистанция до союзника для помощи.', 'meters', 3, 40, 1],
  ['cooperation.personalitySpread', 'Разнообразие', 'Разброс индивидуальной дистанции и ритма относительно профиля.', 'ratio', 0, 0.4, 0.05],
]
BOT_DESCRIPTORS.push(...shared.map(([key, label, description, unit, minimum, maximum, step]) => ({ path: `rules.bots.${key}`, group: 'bots' as const, label, description, unit, minimum, maximum, step })))
