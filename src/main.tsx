import { bootstrapBrowserRuntime } from './runtime/bootstrapBrowserRuntime'
import './styles.css'

const mount = document.querySelector<HTMLDivElement>('#app')

if (!mount) {
  throw new Error('Не найден корневой элемент приложения #app')
}

void bootstrapBrowserRuntime(mount)
