import { createRoot } from 'react-dom/client'
import productionAppIcon from './assets/app-icon.png'
import developmentAppIcon from './assets/app-icon-dev.png'
import { StarTournamentApp } from './ui/StarTournamentApp'
import './styles.css'
import './ui/gameplayHud.css'

const iconLink = document.querySelector<HTMLLinkElement>('#app-icon')

if (iconLink) {
  iconLink.href = import.meta.env.DEV ? developmentAppIcon : productionAppIcon
}

const mount = document.querySelector<HTMLDivElement>('#app')

if (!mount) {
  throw new Error('Не найден корневой элемент приложения #app')
}

createRoot(mount).render(<StarTournamentApp />)
