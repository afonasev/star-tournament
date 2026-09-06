import { releaseCaption } from '../releaseInfo'

export function ReleaseHeader() {
  return <span className="release-header" aria-label="Версия приложения">{releaseCaption()}</span>
}
