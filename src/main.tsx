import { createRoot } from 'react-dom/client'
import { StarTournamentApp } from './ui/StarTournamentApp'
import './styles.css'
import './ui/gameplayHud.css'

const mount = document.querySelector<HTMLDivElement>('#app')

if (!mount) {
  throw new Error('Не найден корневой элемент приложения #app')
}

createRoot(mount).render(<StarTournamentApp />)
